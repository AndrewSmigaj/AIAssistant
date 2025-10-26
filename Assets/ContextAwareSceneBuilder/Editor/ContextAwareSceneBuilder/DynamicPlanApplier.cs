using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using SimpleJSON;

namespace ContextAwareSceneBuilder.Editor
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
                    // Modification actions
                    else if (action is ModifyGameObjectAction modifyAction)
                    {
                        result = ExecuteModifyGameObject(modifyAction, previewMode);
                    }
                    else if (action is DeleteGameObjectAction deleteAction)
                    {
                        result = ExecuteDeleteGameObject(deleteAction, previewMode);
                    }
                    // Component management actions
                    else if (action is AddComponentAction addCompAction)
                    {
                        result = ExecuteAddComponent(addCompAction, previewMode);
                    }
                    else if (action is RemoveComponentAction removeCompAction)
                    {
                        result = ExecuteRemoveComponent(removeCompAction, previewMode);
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
                string scaleInfo = action.scale.HasValue
                    ? $"scale ({action.scale.Value.x}, {action.scale.Value.y}, {action.scale.Value.z})"
                    : "scale (prefab default)";
                Debug.Log($"[Preview] Would instantiate prefab '{prefabName}' named '{action.name}' at position ({action.position.x}, {action.position.y}, {action.position.z}), " +
                         $"rotation ({action.rotation.x}, {action.rotation.y}, {action.rotation.z}), " +
                         $"{scaleInfo} with {paramCount} parameter(s)");
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

            // Set scale (only if specified, otherwise preserve prefab's default scale)
            if (action.scale.HasValue)
            {
                instance.transform.localScale = action.scale.Value;
            }

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
                CreatedObject = instance,
                InstanceId = instance.GetInstanceID()
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

        /// <summary>
        /// Modifies an existing GameObject's transform or component properties.
        /// Uses Undo.RecordObject for component modifications to enable proper undo support.
        /// </summary>
        /// <param name="action">Modification parameters</param>
        /// <param name="previewMode">If true, log but don't modify</param>
        /// <returns>ActionResult with success status and instanceId</returns>
        private static ActionResult ExecuteModifyGameObject(ModifyGameObjectAction action, bool previewMode)
        {
            // Convert instanceID to GameObject reference
            GameObject target = EditorUtility.InstanceIDToObject(action.instanceId) as GameObject;
            if (target == null)
            {
                return new ActionResult
                {
                    Action = action,
                    Success = false,
                    ErrorMessage = $"GameObject with instanceId {action.instanceId} not found"
                };
            }

            // Preview mode: log but don't modify
            if (previewMode)
            {
                Debug.Log($"[Preview] Would modify GameObject '{target.name}' (ID: {action.instanceId})");
                return new ActionResult
                {
                    Action = action,
                    Success = true,
                    InstanceId = action.instanceId
                };
            }

            // Apply transform modifications (Undo.RecordObject handled at GameObject level)
            Undo.RecordObject(target.transform, "AI Assistant Actions");

            if (!string.IsNullOrEmpty(action.name))
            {
                target.name = action.name;
            }

            if (action.position.HasValue)
            {
                target.transform.position = action.position.Value;
            }

            if (action.rotation.HasValue)
            {
                target.transform.rotation = Quaternion.Euler(action.rotation.Value);
            }

            if (action.scale.HasValue)
            {
                target.transform.localScale = action.scale.Value;
            }

            if (action.active.HasValue)
            {
                target.SetActive(action.active.Value);
            }

            // Apply component parameter modifications
            if (action.parameters != null && action.parameters.Count > 0)
            {
                foreach (var kvp in action.parameters)
                {
                    string paramKey = kvp.Key;  // Format: "ComponentType_fieldName"
                    object value = kvp.Value;

                    try
                    {
                        // Parse component type and field name
                        int separatorIndex = paramKey.IndexOf('_');
                        if (separatorIndex == -1)
                        {
                            Debug.LogWarning($"[AI Assistant] Invalid parameter key format '{paramKey}' (expected 'ComponentType_fieldName')");
                            continue;
                        }

                        string componentTypeName = paramKey.Substring(0, separatorIndex);
                        string fieldName = paramKey.Substring(separatorIndex + 1);

                        // Find component by type name (search in UnityEngine and user assemblies)
                        Component component = FindComponentByTypeName(target, componentTypeName);
                        if (component == null)
                        {
                            Debug.LogWarning($"[AI Assistant] Component '{componentTypeName}' not found on GameObject '{target.name}'");
                            continue;
                        }

                        // CRITICAL: Record component for Undo before modification
                        Undo.RecordObject(component, "AI Assistant Actions");

                        // Get field via reflection
                        Type componentType = component.GetType();
                        FieldInfo fieldInfo = componentType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (fieldInfo == null)
                        {
                            Debug.LogWarning($"[AI Assistant] Field '{fieldName}' not found on component '{componentTypeName}'");
                            continue;
                        }

                        // Convert value to target type
                        object convertedValue = ConvertValue(value, fieldInfo.FieldType);

                        // Apply value via reflection
                        fieldInfo.SetValue(component, convertedValue);

                        Debug.Log($"[AI Assistant] Modified {target.name}.{componentTypeName}.{fieldName} = {convertedValue}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[AI Assistant] Failed to apply parameter '{paramKey}': {ex.Message}");
                        // Continue with other parameters (partial success)
                    }
                }
            }

            Debug.Log($"[AI Assistant] Successfully modified GameObject: {target.name} (ID: {action.instanceId})");

            return new ActionResult
            {
                Action = action,
                Success = true,
                InstanceId = action.instanceId
            };
        }

        /// <summary>
        /// Deletes a GameObject from the scene using Undo.DestroyObjectImmediate.
        /// </summary>
        /// <param name="action">Delete parameters</param>
        /// <param name="previewMode">If true, log but don't delete</param>
        /// <returns>ActionResult with success status</returns>
        private static ActionResult ExecuteDeleteGameObject(DeleteGameObjectAction action, bool previewMode)
        {
            // Convert instanceID to GameObject reference
            GameObject target = EditorUtility.InstanceIDToObject(action.instanceId) as GameObject;
            if (target == null)
            {
                return new ActionResult
                {
                    Action = action,
                    Success = false,
                    ErrorMessage = $"GameObject with instanceId {action.instanceId} not found"
                };
            }

            string targetName = target.name;  // Cache name before deletion

            // Preview mode: log but don't delete
            if (previewMode)
            {
                Debug.Log($"[Preview] Would delete GameObject '{targetName}' (ID: {action.instanceId})");
                return new ActionResult
                {
                    Action = action,
                    Success = true
                };
            }

            // Delete with Undo support
            Undo.DestroyObjectImmediate(target);

            Debug.Log($"[AI Assistant] Successfully deleted GameObject: {targetName} (ID: {action.instanceId})");

            return new ActionResult
            {
                Action = action,
                Success = true
            };
        }

        /// <summary>
        /// Finds a component on a GameObject by type name (short name like "Rigidbody" or fully qualified).
        /// Searches UnityEngine types first, then all loaded assemblies.
        /// </summary>
        /// <param name="go">GameObject to search</param>
        /// <param name="typeName">Component type name (e.g., "Rigidbody", "CarController")</param>
        /// <returns>Component instance or null if not found</returns>
        private static Component FindComponentByTypeName(GameObject go, string typeName)
        {
            // Try UnityEngine namespace first (most common)
            Type type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null)
            {
                return go.GetComponent(type);
            }

            // Try exact type name (might be fully qualified)
            type = Type.GetType(typeName);
            if (type != null)
            {
                return go.GetComponent(type);
            }

            // Search all components on GameObject and match by short name
            Component[] components = go.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component.GetType().Name == typeName)
                {
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// Adds a component to an existing GameObject with optional initial parameter values.
        /// Uses Undo.AddComponent for proper undo support.
        /// </summary>
        /// <param name="action">Add component parameters</param>
        /// <param name="previewMode">If true, log but don't add</param>
        /// <returns>ActionResult with success status and instanceId</returns>
        private static ActionResult ExecuteAddComponent(AddComponentAction action, bool previewMode)
        {
            // Convert instanceID to GameObject reference
            GameObject target = EditorUtility.InstanceIDToObject(action.instanceId) as GameObject;
            if (target == null)
            {
                return new ActionResult
                {
                    Action = action,
                    Success = false,
                    ErrorMessage = $"GameObject with instanceId {action.instanceId} not found"
                };
            }

            // Resolve component type
            Type componentType = ResolveComponentType(action.componentType);
            if (componentType == null)
            {
                return new ActionResult
                {
                    Action = action,
                    Success = false,
                    ErrorMessage = $"Component type '{action.componentType}' not found"
                };
            }

            // Preview mode: log but don't add
            if (previewMode)
            {
                Debug.Log($"[Preview] Would add {action.componentType} to GameObject '{target.name}' (ID: {action.instanceId})");
                return new ActionResult
                {
                    Action = action,
                    Success = true,
                    InstanceId = action.instanceId
                };
            }

            // Add component with Undo support (Unity handles duplicate check)
            Component newComponent = Undo.AddComponent(target, componentType);
            if (newComponent == null)
            {
                return new ActionResult
                {
                    Action = action,
                    Success = false,
                    ErrorMessage = $"Failed to add component '{action.componentType}'"
                };
            }

            // Apply initial parameter values if provided
            if (action.parameters != null && action.parameters.Count > 0)
            {
                // Record component for undo before modifying parameters
                Undo.RecordObject(newComponent, "AI Assistant Actions");

                foreach (var kvp in action.parameters)
                {
                    string fieldName = kvp.Key;
                    object value = kvp.Value;

                    try
                    {
                        // Get field via reflection
                        FieldInfo fieldInfo = componentType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (fieldInfo == null)
                        {
                            Debug.LogWarning($"[AI Assistant] Field '{fieldName}' not found on component '{action.componentType}'");
                            continue;
                        }

                        // Convert value to target type
                        object convertedValue = ConvertValue(value, fieldInfo.FieldType);

                        // Apply value via reflection
                        fieldInfo.SetValue(newComponent, convertedValue);

                        Debug.Log($"[AI Assistant] Set {target.name}.{action.componentType}.{fieldName} = {convertedValue}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[AI Assistant] Failed to set parameter '{fieldName}': {ex.Message}");
                        // Continue with other parameters (partial success)
                    }
                }
            }

            Debug.Log($"[AI Assistant] Successfully added {action.componentType} to GameObject: {target.name} (ID: {action.instanceId})");

            return new ActionResult
            {
                Action = action,
                Success = true,
                InstanceId = action.instanceId
            };
        }

        /// <summary>
        /// Removes a component from a GameObject.
        /// Uses Undo.DestroyObjectImmediate for proper undo support.
        /// </summary>
        /// <param name="action">Remove component parameters</param>
        /// <param name="previewMode">If true, log but don't remove</param>
        /// <returns>ActionResult with success status</returns>
        private static ActionResult ExecuteRemoveComponent(RemoveComponentAction action, bool previewMode)
        {
            // Convert instanceID to GameObject reference
            GameObject target = EditorUtility.InstanceIDToObject(action.instanceId) as GameObject;
            if (target == null)
            {
                return new ActionResult
                {
                    Action = action,
                    Success = false,
                    ErrorMessage = $"GameObject with instanceId {action.instanceId} not found"
                };
            }

            // Find component by type name
            Component component = FindComponentByTypeName(target, action.componentType);
            if (component == null)
            {
                return new ActionResult
                {
                    Action = action,
                    Success = false,
                    ErrorMessage = $"Component '{action.componentType}' not found on GameObject '{target.name}'"
                };
            }

            // Prevent removing Transform (Unity requirement)
            if (component is Transform)
            {
                return new ActionResult
                {
                    Action = action,
                    Success = false,
                    ErrorMessage = "Cannot remove Transform component"
                };
            }

            // Preview mode: log but don't remove
            if (previewMode)
            {
                Debug.Log($"[Preview] Would remove {action.componentType} from GameObject '{target.name}' (ID: {action.instanceId})");
                return new ActionResult
                {
                    Action = action,
                    Success = true,
                    InstanceId = action.instanceId
                };
            }

            // Remove component with Undo support
            Undo.DestroyObjectImmediate(component);

            Debug.Log($"[AI Assistant] Successfully removed {action.componentType} from GameObject: {target.name} (ID: {action.instanceId})");

            return new ActionResult
            {
                Action = action,
                Success = true,
                InstanceId = action.instanceId
            };
        }

        /// <summary>
        /// Resolves a component type name to a Type object.
        /// Tries UnityEngine namespace first, then searches all loaded assemblies.
        /// </summary>
        /// <param name="typeName">Component type name (e.g., "Rigidbody", "BoxCollider")</param>
        /// <returns>Type object or null if not found</returns>
        private static Type ResolveComponentType(string typeName)
        {
            // Try UnityEngine namespace first (most common)
            Type type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null)
            {
                return type;
            }

            // Try exact type name (might be fully qualified)
            type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            // Search all loaded assemblies for matching type name
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    // Skip assemblies that can't be fully loaded
                    continue;
                }

                foreach (var t in types)
                {
                    if (t.Name == typeName && typeof(Component).IsAssignableFrom(t))
                    {
                        return t;
                    }
                }
            }

            return null;
        }
    }
}
