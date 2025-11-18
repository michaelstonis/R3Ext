using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace R3Ext.Bindings.MauiTargets;

public sealed class GenerateUiBindingTargetsTask : Microsoft.Build.Utilities.Task
{
    [Required]
    public string ProjectAssemblyName { get; set; } = string.Empty;

    [Required]
    public string IntermediateOutputPath { get; set; } = string.Empty;

    public ITaskItem[]? GeneratedCodeFiles { get; set; }

    public ITaskItem[]? XamlFiles { get; set; }

    public override bool Execute()
    {
        try
        {
            var controls = new Dictionary<string, HashSet<(string FieldName, string FieldType)>>();

            // Prefer generated .g.cs files for precise field names/types
            if (GeneratedCodeFiles is not null && GeneratedCodeFiles.Length > 0)
            {
                foreach (var item in GeneratedCodeFiles)
                {
                    var path = item.ItemSpec;
                    if (!File.Exists(path)) continue;
                    ScanGeneratedCode(path, controls);
                }
            }

            // Fallback to XAML parse if nothing found from generated code
            if (controls.Count == 0 && XamlFiles is not null)
            {
                foreach (var item in XamlFiles)
                {
                    var path = item.ItemSpec;
                    if (!File.Exists(path)) continue;
                    ScanXaml(path, controls);
                }
            }

            if (controls.Count == 0)
            {
                Log.LogMessage(MessageImportance.Low, "R3Ext: No UI binding targets discovered; skipping metadata emission.");
                return true;
            }

            var metadata = new UiBindingMetadataDto
            {
                AssemblyName = ProjectAssemblyName,
                Controls = controls.Select(kvp => new UiControlTypeDto
                {
                    Type = NormalizeContainingType(kvp.Key),
                    Fields = kvp.Value
                        .Select(f => new UiControlFieldDto { Name = f.FieldName, Type = NormalizeType(f.FieldType) })
                        .ToArray()
                }).ToArray()
            };

            Directory.CreateDirectory(IntermediateOutputPath);
            var outputPath = Path.Combine(IntermediateOutputPath, "R3Ext.BindingTargets.json");
            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(outputPath, json);
            Log.LogMessage(MessageImportance.Low, $"R3Ext: wrote UI binding metadata to {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            Log.LogWarning($"R3Ext: GenerateUiBindingTargetsTask failed: {ex.Message}");
            return true; // don't break builds; core generator will just have less info
        }
    }

    private static string NormalizeType(string type)
        => type.StartsWith("global::", StringComparison.Ordinal) ? type : "global::" + type.Trim();

    private static string NormalizeContainingType(string type)
        => type.StartsWith("global::", StringComparison.Ordinal) ? type : "global::" + type.Trim();

    private static readonly Dictionary<string, string> KnownNamespaceMappings = new(StringComparer.Ordinal)
    {
        ["http://schemas.microsoft.com/dotnet/2021/maui"] = "Microsoft.Maui.Controls",
        ["http://schemas.microsoft.com/winfx/2009/xaml"] = "Microsoft.Maui.Controls.Xaml"
    };

    private static void ScanGeneratedCode(string path, Dictionary<string, HashSet<(string FieldName, string FieldType)>> controls)
    {
        var text = File.ReadAllText(path);
        // Capture namespace and partial class to form fully qualified containing type
        var namespaceRegex = new Regex(@"^\s*namespace\s+(?<ns>[A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.Compiled);
        // Support file-scoped namespaces: namespace Foo.Bar;
        var fileScopedNamespaceRegex = new Regex(@"^\s*namespace\s+(?<ns>[A-Za-z_][A-Za-z0-9_.]*)\s*;", RegexOptions.Compiled);
        var classRegex = new Regex(@"partial\s+class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
        // Capture field declarations; tolerate modifiers
        var fieldRegex = new Regex(@"(?:(?:public|internal|protected|private)\s+)?(?:(?:static|readonly|volatile)\s+)*(?<type>(?:global::)?[A-Za-z_][A-Za-z0-9_.<>]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*;", RegexOptions.Compiled);

        string? currentNs = null;
        string? currentClass = null;
        int namespaceBraceDepth = -1;
        int classBraceDepth = -1;
        bool fileScopedNamespaceActive = false;
        var braceDepth = 0;

        foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var openCount = CountChar(line, '{');
            var closeCount = CountChar(line, '}');

            var fsMatch = fileScopedNamespaceRegex.Match(line);
            if (fsMatch.Success)
            {
                currentNs = fsMatch.Groups["ns"].Value;
                fileScopedNamespaceActive = true;
                namespaceBraceDepth = -1;
            }
            else
            {
                var nsMatch = namespaceRegex.Match(line);
                if (nsMatch.Success)
                {
                    currentNs = nsMatch.Groups["ns"].Value;
                    fileScopedNamespaceActive = false;
                    namespaceBraceDepth = braceDepth + (openCount > 0 ? openCount : 1);
                }
            }

            var classMatch = classRegex.Match(line);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups["name"].Value;
                var depthIncrement = openCount > 0 ? openCount : 1;
                classBraceDepth = braceDepth;
                classBraceDepth += depthIncrement;
            }

            if (currentClass is null) continue;

            var fieldMatch = fieldRegex.Match(line);
            if (fieldMatch.Success)
            {
                var fieldType = fieldMatch.Groups["type"].Value;
                var fieldName = fieldMatch.Groups["name"].Value;
                var fullType = (string.IsNullOrEmpty(currentNs) ? currentClass : currentNs + "." + currentClass);
                fullType = NormalizeContainingType(fullType);

                if (!controls.TryGetValue(fullType, out var set))
                {
                    set = new HashSet<(string FieldName, string FieldType)>();
                    controls[fullType] = set;
                }
                set.Add((fieldName, NormalizeType(fieldType)));
            }

            braceDepth += openCount;
            braceDepth -= closeCount;
            if (closeCount > 0)
            {
                if (!fileScopedNamespaceActive && namespaceBraceDepth >= 0 && braceDepth < namespaceBraceDepth)
                {
                    currentNs = null;
                    namespaceBraceDepth = -1;
                }
                if (classBraceDepth >= 0 && braceDepth < classBraceDepth)
                {
                    currentClass = null;
                    classBraceDepth = -1;
                }
            }
        }
    }

    private static void ScanXaml(string path, Dictionary<string, HashSet<(string FieldName, string FieldType)>> controls)
    {
        try
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root is null) return;

            var xClass = root.Attributes().FirstOrDefault(a => a.Name.LocalName == "Class")?.Value
                      ?? root.Attributes().FirstOrDefault(a => a.Name.LocalName == "x:Class")?.Value;
            if (string.IsNullOrWhiteSpace(xClass)) return;

            var containingType = NormalizeContainingType(xClass!);
            if (!controls.TryGetValue(containingType, out var set))
            {
                set = new HashSet<(string FieldName, string FieldType)>();
                controls[containingType] = set;
            }

            var nsMappings = root.Attributes()
                .Where(a => a.IsNamespaceDeclaration)
                .ToDictionary(a => a.Name.LocalName, a => a.Value, StringComparer.Ordinal);

            var defaultNs = root.GetDefaultNamespace();
            if (!string.IsNullOrEmpty(defaultNs.NamespaceName) && !nsMappings.ContainsKey(string.Empty))
            {
                nsMappings[string.Empty] = defaultNs.NamespaceName;
            }

            foreach (var el in root.Descendants())
            {
                var nameAttr = el.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")
                            ?? el.Attributes().FirstOrDefault(a => a.Name.LocalName.EndsWith(":Name", StringComparison.Ordinal));
                if (nameAttr is null) continue;

                var name = nameAttr.Value;
                var clrType = ResolveClrType(el.Name, nsMappings);
                if (clrType is null) continue;

                set.Add((name, NormalizeType(clrType)));
            }
        }
        catch
        {
            // Ignore XAML parse errors; best-effort
        }
    }

    private static string? ResolveClrType(XName elementName, Dictionary<string, string> nsMappings)
    {
        var local = elementName.LocalName;
        if (string.IsNullOrEmpty(local)) return null;

        var ns = elementName.NamespaceName;
        if (!string.IsNullOrEmpty(ns))
        {
            var resolved = ResolveNamespaceValue(ns, local);
            if (resolved is not null) return resolved;

            foreach (var mapping in nsMappings)
            {
                if (string.Equals(mapping.Value, ns, StringComparison.Ordinal))
                {
                    resolved = ResolveNamespaceValue(mapping.Value, local);
                    if (resolved is not null) return resolved;
                }
            }
        }
        else if (nsMappings.TryGetValue(string.Empty, out var defaultNamespaceValue))
        {
            var resolved = ResolveNamespaceValue(defaultNamespaceValue, local);
            if (resolved is not null) return resolved;
        }

        return null;
    }

    private static string? ResolveNamespaceValue(string nsValue, string local)
    {
        if (string.IsNullOrWhiteSpace(nsValue)) return null;

        if (nsValue.StartsWith("clr-namespace:", StringComparison.Ordinal))
        {
            var remainder = nsValue.Substring("clr-namespace:".Length);
            var separatorIndex = remainder.IndexOf(';');
            if (separatorIndex >= 0)
            {
                remainder = remainder.Substring(0, separatorIndex);
            }
            remainder = remainder.Trim();
            if (remainder.Length == 0) return null;
            return remainder + "." + local;
        }

        if (nsValue.StartsWith("using:", StringComparison.Ordinal))
        {
            var remainder = nsValue.Substring("using:".Length).Trim();
            if (remainder.Length == 0) return null;
            return remainder + "." + local;
        }

        if (KnownNamespaceMappings.TryGetValue(nsValue, out var clrNamespace))
        {
            return clrNamespace + "." + local;
        }

        if (!nsValue.Contains("://", StringComparison.Ordinal))
        {
            var trimmed = nsValue.Trim().TrimEnd('.');
            if (trimmed.Length == 0) return null;
            return trimmed + "." + local;
        }

        return null;
    }

    private static int CountChar(string text, char ch)
    {
        var count = 0;
        foreach (var c in text)
        {
            if (c == ch) count++;
        }
        return count;
    }

    private sealed class UiBindingMetadataDto
    {
        public string AssemblyName { get; set; } = string.Empty;
        public UiControlTypeDto[] Controls { get; set; } = Array.Empty<UiControlTypeDto>();
    }

    private sealed class UiControlTypeDto
    {
        public string Type { get; set; } = string.Empty;
        public UiControlFieldDto[] Fields { get; set; } = Array.Empty<UiControlFieldDto>();
    }

    private sealed class UiControlFieldDto
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}
