using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace R3Ext.Bindings.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public sealed class BindingGeneratorV2 : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor IncompleteBindingDescriptor = new(
        "R3BG001",
        "Binding generation incomplete",
        "Binding '{0}' was not generated: {1}",
        "R3.BindingGenerator",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor SymbolResolutionFailureDescriptor = new(
        "R3BG002",
        "Symbol resolution failed",
        "Failed to resolve symbol for '{0}' in expression '{1}': {2}",
        "R3.BindingGenerator",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor InvocationFilteredDescriptor = new(
        "R3BG003",
        "Invocation filtered during Transform",
        "Binding invocation at {0} was filtered: {1}",
        "R3.BindingGenerator",
        DiagnosticSeverity.Info,
        true);

    private static readonly SymbolDisplayFormat FullyQualifiedFormatWithNullability =
        new(
            SymbolDisplayGlobalNamespaceStyle.Included,
            SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                                  SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                  SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<UiBindingMetadata> uiMetadata = context.AdditionalTextsProvider
            .Where(a => a.Path.EndsWith("R3Ext.BindingTargets.json", StringComparison.OrdinalIgnoreCase))
            .Select((text, _) => UiBindingMetadata.TryLoad(text))
            .Where(m => m is not null)!
            .Select((m, _) => m!);

        IncrementalValueProvider<UiBindingLookup> uiLookup = uiMetadata.Collect().Select((items, _) => UiBindingLookup.Build(items));

        IncrementalValuesProvider<object?> invocations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax ies && LooksLikeBindingInvocation(ies),
                static (ctx, _) => TransformOrCapture(ctx))
            .Where(m => m is not null)!;

        IncrementalValueProvider<ImmutableArray<object?>> collected = invocations.Collect();
        IncrementalValueProvider<Compilation> compilationProvider = context.CompilationProvider;
        IncrementalValueProvider<((ImmutableArray<object?> Left, Compilation Right) Left, UiBindingLookup Right)> combined =
            collected.Combine(compilationProvider).Combine(uiLookup);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            ImmutableArray<object?> models = tuple.Left.Left;
            Compilation? compilation = tuple.Left.Right;
            string asmName = compilation.AssemblyName ?? string.Empty;
            UiBindingLookup? uiMap = tuple.Right;
            UiBindingLookupProvider.Current = uiMap;

            // Report filtered invocations
            int filteredCount = 0;
            foreach (object? item in models)
            {
                if (item is FilteredInvocation filtered)
                {
                    filteredCount++;
                    spc.ReportDiagnostic(Diagnostic.Create(InvocationFilteredDescriptor, filtered.Location,
                        filtered.Location.GetLineSpan().StartLinePosition.ToString(), filtered.Reason));
                }
            }

            ImmutableArray<InvocationModel> all = models!.OfType<InvocationModel>().ToImmutableArray();

            // Debug logging - write to generated file as comments
            // Debug logging - commented out after confirming deduplication fix
            // var debugInfo = new StringBuilder();
            // debugInfo.AppendLine($"// DEBUG: Assembly={asmName}, InvocationModels={all.Length}, Filtered={filteredCount}");
            // debugInfo.AppendLine($"// DEBUG: WhenChanged invocations BEFORE deduplication: {all.Count(m => m.Kind == "WhenChanged")}");
            // foreach (var inv in all.Where(m => m.Kind == "WhenChanged"))
            // {
            //     var hasSegments = inv.FromSegments.Count > 0;
            //     var key = inv.Kind + "|" + inv.WhenPath;
            //     debugInfo.AppendLine($"//   - Root={inv.WhenRootTypeName ?? "NULL"}, Path={inv.WhenPath}, Segments={inv.FromSegments.Count}, Key={key}, Loc={inv.Location.GetLineSpan()}");
            //     if (inv.SymbolResolutionFailures.Count > 0)
            //     {
            //         foreach (var (member, expr, reason) in inv.SymbolResolutionFailures)
            //         {
            //             debugInfo.AppendLine($"//     FAILURE: {member} - {reason}");
            //         }
            //     }
            // }
            // debugInfo.AppendLine($"// DEBUG: Filtered invocations: {filteredCount}");
            // foreach (var item in models.OfType<FilteredInvocation>())
            // {
            //     debugInfo.AppendLine($"//   - Filtered at {item.Location.GetLineSpan()}: {item.Reason}");
            // }

            // Post-process unresolved target chains using UI metadata (now that we have compilation + lookup)
            foreach (InvocationModel? m in all)
            {
                if (m.Kind is "BindOneWay" or "BindTwoWay")
                {
                    if ((m.ToSegments == null || m.ToSegments.Count == 0) && m.ToLambda is not null && m.TargetIdentifierName is not null &&
                        m.ContainingTypeName is not null)
                    {
                        if (uiMap.TryGetTargetType(compilation, asmName, m.ContainingTypeName, m.TargetIdentifierName, out ITypeSymbol? resolvedType) &&
                            resolvedType is not null)
                        {
                            List<string> names = GetMemberNames(m.ToLambda);
                            List<PropertySegment> segs = TryBuildSegmentsFromNames(resolvedType, names);
                            if (segs.Count > 0)
                            {
                                m.ToSegments = segs;
                            }
                        }
                    }
                }
            }

            // Emit diagnostics for incomplete bindings prior to dedupe
            foreach (InvocationModel? m in all)
            {
                if (m.Kind is "BindOneWay" or "BindTwoWay")
                {
                    if (m.FromLambda is null || m.ToLambda is null)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(IncompleteBindingDescriptor, m.Location, m.Kind, "missing lambda expression(s)"));
                        continue;
                    }

                    if (m.FromSegments.Count == 0)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(IncompleteBindingDescriptor, m.Location, m.Kind, "unable to resolve 'from' property chain"));

                        // Report detailed symbol resolution failures
                        foreach ((string memberName, string expr, string reason) in m.SymbolResolutionFailures)
                        {
                            spc.ReportDiagnostic(Diagnostic.Create(SymbolResolutionFailureDescriptor, m.Location, memberName, expr, reason));
                        }
                    }

                    if (m.ToSegments.Count == 0)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(IncompleteBindingDescriptor, m.Location, m.Kind, "unable to resolve 'to' property chain"));
                    }
                }
                else if (m.Kind == "WhenChanged" && m.FromSegments.Count == 0)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(IncompleteBindingDescriptor, m.Location, m.Kind, "unable to resolve monitored property chain"));

                    // Report detailed symbol resolution failures
                    foreach ((string memberName, string expr, string reason) in m.SymbolResolutionFailures)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(SymbolResolutionFailureDescriptor, m.Location, memberName, expr, reason));
                    }
                }
            }

            // Populate type name metadata BEFORE deduplication so we can use root type in the key
            foreach (InvocationModel? m in all)
            {
                if (m.Kind == "WhenChanged")
                {
                    if (m.FromSegments.Count > 0)
                    {
                        m.WhenRootTypeName = m.FromSegments[0].DeclaringTypeName;
                        m.WhenLeafTypeName = m.FromSegments.Last().TypeName;

                        // For root INPC check: need to lookup the declaring type symbol
                        // But we don't have Compilation here, so we'll use a heuristic:
                        // Check if the first segment came from an INPC type by looking at segment metadata
                        // Actually, the segments don't store the parent's IsNotify, so we'll handle this in code generation
                        m.WhenRootImplementsNotify = false; // Will determine at generation time
                    }
                }
                else
                {
                    if (m.FromSegments.Count > 0)
                    {
                        m.RootFromTypeName = m.FromSegments[0].DeclaringTypeName;
                        m.FromLeafTypeName = m.FromSegments.Last().TypeName;
                        m.RootFromImplementsNotify = false; // Will determine at generation time
                    }

                    if (m.ToSegments.Count > 0)
                    {
                        m.RootTargetTypeName = m.ToSegments[0].DeclaringTypeName;
                        m.TargetLeafTypeName = m.ToSegments.Last().TypeName;
                        m.RootTargetImplementsNotify = false; // Will determine at generation time
                    }
                }
            }

            // De-duplicate by key (kind + root type + paths)
            static string Key(InvocationModel m)
            {
                return m.Kind switch
                {
                    "BindOneWay" => $"{m.Kind}|{m.RootFromTypeName}|{m.FromPath}|{m.RootTargetTypeName}|{m.ToPath}",
                    "BindTwoWay" => $"{m.Kind}|{m.RootFromTypeName}|{m.FromPath}|{m.RootTargetTypeName}|{m.ToPath}",
                    "WhenChanged" => $"{m.Kind}|{m.WhenRootTypeName}|{m.WhenPath}",
                    _ => m.Kind,
                };
            }

            HashSet<string> seen = new();
            List<InvocationModel> distinct = new();
            foreach (InvocationModel? m in all)
            {
                string k = Key(m);
                if (seen.Add(k))
                {
                    distinct.Add(m); // silently suppress duplicates
                }
            }

            CodeEmitter emitter = new();
            string source = emitter.Emit(distinct.ToImmutableArray(), asmName);

            // Prepend debug info
            // debugInfo.AppendLine($"// DEBUG: After deduplication: {distinct.Count} distinct invocations");
            // foreach (var inv in distinct.Where(m => m.Kind == "WhenChanged"))
            // {
            //     debugInfo.AppendLine($"//   - KEPT: Path={inv.WhenPath}, Loc={inv.Location.GetLineSpan()}");
            // }

            // var finalSource = debugInfo.ToString() + source;
            spc.AddSource("R3Ext_BindingGeneratorV2.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static bool LooksLikeBindingInvocation(InvocationExpressionSyntax ies)
    {
        if (ies.Expression is MemberAccessExpressionSyntax maes)
        {
            string name = maes.Name.Identifier.ValueText;
            return name is "BindTwoWay" or "BindOneWay" or "WhenChanged";
        }

        return false;
    }

    private static object? TransformOrCapture(GeneratorSyntaxContext ctx)
    {
        InvocationExpressionSyntax ies = (InvocationExpressionSyntax)ctx.Node;
        Location location = ies.GetLocation();

        if (ies.Expression is not MemberAccessExpressionSyntax maes)
        {
            return new FilteredInvocation(location, "expression is not a member access", ies.ToString());
        }

        string name = maes.Name.Identifier.ValueText;

        // Use invocation expression for symbol resolution to properly catch extension methods in other assemblies.
        SymbolInfo si = ctx.SemanticModel.GetSymbolInfo(ies);
        IMethodSymbol? symbol = si.Symbol as IMethodSymbol ?? si.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

        if (symbol is null)
        {
            string reason = si.CandidateReason switch
            {
                CandidateReason.None => "method symbol not found",
                CandidateReason.OverloadResolutionFailure => $"overload resolution failure (candidates: {si.CandidateSymbols.Length})",
                CandidateReason.Inaccessible => "method is inaccessible",
                _ => $"unknown symbol resolution issue: {si.CandidateReason}",
            };
            return new FilteredInvocation(location, reason, ies.ToString());
        }

        if (symbol.Name is not ("BindTwoWay" or "BindOneWay" or "WhenChanged"))
        {
            return new FilteredInvocation(location, $"method name '{symbol.Name}' is not a recognized binding method", ies.ToString());
        }

        return Transform(ctx, ies, maes, symbol, location);
    }

    private static InvocationModel? Transform(GeneratorSyntaxContext ctx, InvocationExpressionSyntax ies, MemberAccessExpressionSyntax maes,
        IMethodSymbol symbol, Location location)
    {
        SeparatedSyntaxList<ArgumentSyntax> args = ies.ArgumentList.Arguments;
        string? fromPath = null, toPath = null, whenPath = null;
        SimpleLambdaExpressionSyntax? fromLambda = null, toLambda = null, whenLambda = null;
        List<PropertySegment> fromSegments = new();
        List<PropertySegment> toSegments = new();
        string? capturedTargetIdentifier = null;
        string? capturedContainingTypeName = null;

        string? inferredTargetRootTypeNameFromHeuristic = null;
        List<(string, string, string)> failures = new();

        if (symbol.Name == "WhenChanged")
        {
            if (args.Count < 1)
            {
                return null;
            }

            whenLambda = args[0].Expression as SimpleLambdaExpressionSyntax ?? TryExtractLambdaFromNameof(args[0].Expression) as SimpleLambdaExpressionSyntax;
            whenPath = whenLambda?.ToString();
            if (whenLambda is not null)
            {
                ExtractSegments(ctx.SemanticModel, whenLambda, fromSegments,
                    (member, expr, reason) => failures.Add((member, expr, reason))); // reuse fromSegments for WhenChanged chain
            }
        }
        else
        {
            // BindOneWay / BindTwoWay minimal required args: targetObject, fromProperty, toProperty
            if (args.Count < 3)
            {
                return null;
            }

            fromLambda = args[1].Expression as SimpleLambdaExpressionSyntax ?? TryExtractLambdaFromNameof(args[1].Expression) as SimpleLambdaExpressionSyntax;
            toLambda = args[2].Expression as SimpleLambdaExpressionSyntax ?? TryExtractLambdaFromNameof(args[2].Expression) as SimpleLambdaExpressionSyntax;
            fromPath = fromLambda?.ToString();
            toPath = toLambda?.ToString();
            if (fromLambda is not null)
            {
                ExtractSegments(ctx.SemanticModel, fromLambda, fromSegments, (member, expr, reason) => failures.Add((member, expr, reason)));
            }

            if (toLambda is not null)
            {
                ITypeSymbol? targetRootType = null;
                if (symbol.IsGenericMethod && symbol.TypeArguments.Length >= 3)
                {
                    ITypeSymbol? ta = symbol.TypeArguments[2];
                    if (ta is not null && ta.TypeKind != TypeKind.TypeParameter)
                    {
                        targetRootType = ta;
                    }
                }

                INamedTypeSymbol? convType = ctx.SemanticModel.GetTypeInfo(toLambda).ConvertedType as INamedTypeSymbol;
                if (convType is not null)
                {
                    if (convType.Name == "Expression" && convType.TypeArguments.Length == 1 && convType.TypeArguments[0] is INamedTypeSymbol inner &&
                        inner.Name == "Func" && inner.TypeArguments.Length >= 1)
                    {
                        targetRootType = inner.TypeArguments[0];
                    }
                    else if (convType.Name == "Func" && convType.TypeArguments.Length >= 1)
                    {
                        targetRootType = convType.TypeArguments[0];
                    }
                }

                targetRootType ??= ctx.SemanticModel.GetTypeInfo(args[0].Expression).Type;

                // Capture identifiers for potential post-resolution via UI metadata
                if (args[0].Expression is IdentifierNameSyntax idName2)
                {
                    INamedTypeSymbol? containingType = ctx.SemanticModel.GetEnclosingSymbol(ies.SpanStart) as INamedTypeSymbol;
                    if (containingType is null)
                    {
                        ClassDeclarationSyntax? classDecl = ies.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                        if (classDecl is not null)
                        {
                            containingType = ctx.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                        }
                    }

                    if (containingType is not null)
                    {
                        string containingTypeName = containingType.ToDisplayString(FullyQualifiedFormatWithNullability);
                        capturedTargetIdentifier = idName2.Identifier.ValueText;
                        capturedContainingTypeName = containingTypeName;

                        // store for post-processing in source output when ui lookup + compilation are available
                        // actual resolution here may fail due to generator timing; defer to post phase
                        // but if type info is available now, use it
                        if (targetRootType is null)
                        {
                            // Will attempt later using lookup
                        }

                        inferredTargetRootTypeNameFromHeuristic = null;
                    }
                }

                // Removed platform-specific suffix heuristics for target control type inference to keep generator agnostic.
                ExtractSegments(ctx.SemanticModel, toLambda, toSegments, targetRootType);

                // No synthetic segment insertion; unresolved chains will produce warning R3BG001.
            }
        }

        InvocationModel model =
            new(symbol.Name, fromLambda, toLambda, whenLambda, fromPath, toPath, whenPath, ies.GetLocation(),
                ctx.SemanticModel.Compilation.AssemblyName ?? string.Empty)
            {
                FromSegments = fromSegments,
                ToSegments = toSegments,
                TargetIdentifierName = capturedTargetIdentifier,
                ContainingTypeName = capturedContainingTypeName,
                SymbolResolutionFailures = failures,
            };
        if (symbol.Name != "WhenChanged")
        {
            ITypeSymbol? ta = null;
            if (symbol.IsGenericMethod && symbol.TypeArguments.Length >= 3)
            {
                ITypeSymbol? targ = symbol.TypeArguments[2];
                if (targ is not null && targ.TypeKind != TypeKind.TypeParameter)
                {
                    ta = targ;
                }
            }

            if (toLambda is not null)
            {
                INamedTypeSymbol? convType = ctx.SemanticModel.GetTypeInfo(toLambda).ConvertedType as INamedTypeSymbol;
                if (convType is not null)
                {
                    if (convType.Name == "Expression" && convType.TypeArguments.Length == 1 && convType.TypeArguments[0] is INamedTypeSymbol inner &&
                        inner.Name == "Func" && inner.TypeArguments.Length >= 1)
                    {
                        ta = inner.TypeArguments[0];
                    }
                    else if (convType.Name == "Func" && convType.TypeArguments.Length >= 1)
                    {
                        ta = convType.TypeArguments[0];
                    }
                }
            }

            ta ??= ctx.SemanticModel.GetTypeInfo(args[0].Expression).Type;
            model.TargetArgTypeName = ta?.ToDisplayString(FullyQualifiedFormatWithNullability) ?? inferredTargetRootTypeNameFromHeuristic;
        }

        return model;
    }

    private static List<string> GetMemberNames(SimpleLambdaExpressionSyntax lambda)
    {
        List<string> names = new();
        if (lambda.Body is MemberAccessExpressionSyntax maes)
        {
            Stack<string> stack = new();
            ExpressionSyntax? cur = maes;
            while (cur is MemberAccessExpressionSyntax mae)
            {
                stack.Push(mae.Name.Identifier.ValueText);
                cur = mae.Expression;
            }

            while (stack.Count > 0)
            {
                names.Add(stack.Pop());
            }
        }

        return names;
    }

    private static List<PropertySegment> TryBuildSegmentsFromNames(ITypeSymbol rootType, List<string> names)
    {
        List<PropertySegment> segs = new();
        ITypeSymbol current = rootType;
        foreach (string? name in names)
        {
            ISymbol? member = null;
            ITypeSymbol? search = current;
            while (search is not null && member is null)
            {
                member = search.GetMembers(name).FirstOrDefault(m => m is IPropertySymbol || m is IFieldSymbol);
                if (member is null)
                {
                    search = search.BaseType;
                }
            }

            if (member is null)
            {
                segs.Clear();
                return segs;
            }

            if (member is IPropertySymbol ps)
            {
                PropertySegment seg = new()
                {
                    Name = ps.Name,
                    TypeName = ps.Type.ToDisplayString(FullyQualifiedFormatWithNullability),
                    DeclaringTypeName = ps.ContainingType.ToDisplayString(FullyQualifiedFormatWithNullability),
                    IsReferenceType = ps.Type.IsReferenceType,
                    IsNotify = ImplementsNotify(ps.Type),
                    DeclaringTypeImplementsNotify = ImplementsNotify(ps.ContainingType),
                    HasSetter = ps.SetMethod is not null,
                    SetterIsNonPublic = ps.SetMethod is not null && ps.SetMethod.DeclaredAccessibility != Accessibility.Public,
                    IsNonPublic = ps.DeclaredAccessibility != Accessibility.Public,
                    IsField = false,
                    IsNullable = ps.Type.NullableAnnotation == NullableAnnotation.Annotated,
                };
                segs.Add(seg);
                current = ps.Type;
            }
            else if (member is IFieldSymbol fs)
            {
                PropertySegment seg = new()
                {
                    Name = fs.Name,
                    TypeName = fs.Type.ToDisplayString(FullyQualifiedFormatWithNullability),
                    DeclaringTypeName = fs.ContainingType.ToDisplayString(FullyQualifiedFormatWithNullability),
                    IsReferenceType = fs.Type.IsReferenceType,
                    IsNotify = ImplementsNotify(fs.Type),
                    DeclaringTypeImplementsNotify = ImplementsNotify(fs.ContainingType),
                    HasSetter = false,
                    SetterIsNonPublic = false,
                    IsNonPublic = fs.DeclaredAccessibility != Accessibility.Public,
                    IsField = true,
                    IsNullable = fs.Type.NullableAnnotation == NullableAnnotation.Annotated,
                };
                segs.Add(seg);
                current = fs.Type;
            }
        }

        return segs;
    }

    private static LambdaExpressionSyntax? TryExtractLambdaFromNameof(ExpressionSyntax expr)
    {
        return expr as LambdaExpressionSyntax;
    }

    private static void ExtractSegments(SemanticModel model, SimpleLambdaExpressionSyntax lambda, List<PropertySegment> into,
        Action<string, string, string>? reportFailure = null)
    {
        into.Clear();
        ExpressionSyntax? body = lambda.Body as ExpressionSyntax;

        // unwrap null-forgiving operators
        while (body is PostfixUnaryExpressionSyntax px and { OperatorToken.RawKind: (int)SyntaxKind.ExclamationToken, })
        {
            body = px.Operand;
        }

        if (body is not MemberAccessExpressionSyntax)
        {
            reportFailure?.Invoke("lambda body", lambda.ToString(), "not a member access expression");
            return;
        }

        // Gather chain root->leaf
        Stack<MemberAccessExpressionSyntax> stack = new();
        ExpressionSyntax? cur = body;
        while (cur is MemberAccessExpressionSyntax mae)
        {
            stack.Push(mae);
            cur = mae.Expression;
            while (cur is PostfixUnaryExpressionSyntax px2 && px2.OperatorToken.IsKind(SyntaxKind.ExclamationToken))
            {
                cur = px2.Operand;
            }
        }

        if (cur is not IdentifierNameSyntax)
        {
            reportFailure?.Invoke("lambda parameter", lambda.ToString(), "root expression is not an identifier");
            return;
        }

        while (stack.Count > 0)
        {
            MemberAccessExpressionSyntax? mae = stack.Pop();
            string memberName = mae.Name.Identifier.ValueText;
            SymbolInfo si = model.GetSymbolInfo(mae);
            ISymbol? symbol = si.Symbol;

            if (symbol is null)
            {
                string reason = si.CandidateReason switch
                {
                    CandidateReason.None => "symbol not found",
                    CandidateReason.NotAValue => "not a value",
                    CandidateReason.NotAVariable => "not a variable",
                    CandidateReason.NotInvocable => "not invocable",
                    CandidateReason.Inaccessible => "inaccessible",
                    CandidateReason.OverloadResolutionFailure => "overload resolution failure",
                    CandidateReason.LateBound => "late bound",
                    CandidateReason.Ambiguous =>
                        $"ambiguous ({si.CandidateSymbols.Length} candidates: {string.Join(", ", si.CandidateSymbols.Take(3).Select(s => s.ToDisplayString()))})",
                    CandidateReason.MemberGroup => "member group",
                    _ => $"unknown reason ({si.CandidateReason})",
                };
                reportFailure?.Invoke(memberName, lambda.ToString(), reason);
                into.Clear();
                return;
            }

            if (symbol is IPropertySymbol ps)
            {
                PropertySegment seg = new()
                {
                    Name = ps.Name,
                    TypeName = ps.Type.ToDisplayString(FullyQualifiedFormatWithNullability),
                    DeclaringTypeName = ps.ContainingType.ToDisplayString(FullyQualifiedFormatWithNullability),
                    IsReferenceType = ps.Type.IsReferenceType,
                    IsNotify = ImplementsNotify(ps.Type),
                    DeclaringTypeImplementsNotify = ImplementsNotify(ps.ContainingType),
                    HasSetter = ps.SetMethod is not null,
                    SetterIsNonPublic = ps.SetMethod is not null && ps.SetMethod.DeclaredAccessibility != Accessibility.Public,
                    IsNonPublic = ps.DeclaredAccessibility != Accessibility.Public,
                    IsField = false,
                    IsNullable = ps.Type.NullableAnnotation == NullableAnnotation.Annotated,
                };
                into.Add(seg);
            }
            else if (symbol is IFieldSymbol fs)
            {
                PropertySegment seg = new()
                {
                    Name = fs.Name,
                    TypeName = fs.Type.ToDisplayString(FullyQualifiedFormatWithNullability),
                    DeclaringTypeName = fs.ContainingType.ToDisplayString(FullyQualifiedFormatWithNullability),
                    IsReferenceType = fs.Type.IsReferenceType,
                    IsNotify = ImplementsNotify(fs.Type),
                    DeclaringTypeImplementsNotify = ImplementsNotify(fs.ContainingType),
                    HasSetter = false,
                    SetterIsNonPublic = false,
                    IsNonPublic = fs.DeclaredAccessibility != Accessibility.Public,
                    IsField = true,
                    IsNullable = fs.Type.NullableAnnotation == NullableAnnotation.Annotated,
                };
                into.Add(seg);
            }
            else
            {
                reportFailure?.Invoke(memberName, lambda.ToString(), $"unexpected symbol type: {symbol.GetType().Name}");
                into.Clear();
                return;
            }
        }
    }

    private static void ExtractSegments(SemanticModel model, SimpleLambdaExpressionSyntax lambda, List<PropertySegment> into, ITypeSymbol? explicitRoot)
    {
        into.Clear();
        ExpressionSyntax? body = lambda.Body as ExpressionSyntax;
        while (body is PostfixUnaryExpressionSyntax px && px.OperatorToken.IsKind(SyntaxKind.ExclamationToken))
        {
            body = px.Operand;
        }

        if (body is not MemberAccessExpressionSyntax)
        {
            return;
        }

        Stack<string> parts = new();
        ExpressionSyntax? cur = body;
        while (cur is MemberAccessExpressionSyntax mae)
        {
            parts.Push(mae.Name.Identifier.ValueText);
            cur = mae.Expression;
            while (cur is PostfixUnaryExpressionSyntax px2 && px2.OperatorToken.IsKind(SyntaxKind.ExclamationToken))
            {
                cur = px2.Operand;
            }
        }

        if (explicitRoot is null)
        {
            ExtractSegments(model, lambda, into);
            return;
        }

        ITypeSymbol current = explicitRoot;
        while (parts.Count > 0)
        {
            string? name = parts.Pop();
            ISymbol? member = null;
            ITypeSymbol? search = current;
            while (search is not null && member is null)
            {
                member = search.GetMembers(name).FirstOrDefault(m => m is IPropertySymbol || m is IFieldSymbol);
                if (member is null)
                {
                    search = search.BaseType;
                }
            }

            if (member is null)
            {
                // Fallback: try semantic model symbol resolution on the full member access
                if (body is MemberAccessExpressionSyntax mae)
                {
                    ISymbol? sym = model.GetSymbolInfo(mae).Symbol;
                    if (sym is null)
                    {
                        into.Clear();
                        return;
                    }

                    member = sym;
                }
                else
                {
                    into.Clear();
                    return;
                }
            }

            if (member is IPropertySymbol prop)
            {
                PropertySegment seg = new()
                {
                    Name = prop.Name,
                    TypeName = prop.Type.ToDisplayString(FullyQualifiedFormatWithNullability),
                    DeclaringTypeName = prop.ContainingType.ToDisplayString(FullyQualifiedFormatWithNullability),
                    IsReferenceType = prop.Type.IsReferenceType,
                    IsNotify = ImplementsNotify(prop.Type),
                    DeclaringTypeImplementsNotify = ImplementsNotify(prop.ContainingType),
                    HasSetter = prop.SetMethod is not null,
                    SetterIsNonPublic = prop.SetMethod is not null && prop.SetMethod.DeclaredAccessibility != Accessibility.Public,
                    IsNonPublic = prop.DeclaredAccessibility != Accessibility.Public,
                    IsField = false,
                    IsNullable = prop.Type.NullableAnnotation == NullableAnnotation.Annotated,
                };
                into.Add(seg);
                current = prop.Type;
            }
            else if (member is IFieldSymbol field)
            {
                PropertySegment seg = new()
                {
                    Name = field.Name,
                    TypeName = field.Type.ToDisplayString(FullyQualifiedFormatWithNullability),
                    DeclaringTypeName = field.ContainingType.ToDisplayString(FullyQualifiedFormatWithNullability),
                    IsReferenceType = field.Type.IsReferenceType,
                    IsNotify = ImplementsNotify(field.Type),
                    DeclaringTypeImplementsNotify = ImplementsNotify(field.ContainingType),
                    HasSetter = false,
                    SetterIsNonPublic = false,
                    IsNonPublic = field.DeclaredAccessibility != Accessibility.Public,
                    IsField = true,
                    IsNullable = field.Type.NullableAnnotation == NullableAnnotation.Annotated,
                };
                into.Add(seg);
                current = field.Type;
            }
        }
    }

    private static bool ImplementsNotify(ITypeSymbol t)
    {
        return t.AllInterfaces.Any(i => i.ToDisplayString() == "System.ComponentModel.INotifyPropertyChanged");
    }
}

