// Copyright (c) 2024 Michael Stonis. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace R3Ext.Bindings.AvaloniaTargets;

// AOT-compatible JSON serialization context
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(GenerateAvaloniaUiBindingTargetsTask.UiBindingMetadataDto))]
[JsonSerializable(typeof(GenerateAvaloniaUiBindingTargetsTask.UiControlTypeDto))]
[JsonSerializable(typeof(GenerateAvaloniaUiBindingTargetsTask.UiControlFieldDto))]
internal partial class AvaloniaBindingTargetsJsonContext : JsonSerializerContext
{
}

/// <summary>
/// MSBuild task that scans Avalonia-generated code and AXAML files to extract x:Name control metadata
/// for R3Ext binding source generation.
/// </summary>
public sealed class GenerateAvaloniaUiBindingTargetsTask : Microsoft.Build.Utilities.Task
{
    [Required]
    public string ProjectAssemblyName { get; set; } = string.Empty;

    [Required]
    public string IntermediateOutputPath { get; set; } = string.Empty;

    public ITaskItem[]? GeneratedCodeFiles { get; set; }

    public ITaskItem[]? AxamlFiles { get; set; }

    public override bool Execute()
    {
        try
        {
            Dictionary<string, HashSet<(string FieldName, string FieldType)>> controls = new();

            // Prefer generated .g.cs files from Avalonia's NameGenerator for precise field names/types
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

            // Fallback to AXAML parse if nothing found from generated code
            if (controls.Count == 0 && AxamlFiles is not null)
            {
                foreach (ITaskItem item in AxamlFiles)
                {
                    string? path = item.ItemSpec;
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    ScanAxaml(path, controls);
                }
            }

            if (controls.Count == 0)
            {
                Log.LogMessage(MessageImportance.Low, "R3Ext: No Avalonia UI binding targets discovered; skipping metadata emission.");
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
            string json = JsonSerializer.Serialize(metadata, AvaloniaBindingTargetsJsonContext.Default.UiBindingMetadataDto);
            File.WriteAllText(outputPath, json);
            Log.LogMessage(MessageImportance.Low, $"R3Ext: wrote Avalonia UI binding metadata to {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            Log.LogWarning($"R3Ext: GenerateAvaloniaUiBindingTargetsTask failed: {ex.Message}");
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

    /// <summary>
    /// Known Avalonia XML namespace mappings.
    /// </summary>
    private static readonly Dictionary<string, string> KnownNamespaceMappings = new(StringComparer.Ordinal)
    {
        ["https://github.com/avaloniaui"] = "Avalonia.Controls",
        ["http://schemas.microsoft.com/winfx/2006/xaml"] = "System",
    };

    /// <summary>
    /// Scans Avalonia-generated .g.cs files for field declarations.
    /// Avalonia's NameGenerator creates fields like:
    /// <code>
    /// internal global::Avalonia.Controls.TextBox UserNameTextBox;
    /// </code>
    /// or properties like:
    /// <code>
    /// internal global::Avalonia.Controls.TextBox UserNameTextBox => this.FindNameScope()?.Find&lt;...&gt;("...");
    /// </code>
    /// </summary>
    private static void ScanGeneratedCode(string path, Dictionary<string, HashSet<(string FieldName, string FieldType)>> controls)
    {
        string text = File.ReadAllText(path);

        // Skip if not an auto-generated file (netstandard2.0 compatible)
        if (text.IndexOf("<auto-generated", StringComparison.Ordinal) < 0)
        {
            return;
        }

        // Capture namespace and partial class to form fully qualified containing type
        Regex namespaceRegex = new(@"^\s*namespace\s+(?<ns>[A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.Compiled);

        // Support file-scoped namespaces: namespace Foo.Bar;
        Regex fileScopedNamespaceRegex = new(@"^\s*namespace\s+(?<ns>[A-Za-z_][A-Za-z0-9_.]*)\s*;", RegexOptions.Compiled);
        Regex classRegex = new(@"partial\s+class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

        // Capture field declarations; tolerate modifiers (for InitializeComponent mode)
        Regex fieldRegex =
            new(
                @"(?:(?:public|internal|protected|private)\s+)?(?:(?:static|readonly|volatile)\s+)*(?<type>(?:global::)?[A-Za-z_][A-Za-z0-9_.<>?]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*;",
                RegexOptions.Compiled);

        // Capture property declarations (for OnlyProperties mode)
        // Pattern: internal global::Avalonia.Controls.TextBox UserNameTextBox => ...
        Regex propertyRegex =
            new(
                @"(?:(?:public|internal|protected|private)\s+)?(?<type>(?:global::)?[A-Za-z_][A-Za-z0-9_.<>?]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=>\s*this\.FindNameScope\(\)",
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
                goto UpdateBraces;
            }

            // Try field match first
            Match fieldMatch = fieldRegex.Match(line);
            if (fieldMatch.Success)
            {
                string fieldType = fieldMatch.Groups["type"].Value;
                string fieldName = fieldMatch.Groups["name"].Value;

                // Skip common non-control fields
                if (fieldName == "__thisNameScope__" || fieldName.StartsWith("_"))
                {
                    goto UpdateBraces;
                }

                AddControl(controls, currentNs, currentClass, fieldName, fieldType);
            }
            else
            {
                // Try property match (OnlyProperties mode)
                Match propertyMatch = propertyRegex.Match(line);
                if (propertyMatch.Success)
                {
                    string propType = propertyMatch.Groups["type"].Value;
                    string propName = propertyMatch.Groups["name"].Value;
                    AddControl(controls, currentNs, currentClass, propName, propType);
                }
            }

        UpdateBraces:
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

    private static void AddControl(
        Dictionary<string, HashSet<(string FieldName, string FieldType)>> controls,
        string? ns,
        string className,
        string fieldName,
        string fieldType)
    {
        string fullType = string.IsNullOrEmpty(ns) ? className : ns + "." + className;
        fullType = NormalizeContainingType(fullType);

        if (!controls.TryGetValue(fullType, out HashSet<(string FieldName, string FieldType)>? set))
        {
            set = new HashSet<(string FieldName, string FieldType)>();
            controls[fullType] = set;
        }

        set.Add((fieldName, NormalizeType(fieldType)));
    }

    /// <summary>
    /// Scans AXAML files directly for x:Name controls.
    /// </summary>
    private static void ScanAxaml(string path, Dictionary<string, HashSet<(string FieldName, string FieldType)>> controls)
    {
        try
        {
            XDocument doc = XDocument.Load(path);
            XElement? root = doc.Root;
            if (root is null)
            {
                return;
            }

            // Get x:Class attribute
            XNamespace xNs = "http://schemas.microsoft.com/winfx/2006/xaml";
            string? xClass = root.Attribute(xNs + "Class")?.Value;
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

            // Build namespace mappings from root element
            Dictionary<string, string> nsMappings = root.Attributes()
                .Where(a => a.IsNamespaceDeclaration)
                .ToDictionary(a => a.Name.LocalName, a => a.Value, StringComparer.Ordinal);

            XNamespace defaultNs = root.GetDefaultNamespace();
            if (!string.IsNullOrEmpty(defaultNs.NamespaceName) && !nsMappings.ContainsKey(string.Empty))
            {
                nsMappings[string.Empty] = defaultNs.NamespaceName;
            }

            // Find all elements with x:Name or Name attributes
            foreach (XElement? el in root.DescendantsAndSelf())
            {
                // Check for x:Name first, then Name
                XAttribute? nameAttr = el.Attribute(xNs + "Name") ?? el.Attribute("Name");
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
            // Ignore AXAML parse errors; best-effort
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

        // Handle clr-namespace: prefix
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

        // Handle using: prefix (common in Avalonia)
        if (nsValue.StartsWith("using:", StringComparison.Ordinal))
        {
            string remainder = nsValue.Substring("using:".Length).Trim();
            if (remainder.Length == 0)
            {
                return null;
            }

            return remainder + "." + local;
        }

        // Check known namespace mappings
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
