using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace R3Ext.Bindings.SourceGenerator;

/// <summary>
/// Source generator that generates the IViewFor infrastructure for classes implementing IViewFor{TViewModel}.
/// This generator is platform-agnostic - it generates code that calls into a platform-registered
/// activation provider via <c>ActivationProviderRegistry</c>.
/// Generates:
/// - ViewModel property with two-way sync to a platform BindingContext (if available).
/// - Activation property that uses the registered platform provider.
/// - InitializeViewFor helper for optional DI resolution.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ViewForGenerator : IIncrementalGenerator
{
    private const string IViewForFullName = "R3Ext.Activation.IViewFor`1";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all partial class declarations that implement IViewFor<T>
        IncrementalValuesProvider<ViewForCandidate> candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsViewForCandidate(node),
                transform: static (ctx, _) => GetViewForCandidate(ctx))
            .Where(static candidate => candidate is not null)!;

        // Combine with compilation for full symbol resolution
        IncrementalValueProvider<(Compilation, ImmutableArray<ViewForCandidate>)> combined =
            context.CompilationProvider.Combine(candidates.Collect());

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (compilation, candidatesArray) = tuple;
            Execute(spc, compilation, candidatesArray);
        });
    }

    private static bool IsViewForCandidate(SyntaxNode node)
    {
        // Must be a partial class declaration with base list
        if (node is not ClassDeclarationSyntax classDecl)
        {
            return false;
        }

        if (!classDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return false;
        }

        if (classDecl.BaseList is null)
        {
            return false;
        }

        // Check if any base type looks like IViewFor<...>
        foreach (BaseTypeSyntax baseType in classDecl.BaseList.Types)
        {
            if (baseType.Type is GenericNameSyntax genericName &&
                genericName.Identifier.Text == "IViewFor")
            {
                return true;
            }
        }

        return false;
    }

    private static ViewForCandidate? GetViewForCandidate(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        SemanticModel semanticModel = context.SemanticModel;

        // Get the class symbol
        if (semanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        // Find the IViewFor<T> interface implementation
        INamedTypeSymbol? viewForInterface = null;
        ITypeSymbol? viewModelType = null;

        foreach (INamedTypeSymbol iface in classSymbol.AllInterfaces)
        {
            INamedTypeSymbol originalDef = iface.OriginalDefinition;
            string metadataName = $"{originalDef.ContainingNamespace}.{originalDef.MetadataName}";
            if (metadataName == IViewForFullName)
            {
                viewForInterface = iface;
                viewModelType = iface.TypeArguments[0];
                break;
            }
        }

        if (viewForInterface is null || viewModelType is null)
        {
            return null;
        }

        // Detect platform features
        bool hasBindingContext = HasBindingContextProperty(classSymbol);

        // Check if the class already has a ViewModel property implemented
        bool hasViewModelProperty = false;
        bool hasActivationProperty = false;
        bool hasAutoActivateViewModel = false;

        foreach (ISymbol member in classSymbol.GetMembers())
        {
            if (member is IPropertySymbol prop)
            {
                if (prop.Name == "ViewModel" && !prop.IsAbstract)
                {
                    hasViewModelProperty = true;
                }

                if (prop.Name == "Activation" && !prop.IsAbstract)
                {
                    hasActivationProperty = true;
                }

                if (prop.Name == "AutoActivateViewModel" && !prop.IsAbstract)
                {
                    hasAutoActivateViewModel = true;
                }
            }
        }

        return new ViewForCandidate(
            classDecl,
            classSymbol,
            viewModelType,
            hasBindingContext,
            hasViewModelProperty,
            hasActivationProperty,
            hasAutoActivateViewModel);
    }

    private static bool HasBindingContextProperty(INamedTypeSymbol classSymbol)
    {
        // Check if the class or its base types have a BindingContext property
        // This is a MAUI/Xamarin pattern but we detect it generically
        INamedTypeSymbol? current = classSymbol;
        while (current is not null)
        {
            foreach (ISymbol member in current.GetMembers("BindingContext"))
            {
                if (member is IPropertySymbol)
                {
                    return true;
                }
            }

            current = current.BaseType;
        }

        return false;
    }

    private static void Execute(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<ViewForCandidate> candidates)
    {
        if (candidates.IsDefaultOrEmpty)
        {
            return;
        }

        // Group by containing type to handle multiple IViewFor implementations
        var grouped = candidates
            .GroupBy(c => c.ClassSymbol.ToDisplayString())
            .ToList();

        foreach (var group in grouped)
        {
            ViewForCandidate primary = group.First();

            // Skip if already has both ViewModel and Activation properties
            if (primary.HasViewModelProperty && primary.HasActivationProperty)
            {
                continue;
            }

            string source = GenerateSource(primary);
            string hintName = $"{primary.ClassSymbol.Name}_ViewFor.g.cs";

            context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
        }
    }

    private static string GenerateSource(ViewForCandidate candidate)
    {
        var sb = new StringBuilder();

        string namespaceName = candidate.ClassSymbol.ContainingNamespace.ToDisplayString();
        string className = candidate.ClassSymbol.Name;
        string viewModelTypeName = candidate.ViewModelType.ToDisplayString();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using R3;");
        sb.AppendLine("using R3Ext.Activation;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine($"partial class {className}");
        sb.AppendLine("{");

        // Generate ViewModel property if not already present
        if (!candidate.HasViewModelProperty)
        {
            GenerateViewModelProperty(sb, viewModelTypeName, candidate.HasBindingContext);
        }

        // Generate Activation property if not already present
        if (!candidate.HasActivationProperty)
        {
            GenerateActivationProperty(sb);
        }

        // Generate AutoActivateViewModel if not already present
        if (!candidate.HasAutoActivateViewModel)
        {
            GenerateAutoActivateViewModel(sb);
        }

        // Generate InitializeViewFor helper
        GenerateInitializeViewFor(sb, viewModelTypeName, candidate.HasBindingContext);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateViewModelProperty(StringBuilder sb, string viewModelTypeName, bool hasBindingContext)
    {
        sb.AppendLine($"    private {viewModelTypeName}? _viewModel;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets or sets the ViewModel. Setting this automatically updates BindingContext (if available).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public {viewModelTypeName}? ViewModel");
        sb.AppendLine("    {");
        sb.AppendLine("        get => _viewModel;");
        sb.AppendLine("        set");
        sb.AppendLine("        {");
        sb.AppendLine("            if (ReferenceEquals(_viewModel, value))");
        sb.AppendLine("            {");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            _viewModel = value;");

        if (hasBindingContext)
        {
            sb.AppendLine();
            sb.AppendLine("            // Sync to BindingContext if not already in sync");
            sb.AppendLine("            if (!ReferenceEquals(BindingContext, value))");
            sb.AppendLine("            {");
            sb.AppendLine("                BindingContext = value;");
            sb.AppendLine("            }");
        }

        sb.AppendLine();
        sb.AppendLine("            OnPropertyChanged(nameof(ViewModel));");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void GenerateActivationProperty(StringBuilder sb)
    {
        // Platform-agnostic: use the registered activation provider
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the activation observable for this view.");
        sb.AppendLine("    /// Uses the platform-registered activation provider.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    R3.Observable<R3Ext.Activation.ActivationState> R3Ext.Activation.IActivatable.Activation =>");
        sb.AppendLine("        ((R3Ext.Activation.IActivatableView)this).GetActivation();");
        sb.AppendLine();
    }

    private static void GenerateAutoActivateViewModel(StringBuilder sb)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets a value indicating whether to auto-activate the ViewModel.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public bool AutoActivateViewModel => true;");
        sb.AppendLine();
    }

    private static void GenerateInitializeViewFor(StringBuilder sb, string viewModelTypeName, bool hasBindingContext)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Initializes the IViewFor infrastructure with the specified ViewModel.");
        sb.AppendLine("    /// Call from constructor after InitializeComponent.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"viewModel\">The ViewModel instance to associate with this view.</param>");
        sb.AppendLine($"    private void InitializeViewFor({viewModelTypeName} viewModel)");
        sb.AppendLine("    {");
        sb.AppendLine("        ViewModel = viewModel;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
}

/// <summary>
/// Represents a candidate class that implements IViewFor{TViewModel}.
/// </summary>
internal sealed class ViewForCandidate
{
    public ViewForCandidate(
        ClassDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        ITypeSymbol viewModelType,
        bool hasBindingContext,
        bool hasViewModelProperty,
        bool hasActivationProperty,
        bool hasAutoActivateViewModel)
    {
        ClassDeclaration = classDeclaration;
        ClassSymbol = classSymbol;
        ViewModelType = viewModelType;
        HasBindingContext = hasBindingContext;
        HasViewModelProperty = hasViewModelProperty;
        HasActivationProperty = hasActivationProperty;
        HasAutoActivateViewModel = hasAutoActivateViewModel;
    }

    public ClassDeclarationSyntax ClassDeclaration { get; }

    public INamedTypeSymbol ClassSymbol { get; }

    public ITypeSymbol ViewModelType { get; }

    /// <summary>
    /// Gets a value indicating whether the view has a BindingContext property (MAUI/Xamarin pattern).
    /// </summary>
    public bool HasBindingContext { get; }

    public bool HasViewModelProperty { get; }

    public bool HasActivationProperty { get; }

    public bool HasAutoActivateViewModel { get; }
}
