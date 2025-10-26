namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// Action to remove a Unity component from a GameObject.
    /// </summary>
    public class RemoveComponentAction : IAction
    {
        /// <summary>
        /// OpenAI tool call ID for submitting results.
        /// </summary>
        public string callId;

        /// <summary>
        /// Instance ID of GameObject to remove component from.
        /// </summary>
        public int instanceId;

        /// <summary>
        /// Component type name to remove (e.g., "Rigidbody", "BoxCollider").
        /// </summary>
        public string componentType;

        public string GetDescription()
        {
            return $"Remove {componentType} from GameObject (ID: {instanceId})";
        }

        public string GetCallId() => callId;
    }
}
