namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Action to create a rectangle sprite GameObject in the scene.
    /// </summary>
    public class CreateRectangleAction : IAction
    {
        public string callId;  // Tool call ID from OpenAI for submitting results
        public string name;
        public float x;
        public float y;
        public float width;
        public float height;
        public string color;

        public string GetDescription()
        {
            return $"Create rectangle '{name}' at ({x}, {y}) - {width}x{height} units - color {color}";
        }

        public string GetCallId()
        {
            return callId;
        }
    }
}
