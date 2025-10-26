using UnityEngine;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Settings for the AI Assistant plugin.
    /// Stores user preferences and configuration for GPT-5 Responses API.
    /// API key is stored separately in EditorPrefs for security (never committed to git).
    /// </summary>
    [CreateAssetMenu(fileName = "AIAssistantSettings", menuName = "AI Assistant/Settings")]
    public class AIAssistantSettings : ScriptableObject
    {
        private const string API_KEY_PREF = "AIAssistant_OpenAI_APIKey";
        private const string SETTINGS_PATH = "Assets/Editor/AIAssistant/Settings/AIAssistantSettings.asset";

        [Header("OpenAI Configuration")]
        [Tooltip("GPT-5 model variant (gpt-5, gpt-5-mini, gpt-5-nano)")]
        public string Model = "gpt-5-mini";

        [Header("GPT-5 Reasoning Settings")]
        [Tooltip("How much thinking the model does: minimal (fast), medium (balanced), high (deep reasoning)")]
        public ReasoningEffort ReasoningEffort = ReasoningEffort.Medium;

        [Tooltip("Output verbosity: low (terse), medium (balanced), high (verbose/detailed)")]
        public Verbosity Verbosity = Verbosity.Medium;

        [Header("Context Settings")]
        [Tooltip("Maximum estimated tokens for context pack. GPT-5 supports up to 272,000 input tokens.")]
        public int TokenBudget = 50000;

        [Header("Behavior")]
        [Tooltip("If enabled, automatically refresh index when scenes are saved")]
        public bool AutoIndexOnSave = true;

        [Tooltip("If enabled, log actions but don't execute (for testing)")]
        public bool PreviewMode = false;

        [Header("Prefab Scanning")]
        [Tooltip("Root folder for scanning prefabs (prefabs must be tagged)")]
        public string PrefabScanFolder = "Assets/AIPrefabs";

        [Tooltip("Auto-scan when assets are imported (future feature, not implemented yet)")]
        public bool AutoScanOnAssetChange = false;

        /// <summary>
        /// OpenAI API key stored in EditorPrefs (per-machine, not committed to git).
        /// </summary>
        public string APIKey
        {
            get => EditorPrefs.GetString(API_KEY_PREF, "");
            set => EditorPrefs.SetString(API_KEY_PREF, value);
        }

        /// <summary>
        /// Validates that the API key is set and non-empty.
        /// </summary>
        /// <returns>True if API key appears valid</returns>
        public bool ValidateAPIKey()
        {
            string key = APIKey;
            return !string.IsNullOrEmpty(key);
        }

        /// <summary>
        /// Gets the reasoning effort value as a string for API requests.
        /// </summary>
        public string GetReasoningEffortString()
        {
            switch (ReasoningEffort)
            {
                case ReasoningEffort.Minimal: return "minimal";
                case ReasoningEffort.Medium: return "medium";
                case ReasoningEffort.High: return "high";
                default: return "medium";
            }
        }

        /// <summary>
        /// Gets the verbosity value as a string for API requests.
        /// </summary>
        public string GetVerbosityString()
        {
            switch (Verbosity)
            {
                case Verbosity.Low: return "low";
                case Verbosity.Medium: return "medium";
                case Verbosity.High: return "high";
                default: return "medium";
            }
        }

        /// <summary>
        /// Gets or creates the singleton settings instance.
        /// </summary>
        /// <returns>Settings instance</returns>
        public static AIAssistantSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AIAssistantSettings>(SETTINGS_PATH);

            if (settings == null)
            {
                settings = CreateInstance<AIAssistantSettings>();

                // Ensure directory exists (normalize path for Unity AssetDatabase)
                string directory = System.IO.Path.GetDirectoryName(SETTINGS_PATH);
                // Convert backslashes to forward slashes for Unity
                directory = directory.Replace('\\', '/');

                if (!AssetDatabase.IsValidFolder(directory))
                {
                    string parentFolder = "Assets/Editor/AIAssistant";
                    AssetDatabase.CreateFolder(parentFolder, "Settings");
                }

                AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
                AssetDatabase.SaveAssets();
            }

            return settings;
        }
    }

    /// <summary>
    /// GPT-5 reasoning effort levels.
    /// Controls how much thinking the model does before responding.
    /// </summary>
    public enum ReasoningEffort
    {
        Minimal,  // Few or no reasoning tokens; optimized for speed
        Medium,   // Default; balanced reasoning
        High      // Deep reasoning; slower but more thorough
    }

    /// <summary>
    /// Output verbosity levels.
    /// Controls how expansive the model's responses are.
    /// </summary>
    public enum Verbosity
    {
        Low,      // Terse, minimal prose
        Medium,   // Balanced detail (default)
        High      // Verbose, detailed (good for audits/teaching)
    }

    /// <summary>
    /// Custom editor for AIAssistantSettings to provide API key input field.
    /// </summary>
    [CustomEditor(typeof(AIAssistantSettings))]
    public class AIAssistantSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var settings = (AIAssistantSettings)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("API Key (stored in EditorPrefs)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Your API key is stored locally in EditorPrefs and never committed to version control.", MessageType.Info);

            string currentKey = settings.APIKey;
            string newKey = EditorGUILayout.PasswordField("OpenAI API Key", currentKey);

            if (newKey != currentKey)
            {
                settings.APIKey = newKey;
            }

            if (!settings.ValidateAPIKey())
            {
                EditorGUILayout.HelpBox("API key is not set. Please enter your OpenAI API key.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("✓ API key is set.", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("GPT-5 Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "GPT-5 models:\n" +
                "• gpt-5: Full reasoning model\n" +
                "• gpt-5-mini: Balanced performance/cost\n" +
                "• gpt-5-nano: Low-cost, low-latency\n\n" +
                "Context limit: 272,000 input tokens, 128,000 output tokens",
                MessageType.Info);

            EditorGUILayout.Space();

            // Draw default inspector for other fields
            DrawDefaultInspector();
        }
    }
}
