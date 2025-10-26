using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// EditorWindow for selecting which prefab categories (Unity tags) are available to AI.
    /// Selection is persisted to ProjectSettings/AIAssistantPrefabCategories.json
    /// </summary>
    public class PrefabCategoryWindow : EditorWindow
    {
        // UI state
        private Vector2 _scrollPosition;
        private Dictionary<string, bool> _categorySelections;  // tag -> selected
        private Dictionary<string, int> _categoryCounts;       // tag -> prefab count

        /// <summary>
        /// MenuItem to open the Prefab Categories window.
        /// </summary>
        [MenuItem("Window/AI Assistant/Prefab Categories")]
        public static void ShowWindow()
        {
            var window = GetWindow<PrefabCategoryWindow>();
            window.titleContent = new GUIContent("AI Prefab Categories");
            window.Show();
        }

        /// <summary>
        /// Called when window is enabled.
        /// Initializes state and loads saved selection.
        /// </summary>
        void OnEnable()
        {
            _categorySelections = new Dictionary<string, bool>();
            _categoryCounts = new Dictionary<string, int>();

            // Load categories from registry
            LoadCategoriesFromRegistry();
        }

        /// <summary>
        /// Main GUI rendering method.
        /// </summary>
        void OnGUI()
        {
            // Header toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("AI Prefab Categories", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh Prefabs", EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                RefreshPrefabs();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Check if registry exists
            PrefabRegistry registry = PrefabRegistryCache.Load();
            if (registry == null)
            {
                EditorGUILayout.HelpBox("No prefab registry found. Click 'Refresh Prefabs' to scan your project.", MessageType.Info);
                return;
            }

            // Select All / Deselect All buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", GUILayout.Width(100)))
            {
                foreach (var key in new List<string>(_categorySelections.Keys))
                {
                    _categorySelections[key] = true;
                }
                SaveSelection();
                Repaint();  // Force UI update
            }

            if (GUILayout.Button("Deselect All", GUILayout.Width(100)))
            {
                foreach (var key in new List<string>(_categorySelections.Keys))
                {
                    _categorySelections[key] = false;
                }
                SaveSelection();
                Repaint();  // Force UI update
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Category list (scrollable)
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Sort categories alphabetically
            var sortedCategories = _categorySelections.Keys.OrderBy(k => k).ToList();

            bool selectionChanged = false;
            foreach (string tag in sortedCategories)
            {
                int count = _categoryCounts.ContainsKey(tag) ? _categoryCounts[tag] : 0;

                bool wasSelected = _categorySelections[tag];
                bool isSelected = EditorGUILayout.ToggleLeft(
                    $"{tag} ({count} prefab{(count != 1 ? "s" : "")})",
                    wasSelected
                );

                if (isSelected != wasSelected)
                {
                    _categorySelections[tag] = isSelected;
                    selectionChanged = true;
                }
            }

            EditorGUILayout.EndScrollView();

            // Save if selection changed
            if (selectionChanged)
            {
                SaveSelection();
            }

            EditorGUILayout.Space();

            // Footer status
            int selectedCount = _categorySelections.Values.Count(v => v);

            // Count prefabs in SELECTED categories only (what AI will actually use)
            int totalPrefabs = _categorySelections
                .Where(kvp => kvp.Value && _categoryCounts.ContainsKey(kvp.Key))
                .Sum(kvp => _categoryCounts[kvp.Key]);

            if (selectedCount == 0)
            {
                EditorGUILayout.HelpBox("No categories selected - AI will use basic shapes (rectangle/circle)", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField($"{selectedCount} category(ies) selected, {totalPrefabs} prefab(s) available");
            }
        }

        /// <summary>
        /// Loads categories from PrefabRegistry and initializes checkbox states.
        /// </summary>
        private void LoadCategoriesFromRegistry()
        {
            // Get all unique tags
            List<string> allTags = PrefabRegistryCache.GetAllTags();

            // Count prefabs per tag
            PrefabRegistry registry = PrefabRegistryCache.Load();
            _categoryCounts.Clear();

            if (registry != null && registry.prefabs != null)
            {
                foreach (var prefab in registry.prefabs)
                {
                    // Null safety - skip null prefabs or empty tags
                    if (prefab == null || string.IsNullOrEmpty(prefab.prefabTag))
                        continue;

                    string tag = prefab.prefabTag;

                    if (!_categoryCounts.ContainsKey(tag))
                        _categoryCounts[tag] = 0;

                    _categoryCounts[tag]++;
                }
            }

            // Load saved selection
            List<string> savedTags = PrefabCategoryPersistence.LoadSelectedTags();

            // Initialize checkbox state
            _categorySelections.Clear();
            foreach (string tag in allTags)
            {
                bool isSelected = savedTags.Contains(tag);
                _categorySelections[tag] = isSelected;
            }
        }

        /// <summary>
        /// Saves current selection to ProjectSettings.
        /// </summary>
        private void SaveSelection()
        {
            // Build list of selected tags
            List<string> selectedTags = new List<string>();
            foreach (var kvp in _categorySelections)
            {
                if (kvp.Value)  // If checked
                {
                    selectedTags.Add(kvp.Key);
                }
            }

            // Save via persistence utility
            PrefabCategoryPersistence.SaveSelectedTags(selectedTags);
        }

        /// <summary>
        /// Rescans prefabs and refreshes category list.
        /// </summary>
        private void RefreshPrefabs()
        {
            // Trigger prefab scan
            PrefabScanner.ScanAll();

            // Invalidate cache
            PrefabRegistryCache.Invalidate();

            // Reload categories
            LoadCategoriesFromRegistry();

            Debug.Log("[AI Assistant] Refreshed prefab categories");
        }
    }
}
