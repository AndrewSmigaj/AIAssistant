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
            sysSb.AppendLine("[ ] Step 0: WRITE DOWN all scales and R_ls values BEFORE calculations (CRITICAL)");
            sysSb.AppendLine("    - For NEW object: Get scale and R_ls from Prefab Catalog");
            sysSb.AppendLine("    - For TARGET object: Get scale from Scene Context (or Prefab Catalog if not in scene)");
            sysSb.AppendLine("    - WRITE EXPLICITLY: \"TableSquareMedium: scale=[0.341,0.341,0.341], R_ls=[0,0,0,1]\"");
            sysSb.AppendLine("    - CRITICAL: Do NOT assume R_ls is identity [0,0,0,1]!");
            sysSb.AppendLine("      • Many prefabs have non-identity R_ls (e.g., BedDouble has R_ls=[0,1,0,0] for 180° Y rotation)");
            sysSb.AppendLine("      • ALWAYS read R_ls from catalog explicitly - missing this causes rotation errors");
            sysSb.AppendLine("      • R_ls rotates LOCAL coordinates to SLS (Semantic Local Space)");
            sysSb.AppendLine("    - ONLY override new object's scale if user explicitly specifies different scale");
            sysSb.AppendLine("    - Example: Table scale [0.341,0.341,0.341] with top_sls Y=2.383 → world top Y=0.813");
            sysSb.AppendLine();
            sysSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
            sysSb.AppendLine("## MANDATORY: ON-SURFACE PLACEMENT POLICY");
            sysSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
            sysSb.AppendLine();
            sysSb.AppendLine("When placing object(s) ON a surface (table, shelf, floor, bench, etc.), you MUST:");
            sysSb.AppendLine();
            sysSb.AppendLine("1. Use semantic anchor alignment: object.bottom → target.top");
            sysSb.AppendLine("   - NEVER use manual world position [x,y,z] for items on surfaces");
            sysSb.AppendLine("   - ALWAYS perform full Step 0-8 semantic calculations to compute pivot");
            sysSb.AppendLine("   - This ensures objects sit exactly ON surface, not floating or sunken");
            sysSb.AppendLine();
            sysSb.AppendLine("2. Run collision-aware surface placement (see Step 9b below)");
            sysSb.AppendLine("   - When placing MULTIPLE items on SAME surface (lamp + mug on table):");
            sysSb.AppendLine("   - Compute ideal pivot for each item using semantic alignment");
            sysSb.AppendLine("   - Detect LATERAL (X/Z) collisions between items on that surface");
            sysSb.AppendLine("   - Apply deterministic offset search to resolve overlaps");
            sysSb.AppendLine("   - Preserve vertical alignment (all items at same Y = surface.top)");
            sysSb.AppendLine();
            sysSb.AppendLine("4. If collision cannot be resolved within surface bounds:");
            sysSb.AppendLine("   - PAUSE and ask user: 'Cannot place [object] on [surface] without overlap.'");
            sysSb.AppendLine("   - Offer options: (1) enlarge surface, (2) remove existing items, (3) allow overlap");
            sysSb.AppendLine("   - NEVER proceed with unresolvable collision");
            sysSb.AppendLine();
            sysSb.AppendLine("EXAMPLE - Correct mug placement on table:");
            sysSb.AppendLine("  Step 0: TableSquareMedium scale=[0.341,0.341,0.341], R_ls=[0,0,0,1]");
            sysSb.AppendLine("          Table at pivot [2.0, 0.003, -2.0], R_ws=[0,0,0,1]");
            sysSb.AppendLine("          table.top_sls = [0, 2.430, 0] from catalog");
            sysSb.AppendLine("  Step 7: table_top_world = [2.0, 0.003, -2.0] + [0,0,0,1] * ([0.341,0.341,0.341] ⊙ [0,2.430,0])");
            sysSb.AppendLine("          table_top_world = [2.0, 0.003, -2.0] + [0, 0.829, 0] = [2.0, 0.832, -2.0]");
            sysSb.AppendLine("          Mug: bottom_local = [0.025, -0.002, 0], scale = [1,1,1], R_world = [0,0,0,1]");
            sysSb.AppendLine("          anchor_world = [0,0,0,1] * ([1,1,1] ⊙ [0.025, -0.002, 0]) = [0.025, -0.002, 0]");
            sysSb.AppendLine("          desired_mug_pivot = [2.0, 0.832, -2.0] - [0.025, -0.002, 0]");
            sysSb.AppendLine("          desired_mug_pivot = [1.975, 0.834, -2.0]");
            sysSb.AppendLine("  Step 9b: Collision check with lamp → overlap detected");
            sysSb.AppendLine("          Offset search: +0.30m east → final = [2.275, 0.834, -2.0]");
            sysSb.AppendLine("          Verify: mug.bottom_world.y = 0.832 = table.top_world.y ✓");
            sysSb.AppendLine();
            sysSb.AppendLine("ANTI-PATTERN - DO NOT DO THIS:");
            sysSb.AppendLine("  ✗ Manually choosing position [2.1, 0.95, -1.9] without semantic calculation");
            sysSb.AppendLine("  ✗ Result: Mug floats +0.116m above table, overlaps lamp, breaks physics");
            sysSb.AppendLine("  ✗ Why wrong: Ignored table scale, no anchor math, no collision resolution");
            sysSb.AppendLine();
            sysSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 1: Identify semantic points for alignment");
            sysSb.AppendLine("    - If multiple targets exist (e.g., 4 walls), explicitly identify WHICH target");
            sysSb.AppendLine("      Example: \"NorthWall.front\" not just \"wall.front\" - specify North/South/East/West");
            sysSb.AppendLine("    - Identify alignment: Which target semantic point? Which object semantic point?");
            sysSb.AppendLine("      Example: lamp.bottom (with normal [0,-1,0]) → table.top (with normal [0,1,0])");
            sysSb.AppendLine("    - CRITICAL - Wall interior vs exterior:");
            sysSb.AppendLine("      • When mounting on a wall's INTERIOR, target the wall's 'front' semantic point (interior face)");
            sysSb.AppendLine("      • Do NOT use wall's 'back' point (that is the exterior face outside the room)");
            sysSb.AppendLine("    - CRITICAL - Multi-axis alignment for objects that both stand and mount:");
            sysSb.AppendLine("      • WALLS ON FLOORS - MANDATORY RULE:");
            sysSb.AppendLine("        - Y-axis: MUST use wall.bottom → floor.top (NOT wall.back or wall.front!)");
            sysSb.AppendLine("        - X/Z-axis: Use face semantics (wall.back → floor.back, etc.)");
            sysSb.AppendLine("        - NEVER use face semantic points (back/front) for Y-axis vertical placement");
            sysSb.AppendLine("        - Verification: wall.bottom_world.y must equal floor.top_world.y ± 0.01");
            sysSb.AppendLine("        - Common mistake: Using wall.back for Y gives pivot.y = -1.55 (floor mid-wall)");
            sysSb.AppendLine("        - Correct result: Using wall.bottom for Y gives pivot.y = 0.0 (floor at wall base)");
            sysSb.AppendLine("      • Wall-mounted objects (shelves, pictures):");
            sysSb.AppendLine("        - Use object.back → wall.front for X/Z alignment (flush mounting)");
            sysSb.AppendLine("        - Use object.bottom → wall.top or floor.top for Y alignment (height)");
            sysSb.AppendLine("    - ABORT if target is ambiguous (\"which wall?\" - ask user to clarify)");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 2: Verify BOTH objects have required semantic points with normals");
            sysSb.AppendLine("    - Check Prefab Catalog for new object's semantic points [name, x, y, z, nx, ny, nz]");
            sysSb.AppendLine("    - Check Scene Context for target's semantic points in SLS");
            sysSb.AppendLine("    - If EITHER is missing: ABORT face/surface alignment");
            sysSb.AppendLine("    - Inform user: 'Prefab X lacks semantic point Y - use Semantic Annotator to add it'");
            sysSb.AppendLine("    - Basic placement (position + rotation) still works without semantic points");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 3: Transform NEW object's semantic points to SLS");
            sysSb.AppendLine("    - Get semantic point from Prefab Catalog (LOCAL coordinates)");
            sysSb.AppendLine("    - Apply R_ls to offset: point_sls = R_ls * point_local");
            sysSb.AppendLine("    - Apply R_ls to normal: normal_sls = R_ls * normal_local");
            sysSb.AppendLine("    - NEVER scale normals (they are unit direction vectors)");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 4: Get TARGET semantic points from Scene Context (already in SLS)");
            sysSb.AppendLine("    - Scene Context stores semantic points in UNSCALED SLS coordinates");
            sysSb.AppendLine("    - Scene Context includes slsAdapters: {pivotWorld, rotationSLSToWorld}");
            sysSb.AppendLine("    - Use R_ws from slsAdapters for SLS→world conversion");
            sysSb.AppendLine("    - NEVER use target.position for alignment - use semantic points only");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 5: Calculate alignment rotation in SLS using two-vector alignment");
            sysSb.AppendLine("    - Normals should OPPOSE for surface contact: desired_normal = -target_normal");
            sysSb.AppendLine("    - CRITICAL for wall-mounted objects:");
            sysSb.AppendLine("      • object.back (normal LOCAL −Z) → target.front (normal SLS +Z)");
            sysSb.AppendLine("      • Compute R_sls_final = QuaternionFromTo(object_normal_sls, −target_normal_sls)");
            sysSb.AppendLine("      • This typically results in 180° rotation about Y-axis for shelves/pictures");
            sysSb.AppendLine("      • Do NOT simply copy the wall's R_ws - calculate proper opposing alignment");
            sysSb.AppendLine("    - Use two-vector alignment algorithm (see below for full details):");
            sysSb.AppendLine("      1. Primary alignment: q1 = QuaternionFromTo(object_normal_sls, desired_normal_sls)");
            sysSb.AppendLine("      2. Secondary alignment: q2 to remove twist around normal");
            sysSb.AppendLine("      3. Final SLS rotation: R_sls_final = q2 * q1");
            sysSb.AppendLine("    - Result is quaternion in SLS space");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 6: Convert rotation to world space");
            sysSb.AppendLine("    - R_world_new = R_sls_final * R_ls_new");
            sysSb.AppendLine("    - R_ls_new comes from NEW object's catalog (NOT target's R_ws!)");
            sysSb.AppendLine("    - Output as quaternion [x, y, z, w]");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 7: Calculate position in world space");
            sysSb.AppendLine("    - Get target point in world: p_target = p_wl_target + R_ws_target * (S_target ⊙ offset_sls)");
            sysSb.AppendLine("    - Get anchor offset in world: anchor = R_world_new * (S_new ⊙ anchor_local)");
            sysSb.AppendLine("    - Solve for pivot: p_world_new = p_target - anchor");
            sysSb.AppendLine("    - Apply scale S only during world conversion, NEVER to normals");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 8: VERIFY calculation (MANDATORY for walls/furniture)");
            sysSb.AppendLine("    - Recalculate world positions and verify alignment");
            sysSb.AppendLine("    - Check alignment matches target (within 0.01 tolerance)");
            sysSb.AppendLine("    - MANDATORY checks for walls on floors:");
            sysSb.AppendLine("      • wall.bottom_world.y should equal floor.top_world.y ± 0.01");
            sysSb.AppendLine("      • If wall pivot.y = -1.55, you used wrong semantic point - RECALCULATE");
            sysSb.AppendLine("      • Correct wall pivot.y should be ≈ 0.0 when floor is at Y=0");
            sysSb.AppendLine("    - MANDATORY checks for ON-SURFACE placements (props on tables/shelves):");
            sysSb.AppendLine("      • Compute: target.top_world = p_wl_target + R_ws_target * (S_target ⊙ top_sls)");
            sysSb.AppendLine("      • Compute: object.bottom_world = p_world_new + R_world_new * (S_new ⊙ bottom_local)");
            sysSb.AppendLine("      • ASSERT: object.bottom_world.y == target.top_world.y ± 0.01");
            sysSb.AppendLine("      • If object floats (bottom.y > top.y + 0.01): ABORT, recalculate with correct anchor");
            sysSb.AppendLine("      • If object sinks (bottom.y < top.y - 0.01): ABORT, recalculate with correct anchor");
            sysSb.AppendLine("      • Example: Mug on table with scale [0.341,0.341,0.341] and top_sls Y=2.430:");
            sysSb.AppendLine("        table.top_world.y = 0.003 + 0.829 = 0.832");
            sysSb.AppendLine("        mug.bottom_world.y MUST equal 0.832 ± 0.01");
            sysSb.AppendLine("        If mug.bottom_world.y = 0.948, it's floating +0.116m → ABORT");
            sysSb.AppendLine("    - If verification fails: STOP and recalculate - do not proceed with incorrect placement");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 9: COLLISION DETECTION (required when placing multiple objects)");
            sysSb.AppendLine("    - CRITICAL: Check if proposed position overlaps with existing objects in scene");
            sysSb.AppendLine("    - For each existing object in Scene Context:");
            sysSb.AppendLine("      1. Compute axis-aligned bounding box (AABB) from semantic points:");
            sysSb.AppendLine("         • Find min/max X, Y, Z from all semantic points in world coordinates");
            sysSb.AppendLine("         • For object at pivot [px,py,pz] with semantic points in SLS:");
            sysSb.AppendLine("           world_point = pivot + R_ws * (scale ⊙ point_sls)");
            sysSb.AppendLine("         • AABB = {min: [minX, minY, minZ], max: [maxX, maxY, maxZ]}");
            sysSb.AppendLine("      2. Compute AABB for NEW object at proposed position:");
            sysSb.AppendLine("         • Use semantic points from catalog in LOCAL coordinates");
            sysSb.AppendLine("         • Transform to world: pivot_new + R_world_new * (scale_new ⊙ point_local)");
            sysSb.AppendLine("         • Get min/max bounds");
            sysSb.AppendLine("      3. Check for AABB overlap (intersection test):");
            sysSb.AppendLine("         • Overlap if: (new.minX < existing.maxX && new.maxX > existing.minX) AND");
            sysSb.AppendLine("                       (new.minY < existing.maxY && new.maxY > existing.minY) AND");
            sysSb.AppendLine("                       (new.minZ < existing.maxZ && new.maxZ > existing.minZ)");
            sysSb.AppendLine("         • Add small tolerance (e.g., 0.05m) for acceptable proximity");
            sysSb.AppendLine("    - EXCEPTIONS - Valid intentional overlaps (skip collision check):");
            sysSb.AppendLine("      • Floor objects under furniture (rugs under beds, carpets under tables)");
            sysSb.AppendLine("      • Decorative items ON surfaces (books on shelves, lamps on tables)");
            sysSb.AppendLine("      • Nested containment (items inside boxes, objects in rooms)");
            sysSb.AppendLine("      • Layered architecture (floor tiles, wall panels)");
            sysSb.AppendLine("      • Rule: If object A is being placed ON/UNDER object B using semantic alignment,");
            sysSb.AppendLine("        overlap is EXPECTED and CORRECT - do not treat as collision");
            sysSb.AppendLine("      • Example: \"Place rug under bed\" - rug.top aligns with bed.bottom, overlap is valid");
            sysSb.AppendLine("    - If collision detected AND not an intentional overlap:");
            sysSb.AppendLine("      • OPTION A: Choose alternative placement using semantic points of existing object");
            sysSb.AppendLine("        Example: If bed occupies center, place table to bed.right + offset");
            sysSb.AppendLine("      • OPTION B: Ask user for clarification on where to place object");
            sysSb.AppendLine("      • NEVER proceed with unintentional overlapping placement");
            sysSb.AppendLine("    - When user specifies vague placement (\"put table in room\"):");
            sysSb.AppendLine("      • Default to room center ONLY if no existing objects nearby");
            sysSb.AppendLine("      • If objects present, use their semantic points for relative positioning");
            sysSb.AppendLine("      • Example: \"Place table to the right of bed\" instead of centering blindly");
            sysSb.AppendLine();
            sysSb.AppendLine("[ ] Step 9b: COLLISION-AWARE SURFACE PLACEMENT (required for multiple items on same surface)");
            sysSb.AppendLine();
            sysSb.AppendLine("    When placing MULTIPLE props on the SAME surface (lamp + mug + vase on table):");
            sysSb.AppendLine();
            sysSb.AppendLine("    Algorithm:");
            sysSb.AppendLine("      A. Compute surface bounds in world:");
            sysSb.AppendLine("         target_top_world = p_wl_target + R_ws_target * (S_target ⊙ top_sls)");
            sysSb.AppendLine("         Get surface dimensions from semantic points (left, right, front, back)");
            sysSb.AppendLine();
            sysSb.AppendLine("      B. For EACH prop to place on surface:");
            sysSb.AppendLine("         1. Compute IDEAL pivot using semantic alignment (Steps 0-7):");
            sysSb.AppendLine("            anchor_world = R_world_prop * (S_prop ⊙ bottom_local)");
            sysSb.AppendLine("            ideal_pivot = target_top_world - anchor_world");
            sysSb.AppendLine();
            sysSb.AppendLine("         2. Compute FOOTPRINT (lateral AABB in X/Z plane):");
            sysSb.AppendLine("            • Transform ALL semantic points to world at ideal_pivot");
            sysSb.AppendLine("            • footprint = {minX, maxX, minZ, maxZ, Y_fixed}");
            sysSb.AppendLine("            • Y is CONSTANT (all props at same Y = surface.top)");
            sysSb.AppendLine("            • Use semantic points from catalog, NOT mesh bounds");
            sysSb.AppendLine();
            sysSb.AppendLine("      C. Sort props by footprint area (largest first):");
            sysSb.AppendLine("         area = (maxX - minX) * (maxZ - minZ)");
            sysSb.AppendLine("         Place largest items first for better packing");
            sysSb.AppendLine();
            sysSb.AppendLine("      D. Place each prop with collision resolution:");
            sysSb.AppendLine("         1. Test LATERAL overlap with already-placed-on-surface items:");
            sysSb.AppendLine("            overlap_XZ = (new.minX < existing.maxX + clearance) AND");
            sysSb.AppendLine("                         (new.maxX > existing.minX - clearance) AND");
            sysSb.AppendLine("                         (new.minZ < existing.maxZ + clearance) AND");
            sysSb.AppendLine("                         (new.maxZ > existing.minZ - clearance)");
            sysSb.AppendLine("            clearance = 0.05m (5cm minimum gap)");
            sysSb.AppendLine();
            sysSb.AppendLine("         2. If NO overlap: ACCEPT ideal_pivot, proceed to next prop");
            sysSb.AppendLine();
            sysSb.AppendLine("         3. If OVERLAP detected: run deterministic offset search:");
            sysSb.AppendLine("            • Offset distances: [0.05, 0.10, 0.15, 0.20] meters");
            sysSb.AppendLine("            • Directions (8-way): [+X, -X, +Z, -Z, +X+Z, +X-Z, -X+Z, -X-Z]");
            sysSb.AppendLine("              (East, West, North, South, NE, SE, NW, SW in world coordinates)");
            sysSb.AppendLine("            • For EACH distance d in [0.05, 0.10, 0.15, 0.20]:");
            sysSb.AppendLine("              For EACH direction [dx, dz] in 8 directions:");
            sysSb.AppendLine("                candidate_pivot = ideal_pivot + [dx*d, 0, dz*d]");
            sysSb.AppendLine("                Recompute footprint at candidate_pivot");
            sysSb.AppendLine("                Check: (a) no overlap with placed items, AND");
            sysSb.AppendLine("                       (b) footprint fully within surface bounds");
            sysSb.AppendLine("                If BOTH true: ACCEPT candidate_pivot, break search");
            sysSb.AppendLine();
            sysSb.AppendLine("         4. If offset search FAILS (all candidates invalid):");
            sysSb.AppendLine("            • Try rotating small props ±15° around Y-axis");
            sysSb.AppendLine("            • Retry offset search with rotated footprint");
            sysSb.AppendLine("            • If still fails: PAUSE and ask user:");
            sysSb.AppendLine("              'Cannot place [prop] on [surface] without overlap.'");
            sysSb.AppendLine("              'Options: (1) enlarge surface, (2) remove items, (3) allow overlap'");
            sysSb.AppendLine();
            sysSb.AppendLine("      E. Record final pivots and verify:");
            sysSb.AppendLine("         For EACH placed prop:");
            sysSb.AppendLine("           prop.bottom_world.y MUST equal target.top_world.y ± 0.01");
            sysSb.AppendLine("           If verification fails: RECALCULATE (offset changed X/Z only, NOT Y)");
            sysSb.AppendLine();
            sysSb.AppendLine("    CRITICAL NOTES:");
            sysSb.AppendLine("      • Offset search modifies ONLY X and Z (lateral position on surface)");
            sysSb.AppendLine("      • Y position is FIXED at target.top_world.y for all props");
            sysSb.AppendLine("      • Collision detection is LATERAL ONLY (X/Z plane)");
            sysSb.AppendLine("      • Vertical overlap with surface is EXPECTED and VALID");
            sysSb.AppendLine("      • Always derive AABB from semantic points, NEVER use mesh bounds");
            sysSb.AppendLine("      • Log all offset attempts for debugging if placement fails");
            sysSb.AppendLine();
            sysSb.AppendLine("    EXAMPLE - Mug and lamp on table:");
            sysSb.AppendLine("      Lamp (larger): ideal=[2.0, 0.832, -2.0] → placed at ideal (no overlap)");
            sysSb.AppendLine("      Mug (smaller): ideal=[1.975, 0.834, -2.0] → overlap with lamp");
            sysSb.AppendLine("                     Offset search: +0.30m east (+X direction)");
            sysSb.AppendLine("                     Final: [2.275, 0.834, -2.0] ✓ No overlap, on surface");
            sysSb.AppendLine();
            sysSb.AppendLine("ABSOLUTE RULES:");
            sysSb.AppendLine("• ALWAYS use quaternions [x,y,z,w] for rotations - NEVER Euler angles");
            sysSb.AppendLine("• ALWAYS use two-vector alignment for rotation calculation");
            sysSb.AppendLine("• NEVER scale normals (they are rotation-only unit vectors)");
            sysSb.AppendLine("• Scene Context stores UNSCALED SLS offsets - apply scale during world conversion only");
            sysSb.AppendLine("• Face/surface alignment REQUIRES semantic points with normals - ABORT if missing");
            sysSb.AppendLine("• NEVER use target.position for alignment - use semantic points only");
            sysSb.AppendLine("• ALWAYS use prefab's default scale from catalog unless user specifies override");
            sysSb.AppendLine("• NEVER proceed with ambiguous targets - if \"which wall?\", ask user or abort");
            sysSb.AppendLine("• ALWAYS postpone downstream placements if upstream placement failed");
            sysSb.AppendLine();
            sysSb.AppendLine("COLLISION & FOOTPRINT COMPUTATION:");
            sysSb.AppendLine("• ALWAYS derive AABB/footprint from semantic points, NEVER from mesh bounds or heuristics");
            sysSb.AppendLine("• Transform each semantic point to world: p_world = pivot + R_ws * (S ⊙ point_sls)");
            sysSb.AppendLine("• Compute min/max X, Y, Z from ALL transformed semantic points");
            sysSb.AppendLine("• For surface placement: footprint is lateral AABB (X/Z plane) at fixed Y");
            sysSb.AppendLine();
            sysSb.AppendLine("LOGGING & DEBUGGING (MANDATORY for surface placements):");
            sysSb.AppendLine("• Log Step 0-9 key intermediate values when placing objects:");
            sysSb.AppendLine("  - Step 0: scale, R_ls for both new object and target");
            sysSb.AppendLine("  - Step 7: target_top_world, anchor_world, ideal_pivot");
            sysSb.AppendLine("  - Step 8: bottom_world.y, top_world.y, verification result");
            sysSb.AppendLine("  - Step 9b: AABB before/after, offsets tried, final pivot");
            sysSb.AppendLine("• If placement fails, include full diagnostic in response:");
            sysSb.AppendLine("  - Why ideal pivot was rejected (collision, out of bounds)");
            sysSb.AppendLine("  - Which offset directions were attempted");
            sysSb.AppendLine("  - Coordinates of conflicting objects");
            sysSb.AppendLine("• Format calculations clearly so user can verify correctness");
            sysSb.AppendLine();
            sysSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
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
            sysSb.AppendLine("   - Format: 7-value array [name, x, y, z, nx, ny, nz]");
            sysSb.AppendLine("   - Includes normal vector for each semantic point");
            sysSb.AppendLine("   - Example: [\"top\", 0.0, 2.383, 0.0, 0.0, 1.0, 0.0] (local Y=2.383, normal up)");
            sysSb.AppendLine("   - Also includes semanticLocalSpaceRotation (R_ls): quaternion [x,y,z,w]");
            sysSb.AppendLine();
            sysSb.AppendLine("2. SCENE CONTEXT (for objects already placed):");
            sysSb.AppendLine("   - semanticPoints are in SLS coordinates (canonical semantic frame)");
            sysSb.AppendLine("   - UNSCALED offsets from instance pivot");
            sysSb.AppendLine("   - Format: same 7-value array [name, x, y, z, nx, ny, nz]");
            sysSb.AppendLine("   - Example: [\"top\", 0.0, 2.383, 0.0, 0.0, 1.0, 0.0] (SLS, unscaled)");
            sysSb.AppendLine("   - Includes slsAdapters: {pivotWorld: [x,y,z], rotationSLSToWorld: [qx,qy,qz,qw]}");
            sysSb.AppendLine("   - Use R_ws from slsAdapters to convert SLS→world");
            sysSb.AppendLine();
            sysSb.AppendLine("3. FOR OBJECTS YOU JUST CREATED THIS SESSION:");
            sysSb.AppendLine("   - They are NOT in Scene Context yet (scene hasn't been re-indexed)");
            sysSb.AppendLine("   - Get their LOCAL semantic points from Prefab Catalog");
            sysSb.AppendLine("   - Transform to SLS: point_sls = R_ls * point_local");
            sysSb.AppendLine("   - Convert to world: point_world = p_wl + R_ws * (S ⊙ point_sls)");
            sysSb.AppendLine();
            sysSb.AppendLine("Key Concepts:");
            sysSb.AppendLine("- Pivot: Always at local [0,0,0]. Position parameter places the pivot in world space.");
            sysSb.AppendLine("- Semantic Points: 7-value format [name, x, y, z, nx, ny, nz] with normal vectors");
            sysSb.AppendLine("- Coordinate System (new scenes): Origin [0,0,0] = Southwest corner, +X=East, +Y=Up, +Z=North");
            sysSb.AppendLine("- Rotation: Use quaternions [x,y,z,w] calculated via two-vector alignment algorithm");
            sysSb.AppendLine("- SLS (Semantic Local Space): Canonical frame where Front=+Z, Up=+Y, Right=+X");
            sysSb.AppendLine();
            sysSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
            sysSb.AppendLine("## Semantic Local Space (SLS) Coordinate System");
            sysSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
            sysSb.AppendLine();
            sysSb.AppendLine("You reason in SEMANTIC LOCAL SPACE (SLS) - a canonical coordinate frame where:");
            sysSb.AppendLine("- Front = +Z axis");
            sysSb.AppendLine("- Up = +Y axis");
            sysSb.AppendLine("- Right = +X axis");
            sysSb.AppendLine();
            sysSb.AppendLine("DATA YOU RECEIVE:");
            sysSb.AppendLine();
            sysSb.AppendLine("1. Prefab Catalog:");
            sysSb.AppendLine("   - semanticPoints in LOCAL coordinates (truthful mesh space)");
            sysSb.AppendLine("   - semanticLocalSpaceRotation (R_ls): quaternion [x,y,z,w] that rotates LOCAL→SLS");
            sysSb.AppendLine("   - Example: [\"top\", 0.0, 2.383, 0.0, 0.0, 1.0, 0.0] (local Y=2.383)");
            sysSb.AppendLine();
            sysSb.AppendLine("2. Scene Context:");
            sysSb.AppendLine("   - semanticPoints in SLS coordinates (already transformed for your reasoning)");
            sysSb.AppendLine("   - slsAdapters: {pivotWorld: [x,y,z], rotationSLSToWorld: [qx,qy,qz,qw]}");
            sysSb.AppendLine("   - Example: [\"top\", 0.0, 0.813, 0.0, 0.0, 1.0, 0.0] (SLS Y=0.813 after transformation)");
            sysSb.AppendLine();
            sysSb.AppendLine("REASONING WORKFLOW:");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 1: Transform prefab data to SLS (if using catalog)");
            sysSb.AppendLine("   - Multiply semantic points by R_ls: point_sls = R_ls * point_local");
            sysSb.AppendLine("   - Multiply normals by R_ls: normal_sls = R_ls * normal_local");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 2: Reason in SLS (everything is now canonical)");
            sysSb.AppendLine("   - Calculate alignments, spacings, rotations in SLS");
            sysSb.AppendLine("   - Use two-vector alignment for rotation calculation (see below)");
            sysSb.AppendLine("   - Output: R_sls_final (alignment rotation in SLS)");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 3: Convert to world for NEW object instantiation");
            sysSb.AppendLine("   - Rotation: R_world_new = R_sls_final * R_ls_new");
            sysSb.AppendLine("     (R_ls_new from catalog, R_sls_final from alignment)");
            sysSb.AppendLine();
            sysSb.AppendLine("   - Position: Calculate anchor point, then solve for pivot");
            sysSb.AppendLine("     1. Get target plane in world (from scene context SLS data):");
            sysSb.AppendLine("        planePoint_world = p_wl_target + R_ws_target * planePoint_sls_target");
            sysSb.AppendLine("        planeNormal_world = R_ws_target * planeNormal_sls_target");
            sysSb.AppendLine("        (Scene context provides SLS coordinates; use R_ws adapter to convert to world)");
            sysSb.AppendLine();
            sysSb.AppendLine("     2. Calculate new object's anchor offset in world:");
            sysSb.AppendLine("        anchor_world = R_world_new * (scale_new ⊙ anchor_local)");
            sysSb.AppendLine("        (Apply scale to offset, rotate to world; NEVER scale normals)");
            sysSb.AppendLine();
            sysSb.AppendLine("     3. Solve for pivot with clearance ε:");
            sysSb.AppendLine("        p_world_new = planePoint_world - ε * planeNormal_world - anchor_world");
            sysSb.AppendLine();
            sysSb.AppendLine("   - Output quaternions [x,y,z,w] (NOT Euler angles)");
            sysSb.AppendLine();
            sysSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
            sysSb.AppendLine("## Coordinate Systems & Conversions (Canonical Rules)");
            sysSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
            sysSb.AppendLine();
            sysSb.AppendLine("Scene context stores UNSCALED SLS offsets (rotation-only; no scale baked in).");
            sysSb.AppendLine("Apply instance scale S only during SLS→world conversion.");
            sysSb.AppendLine();
            sysSb.AppendLine("CONVERSION FORMULAS:");
            sysSb.AppendLine();
            sysSb.AppendLine("1. Normals (unit directions):");
            sysSb.AppendLine("   normal_world = R_ws * normal_sls");
            sysSb.AppendLine("   (NO scale, NO translation)");
            sysSb.AppendLine();
            sysSb.AppendLine("2. Offsets (from pivot):");
            sysSb.AppendLine("   offset_world = R_ws * (S ⊙ offset_sls)");
            sysSb.AppendLine();
            sysSb.AppendLine("   To get world point at semantic location:");
            sysSb.AppendLine("   point_world = p_wl + offset_world");
            sysSb.AppendLine();
            sysSb.AppendLine("   Combined: point_world = p_wl + R_ws * (S ⊙ offset_sls)");
            sysSb.AppendLine();
            sysSb.AppendLine("CRITICAL RULES:");
            sysSb.AppendLine("   - Never scale normals (they are unit directions, not positions)");
            sysSb.AppendLine("   - Never double-scale (apply S once during final world conversion)");
            sysSb.AppendLine("   - Semantic points in scene context are offsets from instance pivot in SLS");
            sysSb.AppendLine();
            sysSb.AppendLine("SANITY CHECK EXAMPLE:");
            sysSb.AppendLine("   Given: top_sls = [0, 2.383, 0], S = [0.341, 0.341, 0.341],");
            sysSb.AppendLine("          R_ws = [0,0,0,1] (identity), p_wl = [3.5, 0, 4.2]");
            sysSb.AppendLine();
            sysSb.AppendLine("   offset_world = R_ws * (S ⊙ top_sls) = [0, 0.813, 0]");
            sysSb.AppendLine("   point_world = p_wl + offset_world = [3.5, 0.813, 4.2]");
            sysSb.AppendLine("   normal_world = R_ws * [0,1,0] = [0, 1, 0]");
            sysSb.AppendLine();
            sysSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
            sysSb.AppendLine("## Two-Vector Alignment Algorithm");
            sysSb.AppendLine("═══════════════════════════════════════════════════════════════════════");
            sysSb.AppendLine();
            sysSb.AppendLine("When aligning an object to a surface/face, calculate rotation using two vectors:");
            sysSb.AppendLine();
            sysSb.AppendLine("INPUTS:");
            sysSb.AppendLine("- target_normal_sls: Normal of target surface in SLS (from scene context)");
            sysSb.AppendLine("- target_up_sls: Up direction of target in SLS (typically [0,1,0])");
            sysSb.AppendLine("- object_normal_local: Normal of object's semantic point in LOCAL (from catalog)");
            sysSb.AppendLine("- object_up_local: Object's up direction in LOCAL (typically [0,1,0])");
            sysSb.AppendLine();
            sysSb.AppendLine("STEP 1: Transform object vectors to SLS");
            sysSb.AppendLine("   object_normal_sls = R_ls_object * object_normal_local");
            sysSb.AppendLine("   object_up_sls = R_ls_object * object_up_local");
            sysSb.AppendLine();
            sysSb.AppendLine("STEP 2: Calculate desired alignment in SLS");
            sysSb.AppendLine("   For surface placement, normals should OPPOSE:");
            sysSb.AppendLine("   desired_normal_sls = -target_normal_sls");
            sysSb.AppendLine();
            sysSb.AppendLine("STEP 3: Two-vector alignment");
            sysSb.AppendLine("   A. Primary alignment (normal):");
            sysSb.AppendLine("      q1 = QuaternionFromTo(object_normal_sls, desired_normal_sls)");
            sysSb.AppendLine();
            sysSb.AppendLine("   B. Apply q1 to object's up vector:");
            sysSb.AppendLine("      rotated_up = q1 * object_up_sls");
            sysSb.AppendLine();
            sysSb.AppendLine("   C. Project to plane perpendicular to desired_normal:");
            sysSb.AppendLine("      projection = rotated_up - (rotated_up · desired_normal_sls) * desired_normal_sls");
            sysSb.AppendLine("      projection_normalized = normalize(projection)");
            sysSb.AppendLine();
            sysSb.AppendLine("   D. Secondary alignment (twist around normal):");
            sysSb.AppendLine("      q2 = QuaternionFromTo(projection_normalized, target_up_sls)");
            sysSb.AppendLine();
            sysSb.AppendLine("   E. Final rotation in SLS:");
            sysSb.AppendLine("      R_sls_final = q2 * q1");
            sysSb.AppendLine();
            sysSb.AppendLine("STEP 4: Convert to world rotation for tool call");
            sysSb.AppendLine("   R_world_new = R_sls_final * R_ls_new");
            sysSb.AppendLine("   (R_ls_new from new object's catalog, NOT target's R_ws)");
            sysSb.AppendLine("   Output as quaternion [x,y,z,w]");
            sysSb.AppendLine();
            sysSb.AppendLine("QUATERNION FROM-TO CALCULATION:");
            sysSb.AppendLine();
            sysSb.AppendLine("QuaternionFromTo(from, to):");
            sysSb.AppendLine("   // Normalize inputs");
            sysSb.AppendLine("   from = normalize(from)");
            sysSb.AppendLine("   to = normalize(to)");
            sysSb.AppendLine();
            sysSb.AppendLine("   // Check if vectors are parallel");
            sysSb.AppendLine("   dot_product = dot(from, to)");
            sysSb.AppendLine();
            sysSb.AppendLine("   if dot_product > 0.9999:");
            sysSb.AppendLine("      // Already aligned");
            sysSb.AppendLine("      return [0, 0, 0, 1]");
            sysSb.AppendLine();
            sysSb.AppendLine("   else if dot_product < -0.9999:");
            sysSb.AppendLine("      // Opposite directions - 180° rotation");
            sysSb.AppendLine("      // Find perpendicular axis");
            sysSb.AppendLine("      if abs(from.x) < 0.9:");
            sysSb.AppendLine("         axis = normalize(cross([1,0,0], from))");
            sysSb.AppendLine("      else:");
            sysSb.AppendLine("         axis = normalize(cross([0,1,0], from))");
            sysSb.AppendLine("      return [axis.x, axis.y, axis.z, 0]");
            sysSb.AppendLine();
            sysSb.AppendLine("   else:");
            sysSb.AppendLine("      // Normal case");
            sysSb.AppendLine("      axis = cross(from, to)");
            sysSb.AppendLine("      w = sqrt((length(from)^2) * (length(to)^2)) + dot_product");
            sysSb.AppendLine("      q = [axis.x, axis.y, axis.z, w]");
            sysSb.AppendLine("      return normalize(q)");
            sysSb.AppendLine();
            sysSb.AppendLine("EXAMPLE: Lamp on Table");
            sysSb.AppendLine();
            sysSb.AppendLine("Prefab Catalog:");
            sysSb.AppendLine("  TableSquareMedium:");
            sysSb.AppendLine("    semanticPoints: [[\"top\", 0.0, 2.383, 0.0, 0.0, 1.0, 0.0]] (local)");
            sysSb.AppendLine("    R_ls: [0, 0, 0, 1] (identity, already aligned)");
            sysSb.AppendLine("    scale: [0.341, 0.341, 0.341]");
            sysSb.AppendLine();
            sysSb.AppendLine("  LampSmall:");
            sysSb.AppendLine("    semanticPoints: [[\"bottom\", 0.005, 0.002, -0.008, 0.0, -1.0, 0.0]] (local)");
            sysSb.AppendLine("    R_ls: [0, 0, 0, 1] (identity)");
            sysSb.AppendLine("    scale: [1.0, 1.0, 1.0]");
            sysSb.AppendLine();
            sysSb.AppendLine("Scene Context (table already placed):");
            sysSb.AppendLine("  table_instance:");
            sysSb.AppendLine("    position: [3.5, 0, 4.2]");
            sysSb.AppendLine("    rotation: [0, 0, 0, 1] (identity in world)");
            sysSb.AppendLine("    scale: [0.341, 0.341, 0.341]");
            sysSb.AppendLine("    semanticPoints: [[\"top\", 0.0, 2.383, 0.0, 0.0, 1.0, 0.0]] (SLS, UNSCALED)");
            sysSb.AppendLine("    slsAdapters: {pivotWorld: [3.5, 0, 4.2], rotationSLSToWorld: [0,0,0,1]}");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 1: Transform lamp to SLS");
            sysSb.AppendLine("  lamp.bottom_sls = R_ls_lamp * lamp.bottom_local");
            sysSb.AppendLine("  lamp.bottom_sls = [0,0,0,1] * [0.005, 0.002, -0.008]");
            sysSb.AppendLine("  lamp.bottom_sls = [0.005, 0.002, -0.008]");
            sysSb.AppendLine();
            sysSb.AppendLine("  lamp.bottom_normal_sls = R_ls_lamp * [0, -1, 0] = [0, -1, 0]");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 2: Check alignment in SLS");
            sysSb.AppendLine("  table.top normal in SLS: [0, 1, 0] (up)");
            sysSb.AppendLine("  lamp.bottom normal in SLS: [0, -1, 0] (down)");
            sysSb.AppendLine("  They already oppose ✓ No rotation needed: R_sls_final = [0, 0, 0, 1]");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 3: Calculate world rotation");
            sysSb.AppendLine("  R_world_lamp = R_sls_final * R_ls_lamp");
            sysSb.AppendLine("  R_world_lamp = [0,0,0,1] * [0,0,0,1] = [0,0,0,1] (identity)");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 4: Calculate world position");
            sysSb.AppendLine("  A. Get table.top in world (from scene context SLS data):");
            sysSb.AppendLine("     table.top_sls = [0.0, 2.383, 0.0] (unscaled SLS from scene context)");
            sysSb.AppendLine("     table.top_world = p_wl_table + R_ws_table * (scale_table ⊙ table.top_sls)");
            sysSb.AppendLine("     table.top_world = [3.5,0,4.2] + [0,0,0,1] * ([0.341,0.341,0.341] ⊙ [0,2.383,0])");
            sysSb.AppendLine("     table.top_world = [3.5,0,4.2] + [0,0.813,0] = [3.5, 0.813, 4.2]");
            sysSb.AppendLine();
            sysSb.AppendLine("  B. Calculate lamp anchor in world (from catalog LOCAL data):");
            sysSb.AppendLine("     lamp.bottom_local = [0.005, 0.002, -0.008]");
            sysSb.AppendLine("     anchor_world = R_world_lamp * (scale_lamp ⊙ lamp.bottom_local)");
            sysSb.AppendLine("     anchor_world = [0,0,0,1] * ([1,1,1] ⊙ [0.005,0.002,-0.008])");
            sysSb.AppendLine("     anchor_world = [0.005, 0.002, -0.008]");
            sysSb.AppendLine();
            sysSb.AppendLine("  C. Solve for lamp pivot (ε=0 for exact contact):");
            sysSb.AppendLine("     lamp_pivot_world = table.top_world - anchor_world");
            sysSb.AppendLine("     lamp_pivot_world = [3.5,0.813,4.2] - [0.005,0.002,-0.008]");
            sysSb.AppendLine("     lamp_pivot_world = [3.495, 0.811, 4.208]");
            sysSb.AppendLine();
            sysSb.AppendLine("Output to tool:");
            sysSb.AppendLine("  position = [3.495, 0.811, 4.208]");
            sysSb.AppendLine("  rotation = [0, 0, 0, 1]");
            sysSb.AppendLine("  scale = [1.0, 1.0, 1.0]");
            sysSb.AppendLine();
            sysSb.AppendLine("EXAMPLE: Shelf Mounted on Interior Wall");
            sysSb.AppendLine();
            sysSb.AppendLine("Prefab Catalog:");
            sysSb.AppendLine("  ShelfWallSmall:");
            sysSb.AppendLine("    semanticPoints: [[\"back\", 0.101, 0.119, -1.212, 0.0, 0.0, -1.0]] (local)");
            sysSb.AppendLine("    R_ls: [0, 0, 0, 1] (identity)");
            sysSb.AppendLine("    scale: [1.0, 1.0, 1.0]");
            sysSb.AppendLine();
            sysSb.AppendLine("Scene Context (east wall already placed):");
            sysSb.AppendLine("  Wall4m_East:");
            sysSb.AppendLine("    position: [2.0, 0, 0]");
            sysSb.AppendLine("    rotation: [0, 0.707, 0, 0.707] (90° Y rotation)");
            sysSb.AppendLine("    scale: [1.0, 1.0, 1.0]");
            sysSb.AppendLine("    semanticPoints: [[\"front\", 0.0, 1.5, 2.0, 0.0, 0.0, 1.0]] (SLS, interior face)");
            sysSb.AppendLine("    slsAdapters: {pivotWorld: [2.0, 0, 0], rotationSLSToWorld: [0, 0.707, 0, 0.707]}");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 1: Transform shelf.back to SLS");
            sysSb.AppendLine("  shelf.back_sls = R_ls_shelf * shelf.back_local");
            sysSb.AppendLine("  shelf.back_sls = [0,0,0,1] * [0.101, 0.119, -1.212]");
            sysSb.AppendLine("  shelf.back_sls = [0.101, 0.119, -1.212]");
            sysSb.AppendLine();
            sysSb.AppendLine("  shelf.back_normal_sls = R_ls_shelf * [0, 0, -1] = [0, 0, -1]");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 2: Calculate alignment rotation in SLS");
            sysSb.AppendLine("  wall.front normal in SLS: [0, 0, 1] (pointing into room)");
            sysSb.AppendLine("  shelf.back normal in SLS: [0, 0, -1] (pointing backward)");
            sysSb.AppendLine("  Desired: shelf.back normal should OPPOSE wall.front normal → [0, 0, -1]");
            sysSb.AppendLine("  Current shelf.back normal: [0, 0, -1] ✓ Already opposing!");
            sysSb.AppendLine("  R_sls_final = [0, 1, 0, 0] (180° rotation about Y-axis to face shelf INTO room)");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 3: Calculate world rotation");
            sysSb.AppendLine("  R_world_shelf = R_sls_final * R_ls_shelf");
            sysSb.AppendLine("  R_world_shelf = [0, 1, 0, 0] * [0, 0, 0, 1] = [0, 1, 0, 0]");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 4: Calculate world position (flush mounting, no intrusion)");
            sysSb.AppendLine("  A. Get wall.front in world:");
            sysSb.AppendLine("     wall.front_sls = [0.0, 1.5, 2.0] (SLS from scene context)");
            sysSb.AppendLine("     wall.front_world = p_wl_wall + R_ws_wall * wall.front_sls");
            sysSb.AppendLine("     wall.front_world = [2.0,0,0] + [0,0.707,0,0.707] * [0,1.5,2.0]");
            sysSb.AppendLine("     wall.front_world = [2.0, 1.5, 2.0]");
            sysSb.AppendLine();
            sysSb.AppendLine("  B. Calculate shelf.back anchor in world:");
            sysSb.AppendLine("     After rotation R_world_shelf = [0,1,0,0], shelf.back_local transforms:");
            sysSb.AppendLine("     anchor_world = R_world_shelf * shelf.back_local");
            sysSb.AppendLine("     anchor_world = [0,1,0,0] * [0.101, 0.119, -1.212]");
            sysSb.AppendLine("     anchor_world = [-0.101, 0.119, 1.212] (180° flip about Y)");
            sysSb.AppendLine();
            sysSb.AppendLine("  C. Solve for shelf pivot (exact flush contact):");
            sysSb.AppendLine("     shelf_pivot_world = wall.front_world - anchor_world");
            sysSb.AppendLine("     shelf_pivot_world = [2.0, 1.5, 2.0] - [-0.101, 0.119, 1.212]");
            sysSb.AppendLine("     shelf_pivot_world = [2.101, 1.381, 0.788]");
            sysSb.AppendLine();
            sysSb.AppendLine("Output to tool:");
            sysSb.AppendLine("  position = [2.101, 1.381, 0.788]");
            sysSb.AppendLine("  rotation = [0, 1, 0, 0] (180° Y to face INTO room, NOT out through wall)");
            sysSb.AppendLine("  scale = [1.0, 1.0, 1.0]");
            sysSb.AppendLine();
            sysSb.AppendLine("KEY: Use wall.FRONT (interior), calculate proper opposing rotation, avoid copying wall's R_ws");
            sysSb.AppendLine();
            sysSb.AppendLine("EXAMPLE: Collision Detection - Table with Existing Bed");
            sysSb.AppendLine();
            sysSb.AppendLine("Scene Context:");
            sysSb.AppendLine("  BedDouble already placed:");
            sysSb.AppendLine("    position: [2.0, 0.003, -2.0]");
            sysSb.AppendLine("    rotation: [0, 0, 0, 1]");
            sysSb.AppendLine("    scale: [1.0, 1.0, 1.0]");
            sysSb.AppendLine("    semanticPoints (SLS): [[\"left\", -0.95, 0.5, 0.0], [\"right\", 0.95, 0.5, 0.0],");
            sysSb.AppendLine("                           [\"back\", 0.0, 0.5, -1.0], [\"front\", 0.0, 0.5, 1.0],");
            sysSb.AppendLine("                           [\"top\", 0.0, 0.6, 0.0], [\"bottom\", 0.0, 0.0, 0.0]]");
            sysSb.AppendLine();
            sysSb.AppendLine("User request: \"Place table in room\"");
            sysSb.AppendLine();
            sysSb.AppendLine("Step 9: Collision check");
            sysSb.AppendLine("  A. Compute bed's AABB:");
            sysSb.AppendLine("     For each semantic point, transform to world:");
            sysSb.AppendLine("       bed.left_world = [2.0,0.003,-2.0] + [0,0,0,1] * ([1,1,1] ⊙ [-0.95,0.5,0])");
            sysSb.AppendLine("                      = [2.0,0.003,-2.0] + [-0.95,0.5,0] = [1.05, 0.503, -2.0]");
            sysSb.AppendLine("       bed.right_world = [2.95, 0.503, -2.0]");
            sysSb.AppendLine("       bed.back_world = [2.0, 0.503, -3.0]");
            sysSb.AppendLine("       bed.front_world = [2.0, 0.503, -1.0]");
            sysSb.AppendLine("     AABB: min=[1.05, 0.003, -3.0], max=[2.95, 0.603, -1.0]");
            sysSb.AppendLine();
            sysSb.AppendLine("  B. Proposed table position (naive center): [2.0, 0.003, -2.0]");
            sysSb.AppendLine("     TableSquareMedium semanticPoints (catalog): [[\"left\", -0.5, 0.0, 0.0],");
            sysSb.AppendLine("                                                   [\"right\", 0.5, 0.0, 0.0], ...]");
            sysSb.AppendLine("     After scale [0.341,0.341,0.341]:");
            sysSb.AppendLine("       table.left_world = [2.0,0.003,-2.0] + [0,0,0,1] * (0.341 * [-0.5,0,0])");
            sysSb.AppendLine("                        = [2.0,0.003,-2.0] + [-0.17,0,0] = [1.83, 0.003, -2.0]");
            sysSb.AppendLine("       table.right_world = [2.17, 0.003, -2.0]");
            sysSb.AppendLine("     Table AABB (approx): min=[1.83, 0.003, -2.17], max=[2.17, 0.82, -1.83]");
            sysSb.AppendLine();
            sysSb.AppendLine("  C. Overlap test:");
            sysSb.AppendLine("     X: table.min(1.83) < bed.max(2.95) && table.max(2.17) > bed.min(1.05) ✓ OVERLAP");
            sysSb.AppendLine("     Y: table.min(0.003) < bed.max(0.603) && table.max(0.82) > bed.min(0.003) ✓ OVERLAP");
            sysSb.AppendLine("     Z: table.min(-2.17) < bed.max(-1.0) && table.max(-1.83) > bed.min(-3.0) ✓ OVERLAP");
            sysSb.AppendLine("     COLLISION DETECTED! Cannot place at [2.0, 0.003, -2.0]");
            sysSb.AppendLine();
            sysSb.AppendLine("  D. Alternative placement - use bed's semantic points:");
            sysSb.AppendLine("     bed.right_world = [2.95, 0.503, -2.0]");
            sysSb.AppendLine("     Place table to right of bed: align table.left → bed.right with 0.3m gap");
            sysSb.AppendLine("     table_x = bed.right.x + gap + table_width/2");
            sysSb.AppendLine("     table_x = 2.95 + 0.3 + 0.17 = 3.42");
            sysSb.AppendLine("     New position: [3.42, 0.003, -2.0] (clear of bed)");
            sysSb.AppendLine();
            sysSb.AppendLine("KEY: Always check collisions when placing multiple objects; use semantic points for relative positioning");
            sysSb.AppendLine();
            sysSb.AppendLine("ANTI-PATTERN: Wall Placement Using Wrong Semantic Point for Y-axis");
            sysSb.AppendLine();
            sysSb.AppendLine("WRONG approach (floor halfway up wall):");
            sysSb.AppendLine("  Wall4m.back_sls = [0.0, 1.5, 2.0, 0, 0, 1] (back face at Y=1.5)");
            sysSb.AppendLine("  Floor2m.back_sls = [0.0, 0.0, 1.0, 0, 0, 1] (floor edge at Y=0)");
            sysSb.AppendLine("  Using wall.back as single anchor for ALL axes:");
            sysSb.AppendLine("    pivot.y = floor.back.y - wall.back.y = 0.0 - 1.5 = -1.5");
            sysSb.AppendLine("  Result: Wall pivot at Y=-1.5, so wall.bottom is at Y=-1.5+0=-1.5 (underground!)");
            sysSb.AppendLine("          and floor.top is at Y=0, appearing halfway up the 3m wall");
            sysSb.AppendLine();
            sysSb.AppendLine("CORRECT approach (wall standing on floor):");
            sysSb.AppendLine("  Wall4m.bottom_sls = [0.0, 0.0, 2.0, 0, -1, 0] (bottom edge at Y=0)");
            sysSb.AppendLine("  Floor2m.top_sls = [0.0, 0.0, 1.0, 0, 1, 0] (top surface at Y=0)");
            sysSb.AppendLine("  Use multi-axis alignment:");
            sysSb.AppendLine("    Y-axis: wall.bottom → floor.top");
            sysSb.AppendLine("      pivot.y = floor.top.y - wall.bottom.y = 0.0 - 0.0 = 0.0");
            sysSb.AppendLine("    Z-axis: wall.back → floor.back (for horizontal positioning)");
            sysSb.AppendLine("      pivot.z = floor.back.z - wall.back.z = 1.0 - 2.0 = -1.0");
            sysSb.AppendLine("  Result: Wall pivot at [x, 0.0, -1.0], wall.bottom at Y=0 (on floor top) ✓");
            sysSb.AppendLine();
            sysSb.AppendLine("KEY: Vertical objects on horizontal surfaces require bottom→top for Y, face semantics for X/Z");
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
