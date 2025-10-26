namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Base interface for all AI Assistant actions.
    /// Actions represent operations that the AI can perform on the Unity scene.
    /// </summary>
    public interface IAction
    {
        /// <summary>
        /// Gets a human-readable description of this action for display in the approval UI.
        /// </summary>
        /// <returns>Description string suitable for checkbox labels</returns>
        string GetDescription();

        /// <summary>
        /// Gets the OpenAI tool call ID for submitting execution results.
        /// </summary>
        /// <returns>Call ID from the function_call response item</returns>
        string GetCallId();
    }
}