internal sealed class FilteredInvocation(Location location, string reason, string? code = null)
{
    public Location Location { get; } = location;

    public string Reason { get; } = reason;

    public string? Code { get; } = code;
}

internal sealed class InvocationModel(
    string kind,
    SimpleLambdaExpressionSyntax? fromLambda,
    SimpleLambdaExpressionSyntax? toLambda,
    SimpleLambdaExpressionSyntax? whenLambda,
    string? fromPath,
    string? toPath,
    string? whenPath,
    Location location,
    string assemblyName)
{
    public string Kind { get; } = kind;

    public SimpleLambdaExpressionSyntax? FromLambda { get; } = fromLambda;

    public SimpleLambdaExpressionSyntax? ToLambda { get; } = toLambda;

    public SimpleLambdaExpressionSyntax? WhenLambda { get; } = whenLambda;

    public string? FromPath { get; } = fromPath;

    public string? ToPath { get; } = toPath;

    public string? WhenPath { get; } = whenPath;

    public Location Location { get; } = location;

    public List<PropertySegment> FromSegments { get; set; } = new();

    public List<PropertySegment> ToSegments { get; set; } = new();

    public string AssemblyName { get; } = assemblyName;

    public string? RootFromTypeName { get; set; }

    public string? FromLeafTypeName { get; set; }

    public string? RootTargetTypeName { get; set; }

    public string? TargetLeafTypeName { get; set; }

    public string? TargetArgTypeName { get; set; }

    public string? WhenRootTypeName { get; set; }

    public string? WhenLeafTypeName { get; set; }

    public string? TargetIdentifierName { get; set; }

    public string? ContainingTypeName { get; set; }

    public bool RootFromImplementsNotify { get; set; }

    public bool RootTargetImplementsNotify { get; set; }

    public bool WhenRootImplementsNotify { get; set; }

    public List<(string MemberName, string Expression, string Reason)> SymbolResolutionFailures { get; set; } = new();
}

