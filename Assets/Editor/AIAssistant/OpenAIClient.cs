using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using SimpleJSON;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Client for OpenAI Responses API (GPT-5).
    /// Sends requests with context packs and tool definitions, parses responses into ActionPlans.
    /// Handles conversation continuity via previous_response_id.
    /// </summary>
    public class OpenAIClient
    {
        private const string API_ENDPOINT = "https://api.openai.com/v1/responses";

        // Fallback tool definitions for GPT-5 function calling (rectangle/circle from Phase 1)
        // Used when no prefabs are available or selected
        public const string FALLBACK_TOOLS_JSON = @"[
  {
    ""type"": ""function"",
    ""name"": ""createRectangle"",
    ""description"": ""Creates a rectangle sprite GameObject in the scene at the specified world position"",
    ""parameters"": {
      ""type"": ""object"",
      ""properties"": {
        ""name"": { ""type"": ""string"", ""description"": ""GameObject name"" },
        ""x"": { ""type"": ""number"", ""description"": ""World X position"" },
        ""y"": { ""type"": ""number"", ""description"": ""World Y position"" },
        ""width"": { ""type"": ""number"", ""description"": ""Width in world units"" },
        ""height"": { ""type"": ""number"", ""description"": ""Height in world units"" },
        ""color"": { ""type"": ""string"", ""description"": ""Hex color like #FF0000"" }
      },
      ""required"": [""name"", ""x"", ""y"", ""width"", ""height"", ""color""]
    }
  },
  {
    ""type"": ""function"",
    ""name"": ""createCircle"",
    ""description"": ""Creates a circle sprite GameObject in the scene at the specified world position"",
    ""parameters"": {
      ""type"": ""object"",
      ""properties"": {
        ""name"": { ""type"": ""string"", ""description"": ""GameObject name"" },
        ""x"": { ""type"": ""number"", ""description"": ""World X position"" },
        ""y"": { ""type"": ""number"", ""description"": ""World Y position"" },
        ""radius"": { ""type"": ""number"", ""description"": ""Radius in world units"" },
        ""color"": { ""type"": ""string"", ""description"": ""Hex color like #00FF00"" }
      },
      ""required"": [""name"", ""x"", ""y"", ""radius"", ""color""]
    }
  }
]";

        /// <summary>
        /// Sends a request to OpenAI Responses API and returns parsed ActionPlan.
        /// </summary>
        /// <param name="settings">Settings containing API key, model, verbosity, reasoning effort</param>
        /// <param name="contextPack">Context pack from ContextBuilder</param>
        /// <param name="previousResponseId">Previous response ID for conversation continuity (null for first message)</param>
        /// <param name="toolOutputs">Tool execution results to submit (null if no tools were executed)</param>
        /// <param name="toolsJson">Dynamic tools JSON from DynamicToolGenerator (null for fallback rectangle/circle)</param>
        /// <returns>ActionPlan with response ID, message, actions, or error</returns>
        public static ActionPlan SendRequest(AIAssistantSettings settings, string contextPack, string previousResponseId = null, List<ActionResult> toolOutputs = null, string toolsJson = null)
        {
            // Validate inputs
            if (settings == null)
            {
                return new ActionPlan { Success = false, ErrorMessage = "Settings are null" };
            }

            if (!settings.ValidateAPIKey())
            {
                return new ActionPlan { Success = false, ErrorMessage = "API key is not set. Please configure it in settings." };
            }

            // Build request JSON
            string requestJson = BuildRequestBody(settings, contextPack, previousResponseId, toolOutputs, toolsJson);
            Debug.Log($"[AI Assistant] Request JSON length: {requestJson.Length} chars");  // DEBUG
            if (toolOutputs != null && toolOutputs.Count > 0)
            {
                Debug.Log($"[AI Assistant] Submitting {toolOutputs.Count} tool outputs with previous_response_id: {previousResponseId}");
                Debug.Log($"[AI Assistant] Request JSON: {requestJson}");  // DEBUG: See full request
            }

            // Send HTTP request
            return SendHTTPRequest(settings.APIKey, requestJson);
        }

        /// <summary>
        /// Builds the JSON request body for OpenAI Responses API.
        /// Uses string template for simplicity and safety.
        /// </summary>
        private static string BuildRequestBody(AIAssistantSettings settings, string contextPack, string previousResponseId, List<ActionResult> toolOutputs, string toolsJson)
        {
            // Get settings values
            string model = settings.Model;
            string verbosity = settings.GetVerbosityString();
            string reasoningEffort = settings.GetReasoningEffortString();

            // Use provided tools or fallback to rectangle/circle
            string toolsToUse = toolsJson ?? FALLBACK_TOOLS_JSON;

            // Build previous_response_id field (omit entirely if null)
            string prevIdField = string.IsNullOrEmpty(previousResponseId)
                ? ""
                : $",\n  \"previous_response_id\": \"{previousResponseId}\"";

            // Build input field - format depends on whether we have tool outputs
            string inputField;
            if (toolOutputs != null && toolOutputs.Count > 0)
            {
                // When submitting tool outputs, input must be an array of function_call_output objects
                var outputsJson = new StringBuilder();
                outputsJson.Append("[\n");

                for (int i = 0; i < toolOutputs.Count; i++)
                {
                    var result = toolOutputs[i];
                    string callId = result.Action.GetCallId();

                    // Build output JSON
                    string outputContent;
                    if (result.Success)
                    {
                        // Safely get object name (might be null or destroyed)
                        string objectName = "unknown";
                        if (result.CreatedObject != null)
                        {
                            try
                            {
                                objectName = result.CreatedObject.name;
                            }
                            catch
                            {
                                objectName = "destroyed";
                            }
                        }
                        outputContent = $"{{\\\"status\\\": \\\"success\\\", \\\"objectName\\\": \\\"{EscapeJsonString(objectName)}\\\"}}";
                    }
                    else
                    {
                        outputContent = $"{{\\\"status\\\": \\\"error\\\", \\\"message\\\": \\\"{EscapeJsonString(result.ErrorMessage ?? "unknown error")}\\\"}}";
                    }

                    outputsJson.Append($"    {{\"type\": \"function_call_output\", \"call_id\": \"{callId}\", \"output\": \"{outputContent}\"}}");

                    if (i < toolOutputs.Count - 1)
                        outputsJson.Append(",\n");
                }

                outputsJson.Append("\n  ]");
                inputField = outputsJson.ToString();
            }
            else
            {
                // Regular text input
                string escapedContext = EscapeJsonString(contextPack);
                inputField = $"\"{escapedContext}\"";
            }

            // Build request with string template
            string requestJson = $@"{{
  ""model"": ""{model}"",
  ""input"": {inputField},
  ""text"": {{
    ""verbosity"": ""{verbosity}""
  }},
  ""reasoning"": {{
    ""effort"": ""{reasoningEffort}""
  }},
  ""store"": true{prevIdField},
  ""tools"": {toolsToUse}
}}";

            return requestJson;
        }

        /// <summary>
        /// Escapes special characters in a string for safe JSON embedding.
        /// </summary>
        private static string EscapeJsonString(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            return text
                .Replace("\\", "\\\\")   // Backslash
                .Replace("\"", "\\\"")   // Quote
                .Replace("\n", "\\n")    // Newline
                .Replace("\r", "\\r")    // Carriage return
                .Replace("\t", "\\t");   // Tab
        }

        /// <summary>
        /// Sends HTTP POST request to OpenAI API synchronously.
        /// Uses UnityWebRequest with blocking wait pattern (acceptable for editor tool).
        /// </summary>
        private static ActionPlan SendHTTPRequest(string apiKey, string requestJson)
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);

            using (var request = new UnityWebRequest(API_ENDPOINT, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                // SECURITY: NEVER log API key or Authorization header
                Debug.Log("[AI Assistant] Sending API request...");

                request.SendWebRequest();

                // Synchronous wait (acceptable for editor tool)
                while (!request.isDone)
                {
                    // Wait for completion
                }

                // Handle connection errors
                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    return new ActionPlan
                    {
                        Success = false,
                        ErrorMessage = $"Network error: {request.error}"
                    };
                }

                // Handle HTTP protocol errors
                if (request.result == UnityWebRequest.Result.ProtocolError)
                {
                    return HandleHTTPError(request);
                }

                // Parse successful response
                string responseJson = request.downloadHandler.text;
                Debug.Log($"[AI Assistant] Received response ({responseJson.Length} chars)");
                Debug.Log($"[AI Assistant] Raw response: {responseJson}");  // DEBUG: See actual structure

                return ParseResponse(responseJson);
            }
        }

        /// <summary>
        /// Handles HTTP error responses with specific error messages for common codes.
        /// </summary>
        private static ActionPlan HandleHTTPError(UnityWebRequest request)
        {
            long code = request.responseCode;

            if (code == 401)
            {
                return new ActionPlan
                {
                    Success = false,
                    ErrorMessage = "Invalid API key. Please check your settings."
                };
            }
            else if (code == 429)
            {
                return new ActionPlan
                {
                    Success = false,
                    ErrorMessage = "Rate limited. Please wait and try again later."
                };
            }
            else if (code >= 500)
            {
                return new ActionPlan
                {
                    Success = false,
                    ErrorMessage = $"OpenAI server error ({code}). Please try again later."
                };
            }
            else
            {
                // For 400 errors, include response body for debugging
                string responseBody = request.downloadHandler?.text ?? "";
                Debug.LogError($"[AI Assistant] HTTP {code} response body: {responseBody}");

                return new ActionPlan
                {
                    Success = false,
                    ErrorMessage = $"HTTP error {code}: {request.error}"
                };
            }
        }

        /// <summary>
        /// Parses OpenAI Responses API response JSON into ActionPlan.
        /// Uses SimpleJSON with null-safe access throughout.
        /// </summary>
        private static ActionPlan ParseResponse(string responseJson)
        {
            try
            {
                var json = JSON.Parse(responseJson);

                // Validate response structure
                if (json == null)
                {
                    return new ActionPlan
                    {
                        Success = false,
                        ErrorMessage = "Failed to parse API response"
                    };
                }

                var plan = new ActionPlan { Success = true };

                // Extract response ID (null-safe)
                var idNode = json["id"];
                plan.ResponseId = idNode != null ? idNode.Value : null;

                // Validate output array exists (GPT-5 uses "output" not "items")
                var outputNode = json["output"];
                if (outputNode == null || !outputNode.IsArray)
                {
                    Debug.LogWarning("[AI Assistant] Response missing 'output' array");
                    return plan;  // Return empty but successful plan
                }

                var items = outputNode.AsArray;

                // Parse each item (foreach on JSONArray returns KeyValuePair)
                foreach (var kvp in items)
                {
                    var item = kvp.Value;
                    if (item == null) continue;

                    var typeNode = item["type"];
                    if (typeNode == null) continue;

                    string itemType = typeNode.Value;
                    Debug.Log($"[AI Assistant] Parsing output item type: {itemType}");  // DEBUG

                    if (itemType == "message")
                    {
                        ParseMessageItem(item, plan);
                    }
                    else if (itemType == "function_call" || itemType == "tool_call")
                    {
                        ParseFunctionCallItem(item, plan);
                    }
                    // Ignore "reasoning" items (encrypted by OpenAI)
                }

                return plan;
            }
            catch (Exception ex)
            {
                return new ActionPlan
                {
                    Success = false,
                    ErrorMessage = $"Failed to parse API response: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Parses a message item and appends content to ActionPlan.Message.
        /// GPT-5 format: message has "content" array with objects containing "text" field.
        /// </summary>
        private static void ParseMessageItem(JSONNode item, ActionPlan plan)
        {
            var contentArrayNode = item["content"];
            if (contentArrayNode == null || !contentArrayNode.IsArray) return;

            // Iterate through content array
            foreach (var contentKvp in contentArrayNode.AsArray)
            {
                var contentObj = contentKvp.Value;
                if (contentObj == null) continue;

                // Check if this is an output_text type
                var typeNode = contentObj["type"];
                if (typeNode == null || typeNode.Value != "output_text") continue;

                // Extract text field
                var textNode = contentObj["text"];
                if (textNode == null) continue;

                string text = textNode.Value;
                if (string.IsNullOrEmpty(text)) continue;

                // Concatenate messages
                if (plan.Message == null)
                    plan.Message = text;
                else
                    plan.Message += "\n" + text;
            }
        }

        /// <summary>
        /// Parses a function_call item and adds corresponding action to ActionPlan.Actions.
        /// Uses defensive coding with try-catch per action to support partial failures.
        /// </summary>
        private static void ParseFunctionCallItem(JSONNode item, ActionPlan plan)
        {
            try
            {
                // Extract call_id (required for tool output submission)
                var callIdNode = item["call_id"];
                if (callIdNode == null)
                {
                    Debug.LogWarning("[AI Assistant] Function call item missing 'call_id' field");
                    return;
                }
                string callId = callIdNode.Value;

                // In GPT-5 Responses API, name and arguments are at the top level, not nested
                var nameNode = item["name"];
                var argsNode = item["arguments"];

                if (nameNode == null || argsNode == null)
                {
                    Debug.LogWarning("[AI Assistant] Function call missing name or arguments");
                    return;
                }

                string functionName = nameNode.Value;
                string argsJson = argsNode.Value;

                if (string.IsNullOrEmpty(argsJson))
                {
                    Debug.LogWarning($"[AI Assistant] Function {functionName} has empty arguments");
                    return;
                }

                // Parse arguments
                var args = JSON.Parse(argsJson);
                if (args == null)
                {
                    Debug.LogWarning($"[AI Assistant] Failed to parse arguments for {functionName}");
                    return;
                }

                // Parse based on function type and assign call_id
                if (functionName == "createRectangle")
                {
                    var action = ParseRectangleAction(args);
                    action.callId = callId;
                    plan.Actions.Add(action);
                }
                else if (functionName == "createCircle")
                {
                    var action = ParseCircleAction(args);
                    action.callId = callId;
                    plan.Actions.Add(action);
                }
                else if (functionName.StartsWith("create"))
                {
                    // Dynamic prefab function (e.g., "createVehiclesRaceCar")
                    var action = ParsePrefabAction(args, functionName);
                    action.callId = callId;
                    plan.Actions.Add(action);
                }
                else
                {
                    Debug.LogWarning($"[AI Assistant] Unknown function: {functionName}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AI Assistant] Failed to parse function call: {ex.Message}");
                // Continue with other items (partial failure support)
            }
        }

        /// <summary>
        /// Parses createRectangle function arguments into CreateRectangleAction.
        /// </summary>
        private static CreateRectangleAction ParseRectangleAction(JSONNode args)
        {
            try
            {
                return new CreateRectangleAction
                {
                    name = args["name"],
                    x = args["x"].AsFloat,
                    y = args["y"].AsFloat,
                    width = args["width"].AsFloat,
                    height = args["height"].AsFloat,
                    color = args["color"]
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Invalid rectangle parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses createCircle function arguments into CreateCircleAction.
        /// </summary>
        private static CreateCircleAction ParseCircleAction(JSONNode args)
        {
            try
            {
                return new CreateCircleAction
                {
                    name = args["name"],
                    x = args["x"].AsFloat,
                    y = args["y"].AsFloat,
                    radius = args["radius"].AsFloat,
                    color = args["color"]
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Invalid circle parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses dynamic prefab function arguments into InstantiatePrefabAction.
        /// Looks up prefab metadata by function name, extracts position and parameters.
        /// </summary>
        /// <param name="args">Function arguments from OpenAI</param>
        /// <param name="functionName">Function name (e.g., "createVehiclesRaceCar")</param>
        /// <returns>InstantiatePrefabAction ready for execution</returns>
        private static InstantiatePrefabAction ParsePrefabAction(JSONNode args, string functionName)
        {
            try
            {
                // Look up prefab metadata by function name
                PrefabMetadata metadata = PrefabRegistryCache.FindByFunctionName(functionName);
                if (metadata == null)
                {
                    throw new Exception($"No prefab found for function '{functionName}'");
                }

                // Extract name parameter (always required)
                string name = args["name"]?.Value ?? "GameObject";

                // Extract position parameters (always required)
                float x = args["x"].AsFloat;
                float y = args["y"].AsFloat;
                float z = args["z"].AsFloat;

                // Extract rotation parameters (optional, default to 0)
                float rotX = args["rotationX"]?.AsFloat ?? 0f;
                float rotY = args["rotationY"]?.AsFloat ?? 0f;
                float rotZ = args["rotationZ"]?.AsFloat ?? 0f;

                // Extract scale parameters (optional, default to 1)
                float scaleX = args["scaleX"]?.AsFloat ?? 1f;
                float scaleY = args["scaleY"]?.AsFloat ?? 1f;
                float scaleZ = args["scaleZ"]?.AsFloat ?? 1f;

                // Extract all other parameters into dictionary
                Dictionary<string, object> parameters = new Dictionary<string, object>();

                foreach (var kvp in args)
                {
                    string key = kvp.Key;

                    // Skip name, position, rotation, and scale parameters (already extracted)
                    if (key == "name" ||
                        key == "x" || key == "y" || key == "z" ||
                        key == "rotationX" || key == "rotationY" || key == "rotationZ" ||
                        key == "scaleX" || key == "scaleY" || key == "scaleZ")
                    {
                        continue;
                    }

                    // Add to parameters dictionary (keep as JSONNode for type conversion later)
                    parameters[key] = kvp.Value;
                }

                return new InstantiatePrefabAction
                {
                    prefabPath = metadata.prefabPath,
                    name = name,
                    position = new Vector3(x, y, z),
                    rotation = new Vector3(rotX, rotY, rotZ),
                    scale = new Vector3(scaleX, scaleY, scaleZ),
                    parameters = parameters
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Invalid prefab parameters for {functionName}: {ex.Message}");
            }
        }
    }
}
