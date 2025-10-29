# Prompt Library Architecture

**Version:** 1.0.0
**Date:** 2025-01-29
**Status:** Simple implementation - no cost tracking, just enable/disable prompts

## Overview

The Prompt Library is a separate tool window that allows users to manage custom prompt snippets. Enabled prompts are automatically injected into the AI context to improve placement accuracy.

## Purpose

- Allow users to add custom examples for specific scenarios
- Provide diagnostic prompts that make the LLM explain its reasoning
- Enable quick iteration on prompt formulations

## Architecture

The Prompt Library is a **separate tool window** (like Prefab Scanner and Semantic Annotator):

```
Window > Context-Aware Scene Builder >
  ├─ Scene Builder (AIAssistantWindow)
  ├─ Prefab Scanner (PrefabScannerWindow)
  ├─ Semantic Annotator (SemanticAnnotatorWindow)
  └─ Prompt Library (PromptLibraryEditor) [NEW]
```

## User Workflow

1. User opens **Prompt Library** window
2. Browse prompts in tree view, check/uncheck to enable/disable
3. Preview selected prompts
4. Close window (settings saved automatically)
5. **Scene Builder** automatically uses enabled prompts

## File Structure

```
Assets/ContextAwareSceneBuilder/
  PromptLibrary/
    Examples/
      Beds/
        bed_against_wall.txt
        bed_in_corner.txt
      Tables/
        table_placement.txt
      Walls/
        wall_consistency.txt
      Scaled/
        scaled_furniture.txt
    Diagnostics/
      explain_calculation.txt
    Fixes/
      (user-created)
    README.md
```

**Prompt File Format:**
- Plain text (.txt) or markdown (.md)
- Optional headers: `# Name: ...` and `# Category: ...`
- Everything else is prompt content

## Components

### 1. PromptLibrarySettings.cs

**Type:** ScriptableObject
**Location:** `Assets/ContextAwareSceneBuilder/Settings/PromptLibrarySettings.asset`

**Purpose:** Stores which prompts are enabled

```csharp
public class PromptLibrarySettings : ScriptableObject
{
    [System.Serializable]
    public class PromptReference
    {
        public string relativePath;  // e.g., "Examples/Beds/bed_against_wall.txt"
        public bool enabled;
    }

    private List<PromptReference> _prompts;

    // Singleton
    public static PromptLibrarySettings GetOrCreateSettings()

    // Check if prompt is enabled
    public bool IsPromptEnabled(string relativePath)

    // Enable/disable a prompt
    public void SetPromptEnabled(string relativePath, bool enabled)

    // Get all enabled prompt paths
    public List<string> GetEnabledPromptPaths()
}
```

Matches the pattern used by AIAssistantSettings.

### 2. PromptLibraryLoader.cs

**Type:** Static utility class

**Purpose:** Scans folder and loads enabled prompts

```csharp
public static class PromptLibraryLoader
{
    // Scans PromptLibrary folder for .txt/.md files
    public static List<string> ScanPromptFiles()

    // Loads content of all enabled prompts, concatenated
    public static string LoadEnabledPrompts(PromptLibrarySettings settings)
}
```

**Implementation:**
- Uses `AssetDatabase.FindAssets()` to find .txt and .md files
- Reads file content with `System.IO.File.ReadAllText()`
- Returns concatenated string of all enabled prompts

### 3. PromptLibraryEditor.cs

**Type:** EditorWindow
**Menu:** Window > Context-Aware Scene Builder > Prompt Library

**UI Layout:**
```
┌───────────────────────────────────────────┐
│ Prompt Library                      [×]   │
├───────────────────────────────────────────┤
│ [Refresh]                                 │
├──────────────┬────────────────────────────┤
│ Tree View    │ Preview                    │
│              │                            │
│ ☐ Examples/  │ bed_against_wall.txt       │
│   ☐ Beds/    │                            │
│     ☑ bed... │ EXAMPLE: Bed placement...  │
│     ☐ bed... │ [content preview]          │
│   ☐ Tables/  │                            │
│ ☑ Diagnostic/│                            │
│              │                            │
└──────────────┴────────────────────────────┘
```

**Features:**
- Tree view showing folder structure
- Checkboxes to enable/disable individual prompts
- Preview pane shows selected prompt content
- Refresh button rescans files

**Implementation:** Unity IMGUI with simple tree view

### 4. ContextBuilder.cs Integration

Add this after built-in examples:

```csharp
// After built-in examples in system message
sysSb.AppendLine("EXAMPLE 3: Bed against wall...");
sysSb.AppendLine();

// Inject prompt library
var promptSettings = PromptLibrarySettings.GetOrCreateSettings();
string libraryPrompts = PromptLibraryLoader.LoadEnabledPrompts(promptSettings);

if (!string.IsNullOrEmpty(libraryPrompts))
{
    sysSb.AppendLine("## Additional Examples:");
    sysSb.AppendLine();
    sysSb.AppendLine(libraryPrompts);
    sysSb.AppendLine();
}

systemMessage = sysSb.ToString();
```

## Data Flow

```
User opens PromptLibraryEditor
           ↓
Scans PromptLibrary/ folder
           ↓
Displays tree with checkboxes
           ↓
User enables/disables prompts
           ↓
Saves to PromptLibrarySettings.asset
           ↓
User uses Scene Builder
           ↓
ContextBuilder.BuildContextPack()
           ↓
Loads enabled prompts
           ↓
Injects into system message
           ↓
Sent to OpenAI
```

## Initial Prompts (6 files)

1. **explain_calculation.txt** - Diagnostic prompt that forces LLM to show work
2. **bed_against_wall.txt** - Shows bed.back → wall.front alignment
3. **bed_in_corner.txt** - Shows corner placement combining X/Z axes
4. **table_placement.txt** - Basic table on floor
5. **wall_consistency.txt** - Emphasizes same semantic point for all walls
6. **scaled_furniture.txt** - Lamp on scaled table with scale multiplication

## Implementation Steps

1. ✅ Create folder structure + README
2. Create PromptLibrarySettings.cs
3. Create PromptLibraryLoader.cs
4. Modify ContextBuilder.cs
5. Create PromptLibraryEditor.cs
6. Write 6 initial prompt files
7. Test end-to-end
8. Git commit
