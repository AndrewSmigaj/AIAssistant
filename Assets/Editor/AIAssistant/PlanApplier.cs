using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Executes approved actions by creating GameObjects in the Unity scene.
    /// Supports single Undo group, texture caching, sprite caching, preview mode, and partial failure tracking.
    /// </summary>
    public static class PlanApplier
    {
        // Lazy-initialized caches (persist for editor session)
        private static Dictionary<string, Texture2D> _textureCache;
        private static Dictionary<string, Sprite> _spriteCache;

        /// <summary>
        /// Applies a list of approved actions with single Undo group.
        /// Returns per-action results for partial failure tracking.
        /// </summary>
        /// <param name="actions">List of actions to execute</param>
        /// <param name="previewMode">If true, log actions but don't create GameObjects</param>
        /// <returns>List of ActionResult with success/failure per action</returns>
        public static List<ActionResult> ApplyPlan(List<IAction> actions, bool previewMode)
        {
            var results = new List<ActionResult>();

            // Handle null or empty input
            if (actions == null || actions.Count == 0)
            {
                Debug.Log("[AI Assistant] ApplyPlan called with null or empty actions list");
                return results;
            }

            Debug.Log($"[AI Assistant] ApplyPlan executing {actions.Count} action(s), previewMode={previewMode}");

            // Setup single Undo group for all actions
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            // Execute each action with try-catch for partial failure support
            foreach (var action in actions)
            {
                ActionResult result;

                try
                {
                    Debug.Log($"[AI Assistant] Processing action: {action.GetDescription()}");

                    if (action is CreateRectangleAction rectangleAction)
                    {
                        Debug.Log($"[AI Assistant] Creating rectangle: name={rectangleAction.name}, x={rectangleAction.x}, y={rectangleAction.y}, w={rectangleAction.width}, h={rectangleAction.height}, color={rectangleAction.color}");
                        result = CreateRectangleGameObject(rectangleAction, previewMode);
                    }
                    else if (action is CreateCircleAction circleAction)
                    {
                        Debug.Log($"[AI Assistant] Creating circle: name={circleAction.name}, x={circleAction.x}, y={circleAction.y}, r={circleAction.radius}, color={circleAction.color}");
                        result = CreateCircleGameObject(circleAction, previewMode);
                    }
                    else
                    {
                        result = new ActionResult
                        {
                            Action = action,
                            Success = false,
                            ErrorMessage = $"Unknown action type: {action.GetType().Name}"
                        };
                    }
                }
                catch (Exception ex)
                {
                    // Catch unexpected exceptions to prevent entire batch from failing
                    result = new ActionResult
                    {
                        Action = action,
                        Success = false,
                        ErrorMessage = $"Unexpected error: {ex.Message}"
                    };
                    Debug.LogWarning($"[AI Assistant] Failed to execute {action.GetDescription()}: {ex.Message}");
                }

                results.Add(result);
            }

            // Collapse all operations into single undo step
            Undo.CollapseUndoOperations(undoGroup);

            return results;
        }

        /// <summary>
        /// Creates a rectangle sprite GameObject with validation and error handling.
        /// </summary>
        /// <param name="action">Rectangle creation parameters</param>
        /// <param name="previewMode">If true, log but don't create</param>
        /// <returns>ActionResult with success status and created object</returns>
        public static ActionResult CreateRectangleGameObject(CreateRectangleAction action, bool previewMode)
        {
            // Validate dimensions
            if (action.width <= 0 || action.height <= 0)
            {
                return new ActionResult
                {
                    Action = action,
                    Success = false,
                    ErrorMessage = "Width and height must be positive"
                };
            }

            // Parse and validate color
            if (!ColorUtility.TryParseHtmlString(action.color, out Color color))
            {
                return new ActionResult
                {
                    Action = action,
                    Success = false,
                    ErrorMessage = $"Invalid color format: {action.color}"
                };
            }

            // Preview mode: log but don't create
            if (previewMode)
            {
                Debug.Log($"[Preview] Would create rectangle '{action.name}' at ({action.x}, {action.y}) - {action.width}x{action.height} - color {action.color}");
                return new ActionResult
                {
                    Action = action,
                    Success = true,
                    CreatedObject = null
                };
            }

            // Get unique name to avoid collisions
            string uniqueName = GetUniqueGameObjectName(action.name ?? "Rectangle");

            // Create GameObject
            var go = new GameObject(uniqueName);

            // Add SpriteRenderer component
            var sr = go.AddComponent<SpriteRenderer>();

            // Get or create cached sprite
            var sprite = GetOrCreateRectangleSprite();

            // Apply sprite and color
            sr.sprite = sprite;
            sr.color = color;

            // Set position in world space
            go.transform.position = new Vector3(action.x, action.y, 0);

            // Scale to match desired dimensions
            go.transform.localScale = new Vector3(action.width, action.height, 1);

            // Register with Undo system
            Undo.RegisterCreatedObjectUndo(go, "AI Assistant Actions");

            Debug.Log($"[AI Assistant] Successfully created rectangle GameObject: {go.name} at position {go.transform.position}");
            Debug.Log($"[AI Assistant] GameObject active: {go.activeSelf}, hideFlags: {go.hideFlags}, scene: {go.scene.name}, instanceID: {go.GetInstanceID()}");
            Debug.Log($"[AI Assistant] SpriteRenderer color: {sr.color}, sprite null? {sr.sprite == null}, enabled: {sr.enabled}");

            return new ActionResult
            {
                Action = action,
                Success = true,
                CreatedObject = go
            };
        }

        /// <summary>
        /// Creates a circle sprite GameObject with validation and error handling.
        /// </summary>
        /// <param name="action">Circle creation parameters</param>
        /// <param name="previewMode">If true, log but don't create</param>
        /// <returns>ActionResult with success status and created object</returns>
        public static ActionResult CreateCircleGameObject(CreateCircleAction action, bool previewMode)
        {
            // Validate radius
            if (action.radius <= 0)
            {
                return new ActionResult
                {
                    Action = action,
                    Success = false,
                    ErrorMessage = "Radius must be positive"
                };
            }

            // Parse and validate color
            if (!ColorUtility.TryParseHtmlString(action.color, out Color color))
            {
                return new ActionResult
                {
                    Action = action,
                    Success = false,
                    ErrorMessage = $"Invalid color format: {action.color}"
                };
            }

            // Preview mode: log but don't create
            if (previewMode)
            {
                Debug.Log($"[Preview] Would create circle '{action.name}' at ({action.x}, {action.y}) - radius {action.radius} - color {action.color}");
                return new ActionResult
                {
                    Action = action,
                    Success = true,
                    CreatedObject = null
                };
            }

            // Get unique name to avoid collisions
            string uniqueName = GetUniqueGameObjectName(action.name ?? "Circle");

            // Create GameObject
            var go = new GameObject(uniqueName);

            // Add SpriteRenderer component
            var sr = go.AddComponent<SpriteRenderer>();

            // Get or create cached sprite
            var sprite = GetOrCreateCircleSprite();

            // Apply sprite and color
            sr.sprite = sprite;
            sr.color = color;

            // Set position in world space
            go.transform.position = new Vector3(action.x, action.y, 0);

            // Scale to match desired diameter (radius * 2)
            go.transform.localScale = new Vector3(action.radius * 2, action.radius * 2, 1);

            // Register with Undo system
            Undo.RegisterCreatedObjectUndo(go, "AI Assistant Actions");

            Debug.Log($"[AI Assistant] Successfully created circle GameObject: {go.name} at position {go.transform.position}");

            return new ActionResult
            {
                Action = action,
                Success = true,
                CreatedObject = go
            };
        }

        /// <summary>
        /// Gets or creates cached rectangle sprite.
        /// Reuses same sprite instance for all rectangles (color applied via SpriteRenderer).
        /// </summary>
        /// <returns>Cached rectangle sprite</returns>
        private static Sprite GetOrCreateRectangleSprite()
        {
            // Lazy initialize cache
            if (_spriteCache == null)
            {
                _spriteCache = new Dictionary<string, Sprite>();
            }

            // Check cache
            if (_spriteCache.ContainsKey("rectangle"))
            {
                return _spriteCache["rectangle"];
            }

            // Get texture
            var tex = GetOrCreateRectangleTexture();

            // Create sprite (64 pixels per unit = 1x1 world unit base, pivot at center)
            var sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
            // NOTE: No hideFlags - we want this to persist with the scene

            // Cache and return
            _spriteCache["rectangle"] = sprite;
            return sprite;
        }

        /// <summary>
        /// Gets or creates cached circle sprite.
        /// Reuses same sprite instance for all circles (color applied via SpriteRenderer).
        /// </summary>
        /// <returns>Cached circle sprite</returns>
        private static Sprite GetOrCreateCircleSprite()
        {
            // Lazy initialize cache
            if (_spriteCache == null)
            {
                _spriteCache = new Dictionary<string, Sprite>();
            }

            // Check cache
            if (_spriteCache.ContainsKey("circle"))
            {
                return _spriteCache["circle"];
            }

            // Get texture
            var tex = GetOrCreateCircleTexture();

            // Create sprite (64 pixels per unit = 1x1 world unit base, pivot at center)
            var sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
            // NOTE: No hideFlags - we want this to persist with the scene

            // Cache and return
            _spriteCache["circle"] = sprite;
            return sprite;
        }

        /// <summary>
        /// Gets or creates 64x64 white rectangle texture (cached).
        /// Uses RGBA32 format with FilterMode.Point for crisp pixel art edges.
        /// </summary>
        /// <returns>Cached white rectangle texture</returns>
        private static Texture2D GetOrCreateRectangleTexture()
        {
            // Lazy initialize cache
            if (_textureCache == null)
            {
                _textureCache = new Dictionary<string, Texture2D>();
            }

            // Check cache
            if (_textureCache.ContainsKey("rectangle"))
            {
                return _textureCache["rectangle"];
            }

            // Create new texture with RGBA32 format for alpha support
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point; // Crisp pixel art rendering
            // NOTE: No hideFlags - we want this to persist with the scene

            // Fill with white using fast array method
            Color[] pixels = new Color[64 * 64];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }
            tex.SetPixels(pixels);
            tex.Apply();

            // Cache and return
            _textureCache["rectangle"] = tex;
            return tex;
        }

        /// <summary>
        /// Gets or creates 64x64 white circle texture (cached).
        /// Uses RGBA32 format with FilterMode.Point for crisp pixel art edges.
        /// Circle has hard edges (no anti-aliasing) for Day 1 simplicity.
        /// </summary>
        /// <returns>Cached white circle texture</returns>
        private static Texture2D GetOrCreateCircleTexture()
        {
            // Lazy initialize cache
            if (_textureCache == null)
            {
                _textureCache = new Dictionary<string, Texture2D>();
            }

            // Check cache
            if (_textureCache.ContainsKey("circle"))
            {
                return _textureCache["circle"];
            }

            // Create new texture with RGBA32 format for alpha support
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point; // Crisp pixel art rendering
            // NOTE: No hideFlags - we want this to persist with the scene

            // Fill with circle using fast array method
            Color[] pixels = new Color[64 * 64];
            Vector2 center = new Vector2(32f, 32f);
            float radius = 32f;

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    int index = y * 64 + x;

                    if (distance <= radius)
                    {
                        pixels[index] = Color.white;
                    }
                    else
                    {
                        pixels[index] = Color.clear; // Transparent background
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            // Cache and return
            _textureCache["circle"] = tex;
            return tex;
        }

        /// <summary>
        /// Finds unique GameObject name by appending numbers if collision detected.
        /// Note: GameObject.Find() only finds active GameObjects (acceptable for Day 1).
        /// </summary>
        /// <param name="baseName">Desired GameObject name</param>
        /// <returns>Unique name with numeric suffix if needed</returns>
        private static string GetUniqueGameObjectName(string baseName)
        {
            // Handle null or empty input
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = "GameObject";
            }

            // Check if name is already unique
            if (GameObject.Find(baseName) == null)
            {
                return baseName;
            }

            // Append numeric suffix until unique name found
            int counter = 1;
            while (true)
            {
                string candidate = $"{baseName} ({counter})";
                if (GameObject.Find(candidate) == null)
                {
                    return candidate;
                }
                counter++;
            }
        }
    }
}
