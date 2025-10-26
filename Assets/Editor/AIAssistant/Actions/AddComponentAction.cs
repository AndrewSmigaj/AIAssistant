using System.Collections.Generic;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Action to add a Unity component to an existing GameObject with optional initial parameter values.
    /// </summary>
    public class AddComponentAction : IAction
    {
        /// <summary>
        /// OpenAI tool call ID for submitting results.
        /// </summary>
        public string callId;

        /// <summary>
        /// Instance ID of GameObject to add component to.
        /// </summary>
        public int instanceId;

        /// <summary>
        /// Component type name (e.g., "Rigidbody", "BoxCollider", "Light").
        /// </summary>
        public string componentType;

        /// <summary>
        /// Initial component field values (optional).
        /// Key: field name (e.g., "mass", "drag")
        /// Value: field value (will be type-converted via reflection)
        /// </summary>
        public Dictionary<string, object> parameters;

        public string GetDescription()
        {
            return $"Add {componentType} to GameObject (ID: {instanceId})";
        }

        public string GetCallId() => callId;
    }
}
