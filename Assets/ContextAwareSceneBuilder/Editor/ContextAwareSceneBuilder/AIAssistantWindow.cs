using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// Window mode enum for switching between Scene Builder and Example Builder workflows.
    /// </summary>
    public enum WindowMode
    {
        SceneBuilder,
        ExampleBuilder
    }

    /// <summary>
    /// Serializable state container for each window mode.
    /// Survives domain reload via Unity serialization.
    /// </summary>
    [System.Serializable]
    public class ModeState
    {
        public WindowMode mode;
        public string responseId;
        public List<string> logs = new List<string>();
        public string prompt = "";
        public Vector2 scrollPosition;
    }

    /// <summary>
    /// Main EditorWindow for the AI Assistant plugin.
    /// Three-column layout: Conversation (40%) | Prompt Library (30%) | Preview (30%)
    /// Supports Scene Builder and Example Builder modes with isolated conversation state.
    /// </summary>
    public class AIAssistantWindow : EditorWindow
    {
        // ============================================================================
        // SERIALIZED STATE (survives domain reload)
        // ============================================================================

        [SerializeField] private List<ModeState> _modeStates = new List<ModeState>();
        [SerializeField] private bool _isExampleMode = false;
        [SerializeField] private string _newExampleName = "";
        [SerializeField] private string _selectedPromptPath = "";

        // ============================================================================
        // NON-SERIALIZED STATE (ephemeral)
        // ============================================================================

        // Settings references
        private AIAssistantSettings _settings;
        private PromptLibrarySettings _promptSettings;

        // Pending actions (ephemeral - cleared on domain reload)
        private List<IAction> _pendingActions;
        private bool[] _actionCheckboxes;

        // UI state
        private string _lastAIMessage = ""; // For copy button
        private List<string> _allPromptPaths = new List<string>();
        private Dictionary<string, bool> _expandedFolders = new Dictionary<string, bool>();

        // Scroll positions for UI panels
        private Vector2 _treeScrollPosition;
        private Vector2 _previewScrollPosition;

        // Cached GUIStyles for visual styling
        private GUIStyle _richTextStyle;
        private GUIStyle _whiteBoxStyle;
        private GUIStyle _whiteBorderedStyle;
        private GUIStyle _styledTextAreaStyle;
        private GUIStyle _previewTextStyle;
        private GUIStyle _treeLabelStyle;

        // ============================================================================
        // HELPER PROPERTIES
        // ============================================================================

        /// <summary>
        /// Gets the current mode state with null-safety.
        /// Creates new state if not found.
        /// </summary>
        private ModeState CurrentState
        {
            get
            {
                var mode = _isExampleMode ? WindowMode.ExampleBuilder : WindowMode.SceneBuilder;
                var state = _modeStates.Find(s => s.mode == mode);

                if (state == null)
                {
                    Debug.LogError($"[AI Assistant] Mode state not found for {mode}. Reinitializing.");
                    state = new ModeState { mode = mode };
                    _modeStates.Add(state);
                }

                return state;
            }
        }

        /// <summary>
        /// Gets or sets the current response ID for conversation continuity.
        /// </summary>
        private string CurrentResponseId
        {
            get => CurrentState?.responseId;
            set { if (CurrentState != null) CurrentState.responseId = value; }
        }

        /// <summary>
        /// Gets the current log entries for the active mode.
        /// </summary>
        private List<string> CurrentLog => CurrentState?.logs ?? new List<string>();

        /// <summary>
        /// Gets or sets the current prompt text for the active mode.
        /// </summary>
        private string CurrentPrompt
        {
            get => CurrentState?.prompt ?? "";
            set { if (CurrentState != null) CurrentState.prompt = value; }
        }

        /// <summary>
        /// Gets or sets the current scroll position for the active mode.
        /// </summary>
        private Vector2 CurrentScrollPosition
        {
            get => CurrentState?.scrollPosition ?? Vector2.zero;
            set { if (CurrentState != null) CurrentState.scrollPosition = value; }
        }

        // ============================================================================
        // UNITY LIFECYCLE
        // ============================================================================

        /// <summary>
        /// MenuItem to open the Context-Aware Scene Builder window.
        /// </summary>
        [MenuItem("Window/Context-Aware Scene Builder/Scene Builder")]
        static void ShowWindow()
        {
            var window = GetWindow<AIAssistantWindow>();
            window.titleContent = new GUIContent("Context-Aware Scene Builder");
            window.minSize = new Vector2(1200, 600); // Minimum size to prevent column overlap
            window.Show();
        }

        /// <summary>
        /// Called when window is enabled.
        /// Initializes mode states, loads settings, and subscribes to events.
        /// </summary>
        void OnEnable()
        {
            _settings = AIAssistantSettings.GetOrCreateSettings();
            _promptSettings = PromptLibrarySettings.GetOrCreateSettings();

            // Initialize mode states FIRST (before accessing CurrentState)
            if (_modeStates.Count == 0)
            {
                _modeStates.Add(new ModeState { mode = WindowMode.SceneBuilder });
                _modeStates.Add(new ModeState { mode = WindowMode.ExampleBuilder });

                // Add initial log message to Scene Builder mode
                var sceneBuilderState = _modeStates.Find(s => s.mode == WindowMode.SceneBuilder);
                if (sceneBuilderState != null && sceneBuilderState.logs.Count == 0)
                {
                    sceneBuilderState.logs.Add($"[{DateTime.Now.ToString("HH:mm:ss")}] [System] AI Assistant ready. Enter a prompt to begin.");
                }
            }

            RefreshPromptList();
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        /// <summary>
        /// Called when window is disabled.
        /// Unsubscribes from events.
        /// </summary>
        void OnDisable()
        {
            EditorSceneManager.sceneSaved -= OnSceneSaved;
        }

        // ============================================================================
        // MAIN GUI RENDERING
        // ============================================================================

        /// <summary>
        /// Main GUI rendering method.
        /// Renders three-column layout with keyboard shortcuts.
        /// </summary>
        void OnGUI()
        {
            // Initialize styles on first OnGUI (EditorStyles not available in OnEnable)
            EnsureStylesInitialized();

            // Handle keyboard shortcuts
            if (Event.current.type == EventType.KeyDown)
            {
                // Ctrl+Enter to submit prompt
                if (Event.current.Equals(Event.KeyboardEvent("^return")))
                {
                    OnSubmitPrompt();
                    Event.current.Use();
                }
                // Ctrl+Shift+C to copy last response
                else if (Event.current.control && Event.current.shift && Event.current.keyCode == KeyCode.C)
                {
                    OnCopyLastResponse();
                    Event.current.Use();
                }
            }

            // Header
            DisplayHeader();

            GUILayout.Space(5);

            // Three-column layout
            EditorGUILayout.BeginHorizontal();

            // Left column: Conversation (40%)
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.4f));
            DisplayConversationColumn();
            EditorGUILayout.EndVertical();

            // Vertical divider
            DrawVerticalSeparator(1f, 5f);

            // Middle column: Prompt Library (30%)
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.3f));
            DisplayPromptLibraryColumn();
            EditorGUILayout.EndVertical();

            // Vertical divider
            DrawVerticalSeparator(1f, 5f);

            // Right column: Preview (30%)
            EditorGUILayout.BeginVertical();
            DisplayPreviewColumn();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Displays header with mode indicator.
        /// </summary>
        void DisplayHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            string modeLabel = _isExampleMode ? "AI Assistant - Example Mode" : "AI Assistant - Scene Builder";
            GUILayout.Label(modeLabel, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            // Keep Copy Last Response in toolbar (most frequently used)
            if (GUILayout.Button("Copy Last Response (Ctrl+Shift+C)", EditorStyles.toolbarButton, GUILayout.Width(200)))
            {
                OnCopyLastResponse();
            }

            EditorGUILayout.EndHorizontal();
        }

        // ============================================================================
        // LEFT COLUMN: CONVERSATION
        // ============================================================================

        /// <summary>
        /// Displays the conversation column (left 40%).
        /// </summary>
        void DisplayConversationColumn()
        {
            // Conversation header with Clear button
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Conversation", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                OnClearConversation();
            }
            EditorGUILayout.EndHorizontal();

            // Log area (scrollable with white background)
            CurrentScrollPosition = EditorGUILayout.BeginScrollView(CurrentScrollPosition, GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginVertical(_whiteBorderedStyle);

            if (CurrentLog.Count > 0)
            {
                foreach (var entry in CurrentLog)
                {
                    EditorGUILayout.LabelField(entry, _richTextStyle);
                }
            }
            else
            {
                EditorGUILayout.LabelField("No messages yet.", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();

            GUILayout.Space(5);

            // Pending actions (if any)
            DisplayPendingActions();

            GUILayout.Space(5);

            // Prompt input (styled with white background)
            EditorGUILayout.LabelField("Your Prompt:", EditorStyles.boldLabel);
            CurrentPrompt = EditorGUILayout.TextArea(CurrentPrompt, _styledTextAreaStyle, GUILayout.Height(60));

            // Submit and Refresh buttons side by side
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Submit (Ctrl+Enter)", GUILayout.Height(30)))
            {
                OnSubmitPrompt();
            }
            if (GUILayout.Button("Refresh Scene Index", GUILayout.Height(30), GUILayout.Width(150)))
            {
                OnRefreshIndex();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Exit Example Mode button (greyed when not in example mode)
            GUI.enabled = _isExampleMode;
            if (GUILayout.Button("Exit Example Mode", GUILayout.Height(25)))
            {
                OnExitExampleMode();
            }
            GUI.enabled = true;
        }

        /// <summary>
        /// Displays pending actions with checkboxes for user approval.
        /// Only visible when actions are pending.
        /// </summary>
        void DisplayPendingActions()
        {
            if (_pendingActions != null && _pendingActions.Count > 0)
            {
                EditorGUILayout.LabelField("Pending Actions:", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Check the actions you want to execute, then click 'Execute Selected'.", MessageType.Info);

                for (int i = 0; i < _pendingActions.Count; i++)
                {
                    _actionCheckboxes[i] = EditorGUILayout.Toggle(
                        _pendingActions[i].GetDescription(),
                        _actionCheckboxes[i]
                    );
                }

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Execute Selected", GUILayout.Height(25)))
                {
                    OnExecuteSelected();
                }

                if (GUILayout.Button("Reject All", GUILayout.Height(25)))
                {
                    OnRejectAll();
                }

                EditorGUILayout.EndHorizontal();

                GUILayout.Space(10);
            }
        }

        // ============================================================================
        // MIDDLE COLUMN: PROMPT LIBRARY TREE
        // ============================================================================

        /// <summary>
        /// Displays the prompt library column (middle 30%).
        /// 70% tree view, 30% example creation controls.
        /// </summary>
        void DisplayPromptLibraryColumn()
        {
            EditorGUILayout.LabelField("Prompt Library", EditorStyles.boldLabel);

            // Tree view (70% of column height, styled)
            float columnHeight = position.height - 100; // Account for header/footer
            _treeScrollPosition = EditorGUILayout.BeginScrollView(_treeScrollPosition, GUILayout.Height(columnHeight * 0.7f));

            EditorGUILayout.BeginVertical(_whiteBoxStyle);

            // Build folder hierarchy
            Dictionary<string, List<string>> folderStructure = BuildFolderStructure();

            // Draw root level folders
            foreach (var folder in folderStructure.Keys.OrderBy(k => k))
            {
                DrawFolder(folder, folderStructure);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();

            GUILayout.Space(10);

            // Example creation controls (30% of column height)
            EditorGUILayout.LabelField("Example Creation", EditorStyles.boldLabel);

            _newExampleName = EditorGUILayout.TextField("Example Name:", _newExampleName);

            if (GUILayout.Button("Create New Example", GUILayout.Height(25)))
            {
                OnCreateNewExample();
            }

            GUI.enabled = !string.IsNullOrEmpty(_selectedPromptPath);
            if (GUILayout.Button("Refine Selected Example", GUILayout.Height(25)))
            {
                OnRefineSelectedExample();
            }
            GUI.enabled = true;

            if (GUILayout.Button("Refresh Prompts", GUILayout.Height(25)))
            {
                RefreshPromptList();
            }
        }

        /// <summary>
        /// Builds folder structure from prompt paths.
        /// Copied from PromptLibraryEditor.cs.
        /// </summary>
        private Dictionary<string, List<string>> BuildFolderStructure()
        {
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

        /// <summary>
        /// Extracts top-level folder name from path.
        /// Copied from PromptLibraryEditor.cs.
        /// </summary>
        private string GetTopLevelFolder(string path)
        {
            int slashIndex = path.IndexOf('/');
            if (slashIndex > 0)
            {
                return path.Substring(0, slashIndex);
            }
            return "Root";
        }

        /// <summary>
        /// Draws a folder with foldout.
        /// Copied from PromptLibraryEditor.cs.
        /// </summary>
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

        /// <summary>
        /// Draws a prompt item with checkbox and selectable label.
        /// Copied from PromptLibraryEditor.cs.
        /// </summary>
        private void DrawPromptItem(string promptPath)
        {
            EditorGUILayout.BeginHorizontal();

            // Checkbox for enable/disable
            bool isEnabled = _promptSettings.IsPromptEnabled(promptPath);
            bool newEnabled = EditorGUILayout.Toggle(isEnabled, GUILayout.Width(20));

            if (newEnabled != isEnabled)
            {
                _promptSettings.SetPromptEnabled(promptPath, newEnabled);
                Repaint(); // Immediate visual feedback
            }

            // Get just the filename for display
            string fileName = Path.GetFileName(promptPath);

            // Use dark mode selection style
            bool isSelected = _selectedPromptPath == promptPath;

            // Selectable label
            Rect labelRect = GUILayoutUtility.GetRect(new GUIContent(fileName), _treeLabelStyle);
            if (GUI.Button(labelRect, fileName, _treeLabelStyle))
            {
                _selectedPromptPath = promptPath;
                Repaint();
            }

            // Highlight selected item with dark mode compatible color
            if (isSelected)
            {
                EditorGUI.DrawRect(labelRect, new Color(0.3f, 0.5f, 0.8f, 0.5f)); // Slightly more visible on dark background
                GUI.Label(labelRect, fileName, _treeLabelStyle);
            }

            EditorGUILayout.EndHorizontal();
        }

        // ============================================================================
        // RIGHT COLUMN: PREVIEW PANE
        // ============================================================================

        /// <summary>
        /// Displays the preview column (right 30%).
        /// Shows selected prompt content.
        /// Copied from PromptLibraryEditor.cs.
        /// </summary>
        void DisplayPreviewColumn()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            if (string.IsNullOrEmpty(_selectedPromptPath))
            {
                EditorGUILayout.HelpBox("Select a prompt to preview its content", MessageType.Info);
                return;
            }

            _previewScrollPosition = EditorGUILayout.BeginScrollView(_previewScrollPosition, GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginVertical(_whiteBorderedStyle);

            // Show filename
            EditorGUILayout.LabelField("File:", _selectedPromptPath, _richTextStyle);
            EditorGUILayout.Space();

            // Load and show content
            string fullPath = PromptLibraryLoader.GetFullPath(_selectedPromptPath);
            if (File.Exists(fullPath))
            {
                try
                {
                    string content = File.ReadAllText(fullPath);
                    EditorGUILayout.TextArea(content, _previewTextStyle, GUILayout.ExpandHeight(true));
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

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        // ============================================================================
        // PROMPT LIBRARY MANAGEMENT
        // ============================================================================

        /// <summary>
        /// Refreshes the prompt list by scanning the PromptLibrary directory.
        /// Copied from PromptLibraryEditor.cs.
        /// </summary>
        private void RefreshPromptList()
        {
            _allPromptPaths = PromptLibraryLoader.ScanPromptFiles();
            _allPromptPaths.Sort();
            Repaint();
        }

        // ============================================================================
        // EVENT HANDLERS - CONVERSATION
        // ============================================================================

        /// <summary>
        /// Handles prompt submission.
        /// Main flow: validate → check dirty scene → index → build context → API call → display results.
        /// </summary>
        void OnSubmitPrompt()
        {
            // Validate prompt
            if (string.IsNullOrWhiteSpace(CurrentPrompt))
            {
                AppendLog("[System] Please enter a prompt", LogType.Warning);
                return;
            }

            // Validate API key
            if (_settings == null || !_settings.ValidateAPIKey())
            {
                AppendLog("[Error] API key not set. Please configure it in the AIAssistantSettings ScriptableObject.", LogType.Error);
                return;
            }

            try
            {
                // Step 1: Check if scene needs re-indexing
                EditorUtility.DisplayProgressBar("AI Assistant", "Checking scene state...", 0.1f);

                if (SceneManager.GetActiveScene().isDirty)
                {
                    AppendLog("[System] Scene has unsaved changes, re-indexing...", LogType.Log);
                    ProjectIndexer.IndexAll();
                }

                // Step 2: Build context pack
                EditorUtility.DisplayProgressBar("AI Assistant", "Building context...", 0.3f);

                string systemMessage, toolsJson;
                string userMessage = ContextBuilder.BuildContextPack(CurrentPrompt, _settings.TokenBudget, out systemMessage, out toolsJson);

                // Step 3: Call OpenAI API
                EditorUtility.DisplayProgressBar("AI Assistant", "Calling OpenAI API...", 0.6f);

                var plan = OpenAIClient.SendRequest(_settings, systemMessage, userMessage, CurrentResponseId, null, toolsJson);

                // Step 4: Process response
                if (!plan.Success)
                {
                    AppendLog($"[Error] {plan.ErrorMessage}", LogType.Error);
                }
                else
                {
                    // Display AI message if present
                    if (!string.IsNullOrEmpty(plan.Message))
                    {
                        _lastAIMessage = plan.Message;
                        AppendLog($"[AI] {plan.Message}", LogType.Log);
                    }

                    // Display pending actions if present
                    if (plan.Actions != null && plan.Actions.Count > 0)
                    {
                        _pendingActions = plan.Actions;
                        _actionCheckboxes = new bool[plan.Actions.Count];
                        AppendLog($"[System] {plan.Actions.Count} action(s) pending approval", LogType.Log);
                        Repaint();
                    }
                    else if (string.IsNullOrEmpty(plan.Message))
                    {
                        AppendLog("[AI] (No response)", LogType.Warning);
                    }

                    // Save response ID for conversation continuity
                    CurrentResponseId = plan.ResponseId;
                }

                // Clear prompt for next input
                CurrentPrompt = "";
                Repaint();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Executes selected actions via DynamicPlanApplier.
        /// </summary>
        void OnExecuteSelected()
        {
            if (_pendingActions == null || _actionCheckboxes == null)
            {
                return;
            }

            // Filter selected actions
            var selectedActions = new List<IAction>();
            for (int i = 0; i < _pendingActions.Count; i++)
            {
                if (_actionCheckboxes[i])
                {
                    selectedActions.Add(_pendingActions[i]);
                }
            }

            if (selectedActions.Count == 0)
            {
                AppendLog("[System] No actions selected", LogType.Warning);
                return;
            }

            // Execute actions
            AppendLog($"[System] Executing {selectedActions.Count} action(s)...", LogType.Log);

            var results = DynamicPlanApplier.ApplyPlan(selectedActions, _settings.PreviewMode);

            // Save scene before re-indexing
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.isDirty)
            {
                EditorSceneManager.SaveScene(activeScene);
            }

            // Re-index to prevent conversation desync
            ProjectIndexer.IndexAll();

            // Log results (with colored output for better UX)
            int successCount = 0;
            foreach (var result in results)
            {
                if (result.Success)
                {
                    AppendLog($"<color=#00ff00>✓ {result.Action.GetDescription()}</color>", LogType.Log);
                    successCount++;
                }
                else
                {
                    AppendLog($"<color=#ff0000>✗ {result.Action.GetDescription()} - {result.ErrorMessage}</color>", LogType.Error);
                }
            }

            AppendLog($"[System] Executed {successCount}/{results.Count} action(s)", LogType.Log);

            // Submit tool outputs back to OpenAI
            if (CurrentResponseId != null && results.Count > 0)
            {
                AppendLog("[System] Submitting tool execution results to OpenAI...", LogType.Log);

                try
                {
                    EditorUtility.DisplayProgressBar("AI Assistant", "Submitting tool results...", 0.5f);

                    string systemMessage, toolsJson;
                    string userMessage = ContextBuilder.BuildContextPack("", _settings.TokenBudget, out systemMessage, out toolsJson);
                    var plan = OpenAIClient.SendRequest(_settings, systemMessage, userMessage, CurrentResponseId, results, toolsJson);

                    if (plan.Success && !string.IsNullOrEmpty(plan.Message))
                    {
                        AppendLog($"[AI] {plan.Message}", LogType.Log);
                    }

                    CurrentResponseId = plan.ResponseId;
                }
                catch (Exception ex)
                {
                    AppendLog($"<color=#ff0000>[Error] Failed to submit tool outputs: {ex.Message}</color>", LogType.Error);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            // Clear pending state
            _pendingActions = null;
            _actionCheckboxes = null;
            Repaint();
        }

        /// <summary>
        /// Rejects all pending actions.
        /// </summary>
        void OnRejectAll()
        {
            if (_pendingActions != null)
            {
                AppendLog($"<color=#ffaa00>[System] Rejected {_pendingActions.Count} pending action(s)</color>", LogType.Warning);
                _pendingActions = null;
                _actionCheckboxes = null;
                Repaint();
            }
        }

        /// <summary>
        /// Clears conversation state by resetting the response ID.
        /// </summary>
        void OnClearConversation()
        {
            CurrentResponseId = null;
            AppendLog("[System] Conversation cleared. Next prompt will start a new conversation.", LogType.Log);
        }

        /// <summary>
        /// Copies the last AI response to clipboard.
        /// </summary>
        void OnCopyLastResponse()
        {
            if (!string.IsNullOrEmpty(_lastAIMessage))
            {
                EditorGUIUtility.systemCopyBuffer = _lastAIMessage;
                AppendLog("[System] Copied last AI response to clipboard", LogType.Log);
            }
            else
            {
                AppendLog("[System] No AI response to copy", LogType.Warning);
            }
        }

        /// <summary>
        /// Manually triggers full project re-indexing.
        /// </summary>
        void OnRefreshIndex()
        {
            AppendLog("[System] Manually refreshing project index...", LogType.Log);
            ProjectIndexer.IndexAll();
            AppendLog("[System] Index refresh complete", LogType.Log);
        }

        /// <summary>
        /// Scene save callback for auto-indexing.
        /// </summary>
        private void OnSceneSaved(Scene scene)
        {
            if (_settings != null && _settings.AutoIndexOnSave)
            {
                ProjectIndexer.IndexAll();
                AppendLog($"[System] Auto-indexed after saving scene: {scene.name}", LogType.Log);
            }
        }

        // ============================================================================
        // EVENT HANDLERS - EXAMPLE MODE (STUBBED FOR PHASE 2)
        // ============================================================================

        /// <summary>
        /// Handles "Create New Example" button.
        /// STUBBED - Full implementation in Phase 3.
        /// </summary>
        void OnCreateNewExample()
        {
            EditorUtility.DisplayDialog("Not Implemented",
                "Example Mode functionality will be implemented in Phase 3.\n\n" +
                "This will allow you to create teaching examples for the prompt library.",
                "OK");
        }

        /// <summary>
        /// Handles "Refine Selected Example" button.
        /// STUBBED - Full implementation in Phase 3.
        /// </summary>
        void OnRefineSelectedExample()
        {
            EditorUtility.DisplayDialog("Not Implemented",
                $"Example Mode functionality will be implemented in Phase 3.\n\n" +
                $"Selected prompt: {_selectedPromptPath}",
                "OK");
        }

        /// <summary>
        /// Handles "Exit Example Mode" button.
        /// STUBBED - Full implementation in Phase 3.
        /// </summary>
        void OnExitExampleMode()
        {
            EditorUtility.DisplayDialog("Not Implemented",
                "Example Mode functionality will be implemented in Phase 3.",
                "OK");
        }

        // ============================================================================
        // LOGGING
        // ============================================================================

        /// <summary>
        /// Appends a timestamped message to the current mode's log.
        /// Supports rich text color tags for better UX.
        /// </summary>
        void AppendLog(string message, LogType type)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string formattedMessage = $"[{timestamp}] {message}";

            CurrentLog.Add(formattedMessage);

            // Limit to 1000 entries to prevent unbounded growth
            if (CurrentLog.Count > 1000)
            {
                CurrentLog.RemoveRange(0, 100); // Remove oldest 100 entries
                CurrentLog.Insert(0, "[System] (Log trimmed - oldest entries removed)");
            }

            // Log errors to Unity Console for debugging
            if (type == LogType.Error)
            {
                Debug.LogWarning($"[AI Assistant] {message}");
            }

            Repaint();
        }

        // ============================================================================
        // STYLING UTILITIES
        // ============================================================================

        /// <summary>
        /// Ensures all GUIStyles are initialized before use.
        /// Safe to call multiple times - only initializes once.
        /// </summary>
        private void EnsureStylesInitialized()
        {
            if (_richTextStyle == null || _whiteBoxStyle == null ||
                _whiteBorderedStyle == null || _styledTextAreaStyle == null ||
                _previewTextStyle == null || _treeLabelStyle == null)
            {
                InitializeStyles();
            }
        }

        /// <summary>
        /// Creates a solid color texture for GUIStyle backgrounds.
        /// </summary>
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        /// <summary>
        /// Draws a vertical separator line between columns.
        /// </summary>
        private void DrawVerticalSeparator(float width = 1f, float padding = 5f)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width + padding * 2));

            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(width), GUILayout.ExpandHeight(true));
            rect.x += padding;
            rect.width = width;

            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f)); // Grey separator

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Initializes all custom GUIStyles.
        /// Called on first OnGUI when EditorStyles are available.
        /// </summary>
        private void InitializeStyles()
        {
            try
            {
                // Dark mode color palette
                Color darkGrey = new Color(0.22f, 0.22f, 0.22f); // Main background
                Color mediumGrey = new Color(0.27f, 0.27f, 0.27f); // Slightly lighter for contrast
                Color darkestGrey = new Color(0.18f, 0.18f, 0.18f); // Text boxes
                Color lightText = new Color(0.85f, 0.85f, 0.85f); // Light grey text
                Color highlightGrey = new Color(0.32f, 0.32f, 0.32f); // Focused state

                // Rich text style for colored logs (with light text on dark background)
                _richTextStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                _richTextStyle.richText = true;
                _richTextStyle.normal.textColor = lightText;
                _richTextStyle.wordWrap = true;

                // Dark grey background box (clean, no border) - for prompt library tree
                _whiteBoxStyle = new GUIStyle(GUI.skin.box);
                _whiteBoxStyle.normal.background = MakeTex(2, 2, darkGrey);
                _whiteBoxStyle.padding = new RectOffset(10, 10, 10, 10);
                _whiteBoxStyle.normal.textColor = lightText;

                // Dark grey background with subtle border - for conversation log
                _whiteBorderedStyle = new GUIStyle(EditorStyles.helpBox);
                _whiteBorderedStyle.normal.background = MakeTex(2, 2, mediumGrey);
                _whiteBorderedStyle.border = new RectOffset(1, 1, 1, 1);
                _whiteBorderedStyle.padding = new RectOffset(8, 8, 8, 8);
                _whiteBorderedStyle.normal.textColor = lightText;

                // Styled text area with dark background and border - for prompt input
                _styledTextAreaStyle = new GUIStyle(EditorStyles.textArea);
                _styledTextAreaStyle.normal.background = MakeTex(2, 2, darkestGrey);
                _styledTextAreaStyle.normal.textColor = lightText;
                _styledTextAreaStyle.focused.background = MakeTex(2, 2, highlightGrey);
                _styledTextAreaStyle.focused.textColor = Color.white;
                _styledTextAreaStyle.border = new RectOffset(2, 2, 2, 2);
                _styledTextAreaStyle.padding = new RectOffset(5, 5, 5, 5);

                // Preview text style - read-only text area for displaying prompt content
                _previewTextStyle = new GUIStyle(EditorStyles.textArea);
                _previewTextStyle.normal.background = MakeTex(2, 2, darkestGrey);
                _previewTextStyle.normal.textColor = lightText;
                _previewTextStyle.wordWrap = true;
                _previewTextStyle.padding = new RectOffset(5, 5, 5, 5);

                // Tree label style - for prompt library file/folder names
                _treeLabelStyle = new GUIStyle(EditorStyles.label);
                _treeLabelStyle.normal.textColor = lightText;
                _treeLabelStyle.hover.textColor = Color.white;
                _treeLabelStyle.active.textColor = Color.white;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to initialize styles: {ex.Message}");

                // Fallback to basic styles
                _richTextStyle = EditorStyles.wordWrappedLabel;
                _whiteBoxStyle = GUI.skin.box;
                _whiteBorderedStyle = EditorStyles.helpBox;
                _styledTextAreaStyle = EditorStyles.textArea;
                _previewTextStyle = EditorStyles.wordWrappedLabel;
                _treeLabelStyle = EditorStyles.label;
            }
        }
    }
}
