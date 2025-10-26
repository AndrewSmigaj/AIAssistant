using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Handles persistence of prefab category selection to ProjectSettings.
    /// Provides static helpers for loading/saving selected tags.
    /// </summary>
    public static class PrefabCategoryPersistence
    {
        private const string SELECTION_PATH = "ProjectSettings/AIAssistantPrefabCategories.json";

        /// <summary>
        /// Loads selected tags from ProjectSettings.
        /// Returns empty list if file doesn't exist or fails to load.
        /// </summary>
        /// <returns>List of selected tag names</returns>
        public static List<string> LoadSelectedTags()
        {
            if (!File.Exists(SELECTION_PATH))
            {
                return new List<string>();
            }

            try
            {
                string json = File.ReadAllText(SELECTION_PATH);
                var selection = JsonUtility.FromJson<PrefabCategorySelection>(json);

                // Null safety - JsonUtility might return null or have null field
                if (selection == null || selection.selectedTags == null)
                {
                    return new List<string>();
                }

                return selection.selectedTags;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AI Assistant] Failed to load category selection: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Saves selected tags to ProjectSettings.
        /// </summary>
        /// <param name="selectedTags">List of tag names to save</param>
        public static void SaveSelectedTags(List<string> selectedTags)
        {
            try
            {
                var selection = new PrefabCategorySelection
                {
                    selectedTags = selectedTags ?? new List<string>()
                };

                string json = JsonUtility.ToJson(selection, true);
                File.WriteAllText(SELECTION_PATH, json);

                Debug.Log($"[AI Assistant] Saved {selection.selectedTags.Count} category selection(s)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to save category selection: {ex.Message}");
            }
        }
    }
}
