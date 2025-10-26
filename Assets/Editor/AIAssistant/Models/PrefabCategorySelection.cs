using System;
using System.Collections.Generic;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Stores user's selected prefab categories.
    /// Serialized to ProjectSettings/AIAssistantPrefabCategories.json
    /// </summary>
    [Serializable]
    public class PrefabCategorySelection
    {
        /// <summary>
        /// List of selected Unity tags for prefab filtering.
        /// Initialized to prevent null reference issues.
        /// </summary>
        public List<string> selectedTags;

        /// <summary>
        /// Constructor ensures selectedTags is never null.
        /// </summary>
        public PrefabCategorySelection()
        {
            selectedTags = new List<string>();
        }
    }
}
