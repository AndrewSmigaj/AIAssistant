using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// Marks individual semantic points with gizmo visualization in Scene view.
    /// Attach to child GameObjects under SemanticPoints container.
    /// </summary>
    public class SemanticPointMarker : MonoBehaviour
    {
#if UNITY_EDITOR
        /// <summary>
        /// Draws yellow sphere gizmo and label in Scene view (always visible).
        /// </summary>
        void OnDrawGizmos()
        {
            // Semi-transparent yellow sphere
            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawSphere(transform.position, 0.05f);

            // Label with point name
            Handles.Label(transform.position, gameObject.name);
        }
#endif
    }
}
