using System;
using System.Reflection;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Stores metadata for a single serializable field on a prefab component.
    /// Includes cached FieldInfo for fast parameter application.
    /// </summary>
    [Serializable]
    public class FieldMetadata
    {
        /// <summary>
        /// C# field name (e.g., "maxSpeed" or backing field name like "<MaxSpeed>k__BackingField")
        /// </summary>
        public string fieldName;

        /// <summary>
        /// Type name for reconstruction ("int", "float", "Vector3", or FullName for enums)
        /// </summary>
        public string fieldTypeName;

        /// <summary>
        /// Parent component type (fully qualified, e.g., "MyNamespace.CarController")
        /// Used for GetComponent() lookup during parameter application.
        /// </summary>
        public string componentTypeName;

        /// <summary>
        /// API parameter name with component prefix (e.g., "CarController_maxSpeed")
        /// Prevents collisions when multiple components have fields with same name.
        /// </summary>
        public string parameterName;

        /// <summary>
        /// Human-readable description (from [Tooltip] attribute or field name fallback)
        /// </summary>
        public string description;

        /// <summary>
        /// Whether this field is required in OpenAI function schema.
        /// Currently all fields are optional (false).
        /// </summary>
        public bool isRequired;

        /// <summary>
        /// For enum types, list of valid values. Null for non-enum types.
        /// Used to generate enum constraint in OpenAI function schema.
        /// </summary>
        public string[] enumValues;

        /// <summary>
        /// Cached FieldInfo for fast SetValue() during parameter application.
        /// NOT serialized to JSON - repopulated via reflection after deserialization.
        /// </summary>
        [NonSerialized]
        public FieldInfo cachedFieldInfo;
    }
}
