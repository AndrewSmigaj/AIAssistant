using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// Generates OpenAI function schemas for batch object creation.
    /// Returns 6 total tools: instantiateObjects, modifyGameObject, deleteGameObject, addComponent, removeComponent, savePromptFile.
    /// Prefab catalog is provided separately in context, not as individual tools.
    /// </summary>
    public static class DynamicToolGenerator
    {
        /// <summary>
        /// Generates tools JSON array - 6 tools for all operations including Example Mode.
        /// NOTE: Prefab catalog is provided in context text, not as tools.
        /// </summary>
        /// <param name="selectedTags">Not used - kept for API compatibility</param>
        /// <returns>JSON array string of 6 tool definitions</returns>
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

                // 6. Save prompt file tool (Example Mode)
                sb.Append(",\n");
                sb.Append(GenerateSavePromptFileTool());

                sb.Append("\n]");

                Debug.Log("[AI Assistant] Generated 6 tools (batch architecture + Example Mode)");
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
                ""description"": ""Rotation as quaternion [x, y, z, w]. Use two-vector alignment algorithm to calculate. Optional, defaults to identity (0,0,0,1)"",
                ""properties"": {
                  ""x"": {""type"": ""number"", ""description"": ""Quaternion x component"", ""default"": 0},
                  ""y"": {""type"": ""number"", ""description"": ""Quaternion y component"", ""default"": 0},
                  ""z"": {""type"": ""number"", ""description"": ""Quaternion z component"", ""default"": 0},
                  ""w"": {""type"": ""number"", ""description"": ""Quaternion w component"", ""default"": 1}
                },
                ""required"": [""x"", ""y"", ""z"", ""w""]
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
    ""description"": ""Modify one or more GameObjects' transform or component properties in a single batch operation"",
    ""parameters"": {
      ""type"": ""object"",
      ""properties"": {
        ""modifications"": {
          ""type"": ""array"",
          ""description"": ""Array of modifications to apply. Each modification targets one GameObject."",
          ""items"": {
            ""type"": ""object"",
            ""properties"": {
              ""instanceId"": {""type"": ""number"", ""description"": ""GameObject instance ID from scene context or creation output""},
              ""name"": {""type"": ""string"", ""description"": ""New name (optional)""},
              ""position"": {""type"": ""object"", ""properties"": {""x"": {""type"": ""number""}, ""y"": {""type"": ""number""}, ""z"": {""type"": ""number""}}, ""description"": ""New position (optional)""},
              ""rotation"": {""type"": ""object"", ""properties"": {""x"": {""type"": ""number""}, ""y"": {""type"": ""number""}, ""z"": {""type"": ""number""}, ""w"": {""type"": ""number""}}, ""description"": ""New rotation as quaternion [x, y, z, w] (optional)""},
              ""scale"": {""type"": ""object"", ""properties"": {""x"": {""type"": ""number""}, ""y"": {""type"": ""number""}, ""z"": {""type"": ""number""}}, ""description"": ""New scale (optional)""},
              ""active"": {""type"": ""boolean"", ""description"": ""Set active state (optional)""},
              ""parameters"": {""type"": ""object"", ""description"": ""Component field modifications (optional). Use 'ComponentType_fieldName' as keys, e.g., {'Rigidbody_mass': 10, 'MeshRenderer_enabled': true}""}
            },
            ""required"": [""instanceId""]
          }
        }
      },
      ""required"": [""modifications""]
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
    ""description"": ""Delete one or more GameObjects from the scene"",
    ""parameters"": {
      ""type"": ""object"",
      ""properties"": {
        ""instanceIds"": {
          ""type"": ""array"",
          ""description"": ""Array of GameObject instance IDs to delete"",
          ""items"": {""type"": ""number""}
        }
      },
      ""required"": [""instanceIds""]
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
    ""description"": ""Add Unity components to one or more GameObjects in a single batch operation"",
    ""parameters"": {
      ""type"": ""object"",
      ""properties"": {
        ""components"": {
          ""type"": ""array"",
          ""description"": ""Array of components to add. Each specifies a GameObject and component type."",
          ""items"": {
            ""type"": ""object"",
            ""properties"": {
              ""instanceId"": {""type"": ""number"", ""description"": ""GameObject instance ID from scene context or creation output""},
              ""componentType"": {""type"": ""string"", ""description"": ""Component type name (e.g., 'Rigidbody', 'BoxCollider', 'Light', 'AudioSource')""},
              ""parameters"": {""type"": ""object"", ""description"": ""Initial component field values (optional). Use field names as keys, e.g., {'mass': 10, 'drag': 0.5} for Rigidbody""}
            },
            ""required"": [""instanceId"", ""componentType""]
          }
        }
      },
      ""required"": [""components""]
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
    ""description"": ""Remove Unity components from one or more GameObjects in a single batch operation"",
    ""parameters"": {
      ""type"": ""object"",
      ""properties"": {
        ""components"": {
          ""type"": ""array"",
          ""description"": ""Array of components to remove. Each specifies a GameObject and component type."",
          ""items"": {
            ""type"": ""object"",
            ""properties"": {
              ""instanceId"": {""type"": ""number"", ""description"": ""GameObject instance ID from scene context or creation output""},
              ""componentType"": {""type"": ""string"", ""description"": ""Component type name to remove (e.g., 'Rigidbody', 'BoxCollider', 'Light')""}
            },
            ""required"": [""instanceId"", ""componentType""]
          }
        }
      },
      ""required"": [""components""]
    }
  }";
        }

        /// <summary>
        /// Generates savePromptFile tool definition for Example Mode.
        /// Allows AI to save or update prompt library teaching examples.
        /// </summary>
        private static string GenerateSavePromptFileTool()
        {
            return @"  {
    ""type"": ""function"",
    ""name"": ""savePromptFile"",
    ""description"": ""Save or update a prompt library teaching example. Use this in Example Mode to create new examples or fix incorrect existing examples with complete SLS Steps 0-9b."",
    ""parameters"": {
      ""type"": ""object"",
      ""properties"": {
        ""relativePath"": {
          ""type"": ""string"",
          ""description"": ""Relative path in PromptLibrary (e.g., 'Examples/Furniture/lamp_on_table.txt')""
        },
        ""content"": {
          ""type"": ""string"",
          ""description"": ""Complete file content with proper SLS Steps 0-9b format""
        }
      },
      ""required"": [""relativePath"", ""content""]
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
