# Semantic Local Space (SLS) Mathematics Reference

## Overview

This document explains the mathematical foundation of the Semantic Local Space (SLS) coordinate system used for object placement in Unity. The SLS approach provides a canonical coordinate frame for spatial reasoning while keeping all stored data truthful to the actual geometry.

## Core Problem

**Why do we need SLS?**

Different 3D models are authored with different forward directions:
- Some models face +Z (Unity convention)
- Some face +X (modeling software convention)
- Some face -Z (rotated in asset pipeline)

Without a canonical coordinate system, the LLM must:
1. Guess which direction is "front" for each object
2. Calculate rotations in inconsistent coordinate frames
3. Handle edge cases for each combination of object orientations

**Result:** Beds facing into walls, lamps rotated 90°, furniture misaligned.

## The SLS Solution

### Three Coordinate Spaces

```
LOCAL SPACE (per prefab)
    ↓ R_ls (rotation only, calculated from semantic normals)
SEMANTIC LOCAL SPACE (canonical, shared by all objects)
    ↓ R_ws (rotation only, per instance)
WORLD SPACE (Unity scene)
```

**Key Insight:** SLS is rotation-only. It shares the same origin (pivot) as local space—only the axes are reoriented.

### Coordinate Space Definitions

#### 1. Local Space (per prefab)
- **Origin:** Prefab's pivot point
- **Axes:** Whatever the artist/pipeline defined
- **Storage:** Prefab asset files (.prefab)
- **Truthfulness:** Fully truthful to mesh geometry

#### 2. Semantic Local Space (SLS) - Canonical
- **Origin:** Same as local space (pivot)
- **Axes:**
  - Front = +Z
  - Up = +Y
  - Right = +X
- **Purpose:** Consistent frame for LLM reasoning
- **Truthfulness:** Rotational transform of local (still truthful)

#### 3. World Space (per instance)
- **Origin:** Unity scene origin
- **Axes:** Unity's global axes
- **Storage:** Scene files, transform.position/rotation
- **Purpose:** Final placement in scene

## Mathematical Relationships

### Quaternion Notation

All rotations are represented as quaternions `[x, y, z, w]` where:
- `(x, y, z)` = rotation axis scaled by `sin(θ/2)`
- `w` = `cos(θ/2)`
- Identity (no rotation) = `[0, 0, 0, 1]`

### Per-Prefab: R_ls (Local → SLS)

**Calculation:**
```csharp
Quaternion R_ls = Quaternion.LookRotation(frontNormal, upNormal);
```

**Meaning:** R_ls rotates vectors from local space to SLS.

**Example:**
```
Car model with front facing +X in local space:
  frontNormal = [1, 0, 0]
  upNormal = [0, 1, 0]
  R_ls = Quaternion.LookRotation([1,0,0], [0,1,0])

Result: R_ls rotates local +X to SLS +Z (and local +Y to SLS +Y)
```

**Storage:** Calculated once during prefab scan, stored in PrefabMetadata

### Per-Instance: R_ws (SLS → World)

**Calculation:**
```csharp
Quaternion R_wl = instance.transform.rotation;  // Local → World
Quaternion R_ws = R_wl * Quaternion.Inverse(R_ls);
```

**Derivation:**
```
World = R_wl * Local           (Unity's transform hierarchy)
SLS = R_ls * Local             (Our canonical transform)

Solve for SLS → World:
  World = R_wl * Local
  World = R_wl * (R_ls⁻¹ * SLS)    (substitute Local = R_ls⁻¹ * SLS)
  World = (R_wl * R_ls⁻¹) * SLS

Therefore: R_ws = R_wl * R_ls⁻¹
```

**Example:**
```
Table rotated 90° in world (Y-axis):
  R_wl = [0, 0.707, 0, 0.707]  (90° around Y)
  R_ls = [0, 0, 0, 1]          (identity, table already canonical)
  R_ws = [0, 0.707, 0, 0.707] * [0, 0, 0, 1]⁻¹
  R_ws = [0, 0.707, 0, 0.707]  (same as R_wl)
```

**Storage:** Calculated during scene indexing, stored in scene context

## Data Flow Architecture

### Prefab Catalog (Static)