internal sealed class PropertySegment
{
    public string Name { get; set; } = string.Empty;

    public string TypeName { get; set; } = string.Empty;

    public string DeclaringTypeName { get; set; } = string.Empty;

    public bool IsReferenceType { get; set; }

    public bool IsNotify { get; set; }

    public bool DeclaringTypeImplementsNotify { get; set; }

    public bool HasSetter { get; set; }

    public bool SetterIsNonPublic { get; set; }

    public bool IsNonPublic { get; set; }

    public bool IsField { get; set; }

    public bool IsNullable { get; set; }
}

internal sealed class CodeEmitter
{
    public string Emit(ImmutableArray<InvocationModel> invocations, string assemblyName)
    {
        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS8600, CS8602, CS0030, CS8121");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Linq.Expressions;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using R3;");
        sb.AppendLine("namespace R3Ext;");
        string asm = assemblyName;
        if (invocations.IsDefaultOrEmpty || invocations.Length == 0)
        {
            if (asm == "R3Ext")
            {
                // Emit full method signatures with CallerArgumentExpression attributes even if there are no local invocation models.
                sb.AppendLine("public static partial class R3BindingExtensions");
                sb.AppendLine("{");
                this.EmitBindOneWay(ImmutableArray<InvocationModel>.Empty, sb);
                this.EmitBindTwoWay(ImmutableArray<InvocationModel>.Empty, sb);
                this.EmitWhenChanged(ImmutableArray<InvocationModel>.Empty, sb);
                sb.AppendLine("}");
            }
            else
            {
                sb.AppendLine(
                    "internal static class __R3BindingRegistration { [ModuleInitializer] internal static void Init(){ /* no binding invocations detected in this assembly */ } }");
            }

            return sb.ToString();
        }

