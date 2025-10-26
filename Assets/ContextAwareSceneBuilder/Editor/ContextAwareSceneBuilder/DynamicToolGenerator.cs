using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// Generates OpenAI function schemas dynamically from PrefabRegistry.
    /// Converts PrefabMetadata into JSON tool definitions for GPT-5 function calling.
    /// </summary>
    public static class DynamicToolGenerator
    {
        /// <summary>
        /// Generates tools JSON array for selected prefab categories.
        /// Always includes fallback tools (rectangle/circle), then adds prefab tools.
        /// </summary>
        /// <param name="selectedTags">Unity tags to filter prefabs (null = all)</param>
        /// <returns>JSON array string of tool definitions</returns>
        public static string GenerateToolsJson(List<string> selectedTags)
        {
            try
            {
                // Start with fallback tools (rectangle/circle) - always available
                var sb = new StringBuilder();
                sb.Append("[\n");

                // Add fallback tools (without outer brackets)
                string fallbackJson = OpenAIClient.FALLBACK_TOOLS_JSON;
                // Strip outer brackets from fallback JSON
                fallbackJson = fallbackJson.Trim();
                if (fallbackJson.StartsWith("[")) fallbackJson = fallbackJson.Substring(1);
                if (fallbackJson.EndsWith("]")) fallbackJson = fallbackJson.Substring(0, fallbackJson.Length - 1);
                sb.Append(fallbackJson.Trim());

                // Add modification tools (always available)
                sb.Append(",\n");
                sb.Append(GenerateModifyGameObjectTool());
                sb.Append(",\n");
                sb.Append(GenerateDeleteGameObjectTool());

                // Add component management tools (always available)
                sb.Append(",\n");
                sb.Append(GenerateAddComponentTool());
                sb.Append(",\n");
                sb.Append(GenerateRemoveComponentTool());

                // Load registry and add prefab tools if available
                PrefabRegistry registry = PrefabRegistryCache.Load();
                if (registry != null)
                {
                    // Get prefabs by tags
                    List<PrefabMetadata> prefabs = PrefabRegistryCache.GetByTags(selectedTags);
                    if (prefabs != null && prefabs.Count > 0)
                    {
                        // Add comma after fallback tools, then add prefab tools
                        for (int i = 0; i < prefabs.Count; i++)
                        {
                            sb.Append(",\n");
                            string schema = GenerateFunctionSchema(prefabs[i]);
                            sb.Append(schema);
                        }

                        Debug.Log($"[AI Assistant] Generated {prefabs.Count} prefab tool(s) + 2 fallback tools + 4 modification tools");
                    }
                    else
                    {
                        Debug.Log("[AI Assistant] No prefabs found for selected tags, using fallback + modification tools only");
                    }
                }
                else
                {
                    Debug.Log("[AI Assistant] PrefabRegistry not found, using fallback + modification tools only");
                }

                sb.Append("\n]");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to generate tools JSON: {ex.Message}. Using fallback only.");
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
        /// Generates modifyGameObject tool definition.
        /// </summary>
        private static string GenerateModifyGameObjectTool()
        {
            return @"  {
    ""type"": ""function"",
    ""name"": ""modifyGameObject"",
    ""description"": ""Modify a GameObject's transform or component properties"",
    ""parameters"": {
      ""type"": ""object"",
      ""properties"": {
        ""instanceId"": {""type"": ""number"", ""description"": ""GameObject instance ID from scene context or creation output""},
        ""name"": {""type"": ""string"", ""description"": ""New name (optional)""},
        ""position"": {""type"": ""object"", ""properties"": {""x"": {""type"": ""number""}, ""y"": {""type"": ""number""}, ""z"": {""type"": ""number""}}, ""description"": ""New position (optional)""},
        ""rotation"": {""type"": ""object"", ""properties"": {""x"": {""type"": ""number""}, ""y"": {""type"": ""number""}, ""z"": {""type"": ""number""}}, ""description"": ""New rotation in Euler degrees (optional)""},
        ""scale"": {""type"": ""object"", ""properties"": {""x"": {""type"": ""number""}, ""y"": {""type"": ""number""}, ""z"": {""type"": ""number""}}, ""description"": ""New scale (optional)""},
        ""active"": {""type"": ""boolean"", ""description"": ""Set active state (optional)""},
        ""parameters"": {""type"": ""object"", ""description"": ""Component field modifications (optional). Use 'ComponentType_fieldName' as keys, e.g., {'Rigidbody_mass': 10, 'MeshRenderer_enabled': true}""}
      },
      ""required"": [""instanceId""]
    }
  }";
        }

        /// <summary>
        /// Generates deleteGameObject tool definition.
        /// </summary>
        private static string GenerateDeleteGameObjectTool()
        {
            return @"  {
    ""type"": ""function"",
    ""name"": ""deleteGameObject"",
    ""description"": ""Delete a GameObject from the scene"",
    ""parameters"": {
      ""type"": ""object"",
      ""properties"": {
        ""instanceId"": {""type"": ""number"", ""description"": ""GameObject instance ID to delete""}
      },
      ""required"": [""instanceId""]
    }
  }";
        }

        /// <summary>
        /// Generates addComponent tool definition.
        /// </summary>
        private static string GenerateAddComponentTool()
        {
            return @"  {
    ""type"": ""function"",
    ""name"": ""addComponent"",
    ""description"": ""Add a Unity component to an existing GameObject"",
    ""parameters"": {
      ""type"": ""object"",
      ""properties"": {
        ""instanceId"": {""type"": ""number"", ""description"": ""GameObject instance ID from scene context or creation output""},
        ""componentType"": {""type"": ""string"", ""description"": ""Component type name (e.g., 'Rigidbody', 'BoxCollider', 'Light', 'AudioSource')""},
        ""parameters"": {""type"": ""object"", ""description"": ""Initial component field values (optional). Use field names as keys, e.g., {'mass': 10, 'drag': 0.5} for Rigidbody""}
      },
      ""required"": [""instanceId"", ""componentType""]
    }
  }";
        }

        /// <summary>
        /// Generates removeComponent tool definition.
        /// </summary>
        private static string GenerateRemoveComponentTool()
        {
            return @"  {
    ""type"": ""function"",
    ""name"": ""removeComponent"",
    ""description"": ""Remove a Unity component from a GameObject"",
    ""parameters"": {
      ""type"": ""object"",
      ""properties"": {
        ""instanceId"": {""type"": ""number"", ""description"": ""GameObject instance ID from scene context or creation output""},
        ""componentType"": {""type"": ""string"", ""description"": ""Component type name to remove (e.g., 'Rigidbody', 'BoxCollider', 'Light')""}
      },
      ""required"": [""instanceId"", ""componentType""]
    }
  }";
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

            // Scale parameters (optional, preserves prefab default if omitted)
            sb.Append("        \"scaleX\": {\"type\": \"number\", \"description\": \"Scale along X axis (optional, preserves prefab default if omitted)\"},\n");
            sb.Append("        \"scaleY\": {\"type\": \"number\", \"description\": \"Scale along Y axis (optional, preserves prefab default if omitted)\"},\n");
            sb.Append("        \"scaleZ\": {\"type\": \"number\", \"description\": \"Scale along Z axis (optional, preserves prefab default if omitted)\"}");

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
