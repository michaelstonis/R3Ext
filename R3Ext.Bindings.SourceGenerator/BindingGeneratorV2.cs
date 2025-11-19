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
                    }
                }
                else
                {
                    if (m.FromSegments.Count > 0)
                    {
                        m.RootFromTypeName = m.FromSegments[0].DeclaringTypeName;
                        m.FromLeafTypeName = m.FromSegments.Last().TypeName;
                    }

                    if (m.ToSegments.Count > 0)
                    {
                        m.RootTargetTypeName = m.ToSegments[0].DeclaringTypeName;
                        m.TargetLeafTypeName = m.ToSegments.Last().TypeName;
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

    public List<(string MemberName, string Expression, string Reason)> SymbolResolutionFailures { get; set; } = new();
}

internal sealed class PropertySegment
{
    public string Name { get; set; } = string.Empty;

    public string TypeName { get; set; } = string.Empty;

    public string DeclaringTypeName { get; set; } = string.Empty;

    public bool IsReferenceType { get; set; }

    public bool IsNotify { get; set; }

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
        sb.AppendLine("#pragma warning disable CS8600, CS8602");
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
                    string id = Hash(inv.FromPath + "|" + inv.ToPath);
                    sb.AppendLine(
                        $"        BindingRegistry.RegisterOneWay<{inv.RootFromTypeName},{inv.FromLeafTypeName},{inv.RootTargetTypeName},{inv.TargetLeafTypeName}>(\"{Escape(inv.FromPath)}\", \"{Escape(inv.ToPath)}\", (f,t,conv) => __RegBindOneWay_{id}(f,t,conv));");
                }
                else if (inv.Kind == "BindTwoWay" && inv.FromPath is not null && inv.ToPath is not null && inv.RootFromTypeName is not null &&
                         inv.FromLeafTypeName is not null && inv.RootTargetTypeName is not null && inv.TargetLeafTypeName is not null)
                {
                    string id = Hash(inv.FromPath + "|" + inv.ToPath);
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
                    string id = Hash(inv.FromPath + "|" + inv.ToPath);
                    if (emitted.Add("ow" + id))
                    {
                        this.EmitOneWayRegistrationBody(id, inv, sb);
                    }
                }
                else if (inv.Kind == "BindTwoWay" && inv.FromPath is not null && inv.ToPath is not null && inv.RootFromTypeName is not null &&
                         inv.FromLeafTypeName is not null && inv.RootTargetTypeName is not null && inv.TargetLeafTypeName is not null)
                {
                    string id = Hash(inv.FromPath + "|" + inv.ToPath);
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
        // n-level chain handling with INPC wiring and fallback EveryValueChanged for non-INPC segments
        sb.AppendLine(
            $"    private static IDisposable __BindOneWay_{id}<TFrom,TFromProperty,TTarget,TTargetProperty>(TFrom fromObject, TTarget targetObject, Func<TFromProperty,TTargetProperty>? convert)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (fromObject is null || targetObject is null) return Disposable.Empty;");
        sb.AppendLine("        convert ??= v => (TTargetProperty)(object?)v!;");

        // compute and wire host chain
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            string access = BuildChainAccess("fromObject", m.FromSegments.Take(i + 1).ToList());
            sb.AppendLine($"        {seg.TypeName} __host_{i} = default!;");
        }

        sb.AppendLine("        // compute and wire target chain lazily inside Push");

        // Handler declarations (assigned after RewireHost/Push declared to avoid self-referential definite assignment issues)
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"        PropertyChangedEventHandler __h_host_{i} = null!;");
        }

        sb.AppendLine("        var __builder = Disposable.CreateBuilder();");
        sb.AppendLine("        Lock __hostGate = new();");

        // RewireHost implementation
        sb.AppendLine("        void RewireHost(){");
        sb.AppendLine("            using (__hostGate.EnterScope()){");
        sb.AppendLine("            // detach all handlers");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc_{i}) npc_{i}.PropertyChanged -= __h_host_{i};");
        }

        // recompute chain
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            string access = BuildChainAccess("fromObject", m.FromSegments.Take(i + 1).ToList());
            sb.AppendLine($"            try {{ __host_{i} = {access}; }} catch {{ __host_{i} = default!; }}");
        }

        // reattach
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc2_{i}) npc2_{i}.PropertyChanged += __h_host_{i};");
        }

        sb.AppendLine("            }");
        sb.AppendLine("        }");

        // Push implementation
        sb.AppendLine("        void Push(){");

        // compute leaf value with try-catch
        string hostLeafAccess = BuildChainAccess("fromObject", m.FromSegments);
        string targetLeafAssign = BuildLeafAssignmentSet("targetObject", m.ToSegments, "convert(v)");
        sb.AppendLine("            try { var v = (TFromProperty)(object?)" + hostLeafAccess + "!; " + targetLeafAssign + " } catch { } ");
        sb.AppendLine("        }");

        // Assign handlers now that helper methods exist
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            string nextProp = i + 1 < m.FromSegments.Count ? m.FromSegments[i + 1].Name : m.FromSegments.Last().Name;
            sb.AppendLine(
                $"        __h_host_{i} = (s,e) => {{ if (e.PropertyName == \"{nextProp}\") {{ RewireHost(); Push(); }} if (e.PropertyName == \"{m.FromSegments.Last().Name}\") Push(); }};");
        }

        // initial wire
        sb.AppendLine("        RewireHost();");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"        if(__host_{i} is INotifyPropertyChanged npc_init_{i}) npc_init_{i}.PropertyChanged += __h_host_{i};");
        }

        // Root handler for first segment if root implements INPC
        if (m.FromSegments.Count > 0)
        {
            string firstProp = m.FromSegments[0].Name;
            sb.AppendLine("        PropertyChangedEventHandler __h_root = (s,e) => { if(e.PropertyName == \"" + firstProp +
                          "\") { RewireHost(); Push(); } }; ");
            sb.AppendLine("        if(fromObject is INotifyPropertyChanged npc_root) npc_root.PropertyChanged += __h_root;");
        }

        // Per-property EveryValueChanged for parents that do NOT implement INPC
        if (m.FromSegments.Count > 0)
        {
            // First property: parent is fromObject (runtime-checked)
            string firstAccess = BuildChainAccess("h", m.FromSegments.Take(1).ToList());
            sb.AppendLine("        try { if(!(fromObject is INotifyPropertyChanged)) __builder.Add(Observable.EveryValueChanged(fromObject, h => " +
                          firstAccess + ").Subscribe(_ => { RewireHost(); Push(); })); } catch { }");
        }

        for (int j = 1; j < m.FromSegments.Count; j++)
        {
            // parent is segment j-1
            if (!m.FromSegments[j - 1].IsNotify)
            {
                string accessJ = BuildChainAccess("h", m.FromSegments.Take(j + 1).ToList());
                bool isLeaf = j == m.FromSegments.Count - 1;
                if (isLeaf)
                {
                    sb.AppendLine("        try { __builder.Add(Observable.EveryValueChanged(fromObject, h => " + accessJ +
                                  ").Subscribe(_ => Push())); } catch { }");
                }
                else
                {
                    sb.AppendLine("        try { __builder.Add(Observable.EveryValueChanged(fromObject, h => " + accessJ +
                                  ").Subscribe(_ => { RewireHost(); Push(); })); } catch { }");
                }
            }
        }

        sb.AppendLine("        Push();");
        sb.AppendLine("        __builder.Add(Disposable.Create(() => {");
        if (m.FromSegments.Count > 0)
        {
            sb.AppendLine("            if(fromObject is INotifyPropertyChanged npc_root2) npc_root2.PropertyChanged -= __h_root;");
        }

        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc3_{i}) npc3_{i}.PropertyChanged -= __h_host_{i};");
        }

        sb.AppendLine("        }));");
        sb.AppendLine("        return __builder.Build();");
        sb.AppendLine("    }");
    }

    private void EmitTwoWayBody(string id, InvocationModel m, StringBuilder sb)
    {
        sb.AppendLine(
            $"    private static IDisposable __BindTwoWay_{id}<TFrom,TFromProperty,TTarget,TTargetProperty>(TFrom fromObject, TTarget targetObject, Func<TFromProperty,TTargetProperty>? hostToTarget, Func<TTargetProperty,TFromProperty>? targetToHost)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (fromObject is null || targetObject is null) return Disposable.Empty;");
        sb.AppendLine("        hostToTarget ??= v => (TTargetProperty)(object?)v!;");
        sb.AppendLine("        targetToHost ??= v => (TFromProperty)(object?)v!;");
        sb.AppendLine("        bool __updating = false;");
        sb.AppendLine("        var __builder = Disposable.CreateBuilder();");

        // Gates to serialize rewires
        sb.AppendLine("        Lock __hostGate = new();");
        sb.AppendLine("        Lock __targetGate = new();");

        // host chain vars
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            sb.AppendLine($"        {seg.TypeName} __host_{i} = default!;");
        }

        // target chain vars
        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            PropertySegment? seg = m.ToSegments[i];
            sb.AppendLine($"        {seg.TypeName} __target_{i} = default!;");
        }

        // Handler declarations (assignment postponed until after helpers)
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"        PropertyChangedEventHandler __h_host_{i} = null!;");
        }

        // Handlers for target
        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            PropertySegment? seg = m.ToSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"        PropertyChangedEventHandler __h_target_{i} = null!;");
        }

        // RewireHost
        sb.AppendLine("        void RewireHost(){");
        sb.AppendLine("            using (__hostGate.EnterScope()){");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc_{i}) npc_{i}.PropertyChanged -= __h_host_{i};");
        }

        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            string access = BuildChainAccess("fromObject", m.FromSegments.Take(i + 1).ToList());
            sb.AppendLine($"            try {{ __host_{i} = {access}; }} catch {{ __host_{i} = default!; }}");
        }

        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc2_{i}) npc2_{i}.PropertyChanged += __h_host_{i};");
        }

        sb.AppendLine("            }");
        sb.AppendLine("        }");

        // RewireTarget
        sb.AppendLine("        void RewireTarget(){");
        sb.AppendLine("            using (__targetGate.EnterScope()){");
        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            PropertySegment? seg = m.ToSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__target_{i} is INotifyPropertyChanged npcT_{i}) npcT_{i}.PropertyChanged -= __h_target_{i};");
        }

        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            string access = BuildChainAccess("targetObject", m.ToSegments.Take(i + 1).ToList());
            sb.AppendLine($"            try {{ __target_{i} = {access}; }} catch {{ __target_{i} = default!; }}");
        }

        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            PropertySegment? seg = m.ToSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__target_{i} is INotifyPropertyChanged npcT2_{i}) npcT2_{i}.PropertyChanged += __h_target_{i};");
        }

        sb.AppendLine("            }");
        sb.AppendLine("        }");

        // UpdateTarget and UpdateHost
        string fromLeaf = BuildChainAccess("fromObject", m.FromSegments);
        string toLeafAssign = BuildLeafAssignmentSet("targetObject", m.ToSegments, "hostToTarget(v)");
        string toLeafRead = BuildChainAccess("targetObject", m.ToSegments);
        string fromLeafAssign = BuildLeafAssignmentSet("fromObject", m.FromSegments, "targetToHost(v)");
        sb.AppendLine("        void UpdateTarget(){ if (__updating) return; __updating = true; try { var v = (TFromProperty)(object?)" + fromLeaf + "!; " +
                      toLeafAssign + " } catch { } finally { __updating = false; } }");
        sb.AppendLine("        void UpdateHost(){ if (__updating) return; __updating = true; try { var v = (TTargetProperty)(object?)" + toLeafRead + "!; " +
                      fromLeafAssign + " } catch { } finally { __updating = false; } }");

        // Assign handlers now that helper methods exist
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            string nextProp = i + 1 < m.FromSegments.Count ? m.FromSegments[i + 1].Name : m.FromSegments.Last().Name;
            sb.AppendLine(
                $"        __h_host_{i} = (s,e)=>{{ if(e.PropertyName==\"{nextProp}\"){{ RewireHost(); UpdateTarget(); }} if(e.PropertyName==\"{m.FromSegments.Last().Name}\") UpdateTarget(); }};");
        }

        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            PropertySegment? seg = m.ToSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            string nextProp = i + 1 < m.ToSegments.Count ? m.ToSegments[i + 1].Name : m.ToSegments.Last().Name;
            sb.AppendLine(
                $"        __h_target_{i} = (s,e)=>{{ if(e.PropertyName==\"{nextProp}\"){{ RewireTarget(); UpdateHost(); }} if(e.PropertyName==\"{m.ToSegments.Last().Name}\") UpdateHost(); }};");
        }

        // Initial wire
        sb.AppendLine("        RewireHost(); RewireTarget();");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"        if(__host_{i} is INotifyPropertyChanged npc_init_{i}) npc_init_{i}.PropertyChanged += __h_host_{i};");
        }

        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            PropertySegment? seg = m.ToSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"        if(__target_{i} is INotifyPropertyChanged npc_initT_{i}) npc_initT_{i}.PropertyChanged += __h_target_{i};");
        }

        // Root handlers for first properties when roots implement INPC
        if (m.FromSegments.Count > 0)
        {
            string firstHostProp = m.FromSegments[0].Name;
            sb.AppendLine("        PropertyChangedEventHandler __h_rootHost = (s,e)=>{ if(e.PropertyName==\"" + firstHostProp +
                          "\") { RewireHost(); UpdateTarget(); } }; ");
            sb.AppendLine("        if(fromObject is INotifyPropertyChanged npc_rootHost) npc_rootHost.PropertyChanged += __h_rootHost;");
        }

        if (m.ToSegments.Count > 0)
        {
            string firstTargetProp = m.ToSegments[0].Name;
            sb.AppendLine("        PropertyChangedEventHandler __h_rootTarget = (s,e)=>{ if(e.PropertyName==\"" + firstTargetProp +
                          "\") { RewireTarget(); UpdateHost(); } }; ");
            sb.AppendLine("        if(targetObject is INotifyPropertyChanged npc_rootTarget) npc_rootTarget.PropertyChanged += __h_rootTarget;");
        }

        // Per-property EveryValueChanged watchers when parent does NOT implement INPC
        if (m.FromSegments.Count > 0)
        {
            string firstAccessH = BuildChainAccess("h", m.FromSegments.Take(1).ToList());
            sb.AppendLine("        try { if(!(fromObject is INotifyPropertyChanged)) __builder.Add(Observable.EveryValueChanged(fromObject, h => " +
                          firstAccessH + ").Subscribe(_ => { RewireHost(); UpdateTarget(); })); } catch { }");
        }

        for (int j = 1; j < m.FromSegments.Count; j++)
        {
            if (!m.FromSegments[j - 1].IsNotify)
            {
                string accessH = BuildChainAccess("h", m.FromSegments.Take(j + 1).ToList());
                bool isLeafH = j == m.FromSegments.Count - 1;
                if (isLeafH)
                {
                    sb.AppendLine("        try { __builder.Add(Observable.EveryValueChanged(fromObject, h => " + accessH +
                                  ").Subscribe(_ => UpdateTarget())); } catch { }");
                }
                else
                {
                    sb.AppendLine("        try { __builder.Add(Observable.EveryValueChanged(fromObject, h => " + accessH +
                                  ").Subscribe(_ => { RewireHost(); UpdateTarget(); })); } catch { }");
                }
            }
        }

        if (m.ToSegments.Count > 0)
        {
            string firstAccessT = BuildChainAccess("t", m.ToSegments.Take(1).ToList());
            sb.AppendLine("        try { if(!(targetObject is INotifyPropertyChanged)) __builder.Add(Observable.EveryValueChanged(targetObject, t => " +
                          firstAccessT + ").Subscribe(_ => { RewireTarget(); UpdateHost(); })); } catch { }");
        }

        for (int j = 1; j < m.ToSegments.Count; j++)
        {
            if (!m.ToSegments[j - 1].IsNotify)
            {
                string accessT = BuildChainAccess("t", m.ToSegments.Take(j + 1).ToList());
                bool isLeafT = j == m.ToSegments.Count - 1;
                if (isLeafT)
                {
                    sb.AppendLine("        try { __builder.Add(Observable.EveryValueChanged(targetObject, t => " + accessT +
                                  ").Subscribe(_ => UpdateHost())); } catch { }");
                }
                else
                {
                    sb.AppendLine("        try { __builder.Add(Observable.EveryValueChanged(targetObject, t => " + accessT +
                                  ").Subscribe(_ => { RewireTarget(); UpdateHost(); })); } catch { }");
                }
            }
        }

        sb.AppendLine("        UpdateTarget();");
        sb.AppendLine("        __builder.Add(Disposable.Create(() => {");
        if (m.FromSegments.Count > 0)
        {
            sb.AppendLine("            if(fromObject is INotifyPropertyChanged npc_rootHost2) npc_rootHost2.PropertyChanged -= __h_rootHost;");
        }

        if (m.ToSegments.Count > 0)
        {
            sb.AppendLine("            if(targetObject is INotifyPropertyChanged npc_rootTarget2) npc_rootTarget2.PropertyChanged -= __h_rootTarget;");
        }

        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc3_{i}) npc3_{i}.PropertyChanged -= __h_host_{i};");
        }

        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            PropertySegment? seg = m.ToSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__target_{i} is INotifyPropertyChanged npc3T_{i}) npc3T_{i}.PropertyChanged -= __h_target_{i};");
        }

        sb.AppendLine("        }));");
        sb.AppendLine("        return __builder.Build();");
        sb.AppendLine("    }");
    }

    private void EmitWhenBody(string id, InvocationModel m, StringBuilder sb)
    {
        sb.AppendLine($"    private static Observable<TReturn> __WhenChanged_{id}<TObj,TReturn>(TObj root)");
        sb.AppendLine("    {");

        // If we don't have segment metadata (empty or any non-notify), fallback to EveryValueChanged
        bool allNotify = m.FromSegments.Count > 0 && m.FromSegments.Take(m.FromSegments.Count - 1).All(s => s.IsNotify);
        if (!allNotify || m.FromSegments.Count == 0)
        {
            string getterFallback = ExtractMemberAccess(m.WhenLambda!, "root");
            sb.AppendLine("        try { return Observable.EveryValueChanged(root, _ => { try { return " + getterFallback +
                          "; } catch { return default!; } }).Select(v => (TReturn)v).DistinctUntilChanged(); } catch { return Observable.Empty<TReturn>(); }");
            sb.AppendLine("    }");
            return;
        }

        // Chain-aware observable using INPC handlers (reflection-free)
        sb.AppendLine("        if (root is null) return Observable.Empty<TReturn>();");

        // segment vars
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            sb.AppendLine($"        {m.FromSegments[i].TypeName} __host_{i} = default!;");
        }

        // handler declarations only for notify-capable segment objects (exclude leaf value segment if non-INPC)
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (!m.FromSegments[i].IsNotify)
            {
                continue;
            }

            sb.AppendLine($"        PropertyChangedEventHandler __h_host_{i} = null!;");
        }

        string leafAccess = BuildChainAccess("root", m.FromSegments);
        sb.AppendLine("        Lock __gate = new();");
        sb.AppendLine("        return Observable.Create<TReturn>(observer => {");
        sb.AppendLine("            void Emit(){ try { observer.OnNext((TReturn)(object?)" + leafAccess + "!); } catch { observer.OnNext(default!); } }");

        // Rewire method (re-evaluate chain and reattach handlers)
        sb.AppendLine("            void Rewire(){");
        sb.AppendLine("                using (__gate.EnterScope()){");

        // detach existing
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (!m.FromSegments[i].IsNotify)
            {
                continue;
            }

            sb.AppendLine($"                if(__host_{i} is INotifyPropertyChanged npc_det_{i}) npc_det_{i}.PropertyChanged -= __h_host_{i};");
        }

        // recompute chain
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            string access = BuildChainAccess("root", m.FromSegments.Take(i + 1).ToList());
            sb.AppendLine($"                try {{ __host_{i} = {access}; }} catch {{ __host_{i} = default!; }}");
        }

        // reattach
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (!m.FromSegments[i].IsNotify)
            {
                continue;
            }

            sb.AppendLine($"                if(__host_{i} is INotifyPropertyChanged npc_att_{i}) npc_att_{i}.PropertyChanged += __h_host_{i};");
        }

        sb.AppendLine("                }");
        sb.AppendLine("            }");

        // assign handlers
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (!m.FromSegments[i].IsNotify)
            {
                continue;
            }

            string nextProp = i + 1 < m.FromSegments.Count ? m.FromSegments[i + 1].Name : m.FromSegments.Last().Name;
            sb.AppendLine(
                $"            __h_host_{i} = (s,e) => {{ if(e.PropertyName == \"{nextProp}\") {{ Rewire(); Emit(); }} if(e.PropertyName == \"{m.FromSegments.Last().Name}\") Emit(); }};");
        }

        // initial wire
        sb.AppendLine("            Rewire(); Emit();");
        sb.AppendLine("            return Disposable.Create(() => {");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (!m.FromSegments[i].IsNotify)
            {
                continue;
            }

            sb.AppendLine($"                if(__host_{i} is INotifyPropertyChanged npc_fin_{i}) npc_fin_{i}.PropertyChanged -= __h_host_{i};");
        }

        sb.AppendLine("            });");
        sb.AppendLine("        }).DistinctUntilChanged();");
        sb.AppendLine("    }");
    }

    // Registration bodies for non-core assemblies (concrete types)
    private void EmitOneWayRegistrationBody(string id, InvocationModel m, StringBuilder sb)
    {
        sb.AppendLine(
            $"    private static IDisposable __RegBindOneWay_{id}({m.RootFromTypeName} fromObject, {m.RootTargetTypeName} targetObject, Func<{m.FromLeafTypeName},{m.TargetLeafTypeName}>? convert)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (fromObject is null || targetObject is null) return Disposable.Empty;");
        sb.AppendLine("        convert ??= v => (" + m.TargetLeafTypeName + ")(object?)v!;");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            sb.AppendLine($"        {m.FromSegments[i].TypeName} __host_{i} = default!;");
        }

        // Handler declarations for registration body (assignment postponed until after helpers)
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"        PropertyChangedEventHandler __h_host_{i} = null!;");
        }

        sb.AppendLine("        var __builder = Disposable.CreateBuilder();");
        sb.AppendLine("        Lock __hostGate = new();");
        sb.AppendLine("        void RewireHost(){");
        sb.AppendLine("            using (__hostGate.EnterScope()){");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc_{i}) npc_{i}.PropertyChanged -= __h_host_{i};");
        }

        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            string access = BuildChainAccess("fromObject", m.FromSegments.Take(i + 1).ToList());
            sb.AppendLine($"            try {{ __host_{i} = {access}; }} catch {{ __host_{i} = default!; }}");
        }

        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc2_{i}) npc2_{i}.PropertyChanged += __h_host_{i};");
        }

        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        void Push(){");
        string hostLeafAccess = BuildChainAccess("fromObject", m.FromSegments);
        string targetLeafAssign = BuildLeafAssignmentSet("targetObject", m.ToSegments, "convert(v)");
        sb.AppendLine("            try { var v = (" + m.FromLeafTypeName + ")(object?)" + hostLeafAccess + "!; " + targetLeafAssign + " } catch { } ");
        sb.AppendLine("        }");

        // Assign handlers
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            bool isLeafOwner = i == m.FromSegments.Count - 1;
            if (isLeafOwner)
            {
                // Leaf object: emit on leaf property changes only
                sb.AppendLine($"        __h_host_{i} = (s,e) => {{ if (e.PropertyName == \"{m.FromSegments.Last().Name}\") Push(); }};");
            }
            else
            {
                string nextProp = m.FromSegments[i + 1].Name;
                sb.AppendLine($"        __h_host_{i} = (s,e) => {{ if (e.PropertyName == \"{nextProp}\") {{ RewireHost(); Push(); }} }};");
            }
        }

        sb.AppendLine("        RewireHost();");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"        if(__host_{i} is INotifyPropertyChanged npc_init_{i}) npc_init_{i}.PropertyChanged += __h_host_{i};");
        }

        // Per-property EveryValueChanged watchers for parents that do NOT implement INPC
        if (m.FromSegments.Count > 0)
        {
            string firstAccessParam = BuildChainAccess("h", m.FromSegments.Take(1).ToList());
            sb.AppendLine("        try { if(!(fromObject is INotifyPropertyChanged)) __builder.Add(Observable.EveryValueChanged(fromObject, h => " +
                          firstAccessParam + ").Subscribe(_ => { RewireHost(); Push(); })); } catch { }");
        }

        for (int j = 1; j < m.FromSegments.Count; j++)
        {
            if (!m.FromSegments[j - 1].IsNotify)
            {
                string accessJ = BuildChainAccess("h", m.FromSegments.Take(j + 1).ToList());
                bool isLeaf = j == m.FromSegments.Count - 1;
                if (isLeaf)
                {
                    sb.AppendLine("        try { __builder.Add(Observable.EveryValueChanged(fromObject, h => " + accessJ +
                                  ").Subscribe(_ => Push())); } catch { }");
                }
                else
                {
                    sb.AppendLine("        try { __builder.Add(Observable.EveryValueChanged(fromObject, h => " + accessJ +
                                  ").Subscribe(_ => { RewireHost(); Push(); })); } catch { }");
                }
            }
        }

        // Root handler for first segment replacement
        if (m.FromSegments.Count > 0)
        {
            string firstProp = m.FromSegments[0].Name;
            sb.AppendLine("        PropertyChangedEventHandler __h_root = (s,e) => { if(e.PropertyName == \"" + firstProp +
                          "\") { RewireHost(); Push(); } }; ");
            sb.AppendLine("        if(fromObject is INotifyPropertyChanged npc_root) npc_root.PropertyChanged += __h_root;");
        }

        sb.AppendLine("        Push();");
        sb.AppendLine("        __builder.Add(Disposable.Create(() => {");
        if (m.FromSegments.Count > 0)
        {
            sb.AppendLine("            if(fromObject is INotifyPropertyChanged npc_root2) npc_root2.PropertyChanged -= __h_root;");
        }

        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc3_{i}) npc3_{i}.PropertyChanged -= __h_host_{i};");
        }

        sb.AppendLine("        }));");
        sb.AppendLine("        return __builder.Build();");
        sb.AppendLine("    }");
    }

    private void EmitTwoWayRegistrationBody(string id, InvocationModel m, StringBuilder sb)
    {
        sb.AppendLine(
            $"    private static IDisposable __RegBindTwoWay_{id}({m.RootFromTypeName} fromObject, {m.RootTargetTypeName} targetObject, Func<{m.FromLeafTypeName},{m.TargetLeafTypeName}>? hostToTargetConv, Func<{m.TargetLeafTypeName},{m.FromLeafTypeName}>? targetToHostConv)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (fromObject is null || targetObject is null) return Disposable.Empty;");
        sb.AppendLine("        hostToTargetConv ??= v => (" + m.TargetLeafTypeName + ")(object?)v!;");
        sb.AppendLine("        targetToHostConv ??= v => (" + m.FromLeafTypeName + ")(object?)v!;");
        sb.AppendLine("        bool __updating = false; var __builder = Disposable.CreateBuilder();");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            sb.AppendLine($"        {m.FromSegments[i].TypeName} __host_{i} = default!;");
        }

        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            sb.AppendLine($"        {m.ToSegments[i].TypeName} __target_{i} = default!;");
        }

        sb.AppendLine("        Lock __hostGate = new();");
        sb.AppendLine("        Lock __targetGate = new();");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"        PropertyChangedEventHandler __h_host_{i} = null!;");
        }

        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            PropertySegment? seg = m.ToSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"        PropertyChangedEventHandler __h_target_{i} = null!;");
        }

        sb.AppendLine("        void RewireHost(){");
        sb.AppendLine("            using (__hostGate.EnterScope()){");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc_{i}) npc_{i}.PropertyChanged -= __h_host_{i};");
        }

        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            string access = BuildChainAccess("fromObject", m.FromSegments.Take(i + 1).ToList());
            sb.AppendLine($"            try {{ __host_{i} = {access}; }} catch {{ __host_{i} = default!; }}");
        }

        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc2_{i}) npc2_{i}.PropertyChanged += __h_host_{i};");
        }

        sb.AppendLine("            R3ExtGeneratedInstrumentation.NotifyWires++;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        void RewireTarget(){");
        sb.AppendLine("            using (__targetGate.EnterScope()){");
        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            PropertySegment? seg = m.ToSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__target_{i} is INotifyPropertyChanged npcT_{i}) npcT_{i}.PropertyChanged -= __h_target_{i};");
        }

        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            string access = BuildChainAccess("targetObject", m.ToSegments.Take(i + 1).ToList());
            sb.AppendLine($"            try {{ __target_{i} = {access}; }} catch {{ __target_{i} = default!; }}");
        }

        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            PropertySegment? seg = m.ToSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__target_{i} is INotifyPropertyChanged npcT2_{i}) npcT2_{i}.PropertyChanged += __h_target_{i};");
        }

        sb.AppendLine("            R3ExtGeneratedInstrumentation.NotifyWires++;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        string fromLeaf = BuildChainAccess("fromObject", m.FromSegments);
        string toLeafAssign = BuildLeafAssignmentSet("targetObject", m.ToSegments, "hostToTargetConv(v)");
        string toLeafRead = BuildChainAccess("targetObject", m.ToSegments);
        string fromLeafAssign = BuildLeafAssignmentSet("fromObject", m.FromSegments, "targetToHostConv(v)");
        sb.AppendLine("        void UpdateTarget(){ if (__updating) return; __updating = true; try { var v = (" + m.FromLeafTypeName + ")(object?)" + fromLeaf +
                      "!; " + toLeafAssign + " R3ExtGeneratedInstrumentation.BindUpdates++; } catch { } finally { __updating = false; } }");
        sb.AppendLine("        void UpdateHost(){ if (__updating) return; __updating = true; try { var v = (" + m.TargetLeafTypeName + ")(object?)" +
                      toLeafRead + "!; " + fromLeafAssign + " R3ExtGeneratedInstrumentation.BindUpdates++; } catch { } finally { __updating = false; } }");

        // Assign handlers now that helper methods exist
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            bool isLeafOwner = i == m.FromSegments.Count - 1;
            if (isLeafOwner)
            {
                sb.AppendLine($"        __h_host_{i} = (s,e)=>{{ if(e.PropertyName==\"{m.FromSegments.Last().Name}\") UpdateTarget(); }};");
            }
            else
            {
                string nextProp = m.FromSegments[i + 1].Name;
                sb.AppendLine($"        __h_host_{i} = (s,e)=>{{ if(e.PropertyName==\"{nextProp}\"){{ RewireHost(); UpdateTarget(); }} }};");
            }
        }

        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            PropertySegment? seg = m.ToSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            bool isLeafOwner = i == m.ToSegments.Count - 1;
            if (isLeafOwner)
            {
                sb.AppendLine($"        __h_target_{i} = (s,e)=>{{ if(e.PropertyName==\"{m.ToSegments.Last().Name}\") UpdateHost(); }};");
            }
            else
            {
                string nextProp = m.ToSegments[i + 1].Name;
                sb.AppendLine($"        __h_target_{i} = (s,e)=>{{ if(e.PropertyName==\"{nextProp}\"){{ RewireTarget(); UpdateHost(); }} }};");
            }
        }

        sb.AppendLine("        RewireHost(); RewireTarget();");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"        if(__host_{i} is INotifyPropertyChanged npc_init_{i}) npc_init_{i}.PropertyChanged += __h_host_{i};");
        }

        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            PropertySegment? seg = m.ToSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"        if(__target_{i} is INotifyPropertyChanged npc_initT_{i}) npc_initT_{i}.PropertyChanged += __h_target_{i};");
        }

        if (m.FromSegments.Count > 0)
        {
            string firstHostProp = m.FromSegments[0].Name;
            sb.AppendLine("        PropertyChangedEventHandler __h_rootHost = (s,e)=>{ if(e.PropertyName==\"" + firstHostProp + "\") UpdateTarget(); }; ");
            sb.AppendLine("        if(fromObject is INotifyPropertyChanged npc_rootHost) npc_rootHost.PropertyChanged += __h_rootHost;");
        }

        if (m.ToSegments.Count > 0)
        {
            string firstTargetProp = m.ToSegments[0].Name;
            sb.AppendLine("        PropertyChangedEventHandler __h_rootTarget = (s,e)=>{ if(e.PropertyName==\"" + firstTargetProp + "\") UpdateHost(); }; ");
            sb.AppendLine("        if(targetObject is INotifyPropertyChanged npc_rootTarget) npc_rootTarget.PropertyChanged += __h_rootTarget;");
        }

        // Per-property EveryValueChanged watchers when parent doesn't implement INPC
        if (m.FromSegments.Count > 0)
        {
            string firstAccessH = BuildChainAccess("h", m.FromSegments.Take(1).ToList());
            sb.AppendLine("        try { if(!(fromObject is INotifyPropertyChanged)) __builder.Add(Observable.EveryValueChanged(fromObject, h => " +
                          firstAccessH + ").Subscribe(_ => { RewireHost(); UpdateTarget(); })); } catch { }");
        }

        for (int j = 1; j < m.FromSegments.Count; j++)
        {
            if (!m.FromSegments[j - 1].IsNotify)
            {
                string accessH = BuildChainAccess("h", m.FromSegments.Take(j + 1).ToList());
                bool isLeafH = j == m.FromSegments.Count - 1;
                if (isLeafH)
                {
                    sb.AppendLine("        try { __builder.Add(Observable.EveryValueChanged(fromObject, h => " + accessH +
                                  ").Subscribe(_ => UpdateTarget())); } catch { }");
                }
                else
                {
                    sb.AppendLine("        try { __builder.Add(Observable.EveryValueChanged(fromObject, h => " + accessH +
                                  ").Subscribe(_ => { RewireHost(); UpdateTarget(); })); } catch { }");
                }
            }
        }

        if (m.ToSegments.Count > 0)
        {
            string firstAccessT = BuildChainAccess("t", m.ToSegments.Take(1).ToList());
            sb.AppendLine("        try { if(!(targetObject is INotifyPropertyChanged)) __builder.Add(Observable.EveryValueChanged(targetObject, t => " +
                          firstAccessT + ").Subscribe(_ => { RewireTarget(); UpdateHost(); })); } catch { }");
        }

        for (int j = 1; j < m.ToSegments.Count; j++)
        {
            if (!m.ToSegments[j - 1].IsNotify)
            {
                string accessT = BuildChainAccess("t", m.ToSegments.Take(j + 1).ToList());
                bool isLeafT = j == m.ToSegments.Count - 1;
                if (isLeafT)
                {
                    sb.AppendLine("        try { __builder.Add(Observable.EveryValueChanged(targetObject, t => " + accessT +
                                  ").Subscribe(_ => UpdateHost())); } catch { }");
                }
                else
                {
                    sb.AppendLine("        try { __builder.Add(Observable.EveryValueChanged(targetObject, t => " + accessT +
                                  ").Subscribe(_ => { RewireTarget(); UpdateHost(); })); } catch { }");
                }
            }
        }

        sb.AppendLine("        UpdateTarget();");
        sb.AppendLine("        __builder.Add(Disposable.Create(() => {");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            PropertySegment? seg = m.FromSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__host_{i} is INotifyPropertyChanged npc3_{i}) npc3_{i}.PropertyChanged -= __h_host_{i};");
        }

        for (int i = 0; i < m.ToSegments.Count; i++)
        {
            PropertySegment? seg = m.ToSegments[i];
            if (!seg.IsNotify)
            {
                continue;
            }

            sb.AppendLine($"            if(__target_{i} is INotifyPropertyChanged npc3T_{i}) npc3T_{i}.PropertyChanged -= __h_target_{i};");
        }

        if (m.FromSegments.Count > 0)
        {
            sb.AppendLine("            if(fromObject is INotifyPropertyChanged npc_rootHost2) npc_rootHost2.PropertyChanged -= __h_rootHost;");
        }

        if (m.ToSegments.Count > 0)
        {
            sb.AppendLine("            if(targetObject is INotifyPropertyChanged npc_rootTarget2) npc_rootTarget2.PropertyChanged -= __h_rootTarget;");
        }

        sb.AppendLine("        }));");
        sb.AppendLine("        return __builder.Build();");
        sb.AppendLine("    }");
    }

    private void EmitWhenRegistrationBody(string id, InvocationModel m, StringBuilder sb)
    {
        sb.AppendLine($"    private static Observable<{m.WhenLeafTypeName}> __RegWhenChanged_{id}({m.WhenRootTypeName} root)");
        sb.AppendLine("    {");
        bool allNotify = m.FromSegments.Count > 0 && m.FromSegments.Take(m.FromSegments.Count - 1).All(s => s.IsNotify);
        if (!allNotify || m.FromSegments.Count == 0)
        {
            string getterFallback = ExtractMemberAccess(m.WhenLambda!, "root");
            sb.AppendLine("        try { return Observable.EveryValueChanged(root, _ => { try { return " + getterFallback +
                          "; } catch { return default!; } }).Select(v => (" + m.WhenLeafTypeName +
                          ")v).DistinctUntilChanged(); } catch { return Observable.Empty<" + m.WhenLeafTypeName + ">(); }");
            sb.AppendLine("    }");
            return;
        }

        sb.AppendLine("        if (root is null) return Observable.Empty<" + m.WhenLeafTypeName + ">();");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            sb.AppendLine($"        {m.FromSegments[i].TypeName} __host_{i} = default!;");
        }

        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (m.FromSegments[i].IsNotify)
            {
                sb.AppendLine($"        PropertyChangedEventHandler __h_host_{i} = null!;");
            }
        }

        string leafAccess = BuildChainAccess("root", m.FromSegments);
        sb.AppendLine("        Lock __gate = new();");
        sb.AppendLine("        return Observable.Create<" + m.WhenLeafTypeName + ">(observer => {");
        sb.AppendLine("            void Emit(){ try { observer.OnNext((" + m.WhenLeafTypeName + ")(object?)" + leafAccess +
                      "!); } catch { observer.OnNext(default!); } }");
        sb.AppendLine("            void Rewire(){");
        sb.AppendLine("                using (__gate.EnterScope()){");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (m.FromSegments[i].IsNotify)
            {
                sb.AppendLine($"                if(__host_{i} is INotifyPropertyChanged npc_det_{i}) npc_det_{i}.PropertyChanged -= __h_host_{i};");
            }
        }

        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            string access = BuildChainAccess("root", m.FromSegments.Take(i + 1).ToList());
            sb.AppendLine($"                try {{ __host_{i} = {access}; }} catch {{ __host_{i} = default!; }}");
        }

        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (m.FromSegments[i].IsNotify)
            {
                sb.AppendLine($"                if(__host_{i} is INotifyPropertyChanged npc_att_{i}) npc_att_{i}.PropertyChanged += __h_host_{i};");
            }
        }

        sb.AppendLine("                }");
        sb.AppendLine("            }");

        // Root handler to detect replacement of first segment object
        string? firstProp = m.FromSegments.Count > 0 ? m.FromSegments[0].Name : null;
        sb.AppendLine("            PropertyChangedEventHandler __h_root = (s,e) => { if(e.PropertyName == \"" + firstProp + "\") { Rewire(); Emit(); } }; ");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (!m.FromSegments[i].IsNotify)
            {
                continue;
            }

            bool isLeafOwner = i == m.FromSegments.Count - 1;
            if (isLeafOwner)
            {
                sb.AppendLine($"            __h_host_{i} = (s,e) => {{ if(e.PropertyName == \"{m.FromSegments.Last().Name}\") Emit(); }};");
            }
            else
            {
                string nextProp = m.FromSegments[i + 1].Name;
                sb.AppendLine($"            __h_host_{i} = (s,e) => {{ if(e.PropertyName == \"{nextProp}\") {{ Rewire(); Emit(); }} }};");
            }
        }

        sb.AppendLine("            Rewire(); Emit();");
        sb.AppendLine("            if(root is INotifyPropertyChanged npc_root) npc_root.PropertyChanged += __h_root;");
        sb.AppendLine("            return Disposable.Create(() => {");
        for (int i = 0; i < m.FromSegments.Count; i++)
        {
            if (m.FromSegments[i].IsNotify)
            {
                sb.AppendLine($"                if(__host_{i} is INotifyPropertyChanged npc_fin_{i}) npc_fin_{i}.PropertyChanged -= __h_host_{i};");
            }
        }

        sb.AppendLine("                if(root is INotifyPropertyChanged npc_root2) npc_root2.PropertyChanged -= __h_root;");
        sb.AppendLine("            });");
        sb.AppendLine("        }).DistinctUntilChanged();");
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
}
