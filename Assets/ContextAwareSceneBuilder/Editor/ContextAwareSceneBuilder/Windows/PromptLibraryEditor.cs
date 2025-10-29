using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// Editor window for managing the Prompt Library.
    /// Displays prompts in a tree view with enable/disable checkboxes and preview pane.
    /// </summary>
    public class PromptLibraryEditor : EditorWindow
    {
        private PromptLibrarySettings _settings;
        private List<string> _allPromptPaths = new List<string>();
        private Dictionary<string, bool> _expandedFolders = new Dictionary<string, bool>();
        private string _selectedPromptPath;
        private Vector2 _treeScrollPosition;
        private Vector2 _previewScrollPosition;

        [MenuItem("Window/Context-Aware Scene Builder/Prompt Library")]
        public static void ShowWindow()
        {
            PromptLibraryEditor window = GetWindow<PromptLibraryEditor>("Prompt Library");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            _settings = PromptLibrarySettings.GetOrCreateSettings();
            RefreshPromptList();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            // Header with refresh button
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Prompt Library", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshPromptList();
            }
            EditorGUILayout.EndHorizontal();

            // Split view: Tree on left, Preview on right
            EditorGUILayout.BeginHorizontal();

            // Left panel: Tree view
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.5f));
            DrawTreeView();
            EditorGUILayout.EndVertical();

            // Right panel: Preview
            EditorGUILayout.BeginVertical();
            DrawPreviewPane();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void RefreshPromptList()
        {
            _allPromptPaths = PromptLibraryLoader.ScanPromptFiles();
            _allPromptPaths.Sort();
            Repaint();
        }

        private void DrawTreeView()
        {
            EditorGUILayout.LabelField("Prompts", EditorStyles.boldLabel);

            _treeScrollPosition = EditorGUILayout.BeginScrollView(_treeScrollPosition);

            // Build folder hierarchy
            Dictionary<string, List<string>> folderStructure = BuildFolderStructure();

            // Draw root level folders
            foreach (var folder in folderStructure.Keys.OrderBy(k => k))
            {
                DrawFolder(folder, folderStructure);
            }

            EditorGUILayout.EndScrollView();
        }

        private Dictionary<string, List<string>> BuildFolderStructure()
        {
            // Group prompts by their top-level folder
            Dictionary<string, List<string>> structure = new Dictionary<string, List<string>>();

            foreach (string path in _allPromptPaths)
            {
                string folder = GetTopLevelFolder(path);
                if (!structure.ContainsKey(folder))
                {
                    structure[folder] = new List<string>();
                }
                structure[folder].Add(path);
            }

            return structure;
        }

        private string GetTopLevelFolder(string path)
        {
            int slashIndex = path.IndexOf('/');
            if (slashIndex > 0)
            {
                return path.Substring(0, slashIndex);
            }
            return "Root";
        }

        private void DrawFolder(string folderName, Dictionary<string, List<string>> folderStructure)
        {
            // Get expanded state
            if (!_expandedFolders.ContainsKey(folderName))
            {
                _expandedFolders[folderName] = true; // Default to expanded
            }

            EditorGUILayout.BeginHorizontal();

            // Foldout for folder
            bool expanded = EditorGUILayout.Foldout(_expandedFolders[folderName], folderName, true);
            _expandedFolders[folderName] = expanded;

            EditorGUILayout.EndHorizontal();

            // Draw contents if expanded
            if (expanded && folderStructure.ContainsKey(folderName))
            {
                EditorGUI.indentLevel++;
                foreach (string promptPath in folderStructure[folderName].OrderBy(p => p))
                {
                    DrawPromptItem(promptPath);
                }
                EditorGUI.indentLevel--;
            }
        }

        private void DrawPromptItem(string promptPath)
        {
            EditorGUILayout.BeginHorizontal();

            // Checkbox for enable/disable
            bool isEnabled = _settings.IsPromptEnabled(promptPath);
            bool newEnabled = EditorGUILayout.Toggle(isEnabled, GUILayout.Width(20));

            if (newEnabled != isEnabled)
            {
                _settings.SetPromptEnabled(promptPath, newEnabled);
            }

            // Get just the filename for display
            string fileName = Path.GetFileName(promptPath);

            // Use Unity's built-in selection style
            bool isSelected = _selectedPromptPath == promptPath;
            GUIStyle labelStyle = isSelected ? new GUIStyle(EditorStyles.label) { normal = { background = EditorStyles.helpBox.normal.background } } : EditorStyles.label;

            // Selectable label - use SelectableLabel for proper selection behavior
            Rect labelRect = GUILayoutUtility.GetRect(new GUIContent(fileName), labelStyle);
            if (GUI.Button(labelRect, fileName, labelStyle))
            {
                _selectedPromptPath = promptPath;
            }

            // Highlight selected item
            if (isSelected)
            {
                EditorGUI.DrawRect(labelRect, new Color(0.3f, 0.5f, 0.8f, 0.3f));
                GUI.Label(labelRect, fileName);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreviewPane()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            if (string.IsNullOrEmpty(_selectedPromptPath))
            {
                EditorGUILayout.HelpBox("Select a prompt to preview its content", MessageType.Info);
                return;
            }

            _previewScrollPosition = EditorGUILayout.BeginScrollView(_previewScrollPosition);

            // Show filename
            EditorGUILayout.LabelField("File:", _selectedPromptPath, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            // Load and show content
            string fullPath = PromptLibraryLoader.GetFullPath(_selectedPromptPath);
            if (File.Exists(fullPath))
            {
                try
                {
                    string content = File.ReadAllText(fullPath);
                    EditorGUILayout.TextArea(content, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
                }
                catch (System.Exception ex)
                {
                    EditorGUILayout.HelpBox($"Failed to load prompt: {ex.Message}", MessageType.Error);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("File not found", MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
