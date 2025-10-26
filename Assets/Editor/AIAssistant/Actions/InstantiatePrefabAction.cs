using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Action to instantiate a prefab with custom parameter values.
    /// Generic action that works with any scanned prefab.
    /// </summary>
    public class InstantiatePrefabAction : IAction
    {
        /// <summary>
        /// OpenAI tool call ID for submitting results.
        /// </summary>
        public string callId;

        /// <summary>
        /// AssetDatabase path to the prefab (e.g., "Assets/AIPrefabs/RaceCar.prefab").
        /// </summary>
        public string prefabPath;

        /// <summary>
        /// Name for the instantiated GameObject.
        /// </summary>
        public string name;

        /// <summary>
        /// World position for the instantiated prefab.
        /// </summary>
        public Vector3 position;

        /// <summary>
        /// Rotation for the instantiated prefab (Euler angles in degrees).
        /// Defaults to (0, 0, 0) if not specified.
        /// </summary>
        public Vector3 rotation;

        /// <summary>
        /// Scale for the instantiated prefab.
        /// Defaults to (1, 1, 1) if not specified.
        /// </summary>
        public Vector3 scale;

        /// <summary>
        /// Parameter values to apply to prefab components.
        /// Key format: "ComponentType_fieldName" (namespaced)
        /// Value: object (JSONNode or native type)
        /// </summary>
        public Dictionary<string, object> parameters;

        /// <summary>
        /// Human-readable description for approval UI.
        /// </summary>
        public string GetDescription()
        {
            // Extract prefab name from path for display
            string prefabName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);

            int paramCount = parameters != null ? parameters.Count : 0;

            // Build description with transform info
            string desc = $"Create prefab '{prefabName}' named '{name}' at pos ({position.x:F1}, {position.y:F1}, {position.z:F1})";

            // Add rotation if non-zero
            if (rotation.x != 0 || rotation.y != 0 || rotation.z != 0)
            {
                desc += $", rot ({rotation.x:F1}, {rotation.y:F1}, {rotation.z:F1})";
            }

            // Add scale if not uniform 1
            if (scale.x != 1 || scale.y != 1 || scale.z != 1)
            {
                desc += $", scale ({scale.x:F1}, {scale.y:F1}, {scale.z:F1})";
            }

            desc += $" with {paramCount} parameter(s)";

            return desc;
        }

        /// <summary>
        /// Gets OpenAI call ID for result submission.
        /// </summary>
        public string GetCallId()
        {
            return callId;
        }
    }
}
