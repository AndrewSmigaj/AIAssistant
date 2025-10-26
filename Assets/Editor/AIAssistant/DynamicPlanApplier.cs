using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using SimpleJSON;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Executes all action types including dynamic prefab instantiation.
    /// Uses metadata-driven parameter application with cached FieldInfo for performance.
    /// Supports Phase 1 actions (rectangle/circle) and Phase 2 (dynamic prefabs).
    /// </summary>
    public static class DynamicPlanApplier
    {
        /// <summary>
        /// Applies a list of approved actions with single Undo group.
        /// Supports both Phase 1 actions (rectangle/circle) and Phase 2 (dynamic prefabs).
        /// Returns per-action results for partial failure tracking.
        /// </summary>
        /// <param name="actions">List of actions to execute</param>
        /// <param name="previewMode">If true, log actions but don't create GameObjects</param>
        /// <returns>List of ActionResult with success/failure per action</returns>
        public static List<ActionResult> ApplyPlan(List<IAction> actions, bool previewMode)
        {
            var results = new List<ActionResult>();

            // Handle null or empty input
            if (actions == null || actions.Count == 0)
            {
                Debug.Log("[AI Assistant] DynamicPlanApplier called with null or empty actions list");
                return results;
            }

            Debug.Log($"[AI Assistant] DynamicPlanApplier executing {actions.Count} action(s), previewMode={previewMode}");

            // Setup single Undo group for all actions
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            // Execute each action with try-catch for partial failure support
            foreach (var action in actions)
            {
                ActionResult result;

                try
                {
                    Debug.Log($"[AI Assistant] Processing action: {action.GetDescription()}");

                    // Phase 1 actions - call PlanApplier methods directly (no nested undo groups)
                    if (action is CreateRectangleAction rectangleAction)
                    {
                        result = PlanApplier.CreateRectangleGameObject(rectangleAction, previewMode);
                    }
                    else if (action is CreateCircleAction circleAction)
                    {
                        result = PlanApplier.CreateCircleGameObject(circleAction, previewMode);
                    }
                    // Phase 2 action - dynamic prefab instantiation
                    else if (action is InstantiatePrefabAction prefabAction)
                    {
                        result = InstantiatePrefabGameObject(prefabAction, previewMode);
                    }
                    else
                    {
                        result = new ActionResult
                        {
                            Action = action,
                            Success = false,
                            ErrorMessage = $"Unknown action type: {action.GetType().Name}"
                        };
                    }
                }
                catch (Exception ex)
                {
                    // Catch unexpected exceptions to prevent entire batch from failing
                    result = new ActionResult
                    {
                        Action = action,
                        Success = false,
                        ErrorMessage = $"Unexpected error: {ex.Message}"
                    };
                    Debug.LogWarning($"[AI Assistant] Failed to execute {action.GetDescription()}: {ex.Message}");
                }

                results.Add(result);
            }

            // Collapse all operations into single undo step
            Undo.CollapseUndoOperations(undoGroup);

            return results;
        }

        /// <summary>
        /// Instantiates a prefab and applies custom parameter values using cached metadata.
        /// </summary>
        /// <param name="action">Prefab instantiation parameters</param>
        /// <param name="previewMode">If true, log but don't create</param>
        /// <returns>ActionResult with success status and created object</returns>
        private static ActionResult InstantiatePrefabGameObject(InstantiatePrefabAction action, bool previewMode)
        {
            // Validate prefab path
            if (string.IsNullOrEmpty(action.prefabPath))
            {
                return new ActionResult
                {
                    Action = action,
                    Success = false,
                    ErrorMessage = "Prefab path is null or empty"
                };
            }

            // Load prefab from AssetDatabase
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(action.prefabPath);
            if (prefabAsset == null)
            {
                return new ActionResult
                {
                    Action = action,
                    Success = false,
                    ErrorMessage = $"Failed to load prefab at path: {action.prefabPath}"
                };
            }

            // Preview mode: log but don't create
            if (previewMode)
            {
                string prefabName = System.IO.Path.GetFileNameWithoutExtension(action.prefabPath);
                int paramCount = action.parameters != null ? action.parameters.Count : 0;
                Debug.Log($"[Preview] Would instantiate prefab '{prefabName}' named '{action.name}' at position ({action.position.x}, {action.position.y}, {action.position.z}), " +
                         $"rotation ({action.rotation.x}, {action.rotation.y}, {action.rotation.z}), " +
                         $"scale ({action.scale.x}, {action.scale.y}, {action.scale.z}) with {paramCount} parameter(s)");
                return new ActionResult
                {
                    Action = action,
                    Success = true,
                    CreatedObject = null
                };
            }

            // Instantiate prefab
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            if (instance == null)
            {
                return new ActionResult
                {
                    Action = action,
                    Success = false,
                    ErrorMessage = "PrefabUtility.InstantiatePrefab returned null"
                };
            }

            // Set name
            instance.name = action.name;

            // Set position
            instance.transform.position = action.position;

            // Set rotation (convert Euler angles to Quaternion)
            instance.transform.rotation = Quaternion.Euler(action.rotation);

            // Set scale
            instance.transform.localScale = action.scale;

            // Apply custom parameters (if any)
            if (action.parameters != null && action.parameters.Count > 0)
            {
                // Load metadata for cached FieldInfo lookup
                PrefabMetadata metadata = PrefabRegistryCache.FindByPath(action.prefabPath);
                if (metadata == null)
                {
                    Debug.LogWarning($"[AI Assistant] No metadata found for '{action.prefabPath}' - parameters will be skipped");
                }
                else
                {
                    ApplyParameters(instance, action.parameters, metadata);
                }
            }

            // Register with Undo system
            Undo.RegisterCreatedObjectUndo(instance, "AI Assistant Actions");

            Debug.Log($"[AI Assistant] Successfully instantiated prefab: {instance.name} at position {instance.transform.position}");

            return new ActionResult
            {
                Action = action,
                Success = true,
                CreatedObject = instance
            };
        }

        /// <summary>
        /// Applies all parameters to instantiated GameObject using cached FieldInfo from metadata.
        /// Uses try-catch per parameter for partial success support.
        /// </summary>
        /// <param name="instance">Instantiated GameObject to modify</param>
        /// <param name="parameters">Parameter dictionary (parameterName -> value)</param>
        /// <param name="metadata">Prefab metadata with cached FieldInfo</param>
        private static void ApplyParameters(GameObject instance, Dictionary<string, object> parameters, PrefabMetadata metadata)
        {
            foreach (var kvp in parameters)
            {
                string paramName = kvp.Key;  // e.g., "CarController_maxSpeed"
                object value = kvp.Value;     // JSONNode or native type

                try
                {
                    // Find FieldMetadata by parameter name
                    FieldMetadata fieldMeta = FindFieldByParameterName(metadata, paramName);
                    if (fieldMeta == null)
                    {
                        Debug.LogWarning($"[AI Assistant] Parameter '{paramName}' not found in metadata");
                        continue;
                    }

                    // Get component type from metadata
                    Type componentType = Type.GetType(fieldMeta.componentTypeName);
                    if (componentType == null)
                    {
                        Debug.LogWarning($"[AI Assistant] Component type '{fieldMeta.componentTypeName}' not found");
                        continue;
                    }

                    // Get component instance on GameObject
                    Component component = instance.GetComponent(componentType);
                    if (component == null)
                    {
                        Debug.LogWarning($"[AI Assistant] Component '{componentType.Name}' not found on {instance.name}");
                        continue;
                    }

                    // Get cached FieldInfo (already populated by PrefabRegistryCache.RepopulateFieldInfo)
                    FieldInfo fieldInfo = fieldMeta.cachedFieldInfo;
                    if (fieldInfo == null)
                    {
                        Debug.LogWarning($"[AI Assistant] FieldInfo not cached for '{paramName}' - was metadata repopulated?");
                        continue;
                    }

                    // Convert value to target field type
                    object convertedValue = ConvertValue(value, fieldInfo.FieldType);

                    // Apply value via reflection
                    fieldInfo.SetValue(component, convertedValue);

                    Debug.Log($"[AI Assistant] Applied {paramName} = {convertedValue}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AI Assistant] Failed to apply parameter '{paramName}': {ex.Message}");
                    // Continue with other parameters (partial success)
                }
            }
        }

        /// <summary>
        /// Finds FieldMetadata by parameter name (e.g., "CarController_maxSpeed").
        /// Searches all components in metadata.
        /// </summary>
        /// <param name="metadata">Prefab metadata to search</param>
        /// <param name="paramName">Namespaced parameter name</param>
        /// <returns>FieldMetadata or null if not found</returns>
        private static FieldMetadata FindFieldByParameterName(PrefabMetadata metadata, string paramName)
        {
            if (metadata == null || metadata.components == null)
            {
                return null;
            }

            foreach (var component in metadata.components)
            {
                if (component == null || component.fields == null)
                {
                    continue;
                }

                foreach (var field in component.fields)
                {
                    if (field.parameterName == paramName)
                    {
                        return field;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Converts a value to target type with support for Vector3, Vector2, Color, enums, and primitives.
        /// Handles JSONNode extraction before conversion.
        /// </summary>
        /// <param name="value">Value to convert (JSONNode or native type)</param>
        /// <param name="targetType">Target field type</param>
        /// <returns>Converted value ready for SetValue()</returns>
        private static object ConvertValue(object value, Type targetType)
        {
            // Null check
            if (value == null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            // Already correct type
            if (targetType.IsAssignableFrom(value.GetType()))
            {
                return value;
            }

            // Handle JSONNode extraction
            if (value is JSONNode node)
            {
                // Vector3 - parse from JSON object {x, y, z}
                if (targetType == typeof(Vector3))
                {
                    if (node.IsObject)
                    {
                        float x = node["x"].AsFloat;
                        float y = node["y"].AsFloat;
                        float z = node["z"].AsFloat;
                        return new Vector3(x, y, z);
                    }
                    else
                    {
                        throw new ArgumentException("Vector3 value must be a JSON object with x, y, z properties");
                    }
                }

                // Vector2 - parse from JSON object {x, y}
                if (targetType == typeof(Vector2))
                {
                    if (node.IsObject)
                    {
                        float x = node["x"].AsFloat;
                        float y = node["y"].AsFloat;
                        return new Vector2(x, y);
                    }
                    else
                    {
                        throw new ArgumentException("Vector2 value must be a JSON object with x, y properties");
                    }
                }

                // For other types, extract native value from JSONNode BEFORE conversion
                if (node.IsNumber)
                    value = node.AsDouble;
                else if (node.IsString)
                    value = node.Value;
                else if (node.IsBoolean)
                    value = node.AsBool;
            }

            // Color - parse from hex string
            if (targetType == typeof(Color))
            {
                if (value is string hexString)
                {
                    if (ColorUtility.TryParseHtmlString(hexString, out Color color))
                    {
                        return color;
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid color format: {hexString}");
                    }
                }
                else
                {
                    throw new ArgumentException("Color value must be a hex string (e.g., #FF0000)");
                }
            }

            // Enum - parse from string
            if (targetType.IsEnum)
            {
                if (value is string enumString)
                {
                    return Enum.Parse(targetType, enumString, true);
                }
                else
                {
                    throw new ArgumentException($"Enum value must be a string");
                }
            }

            // Primitives - use Convert.ChangeType
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to convert value '{value}' to type '{targetType.Name}': {ex.Message}");
            }
        }
    }
}
