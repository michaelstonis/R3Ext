using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace R3Ext.Bindings.SourceGenerator;

internal sealed class UiBindingMetadata
{
    public string AssemblyName { get; set; } = string.Empty;
    public IReadOnlyList<UiControlType> Controls { get; set; } = Array.Empty<UiControlType>();

    public static UiBindingMetadata? TryLoad(AdditionalText text)
    {
        try
        {
            using var stream = text.GetText()?.ToString() is { } content
                ? new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content))
                : null;
            if (stream is null) return null;
            return JsonSerializer.Deserialize<UiBindingMetadata>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
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

    private UiBindingLookup(ImmutableDictionary<(string, string, string), string> map)
    {
        _map = map;
    }

    public static UiBindingLookup Build(ImmutableArray<UiBindingMetadata> items)
    {
        var builder = ImmutableDictionary.CreateBuilder<(string, string, string), string>();
        foreach (var item in items)
        {
            foreach (var control in item.Controls)
            {
                foreach (var field in control.Fields)
                {
                    var key = (item.AssemblyName, control.Type, field.Name);
                    if (builder.TryGetValue(key, out var existing))
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
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        if (string.IsNullOrWhiteSpace(existing)) return true;

        var candidateLooksClr = !candidate.Contains("://", StringComparison.Ordinal);
        var existingLooksClr = !existing.Contains("://", StringComparison.Ordinal);

        if (candidateLooksClr && !existingLooksClr) return true;
        if (!candidateLooksClr && existingLooksClr) return false;

        // If both look CLR-qualified, prefer the newest value to pick up later improvements.
        if (candidateLooksClr && existingLooksClr) return true;

        // Fallback: do not replace.
        return false;
    }

    public bool TryGetTargetType(Compilation compilation, string assemblyName, string containingType, string fieldName, out ITypeSymbol? type)
    {
        type = null;
        if (_map.TryGetValue((assemblyName, containingType, fieldName), out var fqName))
        {
            var metadataName = fqName.Replace("global::", string.Empty);
            type = compilation.GetTypeByMetadataName(metadataName);
            if (type is null)
            {
                foreach (var reference in compilation.References)
                {
                    if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol asmSymbol)
                    {
                        var candidate = asmSymbol.GetTypeByMetadataName(metadataName);
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
        => Current.TryGetTargetType(compilation, assemblyName, containingType, fieldName, out type);
}
