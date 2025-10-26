using System.Collections.Generic;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Container for the parsed response from the OpenAI Responses API.
    /// Contains the response ID for conversation continuity, any message from the AI,
    /// and a list of actions to be approved and executed by the user.
    /// </summary>
    public class ActionPlan
    {
        /// <summary>
        /// Response ID from OpenAI, used as previous_response_id in subsequent requests
        /// to maintain conversation state and reasoning context.
        /// </summary>
        public string ResponseId;

        /// <summary>
        /// Concatenated text messages from the AI (from message items in the response).
        /// May be null if the AI only returned function calls.
        /// </summary>
        public string Message;

        /// <summary>
        /// List of actions parsed from function_call items in the response.
        /// These await user approval before execution.
        /// </summary>
        public List<IAction> Actions;

        /// <summary>
        /// Indicates whether the API request was successful.
        /// If false, check ErrorMessage for details.
        /// </summary>
        public bool Success;

        /// <summary>
        /// Error message if the API request failed (network error, rate limit, invalid response, etc.).
        /// Null if Success is true.
        /// </summary>
        public string ErrorMessage;

        public ActionPlan()
        {
            Actions = new List<IAction>();
            Success = false;
        }
    }
}
