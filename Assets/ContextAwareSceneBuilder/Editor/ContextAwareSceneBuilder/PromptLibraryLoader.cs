using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// Static utility class for scanning and loading prompts from the Prompt Library.
    /// Finds .txt and .md files in the PromptLibrary folder and loads enabled prompts.
    /// </summary>
    public static class PromptLibraryLoader
    {
        private const string PROMPT_LIBRARY_FOLDER = "Assets/ContextAwareSceneBuilder/PromptLibrary";

        /// <summary>
        /// Scans the PromptLibrary folder for all .txt and .md files.
        /// Returns relative paths from the PromptLibrary folder.
        /// </summary>
        /// <returns>List of relative paths (e.g., "Examples/Beds/bed_against_wall.txt")</returns>
        public static List<string> ScanPromptFiles()
        {
            List<string> relativePaths = new List<string>();

            // Find all .txt files
            string[] txtGuids = AssetDatabase.FindAssets("t:TextAsset", new[] { PROMPT_LIBRARY_FOLDER });
            foreach (string guid in txtGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                // Only include .txt and .md files (exclude .meta and other types)
                if (assetPath.EndsWith(".txt") || assetPath.EndsWith(".md"))
                {
                    // Convert to relative path from PromptLibrary folder
                    string relativePath = GetRelativePath(assetPath);
                    if (!string.IsNullOrEmpty(relativePath))
                    {
                        relativePaths.Add(relativePath);
                    }
                }
            }

            return relativePaths;
        }

        /// <summary>
        /// Loads all enabled prompts and concatenates them into a single string.
        /// Used by ContextBuilder to inject prompts into the system message.
        /// </summary>
        /// <param name="settings">PromptLibrarySettings instance</param>
        /// <returns>Concatenated content of all enabled prompts, or empty string if none enabled</returns>
        public static string LoadEnabledPrompts(PromptLibrarySettings settings)
        {
            List<string> enabledPaths = settings.GetEnabledPromptPaths();

            if (enabledPaths.Count == 0)
            {
                return string.Empty;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach (string relativePath in enabledPaths)
            {
                string fullPath = Path.Combine(PROMPT_LIBRARY_FOLDER, relativePath);

                try
                {
                    if (File.Exists(fullPath))
                    {
                        string content = File.ReadAllText(fullPath);
                        sb.AppendLine(content);
                        sb.AppendLine(); // Add blank line between prompts
                    }
                    else
                    {
                        Debug.LogWarning($"[Prompt Library] Enabled prompt not found: {fullPath}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Prompt Library] Failed to load prompt {fullPath}: {ex.Message}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Converts an absolute asset path to a relative path from the PromptLibrary folder.
        /// Example: "Assets/ContextAwareSceneBuilder/PromptLibrary/Examples/Beds/bed.txt" -> "Examples/Beds/bed.txt"
        /// </summary>
        /// <param name="assetPath">Full asset path</param>
        /// <returns>Relative path, or empty string if not in PromptLibrary folder</returns>
        private static string GetRelativePath(string assetPath)
        {
            if (!assetPath.StartsWith(PROMPT_LIBRARY_FOLDER))
            {
                return string.Empty;
            }

            // Remove the base folder path and leading slash
            string relativePath = assetPath.Substring(PROMPT_LIBRARY_FOLDER.Length);
            if (relativePath.StartsWith("/"))
            {
                relativePath = relativePath.Substring(1);
            }

            return relativePath;
        }

        /// <summary>
        /// Gets the full asset path from a relative path.
        /// Example: "Examples/Beds/bed.txt" -> "Assets/ContextAwareSceneBuilder/PromptLibrary/Examples/Beds/bed.txt"
        /// </summary>
        /// <param name="relativePath">Relative path from PromptLibrary folder</param>
        /// <returns>Full asset path</returns>
        public static string GetFullPath(string relativePath)
        {
            return Path.Combine(PROMPT_LIBRARY_FOLDER, relativePath);
        }
    }
}
