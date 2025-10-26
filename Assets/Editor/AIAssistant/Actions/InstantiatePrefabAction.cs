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
        /// World position for the instantiated prefab.
        /// </summary>
        public Vector3 position;

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
            return $"Create prefab '{prefabName}' at ({position.x:F1}, {position.y:F1}, {position.z:F1}) with {paramCount} parameter(s)";
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