```json
{
  "prefabName": "TableSquareMedium",
  "scale": [0.341, 0.341, 0.341],
  "semanticLocalSpaceRotation": [0, 0, 0, 1],
  "semanticPoints": [
    ["top", 0.0, 2.383, 0.0, 0.0, 1.0, 0.0]
  ]
}
```

**Coordinates:** LOCAL (truthful to prefab mesh)
**Includes:** R_ls for transformation to SLS

### Scene Context (Dynamic)

```json
{
  "instanceId": 42,
  "prefab": "TableSquareMedium",
  "position": [3.5, 0, 4.2],
  "rotation": [0, 0, 0, 1],
  "scale": [0.341, 0.341, 0.341],
  "semanticPoints": [
    ["top", 0.0, 2.383, 0.0, 0.0, 1.0, 0.0]
  ],
  "slsAdapters": {
    "pivotWorld": [3.5, 0, 4.2],
    "rotationSLSToWorld": [0, 0, 0, 1]
  }
}
```

**Coordinates:** SLS (canonical, UNSCALED)
**Includes:** R_ws and p_wl for reference

## Critical Scale Handling

**Convention:** Scene context stores UNSCALED SLS offsets. Scale is applied only during world conversion.

### Why Unscaled?

1. **Consistency:** SLS is rotation-only by definition
2. **Reusability:** Same SLS data works for any scale
3. **Correctness:** Prevents double-scaling bugs
4. **Clarity:** Normals never scaled (they're directions)

### Conversion Formulas

#### Normal (Unit Direction)
```
normal_world = R_ws * normal_sls
```
- **NO scale** (normals are unit vectors)
- **NO translation** (normals are directions, not positions)

#### Offset from Pivot
```
offset_world = R_ws * (S ⊙ offset_sls)
```
- **Scale component-wise:** `S ⊙ offset = [Sx*x, Sy*y, Sz*z]`
- **Then rotate:** Apply R_ws
- **Result:** World-space offset vector

#### Point at Semantic Location
```
point_world = p_wl + offset_world
point_world = p_wl + R_ws * (S ⊙ offset_sls)
```
- **Translation:** Add pivot's world position
- **Complete affine transform:** Rotate, scale offset, then translate

### Example: Table Top World Position

Given:
```
top_sls = [0, 2.383, 0]         (unscaled SLS offset)
S = [0.341, 0.341, 0.341]       (instance scale)
R_ws = [0, 0, 0, 1]             (identity)
p_wl = [3.5, 0, 4.2]            (pivot world position)
```

Calculate:
```
1. Scale offset:
   scaled = [0.341, 0.341, 0.341] ⊙ [0, 2.383, 0]
   scaled = [0, 0.813, 0]

2. Rotate (identity, so unchanged):
   rotated = [0, 0, 0, 1] * [0, 0.813, 0]
   rotated = [0, 0.813, 0]

3. Translate:
   world = [3.5, 0, 4.2] + [0, 0.813, 0]
   world = [3.5, 0.813, 4.2]
```

**Verification:** Table scaled to 34.1% height. Local top at 2.383 → World top at 0.813 ✓

## Object Placement Mathematics

### Problem: Place lamp on table

**Given:**
- Table already in scene with world transform (R_wl_table, p_wl_table, S_table)
- Lamp prefab with R_ls_lamp
- Want: lamp.bottom contacts table.top

### Step 1: Transform Lamp to SLS

```
lamp.bottom_sls = R_ls_lamp * lamp.bottom_local
lamp.normal_sls = R_ls_lamp * lamp.normal_local
```

### Step 2: Calculate Alignment Rotation in SLS

**Two-Vector Alignment:**

```
1. Primary alignment (normal):
   desired_normal_sls = -table.top_normal_sls  (oppose)
   q1 = QuaternionFromTo(lamp.normal_sls, desired_normal_sls)

2. Secondary alignment (twist):
   rotated_up = q1 * lamp.up_sls
   projection = rotated_up - (rotated_up · desired_normal_sls) * desired_normal_sls
   projection_norm = normalize(projection)
   q2 = QuaternionFromTo(projection_norm, table.up_sls)

3. Final SLS rotation:
   R_sls_final = q2 * q1
```

**Why two vectors?**
- `q1` aligns the contact normals (bottom to top)
- `q2` removes arbitrary twist around the normal axis
- Combined: Fully constrained rotation

### Step 3: Convert Rotation to World

```
R_world_lamp = R_sls_final * R_ls_lamp
```

**Derivation:**
```
Local = R_ls⁻¹ * SLS                    (inverse of SLS = R_ls * Local)
World = R_world * Local                 (Unity transform)
World = R_world * (R_ls⁻¹ * SLS)
World = (R_world * R_ls⁻¹) * SLS

We want: SLS_aligned = R_sls_final * SLS_identity
So: (R_world * R_ls⁻¹) = R_sls_final
Therefore: R_world = R_sls_final * R_ls
```

### Step 4: Calculate Position

**A. Get table top in world:**
```
table.top_world = p_wl_table + R_ws_table * (S_table ⊙ table.top_sls)
```

**B. Calculate lamp anchor in world:**
```
lamp.anchor_world = R_world_lamp * (S_lamp ⊙ lamp.bottom_local)
```

**C. Solve for lamp pivot:**
```
p_world_lamp = table.top_world - lamp.anchor_world
```

**Why subtract anchor?**
```
Desired: anchor_world = table.top_world
anchor_world = p_world_lamp + R_world_lamp * (S_lamp ⊙ lamp.bottom_local)

Solve for p_world_lamp:
p_world_lamp = table.top_world - R_world_lamp * (S_lamp ⊙ lamp.bottom_local)
p_world_lamp = table.top_world - anchor_world
```

## Common Pitfalls

### ❌ Using R_ws_target for New Object
```csharp
// WRONG: R_ws is target's adapter, not for new object
R_world_new = R_ws_target * R_sls_final  // NO!
```
```csharp
// CORRECT: Use new object's R_ls
R_world_new = R_sls_final * R_ls_new     // YES!
```

### ❌ Forgetting Pivot Translation
```csharp
// WRONG: Lands at origin
point_world = R_ws * (S ⊙ offset_sls)
```
```csharp
// CORRECT: Add pivot position
point_world = p_wl + R_ws * (S ⊙ offset_sls)
```

### ❌ Scaling Normals
```csharp
// WRONG: Normals should stay unit length
normal_world = R_ws * (S ⊙ normal_sls)
```
```csharp
// CORRECT: Rotate only
normal_world = R_ws * normal_sls
```

### ❌ Double-Scaling
```csharp
// WRONG: If SLS points already scaled
offset_world = R_ws * (S ⊙ (S ⊙ offset_sls))
```
```csharp
// CORRECT: SLS points are unscaled, apply S once
offset_world = R_ws * (S ⊙ offset_sls)
```

### ❌ Wrong Quaternion Order
```csharp
// WRONG: Reversed order
R_world = R_ls * R_sls_final  // Applies alignment first, then local→SLS
```
```csharp
// CORRECT: Right-to-left application
R_world = R_sls_final * R_ls  // Applies local→SLS first, then alignment
```

## Verification Checklist

When implementing SLS transformations:

- [ ] **R_ls calculation:** Uses `Quaternion.LookRotation(front, up)` with truthful normals
- [ ] **R_ws calculation:** `R_wl * Quaternion.Inverse(R_ls)`
- [ ] **Scene context:** Stores UNSCALED SLS offsets
- [ ] **Normal conversion:** No scale, no translation: `R_ws * normal_sls`
- [ ] **Offset conversion:** Scale then rotate: `R_ws * (S ⊙ offset_sls)`
- [ ] **Point conversion:** Offset + pivot: `p_wl + offset_world`
- [ ] **New object rotation:** `R_sls_final * R_ls_new` (NOT using target's R_ws)
- [ ] **Position calculation:** Includes `+ p_wl` for target plane point
- [ ] **Anchor calculation:** Uses R_world_new (not target's rotation)

## Mathematical Guarantees

When correctly implemented:

1. **Consistency:** All objects reason in same canonical SLS frame
2. **Correctness:** Math ensures perfect alignment (within floating-point precision)
3. **Truthfulness:** All stored data remains truthful to original geometry
4. **Reusability:** R_ls calculated once per prefab, reused for all instances
5. **Efficiency:** Simple quaternion operations, no expensive re-calculations

## References

- **Architecture Document:** `architecture_normals.yaml` - Complete implementation specification
- **Unity Quaternions:** https://docs.unity3d.com/ScriptReference/Quaternion.html
- **Quaternion Math:** https://en.wikipedia.org/wiki/Quaternions_and_spatial_rotation
