# Prompt Library

The Prompt Library system allows you to inject custom prompt snippets into the AI context to improve spatial reasoning, provide targeted examples, and debug placement issues.

## How It Works

1. **Create prompt files** - Add .txt or .md files to the appropriate category folders
2. **Enable prompts** - Use the Prompt Library Editor window to enable/disable prompts
3. **Auto-injection** - Enabled prompts are automatically injected into the system message

## Folder Structure

- **Examples/Beds/** - Examples of bed placement scenarios
- **Examples/Tables/** - Examples of table placement scenarios
- **Examples/Walls/** - Examples of wall placement scenarios
- **Examples/Scaled/** - Examples using scaled objects
- **Diagnostics/** - Prompts that make the LLM explain its reasoning
- **Fixes/** - Prompts that fix specific known issues

## Creating Custom Prompts

### File Format

Prompt files can be plain text (.txt) or markdown (.md). Optionally include headers:

```
# Name: My Custom Example
# Category: examples

Your prompt content here...
```

### Best Practices

- **Be specific** - Show complete worked examples with actual numbers
- **Show scale multiplication** - Always demonstrate scale ⊙ in calculations
- **Include verification** - Show the verification step at the end
- **Use real prefab data** - Reference actual semantic points from the catalog

### Example Prompt Structure

```
EXAMPLE: Table beside bed

Goal: Place table to the right of bed

Given: Bed at [2.0, 0, -3.0], rotation [0,0,0], scale [1,1,1]

Step 1: Check semantic points in Prefab Catalog
  Bed LOCAL: "right" [1.0, 0.5, 0.0]
  Table LOCAL: "left" [-0.5, 0.5, 0.0], "bottom" [0.0, 0.0, 0.0]

Step 2: Calculate bed.right world position
  bed.right LOCAL [1.0, 0.5, 0.0]
  Rotate [0,0,0]: [1.0, 0.5, 0.0] → [1.0, 0.5, 0.0]
  Apply scale [1,1,1]: [1.0, 0.5, 0.0] ⊙ [1,1,1] = [1.0, 0.5, 0.0]
  bed.right world = [2.0,0,-3.0] + [1.0,0.5,0.0] = [3.0, 0.5, -3.0]

Step 3: Calculate table pivot
  ... (complete calculation)

Step 4: Verify
  ... (verification check)
```

## Using the Prompt Library Editor

1. Open **Window → Context-Aware Scene Builder → Prompt Library**
2. Browse prompts in the tree view
3. Check/uncheck prompts to enable/disable them
4. Select a prompt to preview its content
5. Click "Refresh" to reload prompts from disk

## Tips

- Start with a few focused prompts, don't enable everything
- Use diagnostic prompts when debugging placement issues
- Create prompts for your specific use cases (e.g., your room layouts)
- Share successful prompts with your team

## Troubleshooting

**Prompts not showing up?**
- Click "Refresh" in the Prompt Library Editor
- Check that files have .txt or .md extension
- Verify files are in the PromptLibrary folder

**LLM still making errors?**
- Try the diagnostic prompt to see the LLM's reasoning
- Add a more specific example for your scenario
- Check that prompts are actually enabled

**Too many prompts?**
- Disable prompts you're not currently using
- More prompts = higher token usage
- Focus on quality over quantity
