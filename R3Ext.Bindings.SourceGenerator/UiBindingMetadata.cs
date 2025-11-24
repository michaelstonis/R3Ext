using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;

namespace R3Ext.Bindings.SourceGenerator;

// AOT-compatible JSON serialization context
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(UiBindingMetadata))]
[JsonSerializable(typeof(UiControlType))]
[JsonSerializable(typeof(UiControlField))]
internal partial class UiBindingMetadataJsonContext : JsonSerializerContext
{
}

internal sealed class UiBindingMetadata
{
    public string AssemblyName { get; set; } = string.Empty;

    public IReadOnlyList<UiControlType> Controls { get; set; } = Array.Empty<UiControlType>();

    public static UiBindingMetadata? TryLoad(AdditionalText text)
    {
        try
        {
            using MemoryStream? stream = text.GetText()?.ToString() is { } content
                ? new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content))
                : null;
            if (stream is null)
            {
                return null;
            }

            return JsonSerializer.Deserialize(stream, UiBindingMetadataJsonContext.Default.UiBindingMetadata);
        }
        catch
        {
            return null;
        }
    }
}

internal sealed class UiControlType
{
    public string Type { get; set; } = string.Empty; // fully qualified containing type name

    public IReadOnlyList<UiControlField> Fields { get; set; } = Array.Empty<UiControlField>();
}

internal sealed class UiControlField
{
    public string Name { get; set; } = string.Empty; // field identifier

    public string Type { get; set; } = string.Empty; // fully qualified control type name
}

internal sealed class UiBindingLookup
{
    private readonly ImmutableDictionary<(string Assembly, string ContainingType, string FieldName), string> _map;

    private UiBindingLookup(ImmutableDictionary<(string Assembly, string ContainingType, string FieldName), string> map)
    {
        _map = map;
    }

    public static UiBindingLookup Build(ImmutableArray<UiBindingMetadata> items)
    {
        ImmutableDictionary<(string Assembly, string ContainingType, string FieldName), string>.Builder builder =
            ImmutableDictionary.CreateBuilder<(string Assembly, string ContainingType, string FieldName), string>();
        foreach (UiBindingMetadata? item in items)
        {
            foreach (UiControlType? control in item.Controls)
            {
                foreach (UiControlField? field in control.Fields)
                {
                    (string AssemblyName, string Type, string Name) key = (item.AssemblyName, control.Type, field.Name);
                    if (builder.TryGetValue(key, out string? existing))
                    {
                        if (IsPreferredType(existing, field.Type))
                        {
                            builder[key] = field.Type;
                        }
                    }
                    else
                    {
                        builder[key] = field.Type;
                    }
                }
            }
        }

        return new UiBindingLookup(builder.ToImmutable());
    }

    private static bool IsPreferredType(string existing, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(existing))
        {
            return true;
        }

        // netstandard2.0 compatibility: string.Contains(value, StringComparison) is unavailable
        bool candidateLooksClr = candidate.IndexOf("://", StringComparison.Ordinal) < 0;
        bool existingLooksClr = existing.IndexOf("://", StringComparison.Ordinal) < 0;

        if (candidateLooksClr && !existingLooksClr)
        {
            return true;
        }

        if (!candidateLooksClr && existingLooksClr)
        {
            return false;
        }

        // If both look CLR-qualified, prefer the newest value to pick up later improvements.
        if (candidateLooksClr && existingLooksClr)
        {
            return true;
        }

        // Fallback: do not replace.
        return false;
    }

    public bool TryGetTargetType(Compilation compilation, string assemblyName, string containingType, string fieldName, out ITypeSymbol? type)
    {
        type = null;
        if (_map.TryGetValue((assemblyName, containingType, fieldName), out string? fqName))
        {
            string metadataName = fqName.Replace("global::", string.Empty);
            type = compilation.GetTypeByMetadataName(metadataName);
            if (type is null)
            {
                foreach (MetadataReference? reference in compilation.References)
                {
                    if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol asmSymbol)
                    {
                        INamedTypeSymbol? candidate = asmSymbol.GetTypeByMetadataName(metadataName);
                        if (candidate is not null)
                        {
                            type = candidate;
                            break;
                        }
                    }
                }
            }
        }

        return type is not null;
    }
}

internal static class UiBindingLookupProvider
{
    // This is a simple static holder updated per-compilation in Initialize via uiLookup.Select.
    // Generators are instantiated once per compilation, so this is safe for our scenario.
    public static UiBindingLookup Current { get; set; } = UiBindingLookup.Build(ImmutableArray<UiBindingMetadata>.Empty);

    public static bool TryResolve(Compilation compilation, string assemblyName, string containingType, string fieldName, out ITypeSymbol? type)
    {
        return Current.TryGetTargetType(compilation, assemblyName, containingType, fieldName, out type);
    }
}
