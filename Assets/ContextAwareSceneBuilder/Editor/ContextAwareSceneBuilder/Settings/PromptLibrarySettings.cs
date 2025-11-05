using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// ScriptableObject storing which prompts in the Prompt Library are enabled.
    /// Follows the same pattern as AIAssistantSettings for consistency.
    /// </summary>
    public class PromptLibrarySettings : ScriptableObject
    {
        private const string SETTINGS_PATH = "Assets/ContextAwareSceneBuilder/Settings/PromptLibrarySettings.asset";

        [System.Serializable]
        public class PromptReference
        {
            public string relativePath;  // e.g., "Examples/Beds/bed_against_wall.txt"
            public bool enabled;
            public bool autoEnabled;     // True if this prompt should auto-enable in specific modes
        }

        [SerializeField]
        private List<PromptReference> _prompts = new List<PromptReference>();

        /// <summary>
        /// Gets or creates the singleton instance of PromptLibrarySettings.
        /// Follows the same pattern as AIAssistantSettings.GetOrCreateSettings().
        /// </summary>
        /// <returns>The singleton instance</returns>
        public static PromptLibrarySettings GetOrCreateSettings()
        {
            PromptLibrarySettings settings = AssetDatabase.LoadAssetAtPath<PromptLibrarySettings>(SETTINGS_PATH);

            if (settings == null)
            {
                settings = CreateInstance<PromptLibrarySettings>();

                // Ensure settings directory exists
                string settingsDir = System.IO.Path.GetDirectoryName(SETTINGS_PATH);
                if (!System.IO.Directory.Exists(settingsDir))
                {
                    System.IO.Directory.CreateDirectory(settingsDir);
                }

                AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
                AssetDatabase.SaveAssets();

                Debug.Log($"[Prompt Library] Created settings at {SETTINGS_PATH}");
            }

            return settings;
        }

        /// <summary>
        /// Checks if a specific prompt is enabled.
        /// </summary>
        /// <param name="relativePath">Relative path from PromptLibrary folder (e.g., "Examples/Beds/bed_against_wall.txt")</param>
        /// <returns>True if enabled, false otherwise</returns>
        public bool IsPromptEnabled(string relativePath)
        {
            PromptReference prompt = _prompts.Find(p => p.relativePath == relativePath);
            return prompt != null && prompt.enabled;
        }

        /// <summary>
        /// Enables or disables a specific prompt.
        /// Creates a new entry if the prompt doesn't exist in the list.
        /// </summary>
        /// <param name="relativePath">Relative path from PromptLibrary folder</param>
        /// <param name="enabled">Whether the prompt should be enabled</param>
        public void SetPromptEnabled(string relativePath, bool enabled)
        {
            PromptReference prompt = _prompts.Find(p => p.relativePath == relativePath);

            if (prompt == null)
            {
                // Add new entry
                prompt = new PromptReference
                {
                    relativePath = relativePath,
                    enabled = enabled
                };
                _prompts.Add(prompt);
            }
            else
            {
                // Update existing entry
                prompt.enabled = enabled;
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Sets the autoEnabled flag for a specific prompt.
        /// Creates a new entry if the prompt doesn't exist in the list.
        /// When autoEnabled is true, also sets enabled to true.
        /// </summary>
        /// <param name="relativePath">Relative path from PromptLibrary folder</param>
        /// <param name="autoEnabled">Whether the prompt should be auto-enabled</param>
        public void SetPromptAutoEnabled(string relativePath, bool autoEnabled)
        {
            PromptReference prompt = _prompts.Find(p => p.relativePath == relativePath);

            if (prompt == null)
            {
                // Add new entry
                prompt = new PromptReference
                {
                    relativePath = relativePath,
                    enabled = autoEnabled,  // Auto-enabled prompts are also enabled
                    autoEnabled = autoEnabled
                };
                _prompts.Add(prompt);
            }
            else
            {
                // Update existing entry
                prompt.autoEnabled = autoEnabled;
                if (autoEnabled)
                {
                    prompt.enabled = true;  // Auto-enabling also enables the prompt
                }
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Gets a list of all enabled prompt relative paths.
        /// Used by PromptLibraryLoader to load only enabled prompts.
        /// </summary>
        /// <returns>List of relative paths for enabled prompts</returns>
        public List<string> GetEnabledPromptPaths()
        {
            List<string> enabledPaths = new List<string>();

            foreach (PromptReference prompt in _prompts)
            {
                if (prompt.enabled)
                {
                    enabledPaths.Add(prompt.relativePath);
                }
            }

            return enabledPaths;
        }

        /// <summary>
        /// Gets all prompt references (for editor UI).
        /// </summary>
        /// <returns>List of all prompt references</returns>
        public List<PromptReference> GetAllPrompts()
        {
            return new List<PromptReference>(_prompts);
        }

        /// <summary>
        /// Removes a prompt reference from the settings.
        /// Used when a prompt file is deleted and we want to clean up.
        /// </summary>
        /// <param name="relativePath">Relative path of prompt to remove</param>
        public void RemovePrompt(string relativePath)
        {
            _prompts.RemoveAll(p => p.relativePath == relativePath);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}
