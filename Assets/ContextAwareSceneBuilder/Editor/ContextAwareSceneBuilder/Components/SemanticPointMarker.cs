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
        /// <summary>
        /// Normal direction for this semantic point (zero = no normal).
        /// Used for automatic rotation calculation during object placement.
        /// Only directional points (front/back/left/right/top/bottom) have non-zero normals.
        /// </summary>
        [Tooltip("Normal direction for this semantic point. Leave zero for non-directional points.")]
        public Vector3 normal = Vector3.zero;

#if UNITY_EDITOR
        /// <summary>
        /// Draws yellow sphere gizmo and label in Scene view (always visible).
        /// Optionally draws cyan arrow showing normal direction.
        /// </summary>
        void OnDrawGizmos()
        {
            // Semi-transparent yellow sphere
            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawSphere(transform.position, 0.05f);

            // Label with point name
            Handles.Label(transform.position, gameObject.name);

            // Draw normal direction if non-zero
            if (normal != Vector3.zero)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(transform.position, normal.normalized * 0.2f);
            }
        }
#endif
    }
}
