using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// Generates optimized JSON catalog of prefabs for GPT-5 context.
    /// Converts PrefabMetadata from registry into token-efficient format.
    /// Includes bounds metadata (size + centerOffset) for spatial reasoning.
    /// Includes ALL components (even without parameters) for capability discovery.
    /// </summary>
    public static class PrefabCatalogGenerator
    {
        /// <summary>
        /// Generates minified JSON catalog from prefab metadata list.
        /// Heavily optimized for token efficiency while preserving essential metadata.
        /// Includes semantic tags and points for LLM spatial reasoning.
        /// </summary>
        /// <param name="prefabs">List of prefab metadata to convert</param>
        /// <returns>Minified JSON string in catalog format</returns>
        public static string GeneratePrefabCatalogJson(List<PrefabMetadata> prefabs)
        {
            if (prefabs == null || prefabs.Count == 0)
            {
                return "{\"prefabs\":[]}";
            }

            var sb = new StringBuilder();
            sb.Append("{\"prefabs\":[");

            for (int i = 0; i < prefabs.Count; i++)
            {
                var prefab = prefabs[i];
                if (prefab == null) continue;

                sb.Append("{");

                // Essential prefab identity
                sb.Append($"\"path\":\"{EscapeJson(prefab.prefabPath)}\",");
                sb.Append($"\"name\":\"{EscapeJson(prefab.prefabName)}\",");
                sb.Append($"\"category\":\"{EscapeJson(prefab.prefabTag)}\",");

                // Synthesized description for quick understanding
                string description = GenerateDescription(prefab);
                sb.Append($"\"description\":\"{EscapeJson(description)}\",");

                // Prefab default scale for semantic point calculations
                sb.Append($"\"scale\":{FormatVector3(prefab.scale)},");

                // Semantic tags for LLM understanding (optional)
                if (prefab.semanticTags != null && prefab.semanticTags.Length > 0)
                {
                    sb.Append("\"semanticTags\":[");
                    for (int t = 0; t < prefab.semanticTags.Length; t++)
                    {
                        sb.Append($"\"{EscapeJson(prefab.semanticTags[t])}\"");
                        if (t < prefab.semanticTags.Length - 1)
                            sb.Append(",");
                    }
                    sb.Append("],");
                }

                // Semantic points with LOCAL offsets for precise placement (optional)
                if (prefab.semanticPoints != null && prefab.semanticPoints.Length > 0)
                {
                    sb.Append("\"semanticPoints\":[");
                    for (int p = 0; p < prefab.semanticPoints.Length; p++)
                    {
                        var point = prefab.semanticPoints[p];
                        // Compact tuple format: ["name", x, y, z]
                        sb.Append($"[\"{EscapeJson(point.name)}\",{CleanFloat(point.offset.x)},{CleanFloat(point.offset.y)},{CleanFloat(point.offset.z)}]");
                        if (p < prefab.semanticPoints.Length - 1)
                            sb.Append(",");
                    }
                    sb.Append("],");
                }

                // Component list - include ALL components for capability discovery
                sb.Append("\"components\":[");

                if (prefab.components != null && prefab.components.Length > 0)
                {
                    for (int j = 0; j < prefab.components.Length; j++)
                    {
                        var component = prefab.components[j];
                        if (component == null) continue;

                        sb.Append("{");
                        sb.Append($"\"type\":\"{EscapeJson(component.componentTypeShortName)}\"");

                        // Add parameters only if they exist
                        if (component.fields != null && component.fields.Length > 0)
                        {
                            sb.Append(",\"params\":{");

                            for (int k = 0; k < component.fields.Length; k++)
                            {
                                var field = component.fields[k];
                                if (field == null) continue;

                                // Extract display name (handle Unity 6 backing fields)
                                string displayName = ExtractDisplayName(field.fieldName);

                                sb.Append($"\"{displayName}\":{{");
                                sb.Append($"\"type\":\"{GetJsonTypeName(field.fieldTypeName)}\"");

                                // Add enum values if applicable
                                if (field.enumValues != null && field.enumValues.Length > 0)
                                {
                                    sb.Append(",\"enum\":[");
                                    for (int e = 0; e < field.enumValues.Length; e++)
                                    {
                                        sb.Append($"\"{EscapeJson(field.enumValues[e])}\"");
                                        if (e < field.enumValues.Length - 1)
                                            sb.Append(",");
                                    }
                                    sb.Append("]");
                                }

                                // Add description if meaningful (renamed to "desc" for token savings)
                                if (!string.IsNullOrEmpty(field.description) && field.description != displayName)
                                {
                                    sb.Append($",\"desc\":\"{EscapeJson(field.description)}\"");
                                }

                                sb.Append("}");

                                if (k < component.fields.Length - 1)
                                    sb.Append(",");
                            }

                            sb.Append("}");
                        }

                        sb.Append("}");

                        if (j < prefab.components.Length - 1)
                            sb.Append(",");
                    }
                }

                sb.Append("]");
                sb.Append("}");

                if (i < prefabs.Count - 1)
                    sb.Append(",");
            }

            sb.Append("]}");

            return sb.ToString();
        }

        /// <summary>
        /// Generates a helpful description from prefab metadata.
        /// Format: "{category} prefab with {components}" or "{category} prefab" if no components.
        /// </summary>
        private static string GenerateDescription(PrefabMetadata prefab)
        {
            string desc = $"{prefab.prefabTag} prefab";

            if (prefab.components != null && prefab.components.Length > 0)
            {
                // List up to 3 component types
                var componentNames = prefab.components
                    .Where(c => c != null && !string.IsNullOrEmpty(c.componentTypeShortName))
                    .Take(3)
                    .Select(c => c.componentTypeShortName);

                if (componentNames.Any())
                {
                    desc += $" ({string.Join(", ", componentNames)})";
                }
            }

            return desc;
        }

        /// <summary>
        /// Formats Vector3 as compact JSON array with cleaned float values.
        /// Rounds very small values to zero (floating point noise).
        /// Token-optimized: [x,y,z] instead of {"x":x,"y":y,"z":z}
        /// </summary>
        private static string FormatVector3(Vector3 v)
        {
            return $"[{CleanFloat(v.x)},{CleanFloat(v.y)},{CleanFloat(v.z)}]";
        }

        /// <summary>
        /// Formats float with 3 decimal places, rounding floating point noise to zero.
        /// Handles very small values (< 0.0001) as zero to reduce token usage.
        /// </summary>
        private static string CleanFloat(float f)
        {
            // Round floating point noise to zero
            if (Math.Abs(f) < 0.0001f)
            {
                return "0.0";
            }

            // Format with 3 decimal places for good precision
            return f.ToString("F3");
        }

        /// <summary>
        /// Maps C# type names to JSON-friendly type names.
        /// </summary>
        private static string GetJsonTypeName(string fieldTypeName)
        {
            switch (fieldTypeName)
            {
                case "int":
                case "float":
                case "double":
                case "long":
                    return "number";
                case "bool":
                    return "boolean";
                case "string":
                    return "string";
                case "Vector2":
                    return "Vector2";
                case "Vector3":
                    return "Vector3";
                case "Color":
                    return "Color";
                default:
                    // Enums and other types return their type name
                    return fieldTypeName;
            }
        }

        /// <summary>
        /// Extracts display name from field name, handling Unity 6 property backing fields.
        /// Example: "<MaxSpeed>k__BackingField" -> "MaxSpeed"
        /// </summary>
        private static string ExtractDisplayName(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) return "field";

            // Unity 6 auto-property backing field pattern
            if (fieldName.StartsWith("<") && fieldName.Contains(">k__BackingField"))
            {
                int start = 1;  // Skip '<'
                int end = fieldName.IndexOf('>');
                if (end > start)
                {
                    return fieldName.Substring(start, end - start);
                }
            }

            return fieldName;
        }

        /// <summary>
        /// Escapes special characters for JSON string embedding.
        /// </summary>
        private static string EscapeJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            return text
                .Replace("\\", "\\\\")   // Backslash
                .Replace("\"", "\\\"")   // Quote
                .Replace("\n", "\\n")    // Newline
                .Replace("\r", "\\r")    // Carriage return
                .Replace("\t", "\\t");   // Tab
        }
    }
}
