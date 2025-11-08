using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using SimpleJSON;
using UnityEditor;

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// Client for OpenAI Responses API (GPT-5).
    /// Sends requests with context packs and tool definitions, parses responses into ActionPlans.
    /// Handles conversation continuity via previous_response_id.
    /// </summary>
    public class OpenAIClient
    {
        private const string API_ENDPOINT = "https://api.openai.com/v1/responses";

        /// <summary>
        /// Sends a request to OpenAI Responses API and returns parsed ActionPlan.
        /// </summary>
        /// <param name="settings">Settings containing API key, model, verbosity, reasoning effort</param>
        /// <param name="systemMessage">System role message (algorithm/rules with high priority)</param>
        /// <param name="userMessage">User role message (context data + user request)</param>
        /// <param name="previousResponseId">Previous response ID for conversation continuity (null for first message)</param>
        /// <param name="toolOutputs">Tool execution results to submit (null if no tools were executed)</param>
        /// <param name="toolsJson">Dynamic tools JSON from DynamicToolGenerator (null for fallback rectangle/circle)</param>
        /// <returns>ActionPlan with response ID, message, actions, or error</returns>
        public static ActionPlan SendRequest(AIAssistantSettings settings, string systemMessage, string userMessage, string previousResponseId = null, List<ActionResult> toolOutputs = null, string toolsJson = null)
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
            string requestJson = BuildRequestBody(settings, systemMessage, userMessage, previousResponseId, toolOutputs, toolsJson);
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
        /// Creates messages array with system and user roles for proper priority.
        /// </summary>
        private static string BuildRequestBody(AIAssistantSettings settings, string systemMessage, string userMessage, string previousResponseId, List<ActionResult> toolOutputs, string toolsJson)
        {
            // Get settings values
            string model = settings.Model;
            string verbosity = settings.GetVerbosityString();
            string reasoningEffort = settings.GetReasoningEffortString();

            // Use provided tools (no fallback in batch architecture)
            string toolsToUse = toolsJson ?? "[]";

            // Build previous_response_id field (omit entirely if null)
            string prevIdField = string.IsNullOrEmpty(previousResponseId)
                ? ""
                : $",\n  \"previous_response_id\": \"{previousResponseId}\"";

            // Build input field - format depends on whether we have tool outputs
            string inputField;
            if (toolOutputs != null && toolOutputs.Count > 0)
            {
                // When submitting tool outputs, input is an array of outputs (not messages)
                // System/user messages are already in the conversation via previous_response_id

                // Group results by callId (supports batch operations)
                var groupedByCallId = new Dictionary<string, List<ActionResult>>();

                foreach (var result in toolOutputs)
                {
                    string callId = result.Action.GetCallId();
                    if (!groupedByCallId.ContainsKey(callId))
                    {
                        groupedByCallId[callId] = new List<ActionResult>();
                    }
                    groupedByCallId[callId].Add(result);
                }

                // Build outputs array - one per callId
                var outputsJson = new StringBuilder();
                outputsJson.Append("[\n");

                int groupIndex = 0;
                foreach (var kvp in groupedByCallId)
                {
                    string callId = kvp.Key;
                    List<ActionResult> results = kvp.Value;

                    // Build result array for this callId
                    var resultArray = new StringBuilder();
                    resultArray.Append("[");

                    for (int i = 0; i < results.Count; i++)
                    {
                        var result = results[i];

                        if (result.Success)
                        {
                            if (result.InstanceId.HasValue)
                            {
                                int instanceId = result.InstanceId.Value;
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
                                resultArray.Append($"{{\\\"status\\\": \\\"success\\\", \\\"instanceId\\\": {instanceId}, \\\"name\\\": \\\"{EscapeJsonString(objectName)}\\\"}}");
                            }
                            else
                            {
                                // Delete action - no instanceId
                                resultArray.Append($"{{\\\"status\\\": \\\"success\\\"}}");
                            }
                        }
                        else
                        {
                            resultArray.Append($"{{\\\"status\\\": \\\"error\\\", \\\"message\\\": \\\"{EscapeJsonString(result.ErrorMessage ?? "unknown error")}\\\"}}");
                        }

                        if (i < results.Count - 1)
                        {
                            resultArray.Append(",");
                        }
                    }

                    resultArray.Append("]");

                    outputsJson.Append($"    {{\"type\": \"function_call_output\", \"call_id\": \"{callId}\", \"output\": \"{resultArray}\"}}");

                    if (groupIndex < groupedByCallId.Count - 1)
                    {
                        outputsJson.Append(",\n");
                    }
                    groupIndex++;
                }

                outputsJson.Append("\n  ]");
                inputField = outputsJson.ToString();
            }
            else
            {
                // Initial request: send messages array with system (high priority) and user roles
                string escapedSystem = EscapeJsonString(systemMessage);
                string escapedUser = EscapeJsonString(userMessage);

                inputField = $@"[
    {{""role"": ""system"", ""content"": ""{escapedSystem}""}},
    {{""role"": ""user"", ""content"": ""{escapedUser}""}}
  ]";
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

                var asyncOp = request.SendWebRequest();

                // Wait for completion (blocking but necessary for synchronous API)
                while (!asyncOp.isDone)
                {
                    System.Threading.Thread.Sleep(50); // Small sleep to avoid busy-wait CPU spinning
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
                if (functionName == "instantiateObjects")
                {
                    // Batch instantiation - creates multiple actions with SAME callId
                    var actions = ParseInstantiateObjectsAction(args, callId);
                    foreach (var action in actions)
                    {
                        plan.Actions.Add(action);
                    }
                }
                else if (functionName == "modifyGameObject")
                {
                    var actions = ParseModifyGameObjectActions(args, callId);
                    foreach (var action in actions)
                    {
                        plan.Actions.Add(action);
                    }
                }
                else if (functionName == "deleteGameObject")
                {
                    var actions = ParseDeleteGameObjectActions(args, callId);
                    foreach (var action in actions)
                    {
                        plan.Actions.Add(action);
                    }
                }
                else if (functionName == "addComponent")
                {
                    var actions = ParseAddComponentActions(args, callId);
                    foreach (var action in actions)
                    {
                        plan.Actions.Add(action);
                    }
                }
                else if (functionName == "removeComponent")
                {
                    var actions = ParseRemoveComponentActions(args, callId);
                    foreach (var action in actions)
                    {
                        plan.Actions.Add(action);
                    }
                }
                else if (functionName == "savePromptFile")
                {
                    // Example Mode: AI wants to save/update a prompt file
                    plan.PendingSavePath = args["relativePath"]?.Value;
                    plan.PendingSaveContent = args["content"]?.Value;

                    if (!string.IsNullOrEmpty(plan.PendingSavePath))
                    {
                        Debug.Log($"[AI Assistant] savePromptFile called: {plan.PendingSavePath}");
                    }
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
        /// Parses instantiateObjects batch arguments into list of InstantiatePrefabActions.
        /// CRITICAL: All actions share the same callId for output aggregation.
        /// </summary>
        /// <param name="args">Function arguments containing objects array</param>
        /// <param name="callId">Shared call ID for all actions in this batch</param>
        /// <returns>List of InstantiatePrefabActions</returns>
        private static List<InstantiatePrefabAction> ParseInstantiateObjectsAction(JSONNode args, string callId)
        {
            List<InstantiatePrefabAction> actions = new List<InstantiatePrefabAction>();

            var objectsArray = args["objects"];
            if (objectsArray == null || !objectsArray.IsArray)
            {
                Debug.LogWarning("[AI Assistant] instantiateObjects missing 'objects' array");
                return actions;
            }

            foreach (var kvp in objectsArray.AsArray)
            {
                var obj = kvp.Value;
                if (obj == null) continue;

                try
                {
                    // Required fields
                    string prefabPath = obj["prefabPath"]?.Value;
                    string name = obj["name"]?.Value ?? "GameObject";

                    if (string.IsNullOrEmpty(prefabPath))
                    {
                        Debug.LogWarning("[AI Assistant] Object missing prefabPath, skipping");
                        continue;
                    }

                    // Parse position (required)
                    var posNode = obj["position"];
                    if (posNode == null)
                    {
                        Debug.LogWarning($"[AI Assistant] Object '{name}' missing position, skipping");
                        continue;
                    }

                    Vector3 position = new Vector3(
                        posNode["x"].AsFloat,
                        posNode["y"].AsFloat,
                        posNode["z"].AsFloat
                    );

                    // Parse rotation (optional, defaults to identity quaternion)
                    Quaternion rotation = Quaternion.identity;
                    var rotNode = obj["rotation"];
                    if (rotNode != null)
                    {
                        rotation = new Quaternion(
                            rotNode["x"]?.AsFloat ?? 0f,
                            rotNode["y"]?.AsFloat ?? 0f,
                            rotNode["z"]?.AsFloat ?? 0f,
                            rotNode["w"]?.AsFloat ?? 1f
                        );
                    }

                    // Parse scale (optional, null = preserve prefab default)
                    Vector3? scale = null;
                    var scaleNode = obj["scale"];
                    if (scaleNode != null)
                    {
                        scale = new Vector3(
                            scaleNode["x"]?.AsFloat ?? 1f,
                            scaleNode["y"]?.AsFloat ?? 1f,
                            scaleNode["z"]?.AsFloat ?? 1f
                        );
                    }

                    // Parse parameters (optional)
                    Dictionary<string, object> parameters = new Dictionary<string, object>();
                    var paramsNode = obj["parameters"];
                    if (paramsNode != null && paramsNode.Count > 0)
                    {
                        foreach (var paramKvp in paramsNode)
                        {
                            parameters[paramKvp.Key] = paramKvp.Value;
                        }
                    }

                    // Parse roomBindings (optional)
                    Dictionary<string, string> roomBindings = null;
                    var roomBindingsNode = obj["roomBindings"];
                    if (roomBindingsNode != null && roomBindingsNode.Count > 0)
                    {
                        roomBindings = new Dictionary<string, string>();
                        foreach (var rbKvp in roomBindingsNode)
                        {
                            roomBindings[rbKvp.Key] = rbKvp.Value.Value;
                        }
                    }

                    // Parse bindings (optional)
                    Bindings bindings = null;
                    var bindingsNode = obj["bindings"];
                    if (bindingsNode != null)
                    {
                        bindings = ParseBindings(bindingsNode);
                    }

                    // Create action - CRITICAL: All actions share the same callId!
                    actions.Add(new InstantiatePrefabAction
                    {
                        callId = callId,  // Shared across all objects in batch
                        prefabPath = prefabPath,
                        name = name,
                        position = position,
                        rotation = rotation,
                        scale = scale,
                        parameters = parameters,
                        roomBindings = roomBindings,
                        bindings = bindings
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AI Assistant] Failed to parse object in batch: {ex.Message}");
                    // Continue with other objects (partial failure support)
                }
            }

            // Log result
            if (actions.Count > 0)
            {
                Debug.Log($"[AI Assistant] Parsed {actions.Count} object(s) from batch with callId {callId}");
            }
            else
            {
                Debug.LogWarning($"[AI Assistant] No valid objects parsed from instantiateObjects batch (callId: {callId})");
            }

            return actions;
        }


        /// <summary>
        /// Parses modifyGameObject function arguments into list of ModifyGameObjectActions.
        /// CRITICAL: All actions share the same callId for output aggregation.
        /// </summary>
        /// <param name="args">Function arguments containing modifications array</param>
        /// <param name="callId">Shared call ID for all actions in this batch</param>
        /// <returns>List of ModifyGameObjectActions</returns>
        private static List<ModifyGameObjectAction> ParseModifyGameObjectActions(JSONNode args, string callId)
        {
            List<ModifyGameObjectAction> actions = new List<ModifyGameObjectAction>();

            try
            {
                var modificationsArray = args["modifications"];
                foreach (var kvp in modificationsArray.AsArray)
                {
                    var modNode = kvp.Value;
                    if (modNode == null) continue;

                    var action = new ModifyGameObjectAction
                    {
                        callId = callId,
                        instanceId = modNode["instanceId"].AsInt
                    };

                    // Optional string fields
                    if (modNode["name"] != null)
                        action.name = modNode["name"].Value;

                    // Optional bool field
                    if (modNode["active"] != null)
                        action.active = modNode["active"].AsBool;

                    // Optional position
                    var posNode = modNode["position"];
                    if (posNode != null)
                    {
                        action.position = new Vector3(
                            posNode["x"].AsFloat,
                            posNode["y"].AsFloat,
                            posNode["z"].AsFloat
                        );
                    }

                    // Optional rotation (quaternion)
                    var rotNode = modNode["rotation"];
                    if (rotNode != null)
                    {
                        action.rotation = new Quaternion(
                            rotNode["x"].AsFloat,
                            rotNode["y"].AsFloat,
                            rotNode["z"].AsFloat,
                            rotNode["w"].AsFloat
                        );
                    }

                    // Optional scale
                    var scaleNode = modNode["scale"];
                    if (scaleNode != null)
                    {
                        action.scale = new Vector3(
                            scaleNode["x"].AsFloat,
                            scaleNode["y"].AsFloat,
                            scaleNode["z"].AsFloat
                        );
                    }

                    // Optional component parameters
                    var paramsNode = modNode["parameters"];
                    if (paramsNode != null && paramsNode.Count > 0)
                    {
                        action.parameters = new Dictionary<string, object>();
                        foreach (var paramKvp in paramsNode)
                        {
                            action.parameters[paramKvp.Key] = paramKvp.Value;
                        }
                    }

                    actions.Add(action);
                }

                return actions;
            }
            catch (Exception ex)
            {
                throw new Exception($"Invalid modifyGameObject parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses deleteGameObject function arguments into list of DeleteGameObjectActions.
        /// CRITICAL: All actions share the same callId for output aggregation.
        /// </summary>
        /// <param name="args">Function arguments containing instanceIds array</param>
        /// <param name="callId">Shared call ID for all actions in this batch</param>
        /// <returns>List of DeleteGameObjectActions</returns>
        private static List<DeleteGameObjectAction> ParseDeleteGameObjectActions(JSONNode args, string callId)
        {
            List<DeleteGameObjectAction> actions = new List<DeleteGameObjectAction>();

            try
            {
                var instanceIdsArray = args["instanceIds"];
                foreach (var kvp in instanceIdsArray.AsArray)
                {
                    var idNode = kvp.Value;
                    if (idNode == null) continue;

                    actions.Add(new DeleteGameObjectAction
                    {
                        callId = callId,
                        instanceId = idNode.AsInt
                    });
                }

                return actions;
            }
            catch (Exception ex)
            {
                throw new Exception($"Invalid deleteGameObject parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses addComponent function arguments into list of AddComponentActions.
        /// CRITICAL: All actions share the same callId for output aggregation.
        /// </summary>
        /// <param name="args">Function arguments containing components array</param>
        /// <param name="callId">Shared call ID for all actions in this batch</param>
        /// <returns>List of AddComponentActions</returns>
        private static List<AddComponentAction> ParseAddComponentActions(JSONNode args, string callId)
        {
            List<AddComponentAction> actions = new List<AddComponentAction>();

            try
            {
                var componentsArray = args["components"];
                foreach (var kvp in componentsArray.AsArray)
                {
                    var compNode = kvp.Value;
                    if (compNode == null) continue;

                    var action = new AddComponentAction
                    {
                        callId = callId,
                        instanceId = compNode["instanceId"].AsInt,
                        componentType = compNode["componentType"].Value
                    };

                    // Optional component parameters
                    var paramsNode = compNode["parameters"];
                    if (paramsNode != null && paramsNode.Count > 0)
                    {
                        action.parameters = new Dictionary<string, object>();
                        foreach (var paramKvp in paramsNode)
                        {
                            action.parameters[paramKvp.Key] = paramKvp.Value;
                        }
                    }

                    actions.Add(action);
                }

                return actions;
            }
            catch (Exception ex)
            {
                throw new Exception($"Invalid addComponent parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses removeComponent function arguments into list of RemoveComponentActions.
        /// CRITICAL: All actions share the same callId for output aggregation.
        /// </summary>
        /// <param name="args">Function arguments containing components array</param>
        /// <param name="callId">Shared call ID for all actions in this batch</param>
        /// <returns>List of RemoveComponentActions</returns>
        private static List<RemoveComponentAction> ParseRemoveComponentActions(JSONNode args, string callId)
        {
            List<RemoveComponentAction> actions = new List<RemoveComponentAction>();

            try
            {
                var componentsArray = args["components"];
                foreach (var kvp in componentsArray.AsArray)
                {
                    var compNode = kvp.Value;
                    if (compNode == null) continue;

                    actions.Add(new RemoveComponentAction
                    {
                        callId = callId,
                        instanceId = compNode["instanceId"].AsInt,
                        componentType = compNode["componentType"].Value
                    });
                }

                return actions;
            }
            catch (Exception ex)
            {
                throw new Exception($"Invalid removeComponent parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses bindings object from JSON into Bindings data structure.
        /// </summary>
        /// <param name="node">JSON node containing bindings data</param>
        /// <returns>Parsed Bindings object or null if node is null</returns>
        private static Bindings ParseBindings(JSONNode node)
        {
            if (node == null) return null;

            var bindings = new Bindings
            {
                room = node["room"]?.Value
            };

            // Parse contact (primary alignment binding)
            var contactNode = node["contact"];
            if (contactNode != null)
            {
                bindings.contact = new ContactBinding
                {
                    side = contactNode["side"]?.Value,
                    target = contactNode["target"]?.Value,
                    targetSide = contactNode["targetSide"]?.Value
                };
            }

            // Parse adjacent array (optional)
            var adjacentNode = node["adjacent"];
            if (adjacentNode != null && adjacentNode.IsArray)
            {
                bindings.adjacent = new List<AdjacentBinding>();
                foreach (var adjacentKvp in adjacentNode.AsArray)
                {
                    var item = adjacentKvp.Value;
                    if (item == null) continue;

                    bindings.adjacent.Add(new AdjacentBinding
                    {
                        mySide = item["mySide"]?.Value,
                        target = item["target"]?.Value,
                        theirSide = item["theirSide"]?.Value,
                        gap = item["gap"]?.AsFloat ?? 0.05f
                    });
                }
            }

            return bindings;
        }
    }
}
