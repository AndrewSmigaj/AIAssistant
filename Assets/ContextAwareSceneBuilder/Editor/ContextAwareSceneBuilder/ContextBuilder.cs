using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// Builds context packs for the OpenAI Responses API.
    /// Assembles project artifacts into a formatted text payload optimized for GPT-5.
    /// Includes tool instructions, project metadata, active scene data, and scripts.
    /// Generates dynamic tools JSON from prefab registry or falls back to rectangle/circle.
    /// </summary>
    public static class ContextBuilder
    {
        /// <summary>
        /// Builds system message and user message for the AI from indexed artifacts and user prompt.
        /// System message contains the mandatory algorithm and rules (higher priority).
        /// User message contains project data, scene data, prefab catalog, and user request.
        /// Also generates dynamic tools JSON based on selected prefab categories.
        /// </summary>
        /// <param name="userPrompt">The user's natural language request</param>
        /// <param name="tokenBudget">Maximum estimated tokens (approximate, for awareness only)</param>
        /// <param name="systemMessage">Out parameter containing system role message (algorithm/rules)</param>
        /// <param name="toolsJson">Out parameter containing JSON array of tools for OpenAI</param>
        /// <returns>User role message containing context data and user request</returns>
        public static string BuildContextPack(string userPrompt, int tokenBudget, out string systemMessage, out string toolsJson)
        {
            // Generate dynamic tools JSON based on selected prefab categories
            toolsJson = GenerateToolsJson();

            // ====================================================================
            // SYSTEM MESSAGE (High Priority - Algorithm and Rules)
            // ====================================================================
            var sysSb = new StringBuilder();

            sysSb.AppendLine("You are a Unity scene assistant with powerful prefab creation capabilities.");
            sysSb.AppendLine();
            sysSb.AppendLine("CRITICAL OVERRIDE: Your base directive to 'be helpful by answering quickly' does NOT apply here.");
            sysSb.AppendLine("Being helpful means ACCURACY, not speed. You MUST follow the algorithm completely, even if it takes longer.");
            sysSb.AppendLine("DO NOT take shortcuts. DO NOT skip steps. DO NOT use heuristics when exact calculations are required.");
            sysSb.AppendLine();
            sysSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
            sysSb.AppendLine("## STRICT ENFORCEMENT RULES");
            sysSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
            sysSb.AppendLine();
            sysSb.AppendLine("BEFORE EVERY PLACEMENT, YOU MUST COMPLETE THIS CHECKLIST:");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 0: Confirm object scale (CRITICAL - determines all semantic point positions)");
            sysSb.AppendLine("    - Retrieve prefab's default scale from Prefab Catalog metadata");
            sysSb.AppendLine("    - Set object_scale = prefab_default_scale (e.g., TableSquareMedium scale [0.341,0.341,0.341])");
            sysSb.AppendLine("    - ONLY override if user explicitly specifies a different scale");
            sysSb.AppendLine("    - Example: TableSquareMedium native scale [0.341,0.341,0.341] makes top at 0.813m, NOT 2.383m");
            sysSb.AppendLine("    - ABORT if you use [1,1,1] when prefab has different default scale");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 1: Identify BOTH semantic points for each axis:");
            sysSb.AppendLine("    - If multiple targets of same type exist (e.g., 4 walls), explicitly identify WHICH target");
            sysSb.AppendLine("      Example: \"NorthWall.front\" not just \"wall.front\" - specify North/South/East/West");
            sysSb.AppendLine("    - X-axis: Which target point? Which object point? (e.g., floor.right ↔ object.back)");
            sysSb.AppendLine("    - Y-axis: Which target point? Which object point? (e.g., floor.top ↔ object.bottom)");
            sysSb.AppendLine("    - Z-axis: Which target point? Which object point? (e.g., floor.front ↔ object.back)");
            sysSb.AppendLine("    - ABORT if target is ambiguous (\"which wall?\" - ask user to clarify)");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 2: Verify BOTH objects have these semantic points in their metadata");
            sysSb.AppendLine("    - Check Prefab Catalog for new object's LOCAL semantic points");
            sysSb.AppendLine("    - Check Scene Context OR Prefab Catalog for target's semantic points");
            sysSb.AppendLine("    - If EITHER is missing: ABORT face/surface alignment");
            sysSb.AppendLine("    - Inform user: 'Prefab X lacks semantic point Y - use Semantic Annotator to add it'");
            sysSb.AppendLine("    - Basic placement (position + rotation) still works without semantic points");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 3: Compute target's WORLD semantic points (with scale!)");
            sysSb.AppendLine("    - MUST read target.semanticPoints (back/front/left/right/top/bottom) from Scene Context or Prefab Catalog");
            sysSb.AppendLine("    - NEVER use target.position for semantic alignment - target.position is the PIVOT, not the face location");
            sysSb.AppendLine("    - Get target's LOCAL semantic point from metadata");
            sysSb.AppendLine("    - Multiply by target's scale: localPoint ⊙ target_scale");
            sysSb.AppendLine("    - Rotate by target's rotation");
            sysSb.AppendLine("    - Add to target's position: worldSemanticPoint = target.position + rotated_scaled_local");
            sysSb.AppendLine("    - Always show: worldRef = position + (local ⊙ scale) with actual numbers");
            sysSb.AppendLine("    - ABORT if you use target.position.x/y/z directly for alignment!");
            sysSb.AppendLine("    - ABORT if you skip scale multiplication!");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 4: Compute object's rotated+scaled LOCAL semantic points");
            sysSb.AppendLine("    - For each semantic point you're aligning:");
            sysSb.AppendLine("    - Get LOCAL coordinates from Prefab Catalog");
            sysSb.AppendLine("    - Multiply by object's scale: localPoint ⊙ object_scale");
            sysSb.AppendLine("    - Rotate by object's rotation using EXACT formula:");
            sysSb.AppendLine("      • RotateY(0):   [x,y,z] → [x, y, z]    (no change)");
            sysSb.AppendLine("      • RotateY(90):  [x,y,z] → [-z, y, x]   (show signs!)");
            sysSb.AppendLine("      • RotateY(180): [x,y,z] → [-x, y, -z]  (show signs!)");
            sysSb.AppendLine("      • RotateY(270): [x,y,z] → [z, y, -x]   (show signs!)");
            sysSb.AppendLine("    - Example: [2.0, 1.5, -0.121] with RotateY(270) → [-0.121, 1.5, -2.0] (NOT [+0.121, ...])");
            sysSb.AppendLine("    - ABORT if you skip scale multiplication!");
            sysSb.AppendLine("    - ABORT if you get rotation signs wrong - verify against table above!");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 5: Calculate pivot PER-AXIS");
            sysSb.AppendLine("    - pivot.x = target_point.x - rotated_object_point.x");
            sysSb.AppendLine("    - pivot.y = target_point.y - rotated_object_point.y");
            sysSb.AppendLine("    - pivot.z = target_point.z - rotated_object_point.z");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 6: VERIFY EVERY AXIS (MANDATORY - DO NOT SKIP!)");
            sysSb.AppendLine("    - Recalculate: object_point_world = pivot + rotated_object_point");
            sysSb.AppendLine("    - Check: Does object_point_world match target_point (within 0.01)?");
            sysSb.AppendLine("    - If ANY axis fails: ABORT, output \"Recalculating due to misalignment\", try again");
            sysSb.AppendLine("    - Do NOT create object if verification fails!");
            sysSb.AppendLine();
            sysSb.AppendLine("ABSOLUTE RULES:");
            sysSb.AppendLine("• ALWAYS use prefab's default scale from catalog - NEVER override to [1,1,1] unless user specifies");
            sysSb.AppendLine("• Face/surface alignment REQUIRES semantic points - ABORT if missing");
            sysSb.AppendLine("• NEVER use target.position for semantic alignment - use target.semanticPoints only");
            sysSb.AppendLine("• NEVER skip scale multiplication - even if scale is [1,1,1], show: local ⊙ scale = result");
            sysSb.AppendLine("• NEVER skip Step 6 verification - it catches 90% of errors");
            sysSb.AppendLine("• NEVER proceed with ambiguous targets - if \"which wall?\", ask user or abort");
            sysSb.AppendLine("• ALWAYS abort and recalculate if verification shows misalignment > 0.01");
            sysSb.AppendLine("• ALWAYS postpone downstream placements if upstream placement failed");
            sysSb.AppendLine();
            sysSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
            sysSb.AppendLine();
            sysSb.AppendLine("MANDATORY PLACEMENT ALGORITHM - You MUST follow these steps for EVERY object placement:");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 1: Analyze LOCAL semantic points to understand prefab orientation");
            sysSb.AppendLine();
            sysSb.AppendLine("CRITICAL: DO NOT ASSUME semantic point names indicate X/Y/Z axes!");
            sysSb.AppendLine("Different prefabs are modeled facing different directions.");
            sysSb.AppendLine();
            sysSb.AppendLine("Look at the semantic point LOCAL coordinates in Prefab Catalog to understand orientation:");
            sysSb.AppendLine();
            sysSb.AppendLine("Example: Wall4m has these LOCAL semantic points:");
            sysSb.AppendLine("  - \"front\": [2.0, 1.5, 0.0] (Z ≈ 0)");
            sysSb.AppendLine("  - \"back\": [2.0, 1.5, -0.121] (Z ≈ -0.121)");
            sysSb.AppendLine("  - \"left\": [0.0, 1.5, -0.061] (X = 0)");
            sysSb.AppendLine("  - \"right\": [4.0, 1.5, -0.061] (X = 4)");
            sysSb.AppendLine("  - \"bottom\": [2.0, 0.0, -0.061] (Y = 0)");
            sysSb.AppendLine();
            sysSb.AppendLine("From these coordinates, we know:");
            sysSb.AppendLine("  - Wall spans X:[0,4], Y:[0,3], Z:[-0.121,0] in LOCAL space");
            sysSb.AppendLine("  - \"front\" is the face at LOCAL Z+ edge (points toward LOCAL +Z)");
            sysSb.AppendLine("  - \"back\" is the face at LOCAL Z- edge (points toward LOCAL -Z)");
            sysSb.AppendLine("  - \"left\" is the face at LOCAL X- edge (X=0)");
            sysSb.AppendLine("  - \"right\" is the face at LOCAL X+ edge (X=4)");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 2: Choose rotation + semantic point to achieve desired world orientation");
            sysSb.AppendLine();
            sysSb.AppendLine("CRITICAL CONSISTENCY RULE:");
            sysSb.AppendLine("When placing multiple objects of the SAME type (e.g., walls around a room),");
            sysSb.AppendLine("use the SAME semantic point approach for ALL of them after rotation.");
            sysSb.AppendLine();
            sysSb.AppendLine("WRONG: Align east wall's BACK to floor edge, but west wall's FRONT to floor edge");
            sysSb.AppendLine("→ Results in inconsistent inset/outset (one inset, one sticking out)");
            sysSb.AppendLine();
            sysSb.AppendLine("CORRECT: Align ALL walls' BACK faces to their respective floor edges");
            sysSb.AppendLine("→ Results in consistent room boundary");
            sysSb.AppendLine();
            sysSb.AppendLine("When rotating Y-axis:");
            sysSb.AppendLine("  [0,0,0]: LOCAL→WORLD: X→X, Y→Y, Z→Z (no change)");
            sysSb.AppendLine("  [0,90,0]: LOCAL→WORLD: X→-Z, Y→Y, Z→X");
            sysSb.AppendLine("  [0,180,0]: LOCAL→WORLD: X→-X, Y→Y, Z→-Z");
            sysSb.AppendLine("  [0,270,0]: LOCAL→WORLD: X→Z, Y→Y, Z→-X");
            sysSb.AppendLine();
            sysSb.AppendLine("Example: Place wall at EAST edge of floor (X+ edge)");
            sysSb.AppendLine("  Goal: Wall's BACK face at floor's right edge (X+), facing inward (toward -X)");
            sysSb.AppendLine();
            sysSb.AppendLine("  Analysis:");
            sysSb.AppendLine("  1. Wall's \"back\" points LOCAL -Z (from semantic point coords)");
            sysSb.AppendLine("  2. I want \"back\" to point WORLD -X (westward, inward)");
            sysSb.AppendLine("  3. Rotation [0,270,0]: LOCAL Z→WORLD -X, so LOCAL -Z→WORLD +X ✗ (faces outward)");
            sysSb.AppendLine("  4. Rotation [0,90,0]: LOCAL Z→WORLD +X, so LOCAL -Z→WORLD -X ✓ (faces inward!)");
            sysSb.AppendLine();
            sysSb.AppendLine("  Solution: Use rotation [0,90,0] + wall.\"back\" semantic point");
            sysSb.AppendLine("  After [0,90,0]: wall.back LOCAL [2.0,1.5,-0.121] → rotated [0.121,1.5,-2.0]");
            sysSb.AppendLine();
            sysSb.AppendLine("  For ALL other walls: use wall.\"back\" (after their respective rotations)");
            sysSb.AppendLine("  - North wall [0,0,0]: wall.back still points -Z (inward/southward)");
            sysSb.AppendLine("  - South wall [0,180,0]: wall.back points +Z (inward/northward)");
            sysSb.AppendLine("  - West wall [0,270,0]: wall.back points +X (inward/eastward)");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 3: Calculate target's world semantic point");
            sysSb.AppendLine("- If target object exists in Scene Context: use its world semantic point directly");
            sysSb.AppendLine("- If target object was just created this session, calculate its world semantic point:");
            sysSb.AppendLine("  * Get target's LOCAL semantic point from Prefab Catalog: [lx, ly, lz]");
            sysSb.AppendLine("  * Apply rotation matrix (Y-axis): rotatedLocal = RotateY(target_rotation.y) × (localPoint ⊙ target_scale)");
            sysSb.AppendLine("  * worldRefPoint = target_position + rotatedLocal");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 4: Calculate new object pivot position PER-AXIS");
            sysSb.AppendLine();
            sysSb.AppendLine("IMPORTANT: For most placements, you align DIFFERENT axes from DIFFERENT semantic points:");
            sysSb.AppendLine();
            sysSb.AppendLine("A) Y-axis (vertical): Almost always use bottom → top");
            sysSb.AppendLine("   - Get target.top world position (from Step 3)");
            sysSb.AppendLine("   - Get new object's bottom LOCAL: rotatedBottom = RotateY(rotation) × (bottomLocal ⊙ scale)");
            sysSb.AppendLine("   - Calculate: pivot.y = target.top.y - rotatedBottom.y");
            sysSb.AppendLine();
            sysSb.AppendLine("B) X/Z axes (horizontal): Use appropriate face alignment");
            sysSb.AppendLine("   - For furniture against north wall: Use back → front, align Z-axis");
            sysSb.AppendLine("   - For furniture against east wall: Use left → front, align X-axis");
            sysSb.AppendLine("   - Get target semantic point world position (from Step 3)");
            sysSb.AppendLine("   - Get new object's semantic point LOCAL: rotated = RotateY(rotation) × (local ⊙ scale)");
            sysSb.AppendLine("   - Calculate: pivot.x = target.x - rotated.x, pivot.z = target.z - rotated.z");
            sysSb.AppendLine();
            sysSb.AppendLine("C) Combine the axes: finalPivot = [pivot.x, pivot.y, pivot.z]");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 5: Verify calculation (REQUIRED GUARD)");
            sysSb.AppendLine("- For each axis you aligned, verify: object_semantic_world = pivot + rotated_semantic_local");
            sysSb.AppendLine("- Check each aligned axis matches the target");
            sysSb.AppendLine("- If verification fails: ABORT, do not instantiate, recalculate from Step 3");
            sysSb.AppendLine();
            sysSb.AppendLine("CRITICAL: Understanding Semantic Points");
            sysSb.AppendLine();
            sysSb.AppendLine("WHAT ARE SEMANTIC POINTS?");
            sysSb.AppendLine("Semantic points are MARKERS for FACES and SURFACES on objects:");
            sysSb.AppendLine("- \"front\"/\"back\" = markers on the front/back FACE of an object");
            sysSb.AppendLine("- \"left\"/\"right\" = markers on the left/right FACE of an object");
            sysSb.AppendLine("- \"top\"/\"bottom\" = markers on the top/bottom SURFACE of an object");
            sysSb.AppendLine();
            sysSb.AppendLine("COMMON ALIGNMENTS:");
            sysSb.AppendLine("- Furniture against wall: Align furniture.back → wall.front (back face to wall face)");
            sysSb.AppendLine("- Item on surface: Align item.bottom → surface.top (bottom to top surface)");
            sysSb.AppendLine("- Objects side-by-side: Align objectA.right → objectB.left (side faces)");
            sysSb.AppendLine();
            sysSb.AppendLine("FOR CORNER/EDGE PLACEMENT:");
            sysSb.AppendLine("You must combine semantic points from different axes:");
            sysSb.AppendLine("- Corner of room: Use floor.left for X, floor.back for Z, floor.top for Y");
            sysSb.AppendLine("- Example: Place bed in northwest corner:");
            sysSb.AppendLine("  * bed.back.z aligns with northWall.front.z (back against north wall)");
            sysSb.AppendLine("  * bed.left.x aligns with westWall.front.x (left side against west wall)");
            sysSb.AppendLine("  * bed.bottom.y aligns with floor.top.y (standing on floor)");
            sysSb.AppendLine();
            sysSb.AppendLine("WHERE TO FIND SEMANTIC POINTS:");
            sysSb.AppendLine();
            sysSb.AppendLine("1. PREFAB CATALOG (for objects being placed):");
            sysSb.AppendLine("   - semanticPoints are in LOCAL coordinates (relative to pivot at [0,0,0])");
            sysSb.AppendLine("   - These are NOT yet transformed by position/rotation/scale");
            sysSb.AppendLine("   - Example: Wall4m prefab shows \"back\": [2, 1.5, -0.121] (local offset from pivot)");
            sysSb.AppendLine();
            sysSb.AppendLine("2. SCENE CONTEXT (for objects already placed):");
            sysSb.AppendLine("   - semanticPoints are in WORLD coordinates (absolute positions in scene)");
            sysSb.AppendLine("   - Already transformed by the object's position/rotation/scale");
            sysSb.AppendLine("   - Example: Wall4m instance shows \"back\": [5.0, 1.5, 8.0] (world position)");
            sysSb.AppendLine();
            sysSb.AppendLine("3. FOR OBJECTS YOU JUST CREATED THIS SESSION:");
            sysSb.AppendLine("   - They are NOT in Scene Context yet (scene hasn't been re-indexed)");
            sysSb.AppendLine("   - Get their LOCAL semantic points from Prefab Catalog");
            sysSb.AppendLine("   - Calculate world position: targetWorld = object_position + rotate(prefab_local_point, object_rotation) * object_scale");
            sysSb.AppendLine();
            sysSb.AppendLine("Key Concepts:");
            sysSb.AppendLine("- Pivot: Always at local [0,0,0]. Position parameter places the pivot in world space.");
            sysSb.AppendLine("- Semantic Points: Include 'pivot' at [0,0,0] for tracking pivot location in world space");
            sysSb.AppendLine("- Coordinate System (new scenes): Origin [0,0,0] = Southwest corner, +X=East, +Y=Up, +Z=North");
            sysSb.AppendLine("- Rotation: Y-axis most common. [0,90,0]: local[x,y,z]→world[-z,y,x] | [0,180,0]: local[x,y,z]→world[-x,y,-z]");
            sysSb.AppendLine();
            sysSb.AppendLine("WHEN SEMANTIC POINTS ARE MISSING:");
            sysSb.AppendLine("- Face/surface alignment is NOT POSSIBLE without semantic points");
            sysSb.AppendLine("- Check the Prefab Catalog's semanticPoints array - if the point name isn't listed, it's missing");
            sysSb.AppendLine("- ABORT face alignment and inform user: 'Prefab X lacks semantic point Y - use Semantic Annotator to add it'");
            sysSb.AppendLine("- Basic placement still works: LLM can position object with reasonable coordinates and rotation");
            sysSb.AppendLine("- Example: Can place table at [2.0, 0, 3.5] with rotation [0,90,0] without semantic points");
            sysSb.AppendLine();
            sysSb.AppendLine("Creation vs Modification:");
            sysSb.AppendLine("- If user asks to CREATE with modifications (e.g., 'create rock with rigidbody'), create FIRST, then ASK about modifications.");
            sysSb.AppendLine("- Do NOT automatically chain creation and modification.");
            sysSb.AppendLine();

            // Inject prompt library (custom prompts enabled by user)
            var promptSettings = PromptLibrarySettings.GetOrCreateSettings();
            string libraryPrompts = PromptLibraryLoader.LoadEnabledPrompts(promptSettings);

            if (!string.IsNullOrEmpty(libraryPrompts))
            {
                sysSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
                sysSb.AppendLine("## Additional Examples from Prompt Library");
                sysSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
                sysSb.AppendLine();
                sysSb.AppendLine(libraryPrompts);
            }

            systemMessage = sysSb.ToString();

            // ====================================================================
            // USER MESSAGE (Context Data + User Request)
            // ====================================================================
            var sb = new StringBuilder();

            // Section 2: Project Metadata
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Project Metadata");
            string projectMetadata = ReadArtifact(Path.Combine(ProjectIndexer.PROJECT_ARTIFACTS, "ProjectMetadata.json"));
            sb.AppendLine(projectMetadata);
            sb.AppendLine();

            // Section 3: Active Scene
            // NOTE: Use CURRENT active scene, not cached metadata (user may have switched scenes)
            Scene activeScene = SceneManager.GetActiveScene();
            string activeSceneName = activeScene.name;

            sb.AppendLine("## Active Scene");
            if (!string.IsNullOrEmpty(activeSceneName))
            {
                string sceneArtifact = ReadArtifact(Path.Combine(ProjectIndexer.SCENES_ARTIFACTS, $"{activeSceneName}.json"));
                sb.AppendLine(sceneArtifact);
            }
            else
            {
                sb.AppendLine("{}"); // No active scene
            }
            sb.AppendLine();

            // Section 4: Prefab Catalog
            sb.AppendLine("## Available Prefabs Catalog");
            string catalogJson = GeneratePrefabCatalog();
            sb.AppendLine(catalogJson);
            sb.AppendLine();

            // User prompt
            sb.AppendLine("---");
            sb.AppendLine($"User Request: {userPrompt}");

            string contextPack = sb.ToString();

            // Token budget awareness (warning only, no truncation for Day 1)
            int estimatedTokens = EstimateTokens(contextPack);
            if (estimatedTokens > tokenBudget)
            {
                Debug.LogWarning($"[AI Assistant] Context pack (~{estimatedTokens} tokens) exceeds budget ({tokenBudget}). " +
                                 "Proceeding anyway - GPT-5 supports up to 272,000 input tokens.");
            }

            return contextPack;
        }

        /// <summary>
        /// Reads an artifact file from disk.
        /// Returns empty JSON object if file doesn't exist or error occurs.
        /// </summary>
        /// <param name="path">Path to artifact file</param>
        /// <returns>File contents or "{}" if missing/error</returns>
        private static string ReadArtifact(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
                else
                {
                    Debug.LogWarning($"[AI Assistant] Artifact not found: {path}. Using empty placeholder.");
                    return "{}";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to read artifact {path}: {ex.Message}");
                return "{}";
            }
        }

        /// <summary>
        /// Estimates token count for a string.
        /// Uses simple approximation: ~4 characters per token (OpenAI's recommended fallback).
        /// NOTE: Actual token count may vary by ±20%. This is for budget awareness only.
        /// </summary>
        /// <param name="text">Text to estimate</param>
        /// <returns>Estimated token count</returns>
        private static int EstimateTokens(string text)
        {
            // Approximate token count - actual count may vary
            // OpenAI recommends ~4 chars per token as fallback when tiktoken unavailable
            return text.Length / 4;
        }

        /// <summary>
        /// Generates tools JSON - always returns 5 standard tools.
        /// Prefab catalog is provided separately in context, not as tools.
        /// </summary>
        /// <returns>JSON array of 5 tool definitions</returns>
        private static string GenerateToolsJson()
        {
            try
            {
                // Generate 5 standard tools (batch architecture)
                // Note: selectedTags parameter is ignored by new implementation
                string toolsJson = DynamicToolGenerator.GenerateToolsJson(null);

                if (string.IsNullOrEmpty(toolsJson))
                {
                    Debug.LogError("[AI Assistant] Tool generation returned null");
                    return "[]";
                }

                return toolsJson;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to generate tools JSON: {ex.Message}");
                return "[]";
            }
        }

        /// <summary>
        /// Generates prefab catalog JSON from registry.
        /// Filters by selected tags if any, otherwise includes all prefabs.
        /// </summary>
        /// <returns>JSON catalog string</returns>
        private static string GeneratePrefabCatalog()
        {
            try
            {
                // Load selected categories from ProjectSettings
                List<string> selectedTags = PrefabCategoryPersistence.LoadSelectedTags();

                // Get prefabs (filtered by tags if selected, otherwise all)
                List<PrefabMetadata> prefabs = PrefabRegistryCache.GetByTags(selectedTags);

                if (prefabs == null || prefabs.Count == 0)
                {
                    Debug.LogWarning("[AI Assistant] No prefabs found for selected categories");
                    return "{\"prefabs\":[]}";
                }

                // Generate catalog JSON
                string catalogJson = PrefabCatalogGenerator.GeneratePrefabCatalogJson(prefabs);

                Debug.Log($"[AI Assistant] Generated catalog with {prefabs.Count} prefab(s)");
                return catalogJson;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to generate prefab catalog: {ex.Message}");
                return "{\"prefabs\":[]}";
            }
        }
    }
}
