using System;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Stores metadata for a single MonoBehaviour component on a prefab.
    /// Contains all serialized fields for that component.
    /// </summary>
    [Serializable]
    public class ComponentMetadata
    {
        /// <summary>
        /// Fully qualified type name (e.g., "MyNamespace.CarController")
        /// Used for Type.GetType() lookup and GetComponent() during parameter application.
        /// </summary>
        public string componentTypeName;

        /// <summary>
        /// Simple type name (e.g., "CarController")
        /// Used for parameter name prefixes to prevent field name collisions.
        /// </summary>
        public string componentTypeShortName;

        /// <summary>
        /// All serialized fields on this component.
        /// Includes public fields and private fields with [SerializeField].
        /// </summary>
        public FieldMetadata[] fields;
    }
}
