using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// EditorWindow for visually annotating prefabs with semantic points and tags.
    /// Provides 3-panel layout: prefab list, preview, and annotation controls.
    /// </summary>
    public class PrefabSemanticAnnotator : EditorWindow
    {
        // State
        private string _selectedPrefabPath;
        private List<PrefabMetadata> _prefabs;
        private string _pendingPrefabToOpen = null;

        // UI scroll positions
        private Vector2 _leftScrollPos;
        private Vector2 _rightScrollPos;

        // Semantic tags editing
        private string _semanticTagsText = "";

        /// <summary>
        /// MenuItem to open the Semantic Annotator window.
        /// </summary>
        [MenuItem("Window/Context-Aware Scene Builder/Semantic Annotator")]
        public static void ShowWindow()
        {
            var window = GetWindow<PrefabSemanticAnnotator>();
            window.titleContent = new GUIContent("Semantic Annotator");
            window.Show();
        }

        /// <summary>
        /// Called when window is enabled.
        /// </summary>
        void OnEnable()
        {
            LoadPrefabList();
        }

        /// <summary>
        /// Called when window is disabled. Cleanup event subscriptions.
        /// </summary>
        void OnDisable()
        {
            // Clean up event subscription when window closes
            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
        }

        /// <summary>
        /// Main GUI rendering method.
        /// </summary>
        void OnGUI()
        {
            DisplayHeader();

            EditorGUILayout.BeginHorizontal();

            // Left panel: Prefab list
            DisplayLeftPanel();

            // Center panel: Preview placeholder
            DisplayCenterPanel();

            // Right panel: Annotation controls
            DisplayRightPanel();

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Displays header toolbar with title and utility buttons.
        /// </summary>
        void DisplayHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Semantic Annotator", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Initialize Semantic Points", EditorStyles.toolbarButton, GUILayout.Width(160)))
            {
                InitializeSemanticPointsForAll();
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        /// <summary>
        /// Displays left panel with scrollable prefab list.
        /// </summary>
        void DisplayLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            GUILayout.Label("Prefabs", EditorStyles.boldLabel);

            _leftScrollPos = EditorGUILayout.BeginScrollView(_leftScrollPos);

            if (_prefabs == null || _prefabs.Count == 0)
            {
                EditorGUILayout.HelpBox("No prefabs found. Click 'Refresh Prefabs'.", MessageType.Info);
            }
            else
            {
                foreach (var prefab in _prefabs)
                {
                    // Highlight selected prefab
                    bool isSelected = prefab.prefabPath == _selectedPrefabPath;
                    GUIStyle buttonStyle = isSelected ? new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold } : GUI.skin.button;

                    if (GUILayout.Button(prefab.prefabName, buttonStyle))
                    {
                        SelectPrefab(prefab.prefabPath);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Displays center panel with "Open in Prefab Mode" button.
        /// </summary>
        void DisplayCenterPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label("Prefab Editing", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (string.IsNullOrEmpty(_selectedPrefabPath))
            {
                GUIStyle centeredStyle = new GUIStyle(EditorStyles.label);
                centeredStyle.alignment = TextAnchor.MiddleCenter;
                centeredStyle.normal.textColor = Color.gray;
                GUILayout.Label("Select a prefab to edit", centeredStyle);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                EditorGUILayout.BeginVertical(GUILayout.Width(250));
                EditorGUILayout.HelpBox("Click to open prefab in Prefab Mode.\nPosition semantic points using Unity's transform tools.", MessageType.Info);
                GUILayout.Space(10);

                if (GUILayout.Button("Open in Prefab Mode", GUILayout.Height(40)))
                {
                    OpenPrefabInPrefabMode();
                }

                EditorGUILayout.EndVertical();

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Displays right panel with annotation controls.
        /// </summary>
        void DisplayRightPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(280));

            _rightScrollPos = EditorGUILayout.BeginScrollView(_rightScrollPos);

            // Display selected prefab info
            if (string.IsNullOrEmpty(_selectedPrefabPath))
            {
                EditorGUILayout.HelpBox("No prefab selected", MessageType.Info);
            }
            else
            {
                GUILayout.Label($"Editing: {System.IO.Path.GetFileNameWithoutExtension(_selectedPrefabPath)}", EditorStyles.boldLabel);
                GUILayout.Space(10);

                DisplaySemanticTagsSection();
                GUILayout.Space(15);
                DisplaySemanticPointsSection();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Displays semantic tags section with text field and save button.
        /// </summary>
        void DisplaySemanticTagsSection()
        {
            GUILayout.Label("Semantic Tags", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Comma-separated tags (e.g., enemy, melee, hostile)", MessageType.Info);

            _semanticTagsText = EditorGUILayout.TextField("Tags", _semanticTagsText);

            if (GUILayout.Button("Save Tags"))
            {
                SaveSemanticTags();
            }
        }

        /// <summary>
        /// Displays semantic points section with add/create buttons.
        /// </summary>
        void DisplaySemanticPointsSection()
        {
            GUILayout.Label("Semantic Points", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Add points here, then open in Prefab Mode to position them.", MessageType.Info);

            if (GUILayout.Button("+ Add Point"))
            {
                AddSemanticPoint();
            }

            if (GUILayout.Button("Create Directions"))
            {
                CreateDirectionalPoints();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rotate Y-axis (Horizontal)"))
            {
                RotateDirectionalPointsYAxis();
            }

            if (GUILayout.Button("Rotate X-axis (Vertical)"))
            {
                RotateDirectionalPointsXAxis();
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Loads prefab list from registry cache.
        /// </summary>
        void LoadPrefabList()
        {
            List<string> selectedTags = PrefabCategoryPersistence.LoadSelectedTags();
            _prefabs = PrefabRegistryCache.GetByTags(selectedTags);

            if (_prefabs == null || _prefabs.Count == 0)
            {
                Debug.LogWarning("[Semantic Annotator] No prefabs found in registry. Run prefab scanner first.");
            }
            else
            {
                Debug.Log($"[Semantic Annotator] Loaded {_prefabs.Count} prefab(s)");
            }

            Repaint();
        }

        /// <summary>
        /// Initializes SemanticPoints container and pivot point for all loaded prefabs.
        /// Creates container if missing, adds pivot at [0,0,0] if missing.
        /// Idempotent - safe to run multiple times.
        /// </summary>
        void InitializeSemanticPointsForAll()
        {
            if (_prefabs == null || _prefabs.Count == 0)
            {
                EditorUtility.DisplayDialog("No Prefabs", "Load prefabs first using Refresh Prefabs button", "OK");
                return;
            }

            int containerCount = 0;
            int pivotCount = 0;

            foreach (var prefab in _prefabs)
            {
                GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefab.prefabPath);
                try
                {
                    bool modified = false;

                    // Create container if missing
                    Transform container = prefabContents.transform.Find("SemanticPoints");
                    if (container == null)
                    {
                        GameObject containerObj = new GameObject("SemanticPoints");
                        containerObj.transform.SetParent(prefabContents.transform, false);
                        container = containerObj.transform;
                        containerCount++;
                        modified = true;
                    }

                    // Create pivot if missing
                    Transform pivot = container.Find("pivot");
                    if (pivot == null)
                    {
                        GameObject pivotObj = new GameObject("pivot");
                        pivotObj.transform.SetParent(container, false);
                        pivotObj.transform.localPosition = Vector3.zero;
                        pivotObj.AddComponent<SemanticPointMarker>();
                        pivotCount++;
                        modified = true;
                    }

                    // Only save if we made changes
                    if (modified)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabContents, prefab.prefabPath);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabContents);
                }
            }

            Debug.Log($"[Semantic Annotator] Initialized semantic points: {containerCount} containers created, {pivotCount} pivot points added");
            EditorUtility.DisplayDialog("Complete",
                $"Initialized semantic points for {_prefabs.Count} prefab(s):\n\n" +
                $"• {containerCount} SemanticPoints containers created\n" +
                $"• {pivotCount} pivot points added",
                "OK");
        }

        /// <summary>
        /// Selects a prefab and loads its semantic tags for editing.
        /// Temporarily loads prefab to read tags, then immediately unloads.
        /// </summary>
        void SelectPrefab(string prefabPath)
        {
            _selectedPrefabPath = prefabPath;

            // Temporarily load to read existing tags and ensure pivot point exists
            GameObject tempContents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (tempContents == null)
            {
                Debug.LogError($"[Semantic Annotator] Failed to load prefab: {prefabPath}");
                return;
            }

            try
            {
                // Load existing semantic tags
                SemanticTags semanticTagsComp = tempContents.GetComponent<SemanticTags>();
                if (semanticTagsComp != null && semanticTagsComp.tags != null && semanticTagsComp.tags.Count > 0)
                {
                    _semanticTagsText = string.Join(", ", semanticTagsComp.tags);
                }
                else
                {
                    _semanticTagsText = "";
                }

                // Auto-add "pivot" semantic point if it doesn't exist
                EnsurePivotPointExists(tempContents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(tempContents);
            }

            Repaint();
        }

        /// <summary>
        /// Saves semantic tags to the selected prefab.
        /// </summary>
        void SaveSemanticTags()
        {
            if (string.IsNullOrEmpty(_selectedPrefabPath))
            {
                Debug.LogWarning("[Semantic Annotator] No prefab selected");
                return;
            }

            // Load prefab contents for modification
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(_selectedPrefabPath);

            try
            {
                // Find or add SemanticTags component
                SemanticTags semanticTagsComp = prefabContents.GetComponent<SemanticTags>();
                if (semanticTagsComp == null)
                {
                    semanticTagsComp = prefabContents.AddComponent<SemanticTags>();
                }

                // Parse comma-separated tags
                semanticTagsComp.tags.Clear();
                if (!string.IsNullOrWhiteSpace(_semanticTagsText))
                {
                    string[] tagArray = _semanticTagsText.Split(',');
                    foreach (string tag in tagArray)
                    {
                        string trimmed = tag.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            semanticTagsComp.tags.Add(trimmed);
                        }
                    }
                }

                // Save prefab
                PrefabUtility.SaveAsPrefabAsset(prefabContents, _selectedPrefabPath);
                Debug.Log($"[Semantic Annotator] Saved {semanticTagsComp.tags.Count} tag(s) to {System.IO.Path.GetFileName(_selectedPrefabPath)}");

                EditorUtility.DisplayDialog("Success", $"Saved {semanticTagsComp.tags.Count} semantic tag(s)", "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        /// <summary>
        /// Opens the selected prefab in Prefab Mode for editing and selects the SemanticPoints container.
        /// </summary>
        void OpenPrefabInPrefabMode()
        {
            if (string.IsNullOrEmpty(_selectedPrefabPath))
            {
                Debug.LogWarning("[Semantic Annotator] No prefab selected");
                return;
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(_selectedPrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"[Semantic Annotator] Failed to load prefab asset: {_selectedPrefabPath}");
                return;
            }

            // Store which prefab we're trying to open
            _pendingPrefabToOpen = _selectedPrefabPath;

            // Unsubscribe then resubscribe to prevent duplicate handlers
            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;

            AssetDatabase.OpenAsset(prefabAsset);
            Debug.Log($"[Semantic Annotator] Opening {prefabAsset.name} in Prefab Mode...");
        }

        /// <summary>
        /// Called when any prefab stage is opened. Selects SemanticPoints if it's our prefab.
        /// </summary>
        void OnPrefabStageOpened(PrefabStage stage)
        {
            // Only process if this is the prefab we're waiting for
            if (stage.assetPath != _pendingPrefabToOpen)
                return;

            // Unsubscribe immediately to avoid processing future prefab opens
            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
            _pendingPrefabToOpen = null;

            // Find and select SemanticPoints container
            Transform semanticPointsContainer = stage.prefabContentsRoot.transform.Find("SemanticPoints");

            if (semanticPointsContainer != null)
            {
                Selection.activeGameObject = semanticPointsContainer.gameObject;
                EditorGUIUtility.PingObject(semanticPointsContainer.gameObject);
                Debug.Log("[Semantic Annotator] Selected SemanticPoints container in hierarchy");
            }
            else
            {
                Debug.LogWarning($"[Semantic Annotator] No SemanticPoints container found. Create points first using '+ Add Point' or 'Create Directions'.");
            }
        }

        /// <summary>
        /// Adds a new semantic point to the selected prefab with unique auto-incremented name.
        /// </summary>
        void AddSemanticPoint()
        {
            if (string.IsNullOrEmpty(_selectedPrefabPath))
            {
                Debug.LogWarning("[Semantic Annotator] No prefab selected");
                return;
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(_selectedPrefabPath);
            try
            {
                // Ensure SemanticPoints container exists
                Transform container = prefabContents.transform.Find("SemanticPoints");
                if (container == null)
                {
                    GameObject containerObj = new GameObject("SemanticPoints");
                    containerObj.transform.SetParent(prefabContents.transform, false);
                    container = containerObj.transform;
                }

                // Find unique name
                string baseName = "NewPoint";
                string uniqueName = baseName;
                int counter = 1;
                while (container.Find(uniqueName) != null)
                {
                    uniqueName = $"{baseName}_{counter}";
                    counter++;
                }

                // Create point
                GameObject pointObj = new GameObject(uniqueName);
                pointObj.transform.SetParent(container, false);
                pointObj.AddComponent<SemanticPointMarker>();

                PrefabUtility.SaveAsPrefabAsset(prefabContents, _selectedPrefabPath);
                Debug.Log($"[Semantic Annotator] Created semantic point '{uniqueName}'");

                EditorUtility.DisplayDialog("Success", $"Created semantic point '{uniqueName}'.\n\nOpen in Prefab Mode to position it.", "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            Repaint();
        }

        /// <summary>
        /// Creates 6 directional semantic points at the bounds edges of the prefab.
        /// </summary>
        void CreateDirectionalPoints()
        {
            if (string.IsNullOrEmpty(_selectedPrefabPath))
            {
                Debug.LogWarning("[Semantic Annotator] No prefab selected");
                return;
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(_selectedPrefabPath);
            try
            {
                // Calculate bounds
                Renderer[] renderers = prefabContents.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                {
                    EditorUtility.DisplayDialog("No Bounds",
                        "Cannot create directional points: prefab has no Renderer components.\n\nAdd Renderer components to the prefab first.",
                        "OK");
                    return;
                }

                Bounds bounds = CalculateCombinedBounds(renderers, prefabContents.transform);
                Vector3 centerOffset = bounds.center - prefabContents.transform.position;
                Vector3 size = bounds.size;

                // Unscale the size to get local space dimensions
                Vector3 scale = prefabContents.transform.localScale;
                Vector3 unscaledSize = new Vector3(
                    scale.x != 0 ? size.x / scale.x : size.x,
                    scale.y != 0 ? size.y / scale.y : size.y,
                    scale.z != 0 ? size.z / scale.z : size.z
                );

                // Debug logging to diagnose position calculation
                Debug.Log($"[CreateDirections] Prefab: {prefabContents.name}");
                Debug.Log($"[CreateDirections] prefabContents.transform.position: {prefabContents.transform.position}");
                Debug.Log($"[CreateDirections] prefabContents.transform.localScale: {scale}");
                Debug.Log($"[CreateDirections] bounds.center: {bounds.center}");
                Debug.Log($"[CreateDirections] bounds.size (world): {bounds.size}");
                Debug.Log($"[CreateDirections] unscaledSize (local): {unscaledSize}");
                Debug.Log($"[CreateDirections] centerOffset: {centerOffset}");
                Debug.Log($"[CreateDirections] Calculated front position: {centerOffset + Vector3.forward * (unscaledSize.z / 2)}");

                // Ensure SemanticPoints container exists
                Transform container = prefabContents.transform.Find("SemanticPoints");
                if (container == null)
                {
                    GameObject containerObj = new GameObject("SemanticPoints");
                    containerObj.transform.SetParent(prefabContents.transform, false);
                    container = containerObj.transform;
                }

                // Remove existing directional points if they exist
                string[] directions = { "front", "back", "left", "right", "top", "bottom" };
                foreach (string dir in directions)
                {
                    Transform existing = container.Find(dir);
                    if (existing != null)
                    {
                        DestroyImmediate(existing.gameObject);
                    }
                }

                // Create directional points with canonical normals (using unscaled size)
                CreatePoint(container, "front", centerOffset + Vector3.forward * (unscaledSize.z / 2), Vector3.forward);
                CreatePoint(container, "back", centerOffset + Vector3.back * (unscaledSize.z / 2), Vector3.back);
                CreatePoint(container, "left", centerOffset + Vector3.left * (unscaledSize.x / 2), Vector3.left);
                CreatePoint(container, "right", centerOffset + Vector3.right * (unscaledSize.x / 2), Vector3.right);
                CreatePoint(container, "top", centerOffset + Vector3.up * (unscaledSize.y / 2), Vector3.up);
                CreatePoint(container, "bottom", centerOffset + Vector3.down * (unscaledSize.y / 2), Vector3.down);

                PrefabUtility.SaveAsPrefabAsset(prefabContents, _selectedPrefabPath);
                Debug.Log($"[Semantic Annotator] Created 6 directional points");

                EditorUtility.DisplayDialog("Success", "Created 6 directional points (front, back, left, right, top, bottom).\n\nOpen in Prefab Mode to adjust positions if needed.", "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            Repaint();
        }

        /// <summary>
        /// Rotates directional point naming by 90° clockwise around Y-axis.
        /// CRITICAL: Normals NEVER change (they describe truthful geometry).
        /// Only names get reassigned.
        /// </summary>
        void RotateDirectionalPointsYAxis()
        {
            if (string.IsNullOrEmpty(_selectedPrefabPath))
            {
                Debug.LogWarning("[Semantic Annotator] No prefab selected");
                return;
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(_selectedPrefabPath);
            try
            {
                Transform container = prefabContents.transform.Find("SemanticPoints");
                if (container == null)
                {
                    EditorUtility.DisplayDialog("Error", "No SemanticPoints container found. Create directional points first.", "OK");
                    return;
                }

                // Get the 4 horizontal points by their current names
                Transform oldFront = container.Find("front");
                Transform oldBack = container.Find("back");
                Transform oldRight = container.Find("right");
                Transform oldLeft = container.Find("left");

                if (oldFront == null || oldBack == null || oldRight == null || oldLeft == null)
                {
                    EditorUtility.DisplayDialog("Error", "Not all horizontal directional points found. Create directional points first.", "OK");
                    return;
                }

                // Cycle names: front→left, right→front, back→right, left→back
                // CRITICAL: Normals stay unchanged (truthful to geometry)
                oldFront.name = "left";
                oldRight.name = "front";
                oldBack.name = "right";
                oldLeft.name = "back";

                PrefabUtility.SaveAsPrefabAsset(prefabContents, _selectedPrefabPath);
                Debug.Log("[Semantic Annotator] Rotated directional point naming by 90° clockwise");

                EditorUtility.DisplayDialog("Success",
                    "Rotated directional point naming by 90° clockwise.\n\n" +
                    "• front → left\n" +
                    "• right → front\n" +
                    "• back → right\n" +
                    "• left → back\n\n" +
                    "Normals remain unchanged (truthful to geometry).\n" +
                    "Click 'Refresh Prefabs' to recalculate R_ls.",
                    "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            Repaint();
        }

        /// <summary>
        /// Rotates directional point naming by 90° around X-axis.
        /// Used for objects lying on their side (e.g., paddle where handle is "back" but should be "top").
        /// CRITICAL: Normals NEVER change (they describe truthful geometry).
        /// Only names get reassigned.
        /// </summary>
        void RotateDirectionalPointsXAxis()
        {
            if (string.IsNullOrEmpty(_selectedPrefabPath))
            {
                Debug.LogWarning("[Semantic Annotator] No prefab selected");
                return;
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(_selectedPrefabPath);
            try
            {
                Transform container = prefabContents.transform.Find("SemanticPoints");
                if (container == null)
                {
                    EditorUtility.DisplayDialog("Error", "No SemanticPoints container found. Create directional points first.", "OK");
                    return;
                }

                // Get the 4 vertical points by their current names
                Transform oldFront = container.Find("front");
                Transform oldBack = container.Find("back");
                Transform oldTop = container.Find("top");
                Transform oldBottom = container.Find("bottom");

                if (oldFront == null || oldBack == null || oldTop == null || oldBottom == null)
                {
                    EditorUtility.DisplayDialog("Error", "Not all vertical directional points found. Create directional points first.", "OK");
                    return;
                }

                // Cycle names: back→top, top→front, front→bottom, bottom→back
                // CRITICAL: Normals stay unchanged (truthful to geometry)
                oldBack.name = "top";
                oldTop.name = "front";
                oldFront.name = "bottom";
                oldBottom.name = "back";

                PrefabUtility.SaveAsPrefabAsset(prefabContents, _selectedPrefabPath);
                Debug.Log("[Semantic Annotator] Rotated directional point naming by 90° around X-axis");

                EditorUtility.DisplayDialog("Success",
                    "Rotated directional point naming by 90° around X-axis.\n\n" +
                    "• back → top\n" +
                    "• top → front\n" +
                    "• front → bottom\n" +
                    "• bottom → back\n\n" +
                    "Normals remain unchanged (truthful to geometry).\n" +
                    "Click 'Refresh Prefabs' to recalculate R_ls.",
                    "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            Repaint();
        }

        /// <summary>
        /// Helper to create a semantic point child GameObject.
        /// </summary>
        void CreatePoint(Transform parent, string name, Vector3 localPos, Vector3 normal = default)
        {
            GameObject pointObj = new GameObject(name);
            pointObj.transform.SetParent(parent, false);
            pointObj.transform.localPosition = localPos;

            SemanticPointMarker marker = pointObj.AddComponent<SemanticPointMarker>();
            marker.normal = normal;
        }

        /// <summary>
        /// Calculates combined bounds from all renderers in local space.
        /// </summary>
        Bounds CalculateCombinedBounds(Renderer[] renderers, Transform root)
        {
            if (renderers.Length == 0)
                return new Bounds(root.position, Vector3.zero);

            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combined.Encapsulate(renderers[i].bounds);
            }
            return combined;
        }

        /// <summary>
        /// Ensures the prefab has a "pivot" semantic point at LOCAL [0,0,0].
        /// Auto-adds it if missing. This allows tracking pivot location in world space.
        /// </summary>
        void EnsurePivotPointExists(GameObject prefabContents, string prefabPath)
        {
            // Find or create SemanticPoints container
            Transform container = prefabContents.transform.Find("SemanticPoints");
            if (container == null)
            {
                GameObject containerObj = new GameObject("SemanticPoints");
                containerObj.transform.SetParent(prefabContents.transform, false);
                container = containerObj.transform;
            }

            // Check if "pivot" point already exists
            Transform pivotPoint = container.Find("pivot");
            if (pivotPoint != null)
            {
                // Pivot point already exists, no action needed
                return;
            }

            // Create "pivot" point at LOCAL [0,0,0]
            GameObject pointObj = new GameObject("pivot");
            pointObj.transform.SetParent(container, false);
            pointObj.transform.localPosition = Vector3.zero; // Ensure at [0,0,0]
            pointObj.AddComponent<SemanticPointMarker>();

            // Save the prefab with the new pivot point
            PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
            Debug.Log($"[Semantic Annotator] Auto-added 'pivot' semantic point at [0,0,0] to {System.IO.Path.GetFileName(prefabPath)}");
        }
    }
}
