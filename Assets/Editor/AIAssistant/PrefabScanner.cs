using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Scans project prefabs and extracts metadata via reflection.
    /// Generates PrefabRegistry.json with component/field information for dynamic tool generation.
    /// </summary>
    public static class PrefabScanner
    {
        /// <summary>
        /// Scans all prefabs in the configured scan folder and generates PrefabRegistry.json
        /// </summary>
        public static void ScanAll()
        {
            try
            {
                // Get scan folder from settings
                var settings = AIAssistantSettings.GetOrCreateSettings();
                string scanFolder = settings.PrefabScanFolder;

                if (!AssetDatabase.IsValidFolder(scanFolder))
                {
                    Debug.LogError($"[AI Assistant] Scan folder does not exist: {scanFolder}. Create it or update settings.");
                    return;
                }

                // Find all prefabs in scan folder
                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { scanFolder });
                Debug.Log($"[AI Assistant] Found {prefabGuids.Length} prefab(s) in {scanFolder}");

                // Counter for unique name generation
                Dictionary<string, int> nameCounters = new Dictionary<string, int>();
                List<PrefabMetadata> prefabs = new List<PrefabMetadata>();

                // Scan each prefab
                foreach (string guid in prefabGuids)
                {
                    try
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        PrefabMetadata metadata = ScanPrefab(path, nameCounters);
                        if (metadata != null)
                        {
                            prefabs.Add(metadata);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[AI Assistant] Failed to scan prefab: {ex.Message}");
                    }
                }

                // Create registry and save
                PrefabRegistry registry = new PrefabRegistry
                {
                    version = "2.1",
                    prefabs = prefabs.ToArray()
                };

                // Ensure artifacts directory exists
                if (!Directory.Exists(ProjectIndexer.PROJECT_ARTIFACTS))
                {
                    Directory.CreateDirectory(ProjectIndexer.PROJECT_ARTIFACTS);
                }

                string outputPath = Path.Combine(ProjectIndexer.PROJECT_ARTIFACTS, "PrefabRegistry.json");
                string json = JsonUtility.ToJson(registry, true);
                File.WriteAllText(outputPath, json);

                Debug.Log($"[AI Assistant] Successfully scanned {prefabs.Count} prefab(s) to {outputPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to scan prefabs: {ex.Message}");
            }
        }

        /// <summary>
        /// Scans a single prefab and extracts metadata
        /// </summary>
        private static PrefabMetadata ScanPrefab(string path, Dictionary<string, int> nameCounters)
        {
            // Load prefab
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[AI Assistant] Failed to load prefab: {path}");
                return null;
            }

            // Get category: Unity tag > folder name > "Default"
            string category = GetPrefabCategory(prefab, path);
            if (string.IsNullOrEmpty(category))
            {
                Debug.LogWarning($"[AI Assistant] Skipping {prefab.name} - no category determined");
                return null;
            }

            // Get all MonoBehaviour components
            var components = prefab.GetComponents<MonoBehaviour>();
            if (components.Length == 0)
            {
                Debug.LogWarning($"[AI Assistant] Skipping {prefab.name} - no MonoBehaviours found");
                return null;
            }

            // Scan components
            List<ComponentMetadata> componentMetas = new List<ComponentMetadata>();
            foreach (var component in components)
            {
                if (component == null)
                {
                    Debug.LogWarning($"[AI Assistant] Skipping missing script on {prefab.name}");
                    continue;  // Skip missing scripts
                }

                ComponentMetadata meta = ScanComponent(component);
                if (meta != null && meta.fields.Length > 0)
                {
                    componentMetas.Add(meta);
                }
            }

            // Skip prefabs with no serialized fields
            if (componentMetas.Count == 0)
            {
                Debug.Log($"[AI Assistant] Skipping {prefab.name} - no serialized fields found");
                return null;
            }

            // Generate unique function name
            string sanitizedName = SanitizeName(prefab.name);
            string uniqueName = GenerateUniqueFunctionName(sanitizedName, category, nameCounters);

            return new PrefabMetadata
            {
                prefabName = prefab.name,
                prefabPath = path,
                prefabTag = category,
                uniqueFunctionName = uniqueName,
                components = componentMetas.ToArray()
            };
        }

        /// <summary>
        /// Scans a MonoBehaviour component and extracts field metadata
        /// </summary>
        private static ComponentMetadata ScanComponent(MonoBehaviour component)
        {
            Type type = component.GetType();

            List<FieldMetadata> fieldMetas = new List<FieldMetadata>();

            // Scan regular fields
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (IsSerializedField(field))
                {
                    FieldMetadata meta = CreateFieldMetadata(field, type);
                    if (meta != null)
                    {
                        fieldMetas.Add(meta);
                    }
                }
            }

            // Scan Unity 6 properties with [field: SerializeField]
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                // Look for compiler-generated backing field
                string backingFieldName = $"<{prop.Name}>k__BackingField";
                FieldInfo backingField = type.GetField(backingFieldName, BindingFlags.NonPublic | BindingFlags.Instance);

                if (backingField != null && backingField.GetCustomAttribute<SerializeField>() != null)
                {
                    // Create metadata using backing field, but expose as property name
                    FieldMetadata meta = CreateFieldMetadataFromProperty(prop, backingField, type);
                    if (meta != null)
                    {
                        fieldMetas.Add(meta);
                    }
                }
            }

            return new ComponentMetadata
            {
                componentTypeName = type.FullName,
                componentTypeShortName = type.Name,
                fields = fieldMetas.ToArray()
            };
        }

        /// <summary>
        /// Checks if a field is serialized by Unity
        /// </summary>
        private static bool IsSerializedField(FieldInfo field)
        {
            // Skip static fields
            if (field.IsStatic) return false;

            // Skip fields with [NonSerialized]
            if (field.GetCustomAttribute<NonSerializedAttribute>() != null) return false;

            // Skip fields with [HideInInspector]
            if (field.GetCustomAttribute<HideInInspector>() != null) return false;

            // Public fields are serialized
            if (field.IsPublic) return true;

            // Private fields with [SerializeField] are serialized
            if (field.GetCustomAttribute<SerializeField>() != null) return true;

            return false;
        }

        /// <summary>
        /// Creates FieldMetadata from a regular field
        /// </summary>
        private static FieldMetadata CreateFieldMetadata(FieldInfo field, Type componentType)
        {
            // Skip unsupported types for Phase 2
            Type fieldType = field.FieldType;
            if (!IsSupportedType(fieldType))
            {
                Debug.Log($"[AI Assistant] Skipping unsupported field: {componentType.Name}.{field.Name} ({fieldType.Name})");
                return null;
            }

            // Get tooltip description
            var tooltipAttr = field.GetCustomAttribute<TooltipAttribute>();
            string description = tooltipAttr != null ? tooltipAttr.tooltip : field.Name;

            // Get enum values if applicable
            string[] enumValues = null;
            if (fieldType.IsEnum)
            {
                enumValues = Enum.GetNames(fieldType);
            }

            // Generate namespaced parameter name
            string parameterName = $"{componentType.Name}_{field.Name}";

            return new FieldMetadata
            {
                fieldName = field.Name,
                fieldTypeName = GetTypeName(fieldType),
                componentTypeName = componentType.FullName,
                parameterName = parameterName,
                description = description,
                isRequired = false,  // All optional for now
                enumValues = enumValues
            };
        }

        /// <summary>
        /// Creates FieldMetadata from a Unity 6 property with backing field
        /// </summary>
        private static FieldMetadata CreateFieldMetadataFromProperty(PropertyInfo prop, FieldInfo backingField, Type componentType)
        {
            Type fieldType = prop.PropertyType;
            if (!IsSupportedType(fieldType))
            {
                Debug.Log($"[AI Assistant] Skipping unsupported property: {componentType.Name}.{prop.Name} ({fieldType.Name})");
                return null;
            }

            // Get tooltip from backing field (attribute is on backing field, not property)
            var tooltipAttr = backingField.GetCustomAttribute<TooltipAttribute>();
            string description = tooltipAttr != null ? tooltipAttr.tooltip : prop.Name;

            string[] enumValues = null;
            if (fieldType.IsEnum)
            {
                enumValues = Enum.GetNames(fieldType);
            }

            // Use property name for parameter (user-facing), but store backing field name for SetValue
            string parameterName = $"{componentType.Name}_{prop.Name}";

            return new FieldMetadata
            {
                fieldName = backingField.Name,  // Store backing field name for SetValue
                fieldTypeName = GetTypeName(fieldType),
                componentTypeName = componentType.FullName,
                parameterName = parameterName,
                description = description,
                isRequired = false,
                enumValues = enumValues
            };
        }

        /// <summary>
        /// Sanitizes a prefab name to be a valid C# identifier
        /// </summary>
        private static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Prefab";

            // Remove spaces and special characters, keep only letters and digits
            string sanitized = new string(name.Where(c => char.IsLetterOrDigit(c)).ToArray());

            if (string.IsNullOrEmpty(sanitized)) return "Prefab";

            // Handle leading digits (C# identifiers can't start with digit)
            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }

            return sanitized;
        }

        /// <summary>
        /// Generates a globally unique function name for a prefab
        /// </summary>
        private static string GenerateUniqueFunctionName(string prefabName, string tag, Dictionary<string, int> counters)
        {
            string baseName = $"create{tag}{prefabName}";

            if (!counters.ContainsKey(baseName))
            {
                counters[baseName] = 0;
                return baseName;
            }

            counters[baseName]++;
            return $"{baseName}{counters[baseName]}";
        }

        /// <summary>
        /// Maps C# type to string for JSON storage and reconstruction
        /// </summary>
        private static string GetTypeName(Type type)
        {
            // Primitives - use simple names
            if (type == typeof(int)) return "int";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(long)) return "long";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";

            // Unity types - use simple names
            if (type == typeof(Vector2)) return "Vector2";
            if (type == typeof(Vector3)) return "Vector3";
            if (type == typeof(Color)) return "Color";

            // Enums - use full name for Enum.Parse reconstruction
            if (type.IsEnum) return type.FullName;

            // Others - use full name
            return type.FullName;
        }

        /// <summary>
        /// Checks if a type is supported in Phase 2
        /// </summary>
        private static bool IsSupportedType(Type type)
        {
            // Tier 1: Primitives
            if (type.IsPrimitive || type == typeof(string)) return true;

            // Tier 2: Unity types
            if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Color)) return true;

            // Tier 3: Enums
            if (type.IsEnum) return true;

            // Phase 2: Skip everything else (Quaternion, arrays, asset references, custom classes)
            return false;
        }

        /// <summary>
        /// Gets prefab category using hybrid approach: Unity tag > folder name > "Default".
        /// Enables zero-config workflow - most users don't set tags but organize by folders.
        /// </summary>
        private static string GetPrefabCategory(GameObject prefab, string assetPath)
        {
            // Priority 1: Use Unity tag if explicitly set (power users)
            if (!string.IsNullOrEmpty(prefab.tag) && prefab.tag != "Untagged")
            {
                Debug.Log($"[AI Assistant] Using Unity tag '{prefab.tag}' for {prefab.name}");
                return prefab.tag;
            }

            // Priority 2: Use parent folder name (most common case)
            // Example: "Assets/AIPrefabs/Vehicles/RaceCar.prefab" → "Vehicles"
            string folderName = GetParentFolderName(assetPath);
            if (!string.IsNullOrEmpty(folderName) && folderName != "AIPrefabs")
            {
                Debug.Log($"[AI Assistant] Using folder name '{folderName}' for {prefab.name}");
                return folderName;
            }

            // Priority 3: Default category for root-level prefabs
            Debug.Log($"[AI Assistant] Using 'Default' category for {prefab.name}");
            return "Default";
        }

        /// <summary>
        /// Extracts parent folder name from asset path.
        /// Example: "Assets/AIPrefabs/Vehicles/RaceCar.prefab" → "Vehicles"
        /// </summary>
        private static string GetParentFolderName(string assetPath)
        {
            try
            {
                // Get directory of the prefab
                string directory = Path.GetDirectoryName(assetPath);

                // Get folder name (last segment of path)
                if (!string.IsNullOrEmpty(directory))
                {
                    // Convert backslashes to forward slashes for consistency
                    directory = directory.Replace('\\', '/');

                    // Get last folder name
                    string folderName = Path.GetFileName(directory);
                    return folderName;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AI Assistant] Failed to get folder name from {assetPath}: {ex.Message}");
            }

            return null;
        }
    }
}
