using System;

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// Stores complete metadata for a scanned prefab.
    /// Used for generating OpenAI function schemas and applying parameters.
    /// </summary>
    [Serializable]
    public class PrefabMetadata
    {
        /// <summary>
        /// Display name for the prefab (e.g., "RaceCar")
        /// </summary>
        public string prefabName;

        /// <summary>
        /// AssetDatabase path to the .prefab file (e.g., "Assets/AIPrefabs/Vehicles/RaceCar.prefab")
        /// Used for loading and instantiating the prefab.
        /// </summary>
        public string prefabPath;

        /// <summary>
        /// Unity tag for categorization (e.g., "Vehicles")
        /// Used to group prefabs and filter which are sent to AI.
        /// </summary>
        public string prefabTag;

        /// <summary>
        /// Globally unique function name (e.g., "createVehiclesRaceCar")
        /// Prevents collisions when multiple prefabs have the same name.
        /// Format: create{Tag}{SanitizedName}[Counter]
        /// </summary>
        public string uniqueFunctionName;

        /// <summary>
        /// All MonoBehaviour components on this prefab with serialized fields.
        /// Used to generate function parameters and apply values.
        /// </summary>
        public ComponentMetadata[] components;

        /// <summary>
        /// Default local scale of the prefab's root transform.
        /// Used by LLM to calculate world positions of semantic points.
        /// Example: TableSquareMedium has scale [0.341, 0.341, 0.341]
        /// Formula: worldPoint = position + rotation * (localPoint ⊙ scale)
        /// </summary>
        public UnityEngine.Vector3 scale;

        /// <summary>
        /// Semantic Local Space rotation (local → SLS).
        /// Rotation that aligns this prefab's semantic front to +Z and up to +Y.
        /// Calculated from front and top semantic point normals.
        /// Identity if front/top points missing.
        /// </summary>
        public UnityEngine.Quaternion semanticLocalSpaceRotation;

        /// <summary>
        /// Semantic tags describing prefab purpose (e.g., "enemy", "furniture").
        /// Helps LLM understand prefab beyond name/category.
        /// Null if no SemanticTags component on prefab root.
        /// </summary>
        public string[] semanticTags;

        /// <summary>
        /// Named semantic points with local position offsets.
        /// Used for precise object placement (e.g., "shelf_surface_1", "front").
        /// Null if no SemanticPoints container exists in prefab.
        /// </summary>
        public SemanticPoint[] semanticPoints;
    }

    /// <summary>
    /// Represents a named semantic point with local position offset and normal direction.
    /// </summary>
    [Serializable]
    public class SemanticPoint
    {
        /// <summary>
        /// Name of the semantic point (e.g., "front", "shelf_surface_1", "nose_tip").
        /// </summary>
        public string name;

        /// <summary>
        /// Local position offset relative to prefab pivot.
        /// </summary>
        public UnityEngine.Vector3 offset;

        /// <summary>
        /// Normal direction in local space (truthful to mesh geometry).
        /// Zero vector for non-directional points.
        /// Used for automatic rotation calculation and two-vector alignment.
        /// </summary>
        public UnityEngine.Vector3 normal;
    }
}
