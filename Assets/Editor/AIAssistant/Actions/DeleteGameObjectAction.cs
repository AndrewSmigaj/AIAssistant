using UnityEngine;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Action to delete a GameObject from the scene.
    /// </summary>
    public class DeleteGameObjectAction : IAction
    {
        /// <summary>
        /// OpenAI tool call ID for submitting results.
        /// </summary>
        public string callId;

        /// <summary>
        /// Instance ID of GameObject to delete.
        /// </summary>
        public int instanceId;

        public string GetDescription()
        {
            return $"Delete GameObject (ID: {instanceId})";
        }

        public string GetCallId() => callId;
    }
}
