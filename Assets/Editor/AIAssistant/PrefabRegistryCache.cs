using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Caches PrefabRegistry.json in memory with repopulated FieldInfo for performance.
    /// Avoids repeated JSON parsing and reflection lookups during tool generation and instantiation.
    /// </summary>
    public static class PrefabRegistryCache
    {
        private static PrefabRegistry _cachedRegistry;
        private const string EXPECTED_VERSION = "2.1";

        /// <summary>
        /// Loads PrefabRegistry.json from disk, caches it, and repopulates FieldInfo references.
        /// Returns null if file doesn't exist or version mismatch.
        /// </summary>
        public static PrefabRegistry Load()
        {
            // Return cached version if already loaded
            if (_cachedRegistry != null)
            {
                return _cachedRegistry;
            }

            string registryPath = Path.Combine(ProjectIndexer.PROJECT_ARTIFACTS, "PrefabRegistry.json");

            // Check if registry exists
            if (!File.Exists(registryPath))
            {
                Debug.LogWarning($"[AI Assistant] PrefabRegistry.json not found at {registryPath}. Run 'Scan Prefabs' first.");
                return null;
            }

            try
            {
                // Load and parse JSON
                string json = File.ReadAllText(registryPath);
                PrefabRegistry registry = JsonUtility.FromJson<PrefabRegistry>(json);

                // Validate version
                if (registry.version != EXPECTED_VERSION)
                {
                    Debug.LogError($"[AI Assistant] PrefabRegistry version mismatch. Expected {EXPECTED_VERSION}, got {registry.version}. Re-scan prefabs.");
                    return null;
                }

                // Repopulate FieldInfo references (were not serialized)
                RepopulateFieldInfo(registry);

                // Cache and return
                _cachedRegistry = registry;
                Debug.Log($"[AI Assistant] Loaded PrefabRegistry with {registry.prefabs.Length} prefab(s)");
                return registry;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to load PrefabRegistry: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Invalidates the cache, forcing next Load() to re-read from disk.
        /// Call this after scanning prefabs.
        /// </summary>
        public static void Invalidate()
        {
            _cachedRegistry = null;
            Debug.Log("[AI Assistant] PrefabRegistry cache invalidated");
        }

        /// <summary>
        /// Repopulates cachedFieldInfo for all fields via reflection.
        /// This is necessary because FieldInfo is marked [NonSerialized] and can't be stored in JSON.
        /// </summary>
        private static void RepopulateFieldInfo(PrefabRegistry registry)
        {
            foreach (var prefabMeta in registry.prefabs)
            {
                foreach (var componentMeta in prefabMeta.components)
                {
                    // Get component type
                    Type componentType = Type.GetType(componentMeta.componentTypeName);
                    if (componentType == null)
                    {
                        Debug.LogWarning($"[AI Assistant] Failed to find type: {componentMeta.componentTypeName}. Script may have been deleted.");
                        continue;
                    }

                    // Repopulate FieldInfo for each field
                    foreach (var fieldMeta in componentMeta.fields)
                    {
                        FieldInfo fieldInfo = componentType.GetField(
                            fieldMeta.fieldName,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                        );

                        if (fieldInfo == null)
                        {
                            Debug.LogWarning($"[AI Assistant] Failed to find field: {componentMeta.componentTypeName}.{fieldMeta.fieldName}. Field may have been renamed or removed.");
                            continue;
                        }

                        // Cache the FieldInfo
                        fieldMeta.cachedFieldInfo = fieldInfo;
                    }
                }
            }
        }

        /// <summary>
        /// Finds a prefab by its unique function name (e.g., "createVehiclesRaceCar").
        /// Returns null if not found.
        /// </summary>
        public static PrefabMetadata FindByFunctionName(string functionName)
        {
            var registry = Load();
            if (registry == null || registry.prefabs == null)
            {
                return null;
            }

            return registry.prefabs.FirstOrDefault(p => p.uniqueFunctionName == functionName);
        }

        /// <summary>
        /// Finds a prefab by its AssetDatabase path.
        /// Returns null if not found.
        /// </summary>
        public static PrefabMetadata FindByPath(string path)
        {
            var registry = Load();
            if (registry == null || registry.prefabs == null)
            {
                return null;
            }

            return registry.prefabs.FirstOrDefault(p => p.prefabPath == path);
        }

        /// <summary>
        /// Gets all prefabs matching any of the specified tags.
        /// Used by DynamicToolGenerator to filter tools based on selected categories.
        /// </summary>
        /// <param name="tags">List of Unity tags to filter by</param>
        /// <returns>List of matching prefabs</returns>
        public static List<PrefabMetadata> GetByTags(List<string> tags)
        {
            var registry = Load();
            if (registry == null || registry.prefabs == null)
            {
                return new List<PrefabMetadata>();
            }

            // If no tags specified, return all prefabs
            if (tags == null || tags.Count == 0)
            {
                return new List<PrefabMetadata>(registry.prefabs);
            }

            // Filter by tags with null/empty safety
            return registry.prefabs
                .Where(p => !string.IsNullOrEmpty(p.prefabTag) && tags.Contains(p.prefabTag))
                .ToList();
        }

        /// <summary>
        /// Gets all unique tags from the registry.
        /// Used by PrefabCategoryWindow to populate tag selection UI.
        /// </summary>
        /// <returns>Sorted list of unique tags</returns>
        public static List<string> GetAllTags()
        {
            var registry = Load();
            if (registry == null || registry.prefabs == null)
            {
                return new List<string>();
            }

            // Extract unique tags with null/empty safety and sort
            return registry.prefabs
                .Select(p => p.prefabTag)
                .Where(tag => !string.IsNullOrEmpty(tag))
                .Distinct()
                .OrderBy(tag => tag)
                .ToList();
        }
    }
}