        // 'asm' already defined above for empty/non-empty cases
        // Type name metadata is now populated before deduplication in RegisterSourceOutput
        if (asm == "R3Ext")
        {
            sb.AppendLine("public static partial class R3BindingExtensions");
            sb.AppendLine("{");
            EmitUnsafeAccessors(invocations, sb);
            this.EmitBindOneWay(invocations.Where(i => i.Kind == "BindOneWay").ToImmutableArray(), sb);
            this.EmitBindTwoWay(invocations.Where(i => i.Kind == "BindTwoWay").ToImmutableArray(), sb);
            this.EmitWhenChanged(invocations.Where(i => i.Kind == "WhenChanged").ToImmutableArray(), sb);
            sb.AppendLine("}");
        }
        else
        {
            sb.AppendLine("internal static class __R3BindingRegistration");
            sb.AppendLine("{");

            // Emit accessors needed within this assembly for non-public fields encountered
            EmitUnsafeAccessors(invocations, sb);
            int owCount = invocations.Count(i => i.Kind == "BindOneWay");
            int twCount = invocations.Count(i => i.Kind == "BindTwoWay");
            int wcCount = invocations.Count(i => i.Kind == "WhenChanged");
            sb.AppendLine($"    // Generator summary for {asm}: OW={owCount}, TW={twCount}, WC={wcCount}");

            // Debug: list invocations and metadata readiness
            foreach (InvocationModel? inv in invocations)
            {
                if (inv.Kind == "BindOneWay" || inv.Kind == "BindTwoWay")
                {
                    bool ok = inv.FromPath is not null && inv.ToPath is not null && inv.RootFromTypeName is not null && inv.FromLeafTypeName is not null &&
                              inv.RootTargetTypeName is not null && inv.TargetLeafTypeName is not null;
                    sb.AppendLine(
                        $"    // {inv.Kind}: from={Escape(inv.FromPath ?? "-")} to={Escape(inv.ToPath ?? "-")} ok={ok} rf={inv.RootFromTypeName ?? "-"} fl={inv.FromLeafTypeName ?? "-"} rt={inv.RootTargetTypeName ?? "-"} tl={inv.TargetLeafTypeName ?? "-"} ta={inv.TargetArgTypeName ?? "-"}");
                }
            }

            sb.AppendLine("    [ModuleInitializer] internal static void Init(){");
            foreach (InvocationModel? inv in invocations)
            {
                if (inv.Kind == "BindOneWay" && inv.FromPath is not null && inv.ToPath is not null && inv.RootFromTypeName is not null &&
                    inv.FromLeafTypeName is not null && inv.RootTargetTypeName is not null && inv.TargetLeafTypeName is not null)
                {
                    string id = Hash(inv.RootFromTypeName + "|" + inv.FromLeafTypeName + "|" + inv.RootTargetTypeName + "|" + inv.TargetLeafTypeName + "|" + inv.FromPath + "|" + inv.ToPath);
                    sb.AppendLine(
                        $"        BindingRegistry.RegisterOneWay<{inv.RootFromTypeName},{inv.FromLeafTypeName},{inv.RootTargetTypeName},{inv.TargetLeafTypeName}>(\"{Escape(inv.FromPath)}\", \"{Escape(inv.ToPath)}\", (f,t,conv) => __RegBindOneWay_{id}(f,t,conv));");
                }
                else if (inv.Kind == "BindTwoWay" && inv.FromPath is not null && inv.ToPath is not null && inv.RootFromTypeName is not null &&
                         inv.FromLeafTypeName is not null && inv.RootTargetTypeName is not null && inv.TargetLeafTypeName is not null)
                {
                    string id = Hash(inv.RootFromTypeName + "|" + inv.FromLeafTypeName + "|" + inv.RootTargetTypeName + "|" + inv.TargetLeafTypeName + "|" + inv.FromPath + "|" + inv.ToPath);
                    sb.AppendLine(
                        $"        BindingRegistry.RegisterTwoWay<{inv.RootFromTypeName},{inv.FromLeafTypeName},{inv.RootTargetTypeName},{inv.TargetLeafTypeName}>(\"{Escape(inv.FromPath)}\", \"{Escape(inv.ToPath)}\", (f,t,ht,th) => __RegBindTwoWay_{id}(f,t,ht,th));");
                }
                else if (inv.Kind == "WhenChanged" && inv.WhenPath is not null && inv.WhenRootTypeName is not null && inv.WhenLeafTypeName is not null)
                {
                    string id = Hash(inv.WhenRootTypeName + "|" + inv.WhenPath);
                    string simple = Simple(inv.WhenRootTypeName);
                    string composite = simple + "|" + Escape(inv.WhenPath);
                    sb.AppendLine(
                        $"        BindingRegistry.RegisterWhenChanged<{inv.WhenRootTypeName},{inv.WhenLeafTypeName}>(\"{composite}\", obj => __RegWhenChanged_{id}(obj));");
                }
            }

            sb.AppendLine("    }");
            HashSet<string> emitted = new();
            foreach (InvocationModel? inv in invocations)
            {
                if (inv.Kind == "BindOneWay" && inv.FromPath is not null && inv.ToPath is not null && inv.RootFromTypeName is not null &&
                    inv.FromLeafTypeName is not null && inv.RootTargetTypeName is not null && inv.TargetLeafTypeName is not null)
                {
                    string id = Hash(inv.RootFromTypeName + "|" + inv.FromLeafTypeName + "|" + inv.RootTargetTypeName + "|" + inv.TargetLeafTypeName + "|" + inv.FromPath + "|" + inv.ToPath);
                    if (emitted.Add("ow" + id))
                    {
                        this.EmitOneWayRegistrationBody(id, inv, sb);
                    }
                }
                else if (inv.Kind == "BindTwoWay" && inv.FromPath is not null && inv.ToPath is not null && inv.RootFromTypeName is not null &&
                         inv.FromLeafTypeName is not null && inv.RootTargetTypeName is not null && inv.TargetLeafTypeName is not null)
                {
                    string id = Hash(inv.RootFromTypeName + "|" + inv.FromLeafTypeName + "|" + inv.RootTargetTypeName + "|" + inv.TargetLeafTypeName + "|" + inv.FromPath + "|" + inv.ToPath);
                    if (emitted.Add("tw" + id))
                    {
                        this.EmitTwoWayRegistrationBody(id, inv, sb);
                    }
                }
                else if (inv.Kind == "WhenChanged" && inv.WhenPath is not null && inv.WhenRootTypeName is not null && inv.WhenLeafTypeName is not null)
                {
                    string id = Hash(inv.WhenRootTypeName + "|" + inv.WhenPath);
                    if (emitted.Add("wc" + id))
                    {
                        this.EmitWhenRegistrationBody(id, inv, sb);
                    }
                }
            }

            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static string Hash(string s)
    {
        // FNV-1a 32-bit for compact method names
        unchecked
        {
            uint hash = 2166136261;
            foreach (char ch in s)
            {
                hash ^= ch;
                hash *= 16777619;
            }

            return hash.ToString("x8");
        }
    }

    private static string Simple(string full)
    {
        if (full.StartsWith("global::"))
        {
            full = full.Substring(8);
        }

        int idx = full.LastIndexOf('.');
        return idx >= 0 ? full.Substring(idx + 1) : full;
    }

    private void EmitBindOneWay(ImmutableArray<InvocationModel> models, StringBuilder sb)
    {
        sb.AppendLine(
            "    public static partial IDisposable BindOneWay<TFrom,TFromProperty,TTarget,TTargetProperty>(this TFrom fromObject, TTarget targetObject, Expression<Func<TFrom,TFromProperty>> fromProperty, Expression<Func<TTarget,TTargetProperty>> toProperty, Func<TFromProperty,TTargetProperty>? conversionFunc = null, [CallerArgumentExpression(\"fromProperty\")] string? fromPropertyPath = null, [CallerArgumentExpression(\"toProperty\")] string? toPropertyPath = null)");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        if (fromPropertyPath is not null && toPropertyPath is not null && BindingRegistry.TryCreateOneWay<TFrom,TFromProperty,TTarget,TTargetProperty>(fromPropertyPath, toPropertyPath, fromObject, targetObject, conversionFunc, out var __regDisp)) return __regDisp;");
        if (!models.IsDefaultOrEmpty)
        {
            sb.AppendLine("        switch (fromPropertyPath)");
            sb.AppendLine("        {");
            foreach (InvocationModel? m in models)
            {
                if (m.FromPath is null || m.ToPath is null)
                {
                    continue;
                }

                string? key1 = m.FromPath;
                string? key2 = m.ToPath;
                string id = Hash(key1 + "|" + key2);
                sb.AppendLine($"            case \"{Escape(key1)}\":");
                sb.AppendLine("                switch (toPropertyPath)");
                sb.AppendLine("                {");
                sb.AppendLine($"                    case \"{Escape(key2)}\": return __BindOneWay_{id}(fromObject, targetObject, conversionFunc);");
                sb.AppendLine("                }");
                sb.AppendLine("                break;");
            }

            sb.AppendLine("        }");
        }

        sb.AppendLine("        throw new NotSupportedException(\"No generated binding for provided property paths.\");");
        sb.AppendLine("    }");

        // Emit method bodies for each binding
        foreach (InvocationModel? m in models)
        {
            if (m.FromPath is null || m.ToPath is null || m.FromLambda is null || m.ToLambda is null)
            {
                continue;
            }

            string id = Hash(m.FromPath + "|" + m.ToPath);
            this.EmitOneWayBody(id, m, sb);
        }
    }

    private void EmitBindTwoWay(ImmutableArray<InvocationModel> models, StringBuilder sb)
    {
        sb.AppendLine(
            "    public static partial IDisposable BindTwoWay<TFrom,TFromProperty,TTarget,TTargetProperty>(this TFrom fromObject, TTarget targetObject, Expression<Func<TFrom,TFromProperty>> fromProperty, Expression<Func<TTarget,TTargetProperty>> toProperty, Func<TFromProperty,TTargetProperty>? hostToTargetConv = null, Func<TTargetProperty,TFromProperty>? targetToHostConv = null, [CallerArgumentExpression(\"fromProperty\")] string? fromPropertyPath = null, [CallerArgumentExpression(\"toProperty\")] string? toPropertyPath = null)");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        if (fromPropertyPath is not null && toPropertyPath is not null && BindingRegistry.TryCreateTwoWay<TFrom,TFromProperty,TTarget,TTargetProperty>(fromPropertyPath, toPropertyPath, fromObject, targetObject, hostToTargetConv, targetToHostConv, out var __regDisp)) return __regDisp;");
        if (!models.IsDefaultOrEmpty)
        {
            sb.AppendLine("        switch (fromPropertyPath)");
            sb.AppendLine("        {");
            foreach (InvocationModel? m in models)
            {
                if (m.FromPath is null || m.ToPath is null)
                {
                    continue;
                }

                string? key1 = m.FromPath;
                string? key2 = m.ToPath;
                string id = Hash(key1 + "|" + key2);
                sb.AppendLine($"            case \"{Escape(key1)}\":");
                sb.AppendLine("                switch (toPropertyPath)");
                sb.AppendLine("                {");
                sb.AppendLine(
                    $"                    case \"{Escape(key2)}\": return __BindTwoWay_{id}(fromObject, targetObject, hostToTargetConv, targetToHostConv);");
                sb.AppendLine("                }");
                sb.AppendLine("                break;");
            }

            sb.AppendLine("        }");
        }

        sb.AppendLine("        throw new NotSupportedException(\"No generated two-way binding for provided property paths.\");");
        sb.AppendLine("    }");

        foreach (InvocationModel? m in models)
        {
            if (m.FromPath is null || m.ToPath is null || m.FromLambda is null || m.ToLambda is null)
            {
                continue;
            }

            string id = Hash(m.FromPath + "|" + m.ToPath);
            this.EmitTwoWayBody(id, m, sb);
        }
    }

    private void EmitWhenChanged(ImmutableArray<InvocationModel> models, StringBuilder sb)
    {
        sb.AppendLine(
            "    public static partial Observable<TReturn> WhenChanged<TObj,TReturn>(this TObj objectToMonitor, Expression<Func<TObj,TReturn>> propertyExpression, [CallerArgumentExpression(\"propertyExpression\")] string? propertyExpressionPath = null)");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        if (propertyExpressionPath is not null){ var __key = typeof(TObj).Name + \"|\" + propertyExpressionPath; if (BindingRegistry.TryCreateWhenChanged<TObj,TReturn>(__key, objectToMonitor, out var __obs)) return __obs; }");
        if (!models.IsDefaultOrEmpty && models.Count(m => m.WhenPath is not null) > 0)
        {
            sb.AppendLine("        switch (propertyExpressionPath)");
            sb.AppendLine("        {");
            foreach (InvocationModel? m in models)
            {
                if (m.WhenPath is null)
                {
                    continue;
                }

                string id = Hash(m.WhenRootTypeName + "|" + m.WhenPath);
                sb.AppendLine($"            case \"{Escape(m.WhenPath)}\": return __WhenChanged_{id}(objectToMonitor);");
            }

            sb.AppendLine("        }");
        }

        sb.AppendLine("        throw new NotSupportedException(\"No generated WhenChanged for provided property expression.\");");
        sb.AppendLine("    }");

        foreach (InvocationModel? m in models)
        {
            if (m.WhenPath is null || m.WhenLambda is null)
            {
                continue;
            }

            string id = Hash(m.WhenRootTypeName + "|" + m.WhenPath);
            this.EmitWhenBody(id, m, sb);
        }
    }

    private static string Escape(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private void EmitOneWayBody(string id, InvocationModel m, StringBuilder sb)
    {
        // Closure-free implementation using ObservePropertyChanged and EveryValueChanged hybrid
        sb.AppendLine(
            $"    private static IDisposable __BindOneWay_{id}<TFrom,TFromProperty,TTarget,TTargetProperty>(TFrom fromObject, TTarget targetObject, Func<TFromProperty,TTargetProperty>? convert)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (fromObject is null || targetObject is null) return Disposable.Empty;");
        sb.AppendLine("        convert ??= v => (TTargetProperty)(object?)v!;");
        sb.AppendLine("        var __builder = Disposable.CreateBuilder();");

        // Handle empty chain (direct property access on root)
        if (m.FromSegments.Count == 0)
        {
            sb.AppendLine("        // No segments to observe");
            sb.AppendLine("        return Disposable.Empty;");
            sb.AppendLine("    }");
            return;
        }

        string targetLeafAssign = BuildLeafAssignmentSet("targetObject", m.ToSegments, "converted");
        string fullChainAccess = BuildChainAccess("fromObject", m.FromSegments);

        // For single segment, simple observation
        if (m.FromSegments.Count == 1)
        {
            PropertySegment seg = m.FromSegments[0];
            bool rootImplementsNotify = seg.DeclaringTypeImplementsNotify;

            sb.AppendLine($"        // Single property observation: {seg.Name}");

            if (rootImplementsNotify)
            {
                // Root implements INPC - use ObservePropertyChanged
                string propAccess = BuildObservePropertyAccess(seg, $"(({seg.TypeName})x)");
                sb.AppendLine($"            __builder.Add(fromObject.ObservePropertyChanged(static x => {propAccess})");
                sb.AppendLine("                .Subscribe((fromObject, targetObject, convert), static (val, state) =>");
                sb.AppendLine("                {");
                sb.AppendLine("                    try");
                sb.AppendLine("                    {");
                sb.AppendLine("                        var converted = state.convert((TFromProperty)(object?)val!);");
                sb.AppendLine($"                        {targetLeafAssign}");
                sb.AppendLine("                    }");
                sb.AppendLine("                    catch { }");
                sb.AppendLine("                }));");
                sb.AppendLine("            R3ExtGeneratedInstrumentation.NotifyWires++;");
            }
            else
            {
                // Root doesn't implement INPC - use EveryValueChanged
                sb.AppendLine($"            __builder.Add(Observable.EveryValueChanged(fromObject, static x => {fullChainAccess.Replace("fromObject", "x")})");
                sb.AppendLine("                .Subscribe((fromObject, targetObject, convert), static (val, state) =>");
                sb.AppendLine("                {");
                sb.AppendLine("                    try");
                sb.AppendLine("                    {");
                sb.AppendLine("                        var converted = state.convert((TFromProperty)(object?)val!);");
                sb.AppendLine($"                        {targetLeafAssign}");
                sb.AppendLine("                    }");
                sb.AppendLine("                    catch { }");
                sb.AppendLine("                }));");
            }
        }
        else
        {
            // Multi-segment chain: use Observable.Create with dynamic rewiring
            sb.AppendLine($"        // Multi-level chain observation with dynamic rewiring");
            sb.AppendLine("        __builder.Add(Observable.Create<TFromProperty>(observer =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var subscriptions = new IDisposable?[" + m.FromSegments.Count + "];");

            // Build chain of observables for each segment
            for (int i = 0; i < m.FromSegments.Count; i++)
            {
                PropertySegment seg = m.FromSegments[i];
                sb.AppendLine($"            {seg.TypeName} __seg_{i} = default!;");
            }

            sb.AppendLine("            void UpdateChain()");
            sb.AppendLine("            {");
            for (int i = 0; i < m.FromSegments.Count; i++)
            {
                string access = BuildChainAccess("fromObject", m.FromSegments.Take(i + 1).ToList());
                sb.AppendLine($"                try {{ __seg_{i} = {access}; }} catch {{ __seg_{i} = default!; }}");
            }

            sb.AppendLine("            }");

            sb.AppendLine("            void EmitValue()");
            sb.AppendLine("            {");
            sb.AppendLine("                try { observer.OnNext((TFromProperty)(object?)" + fullChainAccess + "!); } catch { }");
            sb.AppendLine("            }");

            sb.AppendLine("            void Rewire(int fromIndex)");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int i = fromIndex; i < subscriptions.Length; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    subscriptions[i]?.Dispose();");
            sb.AppendLine("                    subscriptions[i] = null;");
            sb.AppendLine("                }");
            sb.AppendLine("                UpdateChain();");
            for (int i = 0; i < m.FromSegments.Count; i++)
            {
                PropertySegment seg = m.FromSegments[i];
                string parentRef = i == 0 ? "fromObject" : $"__seg_{i - 1}";
                string parentType = i == 0 ? "TFrom" : m.FromSegments[i - 1].TypeName;
                bool parentImplementsNotify = i == 0 ? seg.DeclaringTypeImplementsNotify : m.FromSegments[i - 1].IsNotify;

                sb.AppendLine($"                if (fromIndex <= {i})");
                sb.AppendLine("                {");

                if (parentImplementsNotify)
                {
                    // Parent implements INPC - use ObservePropertyChanged
                    string propAccess = BuildObservePropertyAccess(seg, $"(({parentType})x)");
                    sb.AppendLine($"                    subscriptions[{i}] = {parentRef}.ObservePropertyChanged(static x => {propAccess})");
                    sb.AppendLine($"                        .Subscribe(_ => {{ Rewire({i + 1}); EmitValue(); }});");
                    sb.AppendLine("                    R3ExtGeneratedInstrumentation.NotifyWires++;");
                }
                else
                {
                    // Parent doesn't implement INPC - use EveryValueChanged
                    string segAccess = BuildChainAccess("fromObject", m.FromSegments.Take(i + 1).ToList());
                    sb.AppendLine($"                    subscriptions[{i}] = Observable.EveryValueChanged(fromObject, static x => {segAccess.Replace("fromObject", "x")})");
                    sb.AppendLine($"                        .Subscribe(_ => {{ Rewire({i + 1}); EmitValue(); }});");
                }

                sb.AppendLine("                }");
            }

            sb.AppendLine("            }");

            sb.AppendLine("            Rewire(0);");
            sb.AppendLine("            EmitValue();");
            sb.AppendLine("            return Disposable.Create(() =>");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int i = 0; i < subscriptions.Length; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    subscriptions[i]?.Dispose();");
            sb.AppendLine("                }");
            sb.AppendLine("            });");
            sb.AppendLine("        })");
            sb.AppendLine("        .Subscribe((fromObject, targetObject, convert), static (val, state) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var converted = state.convert(val);");
            sb.AppendLine($"                {targetLeafAssign}");
            sb.AppendLine("            }");
            sb.AppendLine("            catch { }");
            sb.AppendLine("        }));");
        }

        // Initial push
        sb.AppendLine("        // Initial value");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var initialVal = (TFromProperty)(object?)" + fullChainAccess + "!;");
        sb.AppendLine("            var converted = convert(initialVal);");
        sb.AppendLine($"            {targetLeafAssign}");
        sb.AppendLine("        }");
        sb.AppendLine("        catch { }");

        sb.AppendLine("        return __builder.Build();");
        sb.AppendLine("    }");
    }

    private void EmitTwoWayBody(string id, InvocationModel m, StringBuilder sb)
    {
        // Closure-free two-way binding using ObservePropertyChanged and EveryValueChanged hybrid
        sb.AppendLine(
            $"    private static IDisposable __BindTwoWay_{id}<TFrom,TFromProperty,TTarget,TTargetProperty>(TFrom fromObject, TTarget targetObject, Func<TFromProperty,TTargetProperty>? hostToTarget, Func<TTargetProperty,TFromProperty>? targetToHost)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (fromObject is null || targetObject is null) return Disposable.Empty;");
        sb.AppendLine("        hostToTarget ??= v => (TTargetProperty)(object?)v!;");
        sb.AppendLine("        targetToHost ??= v => (TFromProperty)(object?)v!;");
        sb.AppendLine("        var __builder = Disposable.CreateBuilder();");
        sb.AppendLine("        var __updating = new System.Threading.ThreadLocal<bool>(() => false);");

        // Handle empty chains
        if (m.FromSegments.Count == 0 || m.ToSegments.Count == 0)
        {
            sb.AppendLine("        // Empty chain - no binding possible");
            sb.AppendLine("        return Disposable.Empty;");
            sb.AppendLine("    }");
            return;
        }

        string fromFullChainAccess = BuildChainAccess("fromObject", m.FromSegments);
        string toFullChainAccess = BuildChainAccess("targetObject", m.ToSegments);
        string fromLeafAssign = BuildLeafAssignmentSet("fromObject", m.FromSegments, "convertedBack");
        string toLeafAssign = BuildLeafAssignmentSet("targetObject", m.ToSegments, "convertedTo");

        // From -> To direction using same pattern as OneWay
        sb.AppendLine("        // From -> To direction");
        if (m.FromSegments.Count == 1)
        {
            PropertySegment seg = m.FromSegments[0];
            bool fromRootImplementsNotify = seg.DeclaringTypeImplementsNotify;

            if (fromRootImplementsNotify)
            {
                // From root implements INPC - use ObservePropertyChanged
                string propAccess = BuildObservePropertyAccess(seg, $"(({seg.TypeName})x)");
                sb.AppendLine($"            __builder.Add(fromObject.ObservePropertyChanged(static x => {propAccess})");
                sb.AppendLine("                .Subscribe((__updating, targetObject, hostToTarget), static (val, state) =>");
                sb.AppendLine("                {");
                sb.AppendLine("                    if (state.__updating.Value) return;");
                sb.AppendLine("                    state.__updating.Value = true;");
                sb.AppendLine("                    try");
                sb.AppendLine("                    {");
                sb.AppendLine("                        var convertedTo = state.hostToTarget((TFromProperty)(object?)val!);");
                sb.AppendLine($"                        {toLeafAssign}");
                sb.AppendLine("                    }");
                sb.AppendLine("                    catch { }");
                sb.AppendLine("                    finally { state.__updating.Value = false; }");
                sb.AppendLine("                }));");
                sb.AppendLine("            R3ExtGeneratedInstrumentation.NotifyWires++;");
            }
            else
            {
                // From root doesn't implement INPC - use EveryValueChanged
                sb.AppendLine($"            __builder.Add(Observable.EveryValueChanged(fromObject, static x => {fromFullChainAccess.Replace("fromObject", "x")})");
                sb.AppendLine("                .Subscribe((__updating, targetObject, hostToTarget), static (val, state) =>");
                sb.AppendLine("                {");
                sb.AppendLine("                    if (state.__updating.Value) return;");
                sb.AppendLine("                    state.__updating.Value = true;");
                sb.AppendLine("                    try");
                sb.AppendLine("                    {");
                sb.AppendLine("                        var convertedTo = state.hostToTarget((TFromProperty)(object?)val!);");
                sb.AppendLine($"                        {toLeafAssign}");
                sb.AppendLine("                    }");
                sb.AppendLine("                    catch { }");
                sb.AppendLine("                    finally { state.__updating.Value = false; }");
                sb.AppendLine("                }));");
            }
        }
        else
        {
            // Multi-segment: use Observable.Create with dynamic rewiring
            sb.AppendLine("        __builder.Add(Observable.Create<TFromProperty>(observer =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var subscriptions = new IDisposable?[" + m.FromSegments.Count + "];");
            for (int i = 0; i < m.FromSegments.Count; i++)
            {
                PropertySegment seg = m.FromSegments[i];
                sb.AppendLine($"            {seg.TypeName} __seg_from_{i} = default!;");
            }

            sb.AppendLine("            void UpdateFromChain()");
            sb.AppendLine("            {");
            for (int i = 0; i < m.FromSegments.Count; i++)
            {
                string access = BuildChainAccess("fromObject", m.FromSegments.Take(i + 1).ToList());
                sb.AppendLine($"                try {{ __seg_from_{i} = {access}; }} catch {{ __seg_from_{i} = default!; }}");
            }

            sb.AppendLine("            }");
            sb.AppendLine("            void EmitFromValue()");
            sb.AppendLine("            {");
            sb.AppendLine("                try { observer.OnNext((TFromProperty)(object?)" + fromFullChainAccess + "!); } catch { }");
            sb.AppendLine("            }");

            EmitRewireLogic(sb, m.FromSegments, "fromObject", "TFrom", "seg_from", "From");

            sb.AppendLine("            RewireFrom(0);");
            sb.AppendLine("            EmitFromValue();");
            sb.AppendLine("            return Disposable.Create(() =>");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int i = 0; i < subscriptions.Length; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    subscriptions[i]?.Dispose();");
            sb.AppendLine("                }");
            sb.AppendLine("            });");
            sb.AppendLine("        })");
            sb.AppendLine("        .Subscribe((__updating, targetObject, hostToTarget), static (val, state) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            if (state.__updating.Value) return;");
            sb.AppendLine("            state.__updating.Value = true;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var convertedTo = state.hostToTarget(val);");
            sb.AppendLine($"                {toLeafAssign}");
            sb.AppendLine("            }");
            sb.AppendLine("            catch { }");
            sb.AppendLine("            finally { state.__updating.Value = false; }");
            sb.AppendLine("        }));");
        }

        // To -> From direction (symmetric)
        sb.AppendLine("        // To -> From direction");
        if (m.ToSegments.Count == 1)
        {
            PropertySegment seg = m.ToSegments[0];
            bool toRootImplementsNotify = seg.DeclaringTypeImplementsNotify;

            if (toRootImplementsNotify)
            {
                // Target root implements INPC - use ObservePropertyChanged
                string propAccess = BuildObservePropertyAccess(seg, $"(({seg.TypeName})x)");
                sb.AppendLine($"            __builder.Add(targetObject.ObservePropertyChanged(static x => {propAccess})");
                sb.AppendLine("                .Subscribe((__updating, fromObject, targetToHost), static (val, state) =>");
                sb.AppendLine("                {");
                sb.AppendLine("                    if (state.__updating.Value) return;");
                sb.AppendLine("                    state.__updating.Value = true;");
                sb.AppendLine("                    try");
                sb.AppendLine("                    {");
                sb.AppendLine("                        var convertedBack = state.targetToHost((TTargetProperty)(object?)val!);");
                sb.AppendLine($"                        {fromLeafAssign}");
                sb.AppendLine("                    }");
                sb.AppendLine("                    catch { }");
                sb.AppendLine("                    finally { state.__updating.Value = false; }");
                sb.AppendLine("                }));");
                sb.AppendLine("            R3ExtGeneratedInstrumentation.NotifyWires++;");
            }
            else
            {
                // Target root doesn't implement INPC - use EveryValueChanged
                sb.AppendLine($"            __builder.Add(Observable.EveryValueChanged(targetObject, static x => {toFullChainAccess.Replace("targetObject", "x")})");
                sb.AppendLine("                .Subscribe((__updating, fromObject, targetToHost), static (val, state) =>");
                sb.AppendLine("                {");
                sb.AppendLine("                    if (state.__updating.Value) return;");
                sb.AppendLine("                    state.__updating.Value = true;");
                sb.AppendLine("                    try");
                sb.AppendLine("                    {");
                sb.AppendLine("                        var convertedBack = state.targetToHost((TTargetProperty)(object?)val!);");
                sb.AppendLine($"                        {fromLeafAssign}");
                sb.AppendLine("                    }");
                sb.AppendLine("                    catch { }");
                sb.AppendLine("                    finally { state.__updating.Value = false; }");
                sb.AppendLine("                }));");
            }
        }
        else
        {
            // Multi-segment: use Observable.Create pattern
            sb.AppendLine("        __builder.Add(Observable.Create<TTargetProperty>(observer =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var innerBuilder = Disposable.CreateBuilder();");
            for (int i = 0; i < m.ToSegments.Count; i++)
            {
                PropertySegment seg = m.ToSegments[i];
                sb.AppendLine($"            {seg.TypeName} __seg_to_{i} = default!;");
            }

            sb.AppendLine("            void UpdateToChain()");
            sb.AppendLine("            {");
            for (int i = 0; i < m.ToSegments.Count; i++)
            {
                string access = BuildChainAccess("targetObject", m.ToSegments.Take(i + 1).ToList());
                sb.AppendLine($"                try {{ __seg_to_{i} = {access}; }} catch {{ __seg_to_{i} = default!; }}");
            }

            sb.AppendLine("            }");
            sb.AppendLine("            void EmitToValue()");
            sb.AppendLine("            {");
            sb.AppendLine("                try { observer.OnNext((TTargetProperty)(object?)" + toFullChainAccess + "!); } catch { }");
            sb.AppendLine("            }");

            for (int i = 0; i < m.ToSegments.Count; i++)
            {
                PropertySegment seg = m.ToSegments[i];
                string parentRef = i == 0 ? "targetObject" : $"__seg_to_{i - 1}";
                string parentType = i == 0 ? "TTarget" : m.ToSegments[i - 1].TypeName;
                bool parentImplementsNotify = i == 0 ? seg.DeclaringTypeImplementsNotify : m.ToSegments[i - 1].IsNotify;

                if (parentImplementsNotify)
                {
                    // Parent implements INPC - use ObservePropertyChanged
                    string propAccess = BuildObservePropertyAccess(seg, $"(({parentType})x)");
                    sb.AppendLine($"                innerBuilder.Add({parentRef}.ObservePropertyChanged(static x => {propAccess})");
                    sb.AppendLine("                    .Subscribe(_ => { UpdateToChain(); EmitToValue(); }));");
                }
                else
                {
                    // Parent doesn't implement INPC - use EveryValueChanged
                    string segAccess = BuildChainAccess("targetObject", m.ToSegments.Take(i + 1).ToList());
                    sb.AppendLine($"                innerBuilder.Add(Observable.EveryValueChanged(targetObject, static x => {segAccess.Replace("targetObject", "x")})");
                    sb.AppendLine("                    .Subscribe(_ => { UpdateToChain(); EmitToValue(); }));");
                }
            }

            sb.AppendLine("            UpdateToChain();");
            sb.AppendLine("            EmitToValue();");
            sb.AppendLine("            return innerBuilder.Build();");
            sb.AppendLine("        })");
            sb.AppendLine("        .Subscribe((__updating, fromObject, targetToHost), static (val, state) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            if (state.__updating.Value) return;");
            sb.AppendLine("            state.__updating.Value = true;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var convertedBack = state.targetToHost(val);");
            sb.AppendLine($"                {fromLeafAssign}");
            sb.AppendLine("            }");
            sb.AppendLine("            catch { }");
            sb.AppendLine("            finally { state.__updating.Value = false; }");
            sb.AppendLine("        }));");
        }

        // Initial sync from -> to
        sb.AppendLine("        // Initial sync");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var initialVal = (TFromProperty)(object?)" + fromFullChainAccess + "!;");
        sb.AppendLine("            var convertedTo = hostToTarget(initialVal);");
        sb.AppendLine($"            {toLeafAssign}");
        sb.AppendLine("        }");
        sb.AppendLine("        catch { }");
        sb.AppendLine("        __builder.Add(__updating);");

        sb.AppendLine("        return __builder.Build();");
        sb.AppendLine("    }");
    }

    private void EmitWhenBody(string id, InvocationModel m, StringBuilder sb)
    {
        // Closure-free WhenChanged implementation using ObservePropertyChanged and EveryValueChanged hybrid
        sb.AppendLine($"    private static Observable<TReturn> __WhenChanged_{id}<TObj,TReturn>(TObj root)");
        sb.AppendLine("    {");

        // If no segments, fallback to polling the entire expression
        if (m.FromSegments.Count == 0)
        {
            string getterFallback = ExtractMemberAccess(m.WhenLambda!, "root");
            sb.AppendLine("        try { return Observable.EveryValueChanged(root, static _ => { try { return " + getterFallback +
                          "; } catch { return default!; } }).Select(static v => (TReturn)v).DistinctUntilChanged(); } catch { return Observable.Empty<TReturn>(); }");
            sb.AppendLine("    }");
            return;
        }

        sb.AppendLine("        if (root is null) return Observable.Empty<TReturn>();");

        string leafAccess = BuildChainAccess("root", m.FromSegments);

        // Single segment: simple observation
        if (m.FromSegments.Count == 1)
        {
            PropertySegment seg = m.FromSegments[0];

            // Use compile-time check: does the declaring type implement INPC?
            if (seg.DeclaringTypeImplementsNotify)
            {
                // Root type implements INPC - use ObservePropertyChanged
                string propAccess = BuildObservePropertyAccess(seg, $"(({seg.TypeName})x)");
                sb.AppendLine($"        return ((INotifyPropertyChanged)root).ObservePropertyChanged(static x => {propAccess})");
                sb.AppendLine("            .Select(static v => (TReturn)(object?)v!)");
                sb.AppendLine("            .DistinctUntilChanged();");
            }
            else
            {
                // Root type does not implement INPC - use EveryValueChanged
                sb.AppendLine($"        return Observable.EveryValueChanged(root, static x => {leafAccess.Replace("root", "x")})");
                sb.AppendLine("            .Select(static v => (TReturn)(object?)v!)");
                sb.AppendLine("            .DistinctUntilChanged();");
            }

            sb.AppendLine("    }");
            return;
        }

        // Multi-segment chain: use Observable.Create with dynamic rewiring
        sb.AppendLine("        return Observable.Create<TReturn>(observer => {");
        sb.AppendLine("            var subscriptions = new IDisposable?[" + m.FromSegments.Count + "];");

        // Segment variables
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment seg = m.FromSegments[i];
            sb.AppendLine($"            {seg.TypeName} __seg_{i} = default!;");
        }

        sb.AppendLine("            void UpdateChain()");
        sb.AppendLine("            {");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            string access = BuildChainAccess("root", m.FromSegments.Take(i + 1).ToList());
            sb.AppendLine($"                try {{ __seg_{i} = {access}; }} catch {{ __seg_{i} = default!; }}");
        }

        sb.AppendLine("            }");

        sb.AppendLine("            void EmitValue()");
        sb.AppendLine("            {");
        sb.AppendLine($"                try {{ observer.OnNext((TReturn)(object?){leafAccess}!); }} catch {{ observer.OnNext(default!); }}");
        sb.AppendLine("            }");

        sb.AppendLine("            void Rewire(int fromIndex)");
        sb.AppendLine("            {");
        sb.AppendLine("                for (int i = fromIndex; i < subscriptions.Length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    subscriptions[i]?.Dispose();");
        sb.AppendLine("                    subscriptions[i] = null;");
        sb.AppendLine("                }");
        sb.AppendLine("                UpdateChain();");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment seg = m.FromSegments[i];
            string parentRef = i == 0 ? "root" : $"__seg_{i - 1}";
            string parentType = i == 0 ? "TObj" : m.FromSegments[i - 1].TypeName;
            string propAccess = BuildObservePropertyAccess(seg, $"(({parentType})x)");

            // Use compile-time check: segment 0's parent is root (use DeclaringTypeImplementsNotify),
            // segment i>0's parent is segment[i-1] (use IsNotify)
            bool parentImplementsNotify = i == 0 ? seg.DeclaringTypeImplementsNotify : m.FromSegments[i - 1].IsNotify;

            sb.AppendLine($"                if (fromIndex <= {i})");
            sb.AppendLine("                {");
            if (parentImplementsNotify)
            {
                // Parent implements INPC - use ObservePropertyChanged
                sb.AppendLine($"                    subscriptions[{i}] = ((INotifyPropertyChanged){parentRef}).ObservePropertyChanged(static x => {propAccess})");
                sb.AppendLine($"                        .Subscribe(_ => {{ Rewire({i + 1}); EmitValue(); }});");
                sb.AppendLine("                    R3ExtGeneratedInstrumentation.NotifyWires++;");
            }
            else
            {
                // Parent does not implement INPC - use EveryValueChanged
                string segAccess = BuildChainAccess("root", m.FromSegments.Take(i + 1).ToList());
                sb.AppendLine($"                    subscriptions[{i}] = Observable.EveryValueChanged(root, static x => {segAccess.Replace("root", "x")})");
                sb.AppendLine($"                        .Subscribe(_ => {{ Rewire({i + 1}); EmitValue(); }});");
            }

            sb.AppendLine("                }");
        }

