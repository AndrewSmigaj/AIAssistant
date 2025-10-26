using UnityEngine;

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// Result of executing a single action.
    /// Used for partial failure tracking - allows some actions to succeed while others fail.
    /// </summary>
    public class ActionResult
    {
        /// <summary>
        /// The action that was attempted.
        /// </summary>
        public IAction Action;

        /// <summary>
        /// True if the action was executed successfully.
        /// </summary>
        public bool Success;

        /// <summary>
        /// Error message if the action failed (invalid parameters, GameObject creation failure, etc.).
        /// Null if Success is true.
        /// </summary>
        public string ErrorMessage;

        /// <summary>
        /// Reference to the created GameObject if the action succeeded.
        /// Null if Success is false or the action doesn't create a GameObject.
        /// </summary>
        public GameObject CreatedObject;

        /// <summary>
        /// Instance ID of created/modified GameObject.
        /// Null if action failed or doesn't involve a GameObject.
        /// Used for tracking objects across conversation turns.
        /// </summary>
        public int? InstanceId;
    }
}
