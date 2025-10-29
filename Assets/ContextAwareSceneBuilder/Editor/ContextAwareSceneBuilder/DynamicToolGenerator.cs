using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// Generates OpenAI function schemas for batch object creation.
    /// Returns 5 total tools: instantiateObjects, modifyGameObject, deleteGameObject, addComponent, removeComponent.
    /// Prefab catalog is provided separately in context, not as individual tools.
    /// </summary>
    public static class DynamicToolGenerator
    {
        /// <summary>
        /// Generates tools JSON array - exactly 5 tools for all operations.
        /// NOTE: Prefab catalog is provided in context text, not as tools.
        /// </summary>
        /// <param name="selectedTags">Not used - kept for API compatibility</param>
        /// <returns>JSON array string of 5 tool definitions</returns>
        public static string GenerateToolsJson(List<string> selectedTags)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("[\n");

                // 1. Batch instantiation tool
                sb.Append(GenerateInstantiateObjectsTool());

                // 2. Modification tool
                sb.Append(",\n");
                sb.Append(GenerateModifyGameObjectTool());

                // 3. Deletion tool
                sb.Append(",\n");
                sb.Append(GenerateDeleteGameObjectTool());

                // 4. Add component tool
                sb.Append(",\n");
                sb.Append(GenerateAddComponentTool());

                // 5. Remove component tool
                sb.Append(",\n");
                sb.Append(GenerateRemoveComponentTool());

                sb.Append("\n]");

                Debug.Log("[AI Assistant] Generated 5 standard tools (batch architecture)");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to generate tools JSON: {ex.Message}");
                // Return minimal toolset on error
                return "[" + GenerateInstantiateObjectsTool() + "]";
            }
        }

        /// <summary>
        /// Generates instantiateObjects batch tool definition.
        /// Accepts an array of objects to create in a single operation.
        /// </summary>
        private static string GenerateInstantiateObjectsTool()
        {
            return @"  {
    ""type"": ""function"",
    ""name"": ""instantiateObjects"",
    ""description"": ""Create multiple game objects from the prefab catalog in a single batch operation. Use prefabPath from the catalog provided in context."",
    ""parameters"": {
      ""type"": ""object"",
      ""properties"": {
        ""objects"": {
          ""type"": ""array"",
          ""description"": ""Array of objects to instantiate. Each object must specify prefabPath from catalog."",
          ""items"": {
            ""type"": ""object"",
            ""properties"": {
              ""prefabPath"": {
                ""type"": ""string"",
                ""description"": ""AssetDatabase path from prefab catalog (e.g., 'Assets/Prefabs/Chair.prefab')""
              },
              ""name"": {
                ""type"": ""string"",
                ""description"": ""GameObject instance name""
              },
              ""position"": {
                ""type"": ""object"",
                ""description"": ""World position (pivot point at LOCAL [0,0,0] - see catalog's 'pivot' semantic point)"",
                ""properties"": {
                  ""x"": {""type"": ""number""},
                  ""y"": {""type"": ""number""},
                  ""z"": {""type"": ""number""}
                },
                ""required"": [""x"", ""y"", ""z""]
              },
              ""rotation"": {
                ""type"": ""object"",
                ""description"": ""Euler angles in degrees. Optional, defaults to (0,0,0)"",
                ""properties"": {
                  ""x"": {""type"": ""number"", ""default"": 0},
                  ""y"": {""type"": ""number"", ""default"": 0},
                  ""z"": {""type"": ""number"", ""default"": 0}
                },
                ""required"": [""x"", ""y"", ""z""]
              },
              ""scale"": {
                ""type"": ""object"",
                ""description"": ""Local scale. Optional - if omitted, preserves prefab's default scale"",
                ""properties"": {
                  ""x"": {""type"": ""number""},
                  ""y"": {""type"": ""number""},
                  ""z"": {""type"": ""number""}
                },
                ""required"": [""x"", ""y"", ""z""]
              },
              ""parameters"": {
                ""type"": ""object"",
                ""description"": ""Component parameter overrides using 'ComponentType_fieldName' format. Example: {'Rigidbody_mass': 10, 'Light_intensity': 2.5}"",
                ""additionalProperties"": true
              }
            },
            ""required"": [""prefabPath"", ""name"", ""position""]
          }
        }
      },
      ""required"": [""objects""]
    }
  }";
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
