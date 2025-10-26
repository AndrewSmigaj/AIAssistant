namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Action to create a circle sprite GameObject in the scene.
    /// </summary>
    public class CreateCircleAction : IAction
    {
        public string callId;  // Tool call ID from OpenAI for submitting results
        public string name;
        public float x;
        public float y;
        public float radius;
        public string color;

        public string GetDescription()
        {
            return $"Create circle '{name}' at ({x}, {y}) - radius {radius} - color {color}";
        }

        public string GetCallId()
        {
            return callId;
        }
    }
}
