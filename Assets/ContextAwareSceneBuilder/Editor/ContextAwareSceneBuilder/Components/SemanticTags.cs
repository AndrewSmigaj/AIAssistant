using System.Collections.Generic;
using UnityEngine;

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// Stores semantic tags on prefab root for LLM understanding.
    /// Tags help LLM understand prefab purpose beyond name/category.
    /// Example: 'enemy', 'melee', 'hostile' for an orc prefab.
    /// </summary>
    public class SemanticTags : MonoBehaviour
    {
        /// <summary>
        /// User-defined tags describing the prefab's purpose or characteristics.
        /// </summary>
        [Tooltip("Comma-separated tags like 'enemy', 'furniture', 'weapon'")]
        public List<string> tags = new List<string>();
    }
}