        sb.AppendLine("            }");

        sb.AppendLine("            Rewire(0);");
        sb.AppendLine("            EmitValue();");
        sb.AppendLine("            return Disposable.Create(() =>");
        sb.AppendLine("            {");
        sb.AppendLine("                for (int i = 0; i < subscriptions.Length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    subscriptions[i]?.Dispose();");
        sb.AppendLine("                }");
        sb.AppendLine("            });");
        sb.AppendLine("        }).DistinctUntilChanged();");
        sb.AppendLine("    }");
    }

    // Registration bodies for non-core assemblies (concrete types)
    private void EmitOneWayRegistrationBody(string id, InvocationModel m, StringBuilder sb)
    {
        // Use same closure-free ObservePropertyChanged pattern as EmitOneWayBody
        sb.AppendLine(
            $"    private static IDisposable __RegBindOneWay_{id}({m.RootFromTypeName} fromObject, {m.RootTargetTypeName} targetObject, Func<{m.FromLeafTypeName},{m.TargetLeafTypeName}>? convert)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (fromObject is null || targetObject is null) return Disposable.Empty;");
        sb.AppendLine("        convert ??= v => (" + m.TargetLeafTypeName + ")(object?)v!;");
        sb.AppendLine("        var __builder = Disposable.CreateBuilder();");

        if (m.FromSegments.Count == 0)
        {
            sb.AppendLine("        return Disposable.Empty;");
            sb.AppendLine("    }");
            return;
        }

        string targetLeafAssign = BuildLeafAssignmentSet("targetObject", m.ToSegments, "converted");
        string fullChainAccess = BuildChainAccess("fromObject", m.FromSegments);

        // Single segment: simple observation
        if (m.FromSegments.Count == 1)
        {
            PropertySegment seg = m.FromSegments[0];

            // Use compile-time check: does the declaring type implement INPC?
            if (seg.DeclaringTypeImplementsNotify)
            {
                // Declaring type implements INPC - use ObservePropertyChanged
                string propAccess = BuildObservePropertyAccess(seg, $"(({m.RootFromTypeName})x)");
                sb.AppendLine($"        __builder.Add(((INotifyPropertyChanged)fromObject).ObservePropertyChanged(static x => {propAccess})");
                sb.AppendLine("            .Subscribe((fromObject, targetObject, convert), static (val, state) =>");
                sb.AppendLine("            {");
                sb.AppendLine("                var targetObject = state.targetObject;");
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine("                    var converted = state.convert((" + m.FromLeafTypeName + ")(object?)val!);");
                sb.AppendLine($"                    {targetLeafAssign}");
                sb.AppendLine("                }");
                sb.AppendLine("                catch { }");
                sb.AppendLine("            }));");
                sb.AppendLine("        R3ExtGeneratedInstrumentation.NotifyWires++;");
            }
            else
            {
                // Declaring type does not implement INPC - use EveryValueChanged
                sb.AppendLine($"        __builder.Add(Observable.EveryValueChanged(fromObject, x => {fullChainAccess.Replace("fromObject", "x")})");
                sb.AppendLine("            .Subscribe((fromObject, targetObject, convert), static (val, state) =>");
                sb.AppendLine("            {");
                sb.AppendLine("                var targetObject = state.targetObject;");
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine("                    var converted = state.convert((" + m.FromLeafTypeName + ")(object?)val!);");
                sb.AppendLine($"                    {targetLeafAssign}");
                sb.AppendLine("                }");
                sb.AppendLine("                catch { }");
                sb.AppendLine("            }));");
            }
        }
        else
        {
            // Multi-segment: use Observable.Create with closure-free sealed class pattern
            string stateClassName = $"OneWayState_{m.FromSegments.Count}_{id}";

            sb.AppendLine("        __builder.Add(Observable.Create<" + m.FromLeafTypeName + ">(observer =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            var state = new {stateClassName}(observer, fromObject);");
            sb.AppendLine("            state.Initialize();");
            sb.AppendLine("            return state;");
            sb.AppendLine("        })");
            sb.AppendLine("        .Subscribe((fromObject, targetObject, convert), static (val, state) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var targetObject = state.targetObject;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var converted = state.convert(val);");
            sb.AppendLine($"                {targetLeafAssign}");
            sb.AppendLine("            }");
            sb.AppendLine("            catch { }");
            sb.AppendLine("        }));");
        }

        // Initial push
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var initialVal = (" + m.FromLeafTypeName + ")(object?)" + fullChainAccess + "!;");
        sb.AppendLine("            var converted = convert(initialVal);");
        sb.AppendLine($"            {targetLeafAssign}");
        sb.AppendLine("        }");
        sb.AppendLine("        catch { }");

        sb.AppendLine("        return __builder.Build();");
        sb.AppendLine("    }");

        // Generate state class if multi-segment OneWay binding was used
        if (m.FromSegments.Count > 1)
        {
            string oneWayStateClassName = $"OneWayState_{m.FromSegments.Count}_{id}";
            sb.AppendLine(string.Empty);
            sb.AppendLine($"    sealed class {oneWayStateClassName} : IDisposable");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly Observer<{m.FromLeafTypeName}> observer;");
            sb.AppendLine($"        private readonly {m.RootFromTypeName} root;");
            sb.AppendLine($"        private readonly IDisposable?[] subscriptions = new IDisposable?[{m.FromSegments.Count}];");
            sb.AppendLine("        private bool rewiring;");

            for (int i = 0; i < m.FromSegments.Count; i++)
            {
                PropertySegment seg = m.FromSegments[i];
                sb.AppendLine($"        private {seg.TypeName} seg_{i};");
            }

            sb.AppendLine(string.Empty);
            sb.AppendLine($"        public {oneWayStateClassName}(Observer<{m.FromLeafTypeName}> observer, {m.RootFromTypeName} root)");
            sb.AppendLine("        {");
            sb.AppendLine("            this.observer = observer;");
            sb.AppendLine("            this.root = root;");
            sb.AppendLine("        }");

            sb.AppendLine(string.Empty);
            sb.AppendLine("        public void Initialize()");
            sb.AppendLine("        {");
            sb.AppendLine("            UpdateChain();");
            sb.AppendLine("            Rewire(0);");
            sb.AppendLine("            EmitValue();");
            sb.AppendLine("        }");

            sb.AppendLine(string.Empty);
            sb.AppendLine("        private void UpdateChain()");
            sb.AppendLine("        {");
            for (int i = 0; i < m.FromSegments.Count; i++)
            {
                string access = BuildChainAccess("root", m.FromSegments.Take(i + 1).ToList());
                sb.AppendLine($"            try {{ seg_{i} = {access}; }} catch {{ seg_{i} = default!; }}");
            }

            sb.AppendLine("        }");

            sb.AppendLine(string.Empty);
            sb.AppendLine("        private void EmitValue()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (rewiring) return;");
            string fullChainAccessWithRoot = BuildChainAccess("root", m.FromSegments);
            sb.AppendLine("            try { observer.OnNext((" + m.FromLeafTypeName + ")(object?)" + fullChainAccessWithRoot + "!); } catch { observer.OnNext(default!); }");
            sb.AppendLine("        }");

            sb.AppendLine(string.Empty);
            sb.AppendLine("        private void Rewire(int fromIndex)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (rewiring) return;");
            sb.AppendLine("            rewiring = true;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int i = fromIndex; i < subscriptions.Length; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    subscriptions[i]?.Dispose();");
            sb.AppendLine("                    subscriptions[i] = null;");
            sb.AppendLine("                }");
            sb.AppendLine("                UpdateChain();");

            for (int i = 0; i < m.FromSegments.Count; i++)
            {
                PropertySegment seg = m.FromSegments[i];
                string parentRef = i == 0 ? "root" : $"seg_{i - 1}";
                string parentType = i == 0 ? m.RootFromTypeName! : m.FromSegments[i - 1].TypeName;
                bool parentImplementsNotify = i == 0 ? seg.DeclaringTypeImplementsNotify : m.FromSegments[i - 1].IsNotify;

                sb.AppendLine($"                if (fromIndex <= {i})");
                sb.AppendLine("                {");

                if (parentImplementsNotify)
                {
                    // Parent implements INPC - use ObservePropertyChanged
                    // Add null check for parent segment (handles null intermediates gracefully)
                    if (i > 0)
                    {
                        sb.AppendLine($"                    if ({parentRef} != null)");
                        sb.AppendLine("                    {");
                    }

                    string indent = i > 0 ? "    " : string.Empty;
                    string propAccess = BuildObservePropertyAccess(seg, $"(({parentType})(object)x)");
                    sb.AppendLine($"                    {indent}subscriptions[{i}] = {parentRef}.ObservePropertyChanged(static x => {propAccess})");
                    sb.AppendLine($"                    {indent}    .Subscribe(this, static (_, s) => {{ s.Rewire({i + 1}); s.EmitValue(); }});");
                    sb.AppendLine($"                    {indent}R3ExtGeneratedInstrumentation.NotifyWires++;");

                    if (i > 0)
                    {
                        sb.AppendLine("                    }");
                    }
                }
                else
                {
                    // Parent doesn't implement INPC - use EveryValueChanged
                    string segAccess = BuildChainAccess("root", m.FromSegments.Take(i + 1).ToList());
                    sb.AppendLine($"                    subscriptions[{i}] = Observable.EveryValueChanged(root, x => {segAccess.Replace("root", "x")})");
                    sb.AppendLine($"                        .Subscribe(this, static (_, s) => {{ s.Rewire({i + 1}); s.EmitValue(); }});");
                }

                sb.AppendLine("                }");
            }

            sb.AppendLine("            }");
            sb.AppendLine("            finally { rewiring = false; }");
            sb.AppendLine("        }");

            sb.AppendLine(string.Empty);
            sb.AppendLine("        public void Dispose()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (subscriptions != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int i = 0; i < subscriptions.Length; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    subscriptions[i]?.Dispose();");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }
    }

    private void EmitTwoWayRegistrationBody(string id, InvocationModel m, StringBuilder sb)
    {
        // Use same closure-free ObservePropertyChanged pattern as EmitTwoWayBody
        sb.AppendLine(
            $"    private static IDisposable __RegBindTwoWay_{id}({m.RootFromTypeName} fromObject, {m.RootTargetTypeName} targetObject, Func<{m.FromLeafTypeName},{m.TargetLeafTypeName}>? hostToTargetConv, Func<{m.TargetLeafTypeName},{m.FromLeafTypeName}>? targetToHostConv)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (fromObject is null || targetObject is null) return Disposable.Empty;");
        sb.AppendLine("        hostToTargetConv ??= v => (" + m.TargetLeafTypeName + ")(object?)v!;");
        sb.AppendLine("        targetToHostConv ??= v => (" + m.FromLeafTypeName + ")(object?)v!;");
        sb.AppendLine("        var __builder = Disposable.CreateBuilder();");
        sb.AppendLine("        var __updating = new System.Threading.ThreadLocal<bool>(() => false);");

        if (m.FromSegments.Count == 0 || m.ToSegments.Count == 0)
        {
            sb.AppendLine("        return Disposable.Empty;");
            sb.AppendLine("    }");
            return;
        }

        string fromFullChainAccess = BuildChainAccess("fromObject", m.FromSegments);
        string toFullChainAccess = BuildChainAccess("targetObject", m.ToSegments);
        string fromLeafAssign = BuildLeafAssignmentSet("fromObject", m.FromSegments, "convertedBack");
        string toLeafAssign = BuildLeafAssignmentSet("targetObject", m.ToSegments, "convertedTo");

        // From -> To direction
        if (m.FromSegments.Count == 1)
        {
            PropertySegment seg = m.FromSegments[0];

            // Use compile-time check: does the declaring type implement INPC?
            if (seg.DeclaringTypeImplementsNotify)
            {
                // Declaring type implements INPC - use ObservePropertyChanged
                string propAccess = BuildObservePropertyAccess(seg, $"(({m.RootFromTypeName})x)");
                sb.AppendLine($"        __builder.Add(((INotifyPropertyChanged)fromObject).ObservePropertyChanged(static x => {propAccess})");
                sb.AppendLine("            .Subscribe((__updating, targetObject, hostToTargetConv), static (val, state) =>");
                sb.AppendLine("            {");
                sb.AppendLine("                var targetObject = state.targetObject;");
                sb.AppendLine("                if (state.__updating.Value) return;");
                sb.AppendLine("                state.__updating.Value = true;");
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine("                    var convertedTo = state.hostToTargetConv((" + m.FromLeafTypeName + ")(object?)val!);");
                sb.AppendLine($"                    {toLeafAssign}");
                sb.AppendLine("                    R3ExtGeneratedInstrumentation.BindUpdates++;");
                sb.AppendLine("                }");
                sb.AppendLine("                catch { }");
                sb.AppendLine("                finally { state.__updating.Value = false; }");
                sb.AppendLine("            }));");
                sb.AppendLine("        R3ExtGeneratedInstrumentation.NotifyWires++;");
            }
            else
            {
                // Declaring type does not implement INPC - use EveryValueChanged
                sb.AppendLine($"        __builder.Add(Observable.EveryValueChanged(fromObject, x => {fromFullChainAccess.Replace("fromObject", "x")})");
                sb.AppendLine("            .Subscribe((__updating, targetObject, hostToTargetConv), static (val, state) =>");
                sb.AppendLine("            {");
                sb.AppendLine("                var targetObject = state.targetObject;");
                sb.AppendLine("                if (state.__updating.Value) return;");
                sb.AppendLine("                state.__updating.Value = true;");
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine("                    var convertedTo = state.hostToTargetConv((" + m.FromLeafTypeName + ")(object?)val!);");
                sb.AppendLine($"                    {toLeafAssign}");
                sb.AppendLine("                    R3ExtGeneratedInstrumentation.BindUpdates++;");
                sb.AppendLine("                }");
                sb.AppendLine("                catch { }");
                sb.AppendLine("                finally { state.__updating.Value = false; }");
                sb.AppendLine("            }));");
            }
        }
        else
        {
            // Multi-segment from chain with dynamic rewiring (closure-free)
            string fromStateClassName = $"TwoWayFromState_{m.FromSegments.Count}_{id}";
            sb.AppendLine("        __builder.Add(Observable.Create<" + m.FromLeafTypeName + ">(observer =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            var state = new {fromStateClassName}(observer, fromObject);");
            sb.AppendLine("            state.Initialize();");
            sb.AppendLine("            return state;");
            sb.AppendLine("        })");
            sb.AppendLine("        .Subscribe((__updating, targetObject, hostToTargetConv), static (val, state) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var targetObject = state.targetObject;");
            sb.AppendLine("            if (state.__updating.Value) return;");
            sb.AppendLine("            state.__updating.Value = true;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var convertedTo = state.hostToTargetConv(val);");
            sb.AppendLine($"                {toLeafAssign}");
            sb.AppendLine("                R3ExtGeneratedInstrumentation.BindUpdates++;");
            sb.AppendLine("            }");
            sb.AppendLine("            catch { }");
            sb.AppendLine("            finally { state.__updating.Value = false; }");
            sb.AppendLine("        }));");
        }

        // To -> From direction
        if (m.ToSegments.Count == 1)
        {
            PropertySegment seg = m.ToSegments[0];

            // Use compile-time check: does the declaring type implement INPC?
            if (seg.DeclaringTypeImplementsNotify)
            {
                // Declaring type implements INPC - use ObservePropertyChanged
                string propAccess = BuildObservePropertyAccess(seg, $"(({m.RootTargetTypeName})x)");
                sb.AppendLine($"        __builder.Add(((INotifyPropertyChanged)targetObject).ObservePropertyChanged(static x => {propAccess})");
                sb.AppendLine("            .Subscribe((__updating, fromObject, targetToHostConv), static (val, state) =>");
                sb.AppendLine("            {");
                sb.AppendLine("                var fromObject = state.fromObject;");
                sb.AppendLine("                if (state.__updating.Value) return;");
                sb.AppendLine("                state.__updating.Value = true;");
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine("                    var convertedBack = state.targetToHostConv((" + m.TargetLeafTypeName + ")(object?)val!);");
                sb.AppendLine($"                    {fromLeafAssign}");
                sb.AppendLine("                    R3ExtGeneratedInstrumentation.BindUpdates++;");
                sb.AppendLine("                }");
                sb.AppendLine("                catch { }");
                sb.AppendLine("                finally { state.__updating.Value = false; }");
                sb.AppendLine("            }));");
                sb.AppendLine("        R3ExtGeneratedInstrumentation.NotifyWires++;");
            }
            else
            {
                // Declaring type does not implement INPC - use EveryValueChanged
                sb.AppendLine($"        __builder.Add(Observable.EveryValueChanged(targetObject, x => {toFullChainAccess.Replace("targetObject", "x")})");
                sb.AppendLine("            .Subscribe((__updating, fromObject, targetToHostConv), static (val, state) =>");
                sb.AppendLine("            {");
                sb.AppendLine("                var fromObject = state.fromObject;");
                sb.AppendLine("                if (state.__updating.Value) return;");
                sb.AppendLine("                state.__updating.Value = true;");
                sb.AppendLine("                try");
                sb.AppendLine("                {");
                sb.AppendLine("                    var convertedBack = state.targetToHostConv((" + m.TargetLeafTypeName + ")(object?)val!);");
                sb.AppendLine($"                    {fromLeafAssign}");
                sb.AppendLine("                    R3ExtGeneratedInstrumentation.BindUpdates++;");
                sb.AppendLine("                }");
                sb.AppendLine("                catch { }");
                sb.AppendLine("                finally { state.__updating.Value = false; }");
                sb.AppendLine("            }));");
            }
        }
        else
        {
            // Multi-segment to chain with dynamic rewiring (closure-free)
            string toStateClassName = $"TwoWayToState_{m.ToSegments.Count}_{id}";
            sb.AppendLine("        __builder.Add(Observable.Create<" + m.TargetLeafTypeName + ">(observer =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            var state = new {toStateClassName}(observer, targetObject);");
            sb.AppendLine("            state.Initialize();");
            sb.AppendLine("            return state;");
            sb.AppendLine("        })");
            sb.AppendLine("        .Subscribe((__updating, fromObject, targetToHostConv), static (val, state) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var fromObject = state.fromObject;");
            sb.AppendLine("            if (state.__updating.Value) return;");
            sb.AppendLine("            state.__updating.Value = true;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var convertedBack = state.targetToHostConv(val);");
            sb.AppendLine($"                {fromLeafAssign}");
            sb.AppendLine("                R3ExtGeneratedInstrumentation.BindUpdates++;");
            sb.AppendLine("            }");
            sb.AppendLine("            catch { }");
            sb.AppendLine("            finally { state.__updating.Value = false; }");
            sb.AppendLine("        }));");
        }

        // Initial sync
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var initialVal = (" + m.FromLeafTypeName + ")(object?)" + fromFullChainAccess + "!;");
        sb.AppendLine("            var convertedTo = hostToTargetConv(initialVal);");
        sb.AppendLine($"            {toLeafAssign}");
        sb.AppendLine("        }");
        sb.AppendLine("        catch { }");
        sb.AppendLine("        __builder.Add(__updating);");

        sb.AppendLine("        return __builder.Build();");
        sb.AppendLine("    }");

        // Generate state classes for multi-segment TwoWay chains
        if (m.FromSegments.Count > 1)
        {
            string fromStateClassName = $"TwoWayFromState_{m.FromSegments.Count}_{id}";
            sb.AppendLine(string.Empty);
            sb.AppendLine($"    sealed class {fromStateClassName} : IDisposable");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly Observer<{m.FromLeafTypeName}> observer;");
            sb.AppendLine($"        private readonly {m.RootFromTypeName} root;");
            sb.AppendLine($"        private readonly IDisposable?[] subscriptions = new IDisposable?[{m.FromSegments.Count}];");
            sb.AppendLine("        private bool rewiring;");

            for (int i = 0; i < m.FromSegments.Count; i++)
            {
                PropertySegment seg = m.FromSegments[i];
                sb.AppendLine($"        private {seg.TypeName} seg_{i};");
            }

            sb.AppendLine(string.Empty);
            sb.AppendLine($"        public {fromStateClassName}(Observer<{m.FromLeafTypeName}> observer, {m.RootFromTypeName} root)");
            sb.AppendLine("        {");
            sb.AppendLine("            this.observer = observer;");
            sb.AppendLine("            this.root = root;");
            sb.AppendLine("        }");

            sb.AppendLine(string.Empty);
            sb.AppendLine("        public void Initialize()");
            sb.AppendLine("        {");
            sb.AppendLine("            UpdateChain();");
            sb.AppendLine("            Rewire(0);");
            sb.AppendLine("            EmitValue();");
            sb.AppendLine("        }");

            sb.AppendLine(string.Empty);
            sb.AppendLine("        private void UpdateChain()");
            sb.AppendLine("        {");
            for (int i = 0; i < m.FromSegments.Count; i++)
            {
                string access = BuildChainAccess("root", m.FromSegments.Take(i + 1).ToList());
                sb.AppendLine($"            try {{ seg_{i} = {access}; }} catch {{ seg_{i} = default!; }}");
            }

            sb.AppendLine("        }");

            sb.AppendLine(string.Empty);
            sb.AppendLine("        private void EmitValue()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (rewiring) return;");
            string fromFullChainAccessWithRoot = BuildChainAccess("root", m.FromSegments);
            sb.AppendLine("            try { observer.OnNext((" + m.FromLeafTypeName + ")(object?)" + fromFullChainAccessWithRoot + "!); } catch { observer.OnNext(default!); }");
            sb.AppendLine("        }");

            sb.AppendLine(string.Empty);
            sb.AppendLine("        private void Rewire(int fromIndex)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (rewiring) return;");
            sb.AppendLine("            rewiring = true;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int i = fromIndex; i < subscriptions.Length; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    subscriptions[i]?.Dispose();");
            sb.AppendLine("                    subscriptions[i] = null;");
            sb.AppendLine("                }");
            sb.AppendLine("                UpdateChain();");

            for (int i = 0; i < m.FromSegments.Count; i++)
            {
                PropertySegment seg = m.FromSegments[i];
                string parentRef = i == 0 ? "root" : $"seg_{i - 1}";
                string parentType = i == 0 ? m.RootFromTypeName! : m.FromSegments[i - 1].TypeName;
                bool parentImplementsNotify = i == 0 ? seg.DeclaringTypeImplementsNotify : m.FromSegments[i - 1].IsNotify;

                sb.AppendLine($"                if (fromIndex <= {i})");
                sb.AppendLine("                {");

                if (parentImplementsNotify)
                {
                    // Parent implements INPC - use ObservePropertyChanged
                    // Add null check for parent segment (handles null intermediates gracefully)
                    if (i > 0)
                    {
                        sb.AppendLine($"                    if ({parentRef} != null)");
                        sb.AppendLine("                    {");
                    }

                    string indent = i > 0 ? "    " : string.Empty;
                    string propAccess = BuildObservePropertyAccess(seg, $"(({parentType})(object)x)");
                    sb.AppendLine($"                    {indent}subscriptions[{i}] = {parentRef}.ObservePropertyChanged(static x => {propAccess})");
                    sb.AppendLine($"                    {indent}    .Subscribe(this, static (_, s) => {{ s.Rewire({i + 1}); s.EmitValue(); }});");
                    sb.AppendLine($"                    {indent}R3ExtGeneratedInstrumentation.NotifyWires++;");

                    if (i > 0)
                    {
                        sb.AppendLine("                    }");
                    }
                }
                else
                {
                    // Parent doesn't implement INPC - use EveryValueChanged
                    string segAccess = BuildChainAccess("root", m.FromSegments.Take(i + 1).ToList());
                    sb.AppendLine($"                    subscriptions[{i}] = Observable.EveryValueChanged(root, x => {segAccess.Replace("root", "x")})");
                    sb.AppendLine($"                        .Subscribe(this, static (_, s) => {{ s.Rewire({i + 1}); s.EmitValue(); }});");
                }

                sb.AppendLine("                }");
            }

            sb.AppendLine("            }");
            sb.AppendLine("            finally { rewiring = false; }");
            sb.AppendLine("        }");

            sb.AppendLine(string.Empty);
            sb.AppendLine("        public void Dispose()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (subscriptions != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int i = 0; i < subscriptions.Length; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    subscriptions[i]?.Dispose();");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }

        if (m.ToSegments.Count > 1)
        {
            string toStateClassName = $"TwoWayToState_{m.ToSegments.Count}_{id}";
            sb.AppendLine(string.Empty);
            sb.AppendLine($"    sealed class {toStateClassName} : IDisposable");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly Observer<{m.TargetLeafTypeName}> observer;");
            sb.AppendLine($"        private readonly {m.RootTargetTypeName} root;");
            sb.AppendLine($"        private readonly IDisposable?[] subscriptions = new IDisposable?[{m.ToSegments.Count}];");
            sb.AppendLine("        private bool rewiring;");

            for (int i = 0; i < m.ToSegments.Count; i++)
            {
                PropertySegment seg = m.ToSegments[i];
                sb.AppendLine($"        private {seg.TypeName} seg_{i};");
            }

            sb.AppendLine(string.Empty);
            sb.AppendLine($"        public {toStateClassName}(Observer<{m.TargetLeafTypeName}> observer, {m.RootTargetTypeName} root)");
            sb.AppendLine("        {");
            sb.AppendLine("            this.observer = observer;");
            sb.AppendLine("            this.root = root;");
            sb.AppendLine("        }");

            sb.AppendLine(string.Empty);
            sb.AppendLine("        public void Initialize()");
            sb.AppendLine("        {");
            sb.AppendLine("            UpdateChain();");
            sb.AppendLine("            Rewire(0);");
            sb.AppendLine("            EmitValue();");
            sb.AppendLine("        }");

            sb.AppendLine(string.Empty);
            sb.AppendLine("        private void UpdateChain()");
            sb.AppendLine("        {");
            for (int i = 0; i < m.ToSegments.Count; i++)
            {
                string access = BuildChainAccess("root", m.ToSegments.Take(i + 1).ToList());
                sb.AppendLine($"            try {{ seg_{i} = {access}; }} catch {{ seg_{i} = default!; }}");
            }

            sb.AppendLine("        }");

            sb.AppendLine(string.Empty);
            sb.AppendLine("        private void EmitValue()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (rewiring) return;");
            string toFullChainAccessWithRoot = BuildChainAccess("root", m.ToSegments);
            sb.AppendLine("            try { observer.OnNext((" + m.TargetLeafTypeName + ")(object?)" + toFullChainAccessWithRoot + "!); } catch { observer.OnNext(default!); }");
            sb.AppendLine("        }");

            sb.AppendLine(string.Empty);
            sb.AppendLine("        private void Rewire(int fromIndex)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (rewiring) return;");
            sb.AppendLine("            rewiring = true;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int i = fromIndex; i < subscriptions.Length; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    subscriptions[i]?.Dispose();");
            sb.AppendLine("                    subscriptions[i] = null;");
            sb.AppendLine("                }");
            sb.AppendLine("                UpdateChain();");

            for (int i = 0; i < m.ToSegments.Count; i++)
            {
                PropertySegment seg = m.ToSegments[i];
                string parentRef = i == 0 ? "root" : $"seg_{i - 1}";
                string parentType = i == 0 ? m.RootTargetTypeName! : m.ToSegments[i - 1].TypeName;
                bool parentImplementsNotify = i == 0 ? seg.DeclaringTypeImplementsNotify : m.ToSegments[i - 1].IsNotify;

                sb.AppendLine($"                if (fromIndex <= {i})");
                sb.AppendLine("                {");

                if (parentImplementsNotify)
                {
                    // Parent implements INPC - use ObservePropertyChanged
                    // Add null check for parent segment (handles null intermediates gracefully)
                    if (i > 0)
                    {
                        sb.AppendLine($"                    if ({parentRef} != null)");
                        sb.AppendLine("                    {");
                    }

                    string indent = i > 0 ? "    " : string.Empty;
                    string propAccess = BuildObservePropertyAccess(seg, $"(({parentType})(object)x)");
                    sb.AppendLine($"                    {indent}subscriptions[{i}] = {parentRef}.ObservePropertyChanged(static x => {propAccess})");
                    sb.AppendLine($"                    {indent}    .Subscribe(this, static (_, s) => {{ s.Rewire({i + 1}); s.EmitValue(); }});");
                    sb.AppendLine($"                    {indent}R3ExtGeneratedInstrumentation.NotifyWires++;");

                    if (i > 0)
                    {
                        sb.AppendLine("                    }");
                    }
                }
                else
                {
                    // Parent doesn't implement INPC - use EveryValueChanged
                    string segAccess = BuildChainAccess("root", m.ToSegments.Take(i + 1).ToList());
                    sb.AppendLine($"                    subscriptions[{i}] = Observable.EveryValueChanged(root, x => {segAccess.Replace("root", "x")})");
                    sb.AppendLine($"                        .Subscribe(this, static (_, s) => {{ s.Rewire({i + 1}); s.EmitValue(); }});");
                }

                sb.AppendLine("                }");
            }

            sb.AppendLine("            }");
            sb.AppendLine("            finally { rewiring = false; }");
            sb.AppendLine("        }");

            sb.AppendLine(string.Empty);
            sb.AppendLine("        public void Dispose()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (subscriptions != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int i = 0; i < subscriptions.Length; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    subscriptions[i]?.Dispose();");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }
    }

    private void EmitWhenRegistrationBody(string id, InvocationModel m, StringBuilder sb)
    {
        // Use same closure-free ObservePropertyChanged pattern as EmitWhenBody
        sb.AppendLine($"    private static Observable<{m.WhenLeafTypeName}> __RegWhenChanged_{id}({m.WhenRootTypeName} root)");
        sb.AppendLine("    {");

        if (m.FromSegments.Count == 0)
        {
            string getterFallback = ExtractMemberAccess(m.WhenLambda!, "root");
            sb.AppendLine("        try { return Observable.EveryValueChanged(root, _ => { try { return " + getterFallback +
                          "; } catch { return default!; } }).Select(static v => (" + m.WhenLeafTypeName +
                          ")v).DistinctUntilChanged(); } catch { return Observable.Empty<" + m.WhenLeafTypeName + ">(); }");
            sb.AppendLine("    }");
            return;
        }

        sb.AppendLine("        if (root is null) return Observable.Empty<" + m.WhenLeafTypeName + ">();");

        string leafAccess = BuildChainAccess("root", m.FromSegments);

        // Single segment: simple observation
        if (m.FromSegments.Count == 1)
        {
            PropertySegment seg = m.FromSegments[0];
            bool rootImplementsNotify = seg.DeclaringTypeImplementsNotify;

            if (rootImplementsNotify)
            {
                // Root implements INPC - use ObservePropertyChanged
                string propAccess = BuildObservePropertyAccess(seg, $"(({m.WhenRootTypeName})(object)x)");
                sb.AppendLine($"        return root.ObservePropertyChanged(static x => {propAccess})");
                sb.AppendLine("            .Select(static v => (" + m.WhenLeafTypeName + ")(object?)v!)");
                sb.AppendLine("            .DistinctUntilChanged();");
            }
            else
            {
                // Root doesn't implement INPC - use EveryValueChanged
                sb.AppendLine($"        return Observable.EveryValueChanged(root, x => {leafAccess.Replace("root", "x")})");
                sb.AppendLine("            .Select(static v => (" + m.WhenLeafTypeName + ")(object?)v!)");
                sb.AppendLine("            .DistinctUntilChanged();");
            }

            sb.AppendLine("    }");
            return;
        }

        // Multi-segment: use Observable.Create with dynamic rewiring (closure-free via class state)
        string stateType = $"WhenState_{m.FromSegments.Count}_{id}";
        sb.AppendLine("        return Observable.Create<" + m.WhenLeafTypeName + ">(observer => {");
        sb.AppendLine($"            var state = new {stateType}(observer, root);");
        sb.AppendLine("            state.Initialize();");
        sb.AppendLine("            return state;");
        sb.AppendLine("        }).DistinctUntilChanged();");
        sb.AppendLine("    }");
        sb.AppendLine(string.Empty);

        // Generate state class for this specific chain
        sb.AppendLine($"    sealed class {stateType} : IDisposable");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly Observer<{m.WhenLeafTypeName}> observer;");
        sb.AppendLine($"        private readonly {m.WhenRootTypeName} root;");
        sb.AppendLine($"        private readonly IDisposable?[] subscriptions = new IDisposable?[{m.FromSegments.Count}];");
        sb.AppendLine("        private bool rewiring;");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment seg = m.FromSegments[i];
            sb.AppendLine($"        private {seg.TypeName} seg_{i};");
        }

        sb.AppendLine(string.Empty);
        sb.AppendLine($"        public {stateType}(Observer<{m.WhenLeafTypeName}> observer, {m.WhenRootTypeName} root)");
        sb.AppendLine("        {");
        sb.AppendLine("            this.observer = observer;");
        sb.AppendLine("            this.root = root;");
        sb.AppendLine("        }");

        sb.AppendLine(string.Empty);
        sb.AppendLine("        public void Initialize()");
        sb.AppendLine("        {");
        sb.AppendLine("            UpdateChain();");
        sb.AppendLine("            Rewire(0);");
        sb.AppendLine("            EmitValue();");
        sb.AppendLine("        }");

        sb.AppendLine(string.Empty);
        sb.AppendLine("        private void UpdateChain()");
        sb.AppendLine("        {");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            string access = BuildChainAccess("root", m.FromSegments.Take(i + 1).ToList());
            sb.AppendLine($"            try {{ seg_{i} = {access}; }} catch {{ seg_{i} = default!; }}");
        }

        sb.AppendLine("        }");

        sb.AppendLine(string.Empty);
        sb.AppendLine("        private void EmitValue()");
        sb.AppendLine("        {");
        sb.AppendLine("            if (rewiring) return;");
        string emitAccess = leafAccess.Replace("__seg_", "seg_");
        sb.AppendLine("            try { observer.OnNext((" + m.WhenLeafTypeName + ")(object?)" + emitAccess + "!); } catch { observer.OnNext(default!); }");
        sb.AppendLine("        }");

        sb.AppendLine(string.Empty);
        sb.AppendLine("        private void Rewire(int fromIndex)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (rewiring) return;");
        sb.AppendLine("            rewiring = true;");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                for (int i = fromIndex; i < subscriptions.Length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    subscriptions[i]?.Dispose();");
        sb.AppendLine("                    subscriptions[i] = null;");
        sb.AppendLine("                }");
        sb.AppendLine("                UpdateChain();");

        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment seg = m.FromSegments[i];
            string parentRef = i == 0 ? "root" : $"seg_{i - 1}";
            string parentType = i == 0 ? m.WhenRootTypeName! : m.FromSegments[i - 1].TypeName;
            bool parentImplementsNotify = i == 0 ? seg.DeclaringTypeImplementsNotify : m.FromSegments[i - 1].IsNotify;

            sb.AppendLine($"                if (fromIndex <= {i})");
            sb.AppendLine("                {");

            if (parentImplementsNotify)
            {
                // Parent implements INPC - use ObservePropertyChanged
                // Add null check for parent segment (handles null intermediates gracefully)
                if (i > 0)
                {
                    sb.AppendLine($"                    if ({parentRef} != null)");
                    sb.AppendLine("                    {");
                }

                string indent = i > 0 ? "    " : string.Empty;
                string propAccess = BuildObservePropertyAccess(seg, $"(({parentType})(object)x)");
                sb.AppendLine($"                    {indent}subscriptions[{i}] = {parentRef}.ObservePropertyChanged(static x => {propAccess})");
                sb.AppendLine($"                    {indent}    .Subscribe(this, static (_, s) => {{ s.Rewire({i + 1}); s.EmitValue(); }});");
                sb.AppendLine($"                    {indent}R3ExtGeneratedInstrumentation.NotifyWires++;");

                if (i > 0)
                {
                    sb.AppendLine("                    }");
                }
            }
            else
            {
                // Parent doesn't implement INPC - use EveryValueChanged
                string segAccess = BuildChainAccess("root", m.FromSegments.Take(i + 1).ToList());
                sb.AppendLine($"                    subscriptions[{i}] = Observable.EveryValueChanged(root, x => {segAccess.Replace("root", "x")})");
                sb.AppendLine($"                        .Subscribe(this, static (_, s) => {{ s.Rewire({i + 1}); s.EmitValue(); }});");
            }

            sb.AppendLine("                }");
        }

        sb.AppendLine("            }");
        sb.AppendLine("            finally { rewiring = false; }");
        sb.AppendLine("        }");

        sb.AppendLine(string.Empty);
        sb.AppendLine("        public void Dispose()");
        sb.AppendLine("        {");
        sb.AppendLine("            if (subscriptions != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                for (int i = 0; i < subscriptions.Length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    subscriptions[i]?.Dispose();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    // old placeholder helpers removed; replaced by chain-aware emission above
    private static string ExtractMemberAccess(SimpleLambdaExpressionSyntax lambda, string rootParam = null!)
    {
        // very limited: param => param.Member
        string paramName = rootParam ?? lambda.Parameter.Identifier.ValueText;
        if (lambda.Body is MemberAccessExpressionSyntax maes)
        {
            // Reconstruct chained access
            Stack<string> parts = new();
            ExpressionSyntax? cur = maes;
            while (cur is MemberAccessExpressionSyntax mae)
            {
                parts.Push(mae.Name.Identifier.ValueText);
                cur = mae.Expression;
            }

            StringBuilder sb = new();
            sb.Append(paramName);
            foreach (string? p in parts)
            {
                sb.Append('.').Append(p);
            }

            return sb.ToString();
        }

        return "default!";
    }

    private static string ExtractTopMemberName(SimpleLambdaExpressionSyntax lambda)
    {
        if (lambda.Body is MemberAccessExpressionSyntax maes)
        {
            return maes.Name.Identifier.ValueText;
        }

        return string.Empty;
    }

    private static void EmitUnsafeAccessors(ImmutableArray<InvocationModel> invocations, StringBuilder sb)
    {
        HashSet<string> setList = new();
        foreach (InvocationModel? inv in invocations)
        {
            if (inv.ToSegments.Count > 0)
            {
                PropertySegment? leaf = inv.ToSegments.Last();
                if (leaf.SetterIsNonPublic)
                {
                    string key = Sanitize(leaf.DeclaringTypeName) + "_" + Sanitize(leaf.Name);
                    if (setList.Add(key))
                    {
                        sb.AppendLine("#if NET8_0_OR_GREATER || NET9_0_OR_GREATER");
                        sb.AppendLine(
                            $"    [UnsafeAccessor(UnsafeAccessorKind.Setter, Name=\"{leaf.Name}\")] private static extern void __UA_SET_{key}({leaf.DeclaringTypeName} instance, {leaf.TypeName} value);");
                        sb.AppendLine("#endif");
                    }
                }
            }

            // Non-public fields anywhere in the chain (host or target) need getters
            static void AddFieldGetters(IEnumerable<PropertySegment> segs, StringBuilder sb2, HashSet<string> keys)
            {
                foreach (PropertySegment? s in segs)
                {
                    if (s.IsField && s.IsNonPublic)
                    {
                        string key = Sanitize(s.DeclaringTypeName) + "_" + Sanitize(s.Name);
                        if (keys.Add("GET_" + key))
                        {
                            sb2.AppendLine("#if NET8_0_OR_GREATER || NET9_0_OR_GREATER");
                            sb2.AppendLine(
                                $"    [UnsafeAccessor(UnsafeAccessorKind.Field, Name=\"{s.Name}\")] private static extern ref {s.TypeName} __UA_GETFIELD_{key}({s.DeclaringTypeName} instance);");
                            sb2.AppendLine("#endif");
                        }
                    }
                }
            }

            AddFieldGetters(inv.FromSegments, sb, setList);
            AddFieldGetters(inv.ToSegments, sb, setList);
        }

        static string Sanitize(string s)
        {
            StringBuilder sb2 = new(s.Length);
            foreach (char ch in s)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb2.Append(ch);
                }
                else
                {
                    sb2.Append('_');
                }
            }

            return sb2.ToString();
        }
    }

    private static string BuildChainAccess(string root, List<PropertySegment> segments)
    {
        // Graceful null propagation: if a prior segment in the chain is nullable, subsequent member access
        // uses the null-conditional operator '?.' (when the segment is a reference type). This avoids CS8602
        // warnings and prevents NullReferenceExceptions for partially null chains while still allowing
        // PropertyChanged handler wiring for non-null portions.
        StringBuilder expr = new(root);
        PropertySegment? prev = null;
        foreach (PropertySegment? seg in segments)
        {
            if (seg.IsField && seg.IsNonPublic)
            {
                // Non-public field access via generated unsafe accessor; cannot combine directly with '?.'.
                // If previous segment may be null we wrap the call in a conditional expression to yield default.
                string key = UAKey(seg);
                string currentRoot = expr.ToString();
                if (prev is not null && prev.IsReferenceType && prev.IsNullable)
                {
                    expr.Clear();
                    expr.Append("(").Append(currentRoot).Append(" == null ? default! : __UA_GETFIELD_").Append(key).Append("(").Append(currentRoot)
                        .Append("))");
                }
                else
                {
                    expr.Clear();
                    expr.Append($"__UA_GETFIELD_{key}(").Append(currentRoot).Append(")");
                }

                root = expr.ToString();
            }
            else
            {
                bool useConditional = prev is not null && prev.IsReferenceType && prev.IsNullable;
                expr.Append(useConditional ? "?." : ".").Append(seg.Name);
                root = expr.ToString();
            }

            prev = seg;
        }

        return expr.ToString();
    }

    private static string UAKey(PropertySegment leaf)
    {
        StringBuilder t = new();
        foreach (char ch in leaf.DeclaringTypeName)
        {
            t.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        t.Append('_');
        foreach (char ch in leaf.Name)
        {
            t.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return t.ToString();
    }

    private static string BuildLeafAssignmentSet(string root, List<PropertySegment> segments, string valueExpr)
    {
        if (segments.Count == 0)
        {
            return string.Empty;
        }

        if (segments.Count == 1)
        {
            PropertySegment? leaf = segments[0];
            string key = SanKey(leaf);
            string direct = root + "." + leaf.Name + " = " + valueExpr + ";";
            if (leaf.SetterIsNonPublic)
            {
                return
                    $"#if NET8_0_OR_GREATER || NET9_0_OR_GREATER\n                __UA_SET_{key}({root}, ({leaf.TypeName}) (object?){valueExpr}!);\n#else\n                {direct}\n#endif";
            }

            return direct;
        }
        else
        {
            string containerAccess = BuildChainAccess(root, segments.Take(segments.Count - 1).ToList());
            PropertySegment? leaf = segments.Last();
            string key = SanKey(leaf);
            string direct = containerAccess + "." + leaf.Name + " = " + valueExpr + ";";
            return $"if(({containerAccess}) != null) {{ " + (leaf.SetterIsNonPublic
                ? $"#if NET8_0_OR_GREATER || NET9_0_OR_GREATER\n                __UA_SET_{key}({containerAccess}, ({leaf.TypeName}) (object?){valueExpr}!);\n#else\n                {direct}\n#endif"
                : direct) + " }";
        }

        static string SanKey(PropertySegment leaf)
        {
            StringBuilder t = new();
            foreach (char ch in leaf.DeclaringTypeName)
            {
                t.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }

            t.Append('_');
            foreach (char ch in leaf.Name)
            {
                t.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }

            return t.ToString();
        }
    }

    /// <summary>
    /// Generates dynamic rewiring logic for multi-segment property chains.
    /// Creates a Rewire method that disposes and recreates subscriptions when intermediate objects change.
    /// </summary>
    private static void EmitRewireLogic(StringBuilder sb, List<PropertySegment> segments, string rootVar, string rootType, string segVarPrefix, string methodSuffix)
    {
        sb.AppendLine($"            void Rewire{methodSuffix}(int fromIndex)");
        sb.AppendLine("            {");
        sb.AppendLine("                for (int i = fromIndex; i < subscriptions.Length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    subscriptions[i]?.Dispose();");
        sb.AppendLine("                    subscriptions[i] = null;");
        sb.AppendLine("                }");
        sb.AppendLine($"                Update{methodSuffix}Chain();");

        for (int i = 0; i < segments.Count; i++)
        {
            PropertySegment seg = segments[i];
            string parentRef = i == 0 ? rootVar : $"__{segVarPrefix}_{i - 1}";
            string parentType = i == 0 ? rootType : segments[i - 1].TypeName;
            bool parentImplementsNotify = i == 0 ? seg.DeclaringTypeImplementsNotify : segments[i - 1].IsNotify;

            sb.AppendLine($"                if (fromIndex <= {i})");
            sb.AppendLine("                {");

            if (parentImplementsNotify)
            {
                // Parent implements INPC - use ObservePropertyChanged
                string propAccess = BuildObservePropertyAccess(seg, $"(({parentType})x)");
                sb.AppendLine($"                    subscriptions[{i}] = {parentRef}.ObservePropertyChanged(static x => {propAccess})");
                sb.AppendLine($"                        .Subscribe(_ => {{ Rewire{methodSuffix}({i + 1}); Emit{methodSuffix}Value(); }});");
                sb.AppendLine("                    R3ExtGeneratedInstrumentation.NotifyWires++;");
            }
            else
            {
                // Parent doesn't implement INPC - use EveryValueChanged
                string segAccess = BuildChainAccess(rootVar, segments.Take(i + 1).ToList());
                sb.AppendLine($"                    subscriptions[{i}] = Observable.EveryValueChanged({rootVar}, static x => {segAccess.Replace(rootVar, "x")})");
                sb.AppendLine($"                        .Subscribe(_ => {{ Rewire{methodSuffix}({i + 1}); Emit{methodSuffix}Value(); }});");
            }

            sb.AppendLine("                }");
        }

        sb.AppendLine("            }");
    }

    /// <summary>
    /// Builds the property access expression for ObservePropertyChanged lambda.
    /// If the segment is a non-public field, uses UnsafeAccessor; otherwise uses direct member access.
    /// </summary>
    private static string BuildObservePropertyAccess(PropertySegment seg, string castExpression)
    {
        if (seg.IsField && seg.IsNonPublic)
        {
            string key = UAKey(seg);
            return $"__UA_GETFIELD_{key}({castExpression})";
        }

        return $"{castExpression}.{seg.Name}";
    }
}
