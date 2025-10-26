using System;

namespace UnityEditor.AIAssistant
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
    }
}
