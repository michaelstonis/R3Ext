using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace R3Ext.Bindings.MauiTargets;

// AOT-compatible JSON serialization context
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(GenerateUiBindingTargetsTask.UiBindingMetadataDto))]
[JsonSerializable(typeof(GenerateUiBindingTargetsTask.UiControlTypeDto))]
[JsonSerializable(typeof(GenerateUiBindingTargetsTask.UiControlFieldDto))]
internal partial class UiBindingTargetsJsonContext : JsonSerializerContext
{
}

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
            Dictionary<string, HashSet<(string FieldName, string FieldType)>> controls = new();

            // Prefer generated .g.cs files for precise field names/types
            if (GeneratedCodeFiles is not null && GeneratedCodeFiles.Length > 0)
            {
                foreach (ITaskItem item in GeneratedCodeFiles)
                {
                    string? path = item.ItemSpec;
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    ScanGeneratedCode(path, controls);
                }
            }

            // Fallback to XAML parse if nothing found from generated code
            if (controls.Count == 0 && XamlFiles is not null)
            {
                foreach (ITaskItem item in XamlFiles)
                {
                    string? path = item.ItemSpec;
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    ScanXaml(path, controls);
                }
            }

            if (controls.Count == 0)
            {
                Log.LogMessage(MessageImportance.Low, "R3Ext: No UI binding targets discovered; skipping metadata emission.");
                return true;
            }

            UiBindingMetadataDto metadata = new()
            {
                AssemblyName = ProjectAssemblyName,
                Controls = controls.Select(kvp => new UiControlTypeDto
                {
                    Type = NormalizeContainingType(kvp.Key),
                    Fields = kvp.Value
                                                          .Select(f => new UiControlFieldDto
                                                          {
                                                              Name = f.FieldName,
                                                              Type = NormalizeType(f.FieldType),
                                                          })
                                                          .ToArray(),
                }).ToArray(),
            };

            Directory.CreateDirectory(IntermediateOutputPath);
            string outputPath = Path.Combine(IntermediateOutputPath, "R3Ext.BindingTargets.json");
            string json = JsonSerializer.Serialize(metadata, UiBindingTargetsJsonContext.Default.UiBindingMetadataDto);
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
    {
        return type.StartsWith("global::", StringComparison.Ordinal) ? type : "global::" + type.Trim();
    }

    private static string NormalizeContainingType(string type)
    {
        return type.StartsWith("global::", StringComparison.Ordinal) ? type : "global::" + type.Trim();
    }

    private static readonly Dictionary<string, string> KnownNamespaceMappings = new(StringComparer.Ordinal)
    {
        ["http://schemas.microsoft.com/dotnet/2021/maui"] =
                                                                                        "Microsoft.Maui.Controls",
        ["http://schemas.microsoft.com/winfx/2009/xaml"] =
                                                                                        "Microsoft.Maui.Controls.Xaml",
    };

    private static void ScanGeneratedCode(string path, Dictionary<string, HashSet<(string FieldName, string FieldType)>> controls)
    {
        string text = File.ReadAllText(path);

        // Capture namespace and partial class to form fully qualified containing type
        Regex namespaceRegex = new(@"^\s*namespace\s+(?<ns>[A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.Compiled);

        // Support file-scoped namespaces: namespace Foo.Bar;
        Regex fileScopedNamespaceRegex = new(@"^\s*namespace\s+(?<ns>[A-Za-z_][A-Za-z0-9_.]*)\s*;", RegexOptions.Compiled);
        Regex classRegex = new(@"partial\s+class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

        // Capture field declarations; tolerate modifiers
        Regex fieldRegex =
            new(
                @"(?:(?:public|internal|protected|private)\s+)?(?:(?:static|readonly|volatile)\s+)*(?<type>(?:global::)?[A-Za-z_][A-Za-z0-9_.<>]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*;",
                RegexOptions.Compiled);

        string? currentNs = null;
        string? currentClass = null;
        int namespaceBraceDepth = -1;
        int classBraceDepth = -1;
        bool fileScopedNamespaceActive = false;
        int braceDepth = 0;

        foreach (string line in text.Split(new[] { "\r\n", "\n", }, StringSplitOptions.None))
        {
            int openCount = CountChar(line, '{');
            int closeCount = CountChar(line, '}');

            Match fsMatch = fileScopedNamespaceRegex.Match(line);
            if (fsMatch.Success)
            {
                currentNs = fsMatch.Groups["ns"].Value;
                fileScopedNamespaceActive = true;
                namespaceBraceDepth = -1;
            }
            else
            {
                Match nsMatch = namespaceRegex.Match(line);
                if (nsMatch.Success)
                {
                    currentNs = nsMatch.Groups["ns"].Value;
                    fileScopedNamespaceActive = false;
                    namespaceBraceDepth = braceDepth + (openCount > 0 ? openCount : 1);
                }
            }

            Match classMatch = classRegex.Match(line);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups["name"].Value;
                int depthIncrement = openCount > 0 ? openCount : 1;
                classBraceDepth = braceDepth;
                classBraceDepth += depthIncrement;
            }

            if (currentClass is null)
            {
                continue;
            }

            Match fieldMatch = fieldRegex.Match(line);
            if (fieldMatch.Success)
            {
                string fieldType = fieldMatch.Groups["type"].Value;
                string fieldName = fieldMatch.Groups["name"].Value;
                string fullType = string.IsNullOrEmpty(currentNs) ? currentClass : currentNs + "." + currentClass;
                fullType = NormalizeContainingType(fullType);

                if (!controls.TryGetValue(fullType, out HashSet<(string FieldName, string FieldType)>? set))
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
            XDocument doc = XDocument.Load(path);
            XElement? root = doc.Root;
            if (root is null)
            {
                return;
            }

            string? xClass = root.Attributes().FirstOrDefault(a => a.Name.LocalName == "Class")?.Value
                             ?? root.Attributes().FirstOrDefault(a => a.Name.LocalName == "x:Class")?.Value;
            if (string.IsNullOrWhiteSpace(xClass))
            {
                return;
            }

            string containingType = NormalizeContainingType(xClass!);
            if (!controls.TryGetValue(containingType, out HashSet<(string FieldName, string FieldType)>? set))
            {
                set = new HashSet<(string FieldName, string FieldType)>();
                controls[containingType] = set;
            }

            Dictionary<string, string> nsMappings = root.Attributes()
                .Where(a => a.IsNamespaceDeclaration)
                .ToDictionary(a => a.Name.LocalName, a => a.Value, StringComparer.Ordinal);

            XNamespace defaultNs = root.GetDefaultNamespace();
            if (!string.IsNullOrEmpty(defaultNs.NamespaceName) && !nsMappings.ContainsKey(string.Empty))
            {
                nsMappings[string.Empty] = defaultNs.NamespaceName;
            }

            foreach (XElement? el in root.Descendants())
            {
                XAttribute? nameAttr = el.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")
                                       ?? el.Attributes().FirstOrDefault(a => a.Name.LocalName.EndsWith(":Name", StringComparison.Ordinal));
                if (nameAttr is null)
                {
                    continue;
                }

                string name = nameAttr.Value;
                string? clrType = ResolveClrType(el.Name, nsMappings);
                if (clrType is null)
                {
                    continue;
                }

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
        string local = elementName.LocalName;
        if (string.IsNullOrEmpty(local))
        {
            return null;
        }

        string ns = elementName.NamespaceName;
        if (!string.IsNullOrEmpty(ns))
        {
            string? resolved = ResolveNamespaceValue(ns, local);
            if (resolved is not null)
            {
                return resolved;
            }

            foreach (KeyValuePair<string, string> mapping in nsMappings)
            {
                if (string.Equals(mapping.Value, ns, StringComparison.Ordinal))
                {
                    resolved = ResolveNamespaceValue(mapping.Value, local);
                    if (resolved is not null)
                    {
                        return resolved;
                    }
                }
            }
        }
        else if (nsMappings.TryGetValue(string.Empty, out string? defaultNamespaceValue))
        {
            string? resolved = ResolveNamespaceValue(defaultNamespaceValue, local);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
    }

    private static string? ResolveNamespaceValue(string nsValue, string local)
    {
        if (string.IsNullOrWhiteSpace(nsValue))
        {
            return null;
        }

        if (nsValue.StartsWith("clr-namespace:", StringComparison.Ordinal))
        {
            string remainder = nsValue.Substring("clr-namespace:".Length);
            int separatorIndex = remainder.IndexOf(';');
            if (separatorIndex >= 0)
            {
                remainder = remainder.Substring(0, separatorIndex);
            }

            remainder = remainder.Trim();
            if (remainder.Length == 0)
            {
                return null;
            }

            return remainder + "." + local;
        }

        if (nsValue.StartsWith("using:", StringComparison.Ordinal))
        {
            string remainder = nsValue.Substring("using:".Length).Trim();
            if (remainder.Length == 0)
            {
                return null;
            }

            return remainder + "." + local;
        }

        if (KnownNamespaceMappings.TryGetValue(nsValue, out string? clrNamespace))
        {
            return clrNamespace + "." + local;
        }

        // netstandard2.0 compatibility: string.Contains(value, StringComparison) is unavailable
        if (nsValue.IndexOf("://", StringComparison.Ordinal) < 0)
        {
            string trimmed = nsValue.Trim().TrimEnd('.');
            if (trimmed.Length == 0)
            {
                return null;
            }

            return trimmed + "." + local;
        }

        return null;
    }

    private static int CountChar(string text, char ch)
    {
        int count = 0;
        foreach (char c in text)
        {
            if (c == ch)
            {
                count++;
            }
        }

        return count;
    }

    internal sealed class UiBindingMetadataDto
    {
        public string AssemblyName { get; set; } = string.Empty;

        public UiControlTypeDto[] Controls { get; set; } = Array.Empty<UiControlTypeDto>();
    }

    internal sealed class UiControlTypeDto
    {
        public string Type { get; set; } = string.Empty;

        public UiControlFieldDto[] Fields { get; set; } = Array.Empty<UiControlFieldDto>();
    }

    internal sealed class UiControlFieldDto
    {
        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;
    }
}
