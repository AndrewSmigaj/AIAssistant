using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Generates OpenAI function schemas dynamically from PrefabRegistry.
    /// Converts PrefabMetadata into JSON tool definitions for GPT-5 function calling.
    /// </summary>
    public static class DynamicToolGenerator
    {
        /// <summary>
        /// Generates tools JSON array for selected prefab categories.
        /// Returns fallback tools (rectangle/circle) if no prefabs available.
        /// </summary>
        /// <param name="selectedTags">Unity tags to filter prefabs (null = all)</param>
        /// <returns>JSON array string of tool definitions</returns>
        public static string GenerateToolsJson(List<string> selectedTags)
        {
            try
            {
                // Load registry
                PrefabRegistry registry = PrefabRegistryCache.Load();
                if (registry == null)
                {
                    Debug.Log("[AI Assistant] PrefabRegistry not found, using fallback tools");
                    return GenerateFallbackToolsJson();
                }

                // Get prefabs by tags
                List<PrefabMetadata> prefabs = PrefabRegistryCache.GetByTags(selectedTags);
                if (prefabs == null || prefabs.Count == 0)
                {
                    Debug.Log("[AI Assistant] No prefabs found for selected tags, using fallback tools");
                    return GenerateFallbackToolsJson();
                }

                // Build JSON array
                var sb = new StringBuilder();
                sb.Append("[\n");

                for (int i = 0; i < prefabs.Count; i++)
                {
                    string schema = GenerateFunctionSchema(prefabs[i]);
                    sb.Append(schema);

                    if (i < prefabs.Count - 1)
                        sb.Append(",\n");
                }

                sb.Append("\n]");

                string result = sb.ToString();
                Debug.Log($"[AI Assistant] Generated {prefabs.Count} dynamic tool(s), {result.Length} chars");
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to generate tools JSON: {ex.Message}");
                return GenerateFallbackToolsJson();
            }
        }

        /// <summary>
        /// Returns Phase 1 fallback tools (rectangle/circle).
        /// Used when no prefabs available or generation fails.
        /// </summary>
        public static string GenerateFallbackToolsJson()
        {
            return OpenAIClient.FALLBACK_TOOLS_JSON;
        }

        /// <summary>
        /// Generates single function schema for one prefab.
        /// </summary>
        private static string GenerateFunctionSchema(PrefabMetadata prefab)
        {
            if (prefab == null) return "";

            var sb = new StringBuilder();

            // Function wrapper
            sb.Append("  {\n");
            sb.Append("    \"type\": \"function\",\n");
            sb.Append($"    \"name\": \"{EscapeJson(prefab.uniqueFunctionName)}\",\n");
            sb.Append($"    \"description\": \"Creates a {EscapeJson(prefab.prefabName)} prefab (tag: {EscapeJson(prefab.prefabTag)})\",\n");
            sb.Append("    \"parameters\": {\n");
            sb.Append("      \"type\": \"object\",\n");
            sb.Append("      \"properties\": {\n");

            // Name parameter (always present, always required)
            sb.Append("        \"name\": {\"type\": \"string\", \"description\": \"GameObject name\"},\n");

            // Position parameters (always present, always required)
            sb.Append("        \"x\": {\"type\": \"number\", \"description\": \"World X position\"},\n");
            sb.Append("        \"y\": {\"type\": \"number\", \"description\": \"World Y position\"},\n");
            sb.Append("        \"z\": {\"type\": \"number\", \"description\": \"World Z position\"},\n");

            // Rotation parameters (optional, default to 0)
            sb.Append("        \"rotationX\": {\"type\": \"number\", \"description\": \"Rotation around X axis in degrees (default: 0)\"},\n");
            sb.Append("        \"rotationY\": {\"type\": \"number\", \"description\": \"Rotation around Y axis in degrees (default: 0)\"},\n");
            sb.Append("        \"rotationZ\": {\"type\": \"number\", \"description\": \"Rotation around Z axis in degrees (default: 0)\"},\n");

            // Scale parameters (optional, default to 1)
            sb.Append("        \"scaleX\": {\"type\": \"number\", \"description\": \"Scale along X axis (default: 1.0)\"},\n");
            sb.Append("        \"scaleY\": {\"type\": \"number\", \"description\": \"Scale along Y axis (default: 1.0)\"},\n");
            sb.Append("        \"scaleZ\": {\"type\": \"number\", \"description\": \"Scale along Z axis (default: 1.0)\"}");

            // Field parameters from components
            if (prefab.components != null)
            {
                foreach (var component in prefab.components)
                {
                    if (component == null || component.fields == null) continue;

                    foreach (var field in component.fields)
                    {
                        if (field == null || string.IsNullOrEmpty(field.parameterName)) continue;

                        sb.Append(",\n");
                        sb.Append(MapFieldToJsonParameter(field));
                    }
                }
            }

            sb.Append("\n      },\n");
            sb.Append("      \"required\": [\"name\", \"x\", \"y\", \"z\"]\n");  // Name and position required
            sb.Append("    }\n");
            sb.Append("  }");

            return sb.ToString();
        }

        /// <summary>
        /// Converts FieldMetadata to JSON parameter definition with proper type mapping.
        /// </summary>
        private static string MapFieldToJsonParameter(FieldMetadata field)
        {
            string paramName = EscapeJson(field.parameterName);
            string fieldTypeName = field.fieldTypeName;

            // Create description with component.field path
            string displayName = ExtractDisplayName(field.fieldName);
            string componentShort = field.componentTypeName.Contains(".")
                ? field.componentTypeName.Substring(field.componentTypeName.LastIndexOf('.') + 1)
                : field.componentTypeName;

            string description;
            if (string.IsNullOrEmpty(field.description) || field.description == displayName)
            {
                description = $"{componentShort}.{displayName}";
            }
            else
            {
                description = $"{componentShort}.{displayName} - {field.description}";
            }
            description = EscapeJson(description);

            // Vector3 - nested object
            if (fieldTypeName == "Vector3")
            {
                return $"        \"{paramName}\": {{\"type\": \"object\", \"description\": \"{description} (Vector3)\", \"properties\": {{\"x\": {{\"type\": \"number\"}}, \"y\": {{\"type\": \"number\"}}, \"z\": {{\"type\": \"number\"}}}}}}";
            }

            // Vector2 - nested object
            if (fieldTypeName == "Vector2")
            {
                return $"        \"{paramName}\": {{\"type\": \"object\", \"description\": \"{description} (Vector2)\", \"properties\": {{\"x\": {{\"type\": \"number\"}}, \"y\": {{\"type\": \"number\"}}}}}}";
            }

            // Color - hex string
            if (fieldTypeName == "Color")
            {
                return $"        \"{paramName}\": {{\"type\": \"string\", \"description\": \"{description} - hex #RRGGBB\"}}";
            }

            // Enum - string with constraint
            if (field.enumValues != null && field.enumValues.Length > 0)
            {
                string enumList = "\"" + string.Join("\", \"", field.enumValues) + "\"";
                return $"        \"{paramName}\": {{\"type\": \"string\", \"enum\": [{enumList}], \"description\": \"{description}\"}}";
            }

            // Primitives
            string jsonType = GetJsonType(fieldTypeName);
            return $"        \"{paramName}\": {{\"type\": \"{jsonType}\", \"description\": \"{description}\"}}";
        }

        /// <summary>
        /// Maps C# type name to JSON Schema type.
        /// </summary>
        private static string GetJsonType(string fieldTypeName)
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
                default:
                    Debug.LogWarning($"[AI Assistant] Unknown field type '{fieldTypeName}', defaulting to string");
                    return "string";
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
