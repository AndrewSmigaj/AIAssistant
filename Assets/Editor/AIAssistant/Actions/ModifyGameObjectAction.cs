using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Action to modify an existing GameObject's transform or component properties.
    /// All modification fields are optional - only specified fields get changed.
    /// </summary>
    public class ModifyGameObjectAction : IAction
    {
        /// <summary>
        /// OpenAI tool call ID for submitting results.
        /// </summary>
        public string callId;

        /// <summary>
        /// Instance ID of GameObject to modify (from scene context or creation output).
        /// </summary>
        public int instanceId;

        // Transform modifications (all optional)
        public string name;               // Rename
        public Vector3? position;         // Move
        public Vector3? rotation;         // Rotate (Euler angles)
        public Vector3? scale;            // Scale
        public bool? active;              // Enable/disable

        /// <summary>
        /// Component field modifications (optional).
        /// Key format: "ComponentType_fieldName"
        /// Value: new value (will be type-converted via reflection)
        /// </summary>
        public Dictionary<string, object> parameters;

        public string GetDescription()
        {
            return $"Modify GameObject (ID: {instanceId})";
        }

        public string GetCallId() => callId;
    }
}
