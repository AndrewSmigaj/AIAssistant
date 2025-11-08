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

namespace ContextAwareSceneBuilder.Editor
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
        /// Indexes the active scene only.
        /// </summary>
        public static void IndexScenes()
        {
            try
            {
                // Get the active scene (already open, no need to open/close)
                Scene scene = SceneManager.GetActiveScene();

                if (!scene.IsValid())
                {
                    Debug.LogWarning("[AI Assistant] No valid active scene to index");
                    return;
                }

                Debug.Log($"[AI Assistant] Indexing active scene: {scene.name}");

                // Build minified JSON manually (token-optimized for LLM)
                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append($"\"sceneName\":\"{EscapeJson(scene.name)}\",");
                sb.Append($"\"scenePath\":\"{EscapeJson(scene.path)}\",");
                sb.Append("\"rootObjects\":[");

                // Get root GameObjects
                GameObject[] rootGOs = scene.GetRootGameObjects();
                for (int i = 0; i < rootGOs.Length; i++)
                {
                    GameObject go = rootGOs[i];

                    sb.Append("{");
                    sb.Append($"\"instanceId\":{go.GetInstanceID()},");
                    sb.Append($"\"name\":\"{EscapeJson(go.name)}\",");
                    sb.Append($"\"active\":{(go.activeInHierarchy ? "true" : "false")},");
                    sb.Append($"\"position\":{FormatVector3Array(go.transform.position)},");
                    sb.Append($"\"rotation\":{FormatQuaternionArray(go.transform.rotation)},");
                    sb.Append($"\"scale\":{FormatVector3Array(go.transform.localScale)},");
                    sb.Append($"\"childCount\":{go.transform.childCount}");

                    // Extract semantic points (transform to UNSCALED SLS)
                    Transform semanticPointsContainer = go.transform.Find("SemanticPoints");
                    if (semanticPointsContainer != null && semanticPointsContainer.childCount > 0)
                    {
                        // Get prefab source and load R_ls from registry
                        GameObject prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(go);
                        string prefabPath = prefabRoot != null ? AssetDatabase.GetAssetPath(prefabRoot) : null;
                        Quaternion R_ls = Quaternion.identity;

                        if (!string.IsNullOrEmpty(prefabPath))
                        {
                            string registryPath = Path.Combine(PROJECT_ARTIFACTS, "PrefabRegistry.json");
                            if (File.Exists(registryPath))
                            {
                                string registryJson = File.ReadAllText(registryPath);
                                PrefabRegistry registry = JsonUtility.FromJson<PrefabRegistry>(registryJson);
                                PrefabMetadata metadata = System.Array.Find(registry.prefabs, p => p.prefabPath == prefabPath);
                                if (metadata != null)
                                {
                                    R_ls = metadata.semanticLocalSpaceRotation;
                                }
                            }
                        }

                        // Calculate R_ws (SLS → World): R_ws = R_wl * R_ls⁻¹
                        Quaternion R_wl = go.transform.rotation;
                        Quaternion R_ws = R_wl * Quaternion.Inverse(R_ls);
                        Vector3 p_wl = go.transform.position;
                        Vector3 S = go.transform.localScale;

                        // Export slsAdapters
                        sb.Append(",\"slsAdapters\":{");
                        sb.Append($"\"pivotWorld\":{FormatVector3Array(p_wl)},");
                        sb.Append($"\"rotationSLSToWorld\":{FormatQuaternionArray(R_ws)}");
                        sb.Append("}");

                        // Transform semantic points to UNSCALED SLS
                        sb.Append(",\"semanticPoints\":[");
                        for (int p = 0; p < semanticPointsContainer.childCount; p++)
                        {
                            Transform child = semanticPointsContainer.GetChild(p);

                            // Get normal from marker (in LOCAL space, not transformed by instance)
                            SemanticPointMarker marker = child.GetComponent<SemanticPointMarker>();
                            Vector3 normal_local = marker != null ? marker.normal : Vector3.zero;

                            // Transform offset: world → local (scaled) → local (unscaled) → SLS
                            Vector3 offset_world = child.position - p_wl;
                            Vector3 offset_local_scaled = Quaternion.Inverse(R_wl) * offset_world;
                            Vector3 offset_local = new Vector3(
                                S.x != 0 ? offset_local_scaled.x / S.x : 0,
                                S.y != 0 ? offset_local_scaled.y / S.y : 0,
                                S.z != 0 ? offset_local_scaled.z / S.z : 0
                            );
                            Vector3 offset_sls = R_ls * offset_local;

                            // Transform normal: local → SLS (rotation only, no scale)
                            Vector3 normal_sls = R_ls * normal_local;

                            // Export 7-value tuple [name, offset_x, offset_y, offset_z, normal_x, normal_y, normal_z]
                            sb.Append($"[\"{EscapeJson(child.name)}\",{CleanFloat(offset_sls.x)},{CleanFloat(offset_sls.y)},{CleanFloat(offset_sls.z)},{CleanFloat(normal_sls.x)},{CleanFloat(normal_sls.y)},{CleanFloat(normal_sls.z)}]");
                            if (p < semanticPointsContainer.childCount - 1)
                                sb.Append(",");
                        }
                        sb.Append("]");
                    }

                    sb.Append("}");

                    if (i < rootGOs.Length - 1)
                        sb.Append(",");
                }

                sb.Append("]}");


                // Write artifact
                string json = sb.ToString();
                string outputPath = Path.Combine(SCENES_ARTIFACTS, $"{scene.name}.json");

                if (WriteArtifactIfChanged(outputPath, json))
                {
                    Debug.Log($"[AI Assistant] Updated scene artifact: {scene.name}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to index active scene: {ex.Message}");
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

        // ============================================================================
        // Helper Methods for JSON Generation with Token Optimization
        // ============================================================================

        /// <summary>
        /// Formats Vector3 as compact JSON array with cleaned float values.
        /// Token-optimized: [x,y,z] instead of {"x":x,"y":y,"z":z}
        /// </summary>
        private static string FormatVector3Array(Vector3 v)
        {
            return $"[{CleanFloat(v.x)},{CleanFloat(v.y)},{CleanFloat(v.z)}]";
        }

        /// <summary>
        /// Formats float with 3 decimal places, rounding floating point noise to zero.
        /// </summary>
        private static string CleanFloat(float f)
        {
            if (Math.Abs(f) < 0.0001f)
                return "0.0";
            return f.ToString("F3");
        }

        /// <summary>
        /// Formats Quaternion as compact JSON array [x, y, z, w].
        /// </summary>
        private static string FormatQuaternionArray(Quaternion q)
        {
            return $"[{CleanFloat(q.x)},{CleanFloat(q.y)},{CleanFloat(q.z)},{CleanFloat(q.w)}]";
        }

        /// <summary>
        /// Escapes special characters for JSON string embedding.
        /// </summary>
        private static string EscapeJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
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
    /// GameObject information (name, active state, position, rotation, scale, child count, instanceId).
    /// </summary>
    [Serializable]
    public class GameObjectInfo
    {
        public string name;
        public bool active;
        public Vector3Serializable position;
        public Vector3Serializable rotation;  // Euler angles
        public Vector3Serializable scale;     // Local scale
        public int childCount;
        public int instanceId;                // For object identification (session-only)
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
