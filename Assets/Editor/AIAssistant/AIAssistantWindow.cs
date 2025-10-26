using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Main EditorWindow for the AI Assistant plugin.
    /// Provides UI for submitting prompts, reviewing AI responses, approving actions,
    /// and managing conversation state with OpenAI Responses API.
    /// </summary>
    public class AIAssistantWindow : EditorWindow
    {
        // Settings reference
        private AIAssistantSettings _settings;

        // Conversation state
        private string _lastResponseId;

        // Pending actions awaiting user approval
        private List<IAction> _pendingActions;
        private bool[] _actionCheckboxes;

        // UI state
        private string _userPrompt = "";
        private Vector2 _logScrollPosition;
        private List<string> _logEntries;

        /// <summary>
        /// MenuItem to open the AI Assistant window.
        /// </summary>
        [MenuItem("Window/AI Assistant")]
        static void ShowWindow()
        {
            var window = GetWindow<AIAssistantWindow>();
            window.titleContent = new GUIContent("AI Assistant");
            window.Show();
        }

        /// <summary>
        /// Called when window is enabled.
        /// Loads settings and subscribes to scene save events.
        /// </summary>
        void OnEnable()
        {
            _settings = AIAssistantSettings.GetOrCreateSettings();
            _logEntries = new List<string>();
            EditorSceneManager.sceneSaved += OnSceneSaved;

            AppendLog("[System] AI Assistant ready. Enter a prompt to begin.", LogType.Log);
        }

        /// <summary>
        /// Called when window is disabled.
        /// Unsubscribes from events.
        /// </summary>
        void OnDisable()
        {
            EditorSceneManager.sceneSaved -= OnSceneSaved;
        }

        /// <summary>
        /// Main GUI rendering method.
        /// Handles keyboard shortcuts and delegates to display methods.
        /// </summary>
        void OnGUI()
        {
            // Handle Ctrl+Enter to submit prompt
            if (Event.current.Equals(Event.KeyboardEvent("^return")))
            {
                OnSubmitPrompt();
                Event.current.Use();
            }

            DisplayHeader();
            DisplayLogArea();
            DisplayPendingActions();
            DisplayPromptInput();
        }

        /// <summary>
        /// Displays header section with title and utility buttons.
        /// </summary>
        void DisplayHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("AI Assistant", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh Index", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                OnRefreshIndex();
            }

            if (GUILayout.Button("Clear Conversation", EditorStyles.toolbarButton, GUILayout.Width(130)))
            {
                OnClearConversation();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);
        }

        /// <summary>
        /// Displays scrollable log area with timestamped messages.
        /// </summary>
        void DisplayLogArea()
        {
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);

            _logScrollPosition = EditorGUILayout.BeginScrollView(_logScrollPosition, GUILayout.Height(200));

            if (_logEntries != null && _logEntries.Count > 0)
            {
                foreach (var entry in _logEntries)
                {
                    EditorGUILayout.LabelField(entry, EditorStyles.wordWrappedLabel);
                }
            }
            else
            {
                EditorGUILayout.LabelField("No messages yet.", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Displays pending actions with checkboxes for user approval.
        /// Only visible when actions are pending.
        /// </summary>
        void DisplayPendingActions()
        {
            if (_pendingActions != null && _pendingActions.Count > 0)
            {
                GUILayout.Space(10);
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

        /// <summary>
        /// Displays prompt input area with submit button.
        /// </summary>
        void DisplayPromptInput()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Your Prompt:", EditorStyles.boldLabel);

            // Use EditorGUILayout for better keyboard shortcut support
            _userPrompt = EditorGUILayout.TextArea(_userPrompt, GUILayout.Height(60));

            if (GUILayout.Button("Submit (Ctrl+Enter)", GUILayout.Height(30)))
            {
                OnSubmitPrompt();
            }

            GUILayout.Space(5);
        }

        /// <summary>
        /// Handles prompt submission.
        /// Main flow: validate → check dirty scene → index → build context → API call → display results.
        /// Uses try-finally to ensure progress bar is always cleared.
        /// </summary>
        void OnSubmitPrompt()
        {
            // Validate prompt
            if (string.IsNullOrWhiteSpace(_userPrompt))
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

                // Step 2: Build context pack with dynamic tools
                EditorUtility.DisplayProgressBar("AI Assistant", "Building context...", 0.3f);

                string toolsJson;
                string contextPack = ContextBuilder.BuildContextPack(_userPrompt, _settings.TokenBudget, out toolsJson);

                // Step 3: Call OpenAI API with dynamic tools
                EditorUtility.DisplayProgressBar("AI Assistant", "Calling OpenAI API...", 0.6f);

                var plan = OpenAIClient.SendRequest(_settings, contextPack, _lastResponseId, null, toolsJson);

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
                    _lastResponseId = plan.ResponseId;
                }

                // Clear prompt for next input
                _userPrompt = "";
                Repaint();
            }
            finally
            {
                // ALWAYS clear progress bar, even if exception occurred
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Executes selected actions via PlanApplier.
        /// Re-indexes scene immediately after execution to prevent conversation desync.
        /// </summary>
        void OnExecuteSelected()
        {
            Debug.Log("[AI Assistant] OnExecuteSelected called");

            if (_pendingActions == null || _actionCheckboxes == null)
            {
                Debug.Log("[AI Assistant] OnExecuteSelected - pendingActions or actionCheckboxes is null, returning early");
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
                Debug.Log("[AI Assistant] OnExecuteSelected - No actions selected");
                return;
            }

            Debug.Log($"[AI Assistant] OnExecuteSelected - About to execute {selectedActions.Count} actions, PreviewMode={_settings.PreviewMode}");

            // Execute actions with DynamicPlanApplier (supports both Phase 1 and Phase 2 actions)
            AppendLog($"[System] Executing {selectedActions.Count} action(s)...", LogType.Log);

            var results = DynamicPlanApplier.ApplyPlan(selectedActions, _settings.PreviewMode);

            Debug.Log($"[AI Assistant] OnExecuteSelected - ApplyPlan returned {results.Count} results");

            // CRITICAL: Save scene before re-indexing, otherwise newly created objects will be lost
            // when ProjectIndexer restores the scene setup
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.isDirty)
            {
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log($"[AI Assistant] Saved scene {activeScene.name} before re-indexing");
            }

            // CRITICAL: Re-index immediately to prevent conversation desync
            ProjectIndexer.IndexAll();

            // Log results
            int successCount = 0;
            foreach (var result in results)
            {
                if (result.Success)
                {
                    AppendLog($"✓ {result.Action.GetDescription()}", LogType.Log);
                    successCount++;
                }
                else
                {
                    AppendLog($"✗ {result.Action.GetDescription()} - {result.ErrorMessage}", LogType.Error);
                }
            }

            AppendLog($"[System] Executed {successCount}/{results.Count} action(s)", LogType.Log);

            // CRITICAL: Submit tool outputs back to OpenAI to close the loop
            if (_lastResponseId != null && results.Count > 0)
            {
                AppendLog("[System] Submitting tool execution results to OpenAI...", LogType.Log);

                try
                {
                    EditorUtility.DisplayProgressBar("AI Assistant", "Submitting tool results...", 0.5f);

                    string toolsJson;
                    string contextPack = ContextBuilder.BuildContextPack("", _settings.TokenBudget, out toolsJson);
                    var plan = OpenAIClient.SendRequest(_settings, contextPack, _lastResponseId, results, toolsJson);

                    if (plan.Success && !string.IsNullOrEmpty(plan.Message))
                    {
                        AppendLog($"[AI] {plan.Message}", LogType.Log);
                    }

                    // Save new response ID
                    _lastResponseId = plan.ResponseId;
                }
                catch (Exception ex)
                {
                    AppendLog($"[Error] Failed to submit tool outputs: {ex.Message}", LogType.Error);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
            else
            {
                // No previous_response_id, so clear it to start fresh next time
                _lastResponseId = null;
            }

            // Clear pending state
            _pendingActions = null;
            _actionCheckboxes = null;
            Repaint();
        }

        /// <summary>
        /// Rejects all pending actions without executing them.
        /// </summary>
        void OnRejectAll()
        {
            if (_pendingActions != null)
            {
                AppendLog($"[System] Rejected {_pendingActions.Count} pending action(s)", LogType.Warning);
                _pendingActions = null;
                _actionCheckboxes = null;
                Repaint();
            }
        }

        /// <summary>
        /// Clears conversation state by resetting the response ID.
        /// Next prompt will start a new conversation.
        /// </summary>
        void OnClearConversation()
        {
            _lastResponseId = null;
            AppendLog("[System] Conversation cleared. Next prompt will start a new conversation.", LogType.Log);
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
        /// Callback for scene save events.
        /// Automatically re-indexes if AutoIndexOnSave is enabled in settings.
        /// </summary>
        /// <param name="scene">The scene that was saved</param>
        private void OnSceneSaved(Scene scene)
        {
            if (_settings != null && _settings.AutoIndexOnSave)
            {
                ProjectIndexer.IndexAll();
                AppendLog($"[System] Auto-indexed after saving scene: {scene.name}", LogType.Log);
            }
        }

        /// <summary>
        /// Appends a timestamped message to the log area.
        /// Also logs errors to Unity Console for debugging.
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="type">Message type (Log, Warning, Error)</param>
        void AppendLog(string message, LogType type)
        {
            if (_logEntries == null)
            {
                _logEntries = new List<string>();
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string formattedMessage = $"[{timestamp}] {message}";

            _logEntries.Add(formattedMessage);

            // Also log errors to Unity Console for debugging
            if (type == LogType.Error)
            {
                Debug.LogWarning($"[AI Assistant] {message}");
            }

            Repaint();
        }
    }
}
