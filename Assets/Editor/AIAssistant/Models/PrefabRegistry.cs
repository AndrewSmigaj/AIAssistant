using System;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Root container for all scanned prefab metadata.
    /// Serialized to ProjectArtifacts/PrefabRegistry.json with version field for schema migration.
    /// </summary>
    [Serializable]
    public class PrefabRegistry
    {
        /// <summary>
        /// Schema version for future migration support.
        /// Current version: "2.1"
        /// </summary>
        public string version = "2.1";

        /// <summary>
        /// All scanned prefabs from Assets/AIPrefabs/
        /// </summary>
        public PrefabMetadata[] prefabs;
    }
}
