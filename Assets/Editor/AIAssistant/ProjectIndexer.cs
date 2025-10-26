using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Indexes Unity project data into JSON artifacts for AI context.
    /// Generates artifacts for project metadata, scenes, and scripts.
    /// Uses SHA256 hashing to avoid unnecessary file writes.
    /// </summary>
    public static class ProjectIndexer
    {
        public const string ARTIFACTS_ROOT = "Library/AIAssistant/Artifacts";
        public const string PROJECT_ARTIFACTS = ARTIFACTS_ROOT + "/Project";
        public const string SCENES_ARTIFACTS = ARTIFACTS_ROOT + "/Scenes";
        public const string SCRIPTS_ARTIFACTS = ARTIFACTS_ROOT + "/Scripts";

        /// <summary>
        /// Indexes all project data (project metadata, scenes, and scripts).
        /// Creates artifact directory structure if missing.
        /// </summary>
        public static void IndexAll()
        {
            try
            {
                // Ensure artifact directories exist
                EnsureDirectoryExists(Path.Combine(PROJECT_ARTIFACTS, "dummy.json"));
                EnsureDirectoryExists(Path.Combine(SCENES_ARTIFACTS, "dummy.json"));
                EnsureDirectoryExists(Path.Combine(SCRIPTS_ARTIFACTS, "dummy.json"));

                Debug.Log("[AI Assistant] Starting full project index...");

                IndexProject();
                IndexScenes();
                IndexScripts();

                Debug.Log("[AI Assistant] Project indexing complete.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to index project: {ex.Message}");
            }
        }

        /// <summary>
        /// Indexes project-level metadata (Unity version, product name, etc.).
        /// </summary>
        public static void IndexProject()
        {
            try
            {
                var metadata = new ProjectMetadata
                {
                    unityVersion = Application.unityVersion,
                    productName = Application.productName,
                    projectPath = Application.dataPath.Replace("/Assets", ""),
                    activeScene = SceneManager.GetActiveScene().path
                };

                string json = JsonUtility.ToJson(metadata, true);
                string outputPath = Path.Combine(PROJECT_ARTIFACTS, "ProjectMetadata.json");

                if (WriteArtifactIfChanged(outputPath, json))
                {
                    Debug.Log($"[AI Assistant] Updated project metadata artifact");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to index project metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Indexes all scenes in the project (not just build settings).
        /// Preserves current scene setup and restores it after indexing.
        /// </summary>
        public static void IndexScenes()
        {
            // Save current scene setup to restore later
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                // Find ALL scene files in project (not just build settings)
                string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");

                Debug.Log($"[AI Assistant] Found {sceneGuids.Length} scene(s) to index");

                foreach (string guid in sceneGuids)
                {
                    string scenePath = AssetDatabase.GUIDToAssetPath(guid);

                    // Skip scenes in Packages (read-only, can't be opened)
                    if (scenePath.StartsWith("Packages/"))
                    {
                        Debug.Log($"[AI Assistant] Skipping package scene: {scenePath}");
                        continue;
                    }

                    try
                    {
                        // Open scene additively (don't close current scenes)
                        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                        // Build scene info
                        var sceneInfo = new SceneInfo
                        {
                            sceneName = scene.name,
                            scenePath = scenePath,
                            rootObjects = new List<GameObjectInfo>()
                        };

                        // Get root GameObjects
                        GameObject[] rootGOs = scene.GetRootGameObjects();
                        foreach (GameObject go in rootGOs)
                        {
                            sceneInfo.rootObjects.Add(new GameObjectInfo
                            {
                                name = go.name,
                                active = go.activeInHierarchy,
                                position = new Vector3Serializable(go.transform.position),
                                childCount = go.transform.childCount
                            });
                        }

                        // Serialize and write
                        string json = JsonUtility.ToJson(sceneInfo, true);
                        string outputPath = Path.Combine(SCENES_ARTIFACTS, $"{scene.name}.json");

                        if (WriteArtifactIfChanged(outputPath, json))
                        {
                            Debug.Log($"[AI Assistant] Updated scene artifact: {scene.name}");
                        }

                        // Close the scene (don't save changes)
                        // Only close if it's not the last loaded scene (Unity requirement)
                        if (SceneManager.loadedSceneCount > 1)
                        {
                            EditorSceneManager.CloseScene(scene, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AI Assistant] Failed to index scene {scenePath}: {ex.Message}");
                    }
                }
            }
            finally
            {
                // Always restore original scene setup, even if indexing failed
                if (originalSetup != null && originalSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                }
            }
        }

        /// <summary>
        /// Indexes all C# scripts in the Assets folder.
        /// Extracts class names and namespaces using regex.
        /// </summary>
        public static void IndexScripts()
        {
            try
            {
                // Find all script files in Assets/
                string[] scriptGuids = AssetDatabase.FindAssets("t:Script", new[] { "Assets" });

                var scriptsInfo = new ScriptsCollection
                {
                    scripts = new List<ScriptInfo>()
                };

                Debug.Log($"[AI Assistant] Found {scriptGuids.Length} script(s) to index");

                foreach (string guid in scriptGuids)
                {
                    string scriptPath = AssetDatabase.GUIDToAssetPath(guid);

                    // Only process .cs files (exclude DLLs, etc.)
                    if (!scriptPath.EndsWith(".cs"))
                        continue;

                    try
                    {
                        string content = File.ReadAllText(scriptPath);

                        // Extract class name (simple regex, may miss edge cases)
                        string className = ExtractClassName(content);

                        // Extract namespace (optional)
                        string namespaceName = ExtractNamespace(content);

                        if (!string.IsNullOrEmpty(className))
                        {
                            scriptsInfo.scripts.Add(new ScriptInfo
                            {
                                path = scriptPath,
                                className = className,
                                namespaceName = namespaceName ?? ""
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[AI Assistant] Failed to parse script {scriptPath}: {ex.Message}");
                    }
                }

                // Serialize and write
                string json = JsonUtility.ToJson(scriptsInfo, true);
                string outputPath = Path.Combine(SCRIPTS_ARTIFACTS, "AllScripts.json");

                if (WriteArtifactIfChanged(outputPath, json))
                {
                    Debug.Log($"[AI Assistant] Updated scripts artifact ({scriptsInfo.scripts.Count} scripts)");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to index scripts: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts class name from C# source code using regex.
        /// Returns first match or null if not found.
        /// </summary>
        private static string ExtractClassName(string content)
        {
            // Match: class ClassName
            // Handles: public class, private class, etc.
            Match match = Regex.Match(content, @"class\s+(\w+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Extracts namespace from C# source code using regex.
        /// Returns namespace or null if not found.
        /// </summary>
        private static string ExtractNamespace(string content)
        {
            // Match: namespace Some.Namespace.Here
            Match match = Regex.Match(content, @"namespace\s+([\w\.]+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Computes SHA256 hash of a string.
        /// Uses .NET Standard 2.1 compatible API.
        /// </summary>
        private static string ComputeHash(string content)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                byte[] hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Writes JSON artifact to file only if content has changed.
        /// Uses SHA256 hash comparison to detect changes.
        /// </summary>
        /// <returns>True if file was written, false if unchanged</returns>
        private static bool WriteArtifactIfChanged(string path, string newJson)
        {
            try
            {
                // Compute hash of new content
                string newHash = ComputeHash(newJson);

                // If file exists, check if content changed
                if (File.Exists(path))
                {
                    string existingJson = File.ReadAllText(path);
                    string existingHash = ComputeHash(existingJson);

                    if (newHash == existingHash)
                    {
                        return false; // Content unchanged, skip write
                    }
                }

                // Content changed or file doesn't exist, write it
                File.WriteAllText(path, newJson);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to write artifact {path}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Ensures parent directory exists for a file path.
        /// Creates directory structure if missing.
        /// </summary>
        private static void EnsureDirectoryExists(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

    // ============================================================================
    // Data Models for JSON Serialization
    // ============================================================================

    /// <summary>
    /// Project-level metadata.
    /// </summary>
    [Serializable]
    public class ProjectMetadata
    {
        public string unityVersion;
        public string productName;
        public string projectPath;
        public string activeScene;
    }

    /// <summary>
    /// Scene information with root GameObjects.
    /// </summary>
    [Serializable]
    public class SceneInfo
    {
        public string sceneName;
        public string scenePath;
        public List<GameObjectInfo> rootObjects;
    }

    /// <summary>
    /// GameObject information (name, active state, position, child count).
    /// </summary>
    [Serializable]
    public class GameObjectInfo
    {
        public string name;
        public bool active;
        public Vector3Serializable position;
        public int childCount;
    }

    /// <summary>
    /// Serializable Vector3 (JsonUtility doesn't serialize Vector3 directly).
    /// </summary>
    [Serializable]
    public class Vector3Serializable
    {
        public float x;
        public float y;
        public float z;

        public Vector3Serializable(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }
    }

    /// <summary>
    /// Collection of script information.
    /// </summary>
    [Serializable]
    public class ScriptsCollection
    {
        public List<ScriptInfo> scripts;
    }

    /// <summary>
    /// Script file information (path, class name, namespace).
    /// </summary>
    [Serializable]
    public class ScriptInfo
    {
        public string path;
        public string className;
        public string namespaceName;
    }
}
