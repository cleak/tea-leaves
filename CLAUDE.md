# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**TeaLeaves** is a Godot 4.6 project using **C# for all gameplay logic** with GDScript reserved for editor tooling only. It uses Jolt Physics and Forward Plus rendering with D3D12 on Windows.

## First-Time Setup

Before working on this project for the first time:

```powershell
# 1. Restore NuGet packages (required for C# compilation)
dotnet restore

# 2. Verify C# builds successfully
dotnet build -warnaserror

# 3. Initialize input actions (adds WASD, jump, etc. to project.godot)
pwsh ./tools/godot.ps1 --headless --script res://tools/setup_input_actions_cli.gd

# 4. Run tests to verify everything works
dotnet test
pwsh ./tools/test.ps1
```

If `dotnet build` fails with missing SDK errors, ensure you have .NET 8.0 SDK installed and Godot 4.6+ Mono.

## Core Tenets

- **C# for gameplay**, GDScript only for editor tooling or tiny glue
- **NEVER write GDScript** for gameplay unless absolutely necessary
- **Composition over inheritance** for Nodes
- **Typed EventBus** for cross-system communication
- **Data-driven configs** using Godot Resource assets (`[GlobalClass]` C# classes)
- **Fail-fast validation**: Misconfigured objects are disabled and logged via `GD.PushError()`
- **Deterministic state machines** with explicit state transitions
- **Test-driven debugging**: Create a failing test first, verify it fails, fix the code, verify the test passes

## Language Usage

- **C#** for all gameplay logic, systems, and tests
- **GDScript** ONLY for editor tools and scene glue scripts
- Never use GDScript for gameplay logic

### Namespace Convention
```
TeaLeaves.*           # All gameplay code
TeaLeaves.Systems.*   # Core systems (EventBus, state machines, etc.)
```

## EventBus Pattern

The EventBus provides **typed, decoupled communication** between systems. Use it when:
- Systems need to communicate without direct references
- Multiple listeners need to respond to the same event
- You want to avoid tight coupling between game systems

### When to Use EventBus vs Godot Signals

| Use EventBus | Use Godot Signals |
|--------------|-------------------|
| Cross-system communication (UI ↔ Gameplay) | Parent-child node communication |
| Global events (game state changes, achievements) | Local component events (animation finished) |
| Multiple unrelated listeners | Single known listener |
| Events that need to survive scene changes | Scene-local events |

### EventBus Implementation

Create the EventBus as an AutoLoad in `game/EventBus.cs`:

```csharp
using Godot;
using System;
using System.Collections.Generic;

namespace TeaLeaves.Systems
{
    /// <summary>
    /// Typed event bus for decoupled cross-system communication.
    /// Register as AutoLoad: EventBus="*res://game/EventBus.cs"
    /// </summary>
    public partial class EventBus : Node
    {
        public static EventBus Instance { get; private set; }

        public override void _Ready()
        {
            Instance = this;
        }

        // --- Event Delegates (define one per event type) ---

        public delegate void PlayerDamagedHandler(int damage, Node3D source);
        public delegate void ItemCollectedHandler(string itemId, int quantity);
        public delegate void GameStateChangedHandler(GameState oldState, GameState newState);
        public delegate void InteractionHandler(Node3D target, string verbId);

        // --- Events (subscribers connect to these) ---

        public event PlayerDamagedHandler PlayerDamaged;
        public event ItemCollectedHandler ItemCollected;
        public event GameStateChangedHandler GameStateChanged;
        public event InteractionHandler InteractionStarted;
        public event InteractionHandler InteractionCompleted;

        // --- Emit Methods (publishers call these) ---

        public void EmitPlayerDamaged(int damage, Node3D source)
        {
            PlayerDamaged?.Invoke(damage, source);
        }

        public void EmitItemCollected(string itemId, int quantity = 1)
        {
            ItemCollected?.Invoke(itemId, quantity);
        }

        public void EmitGameStateChanged(GameState oldState, GameState newState)
        {
            GameStateChanged?.Invoke(oldState, newState);
        }

        public void EmitInteractionStarted(Node3D target, string verbId)
        {
            InteractionStarted?.Invoke(target, verbId);
        }

        public void EmitInteractionCompleted(Node3D target, string verbId)
        {
            InteractionCompleted?.Invoke(target, verbId);
        }
    }

    // Define enums/types used by events
    public enum GameState { MainMenu, Playing, Paused, Cutscene, GameOver }
}
```

### Subscribing to Events

```csharp
using Godot;
using TeaLeaves.Systems;

public partial class HealthBar : Control
{
    public override void _Ready()
    {
        // Subscribe to event
        EventBus.Instance.PlayerDamaged += OnPlayerDamaged;
    }

    public override void _ExitTree()
    {
        // CRITICAL: Always unsubscribe to prevent memory leaks
        EventBus.Instance.PlayerDamaged -= OnPlayerDamaged;
    }

    private void OnPlayerDamaged(int damage, Node3D source)
    {
        // Update UI in response to event
        GD.Print($"Player took {damage} damage from {source.Name}");
        UpdateHealthDisplay();
    }
}
```

### Emitting Events

```csharp
using Godot;
using TeaLeaves.Systems;

public partial class Enemy : CharacterBody3D
{
    [Export] public int AttackDamage { get; set; } = 10;

    private void Attack(Node3D target)
    {
        // Emit event - all subscribers will be notified
        EventBus.Instance.EmitPlayerDamaged(AttackDamage, this);
    }
}
```

### Adding New Events

To add a new event type:

1. **Define the delegate** with typed parameters:
   ```csharp
   public delegate void QuestCompletedHandler(string questId, int rewardXp);
   ```

2. **Declare the event**:
   ```csharp
   public event QuestCompletedHandler QuestCompleted;
   ```

3. **Create the emit method**:
   ```csharp
   public void EmitQuestCompleted(string questId, int rewardXp)
   {
       QuestCompleted?.Invoke(questId, rewardXp);
   }
   ```

### EventBus Best Practices

1. **Always unsubscribe in `_ExitTree()`** - Prevents memory leaks and null reference errors
2. **Use descriptive event names** - `PlayerDamaged` not `Damaged`, `ItemCollected` not `Item`
3. **Keep event parameters immutable** - Pass primitives or readonly structs, not mutable objects
4. **Don't emit events in constructors** - Wait until `_Ready()` when EventBus.Instance is available
5. **Validate before emitting** - Check that EventBus.Instance is not null in edge cases

### Testing EventBus

```csharp
[TestSuite]
public class EventBusTests
{
    [TestCase]
    public void EmitPlayerDamaged_NotifiesSubscribers()
    {
        // Arrange
        var bus = new EventBus();
        int receivedDamage = 0;
        bus.PlayerDamaged += (damage, source) => receivedDamage = damage;

        // Act
        bus.EmitPlayerDamaged(25, null);

        // Assert
        AssertInt(receivedDamage).IsEqual(25);
    }
}
```

## C# Conventions

- All physics logic in `_PhysicsProcess(double delta)`
- Use `[Export]` with proper hints for editor properties
- Use `[GlobalClass]` for custom Resource types
- Validate node setup in `_Ready()` with asserts
- Use `GD.PushError()` for runtime issues, don't create silent fallbacks
- **GlobalPosition requires scene tree** - Add node to tree BEFORE setting `GlobalPosition`:
  ```csharp
  // WRONG: GlobalPosition before AddChild causes errors
  obstacle.GlobalPosition = pos;
  parent.AddChild(obstacle);

  // CORRECT: AddChild first, then GlobalPosition
  parent.AddChild(obstacle);
  obstacle.GlobalPosition = pos;
  ```
- **Nullable fields initialized in `_Ready()`** - Use `= null!` for fields set in `_Ready()` rather than the constructor. This avoids CS8618 warnings-as-errors while keeping `<Nullable>enable</Nullable>`:
  ```csharp
  // WRONG: CS8618 warning - non-nullable field not initialized in constructor
  private Sprite3D _visual;
  private AudioStreamPlayer _audio;

  // CORRECT: null-forgiving operator tells compiler these are set before use
  private Sprite3D _visual = null!;
  private AudioStreamPlayer _audio = null!;

  public override void _Ready()
  {
      _visual = GetNode<Sprite3D>("Visual");
      _audio = GetNode<AudioStreamPlayer>("Audio");
  }
  ```

### Resource Patterns
Custom Resource classes for data-driven configs:
```csharp
using Godot;

[GlobalClass]
public partial class ItemData : Resource
{
    [Export] public string DisplayName { get; set; }
    [Export] public int StackSize { get; set; } = 1;
    [Export] public Texture2D Icon { get; set; }
}
```
Name resource files with descriptive suffixes: `sword.item.tres`, `open.verb.tres`

### Node References in Hand-Written Scenes

**CRITICAL**: When writing `.tscn` files by hand (not via the Godot editor), you CANNOT use typed node exports directly. The scene file stores NodePaths, but typed exports expect object references.

**WRONG** - This will silently fail to resolve:
```csharp
// In C#
[Export] public Runner? Player { get; set; }  // Will be null!
```
```ini
# In .tscn - NodePath syntax doesn't deserialize to typed reference
Player = NodePath("../Runner")
```

**CORRECT** - Use NodePath export and resolve manually:
```csharp
// In C#
[Export] public NodePath PlayerPath { get; set; } = "";
private Runner? _player;

public override void _Ready()
{
    if (!string.IsNullOrEmpty(PlayerPath))
    {
        _player = GetNodeOrNull<Runner>(PlayerPath);
    }
}
```
```ini
# In .tscn - matches the NodePath property name
PlayerPath = NodePath("../Runner")
```

**When to use which approach:**
| Scenario | Approach |
|----------|----------|
| Hand-written .tscn files | `[Export] NodePath` + resolve in `_Ready()` |
| Editor-created scenes only | `[Export] TypedNode?` works (editor serializes correctly) |
| Primitives (float, string, int) | `[Export]` works in both cases |
| Resources (PackedScene, Texture2D) | `[Export]` works in both cases |

**Naming convention**: When using NodePath exports, suffix with `Path`:
- `PlayerPath` not `Player`
- `SpawnerPath` not `Spawner`
- `GamePath` not `Game`

## Key Commands

### Building & Testing C#
```powershell
# Build C# code (must pass before commits)
dotnet restore
dotnet build -warnaserror

# Run C# tests (including engine-aware tests via GdUnit4)
dotnet test
```

### Running Godot
```bash
# Run Godot (resolves executable via GODOT4_MONO_EXE env var or standard paths)
pwsh ./tools/godot.ps1

# Run headless (for scripts/linting)
pwsh ./tools/godot.ps1 --headless --script res://path/to/script.gd
```

### Testing (gdUnit4)
```bash
# Run all tests (60s timeout)
pwsh ./tools/test.ps1

# Run tests in specific directory
pwsh ./tools/test.ps1 -Test "res://test/unit/"

# Run a specific test file
pwsh ./tools/test.ps1 -Test "res://test/unit/test_example.gd"

# Continue running all tests even after failures
pwsh ./tools/test.ps1 -Continue

# Custom timeout (default 60s)
pwsh ./tools/test.ps1 -TimeoutSeconds 120
```

Test exit codes: 0=pass, 1=failures, 124=timeout.

**Troubleshooting**: If tests fail with "GdUnitCmdTool.gd not found", ensure `addons/gdUnit4/bin/` exists with `GdUnitCmdTool.gd` and `GdUnitCopyLog.gd`. These files are required for CLI test execution.

### Validation & Linting
```bash
# Full project lint (UIDs + scene warnings)
pwsh ./tools/godot.ps1 --headless --script res://tools/lint_project.gd

# Lint specific scenes
pwsh ./tools/godot.ps1 --headless --script res://tools/lint_project.gd -- --scene res://path/to/scene.tscn

# Lint all shaders
pwsh ./tools/godot.ps1 --headless --script res://tools/lint_shaders.gd

# Lint single shader (use res:// path)
pwsh ./tools/godot.ps1 --headless --script res://tools/lint_shaders.gd -- res://path/to/shader.gdshader

# Lint gdUnit4 test files for common issues
pwsh ./tools/lint_tests.ps1

# GDScript linting (gdlint is on PATH)
gdlint path/to/file.gd
```

#### lint_project.gd Options
- `--scene res://path.tscn` - Lint specific scene(s), can be repeated
- `--all` - Lint all scenes (default behavior)
- `--json` - Output results as JSON for machine parsing
- `--fail-on-warn` - Treat warnings as failures (non-zero exit code)
- `--uids-only` - Skip scene warnings, only validate UIDs
- `--warnings-only` - Skip UID validation, only check scene warnings

**Always lint after changes:**
1. `gdlint` for modified GDScript files
2. `lint_project.gd` for scene/UID validation
3. `lint_shaders.gd` for modified shaders
4. Use short timeouts (20s max) when running Godot commands

### Setup
```bash
# Configure default input actions
pwsh ./tools/godot.ps1 --headless --script res://tools/setup_input_actions_cli.gd
```

To add new input actions, edit `tools/setup_input_actions_cli.gd` and add entries to the `actions` dictionary, then re-run the script. Example:
```gdscript
var actions := {
    "my_action": [KEY_F],           # Single key
    "other_action": [KEY_G, KEY_H], # Multiple keys (alternatives)
}
```

## Project Structure

```
res://
  scripts/         # C# gameplay code
  actors/          # Player and NPCs (scenes)
  levels/          # Level scenes and scripts
  ui/              # HUD and menus
  util/            # Camera rigs, markers, utilities
  game/            # AutoLoads and global state (DevTools, EventBus)
  data/            # Resource definitions (items, verbs)
  test/            # gdUnit4 test suites
    sequences/     # Input simulation sequences (JSON files)
  tools/           # Linting and setup scripts (GDScript, not shipped)
  addons/          # Third-party addons (gdUnit4)
```

## GDScript Conventions (Editor Tools Only)

GDScript is reserved for editor tooling and tiny glue scripts. For gameplay logic, use C#.

### Code Style
- **Static typing everywhere**: `var player: Player`, `func get_items() -> Array[ItemData]`
- **@export** for editor-exposed properties with clear type hints
- **Signals** for decoupling: define at top of class, emit with descriptive names
- Group related functionality with comment headers: `# --- Navigation ---`
- Avoid abbreviations unless extremely common (`hp_max` is ok, avoid `hp_dmg`)

### Comments
- Every script should have a short description of what it does
- Keep comments meaningful and up to date
- Do NOT add temporary comments like "Removed old system" or "Updated from 1.0"

### Node Organization
- Keep nodes self-contained with clear responsibilities
- Use groups for querying: `add_to_group("interactable")`
- Nav meshes should be done by groups
- Validate node setup in `_ready()` with asserts for required children/properties

### Error Handling
- **Fail fast**: use `assert()` for invariants
- Use `push_error()` for runtime issues needing logging
- Implement `_get_configuration_warnings()` for editor-time validation
- Do NOT create fallbacks when code is expected to work—raise errors and flag problems clearly

## Validation Pipeline

Before committing:
1. `dotnet build -warnaserror` - C# must compile without warnings
2. `dotnet test` - All C# tests must pass
3. `pwsh ./tools/test.ps1` - All gdUnit4 tests must pass (GDScript + C# via Godot runtime)
4. `pwsh ./tools/godot.ps1 --headless --script res://tools/lint_project.gd` (UIDs + scene warnings)
5. If GDScript modified: `gdlint path/to/file.gd` for style, plus Godot's `--check-only` for semantic analysis
6. Always check scenes for errors after editing

## Important Notes

- **Physics Layers**: Interactables use layer 2, ground/navigation uses layer 1
- **Platform**: Windows (D3D12 rendering)
- **Godot Version**: Requires Godot 4.6+ Mono (set via `GODOT_VERSION` env var)
- **File Paths**: Use complete absolute Windows paths with drive letters and backslashes for all file operations
- **Commit frequently** to git

### Hand-Writing Scene Files (.tscn)

When writing `.tscn` files by hand instead of using the Godot editor:

1. **UIDs will be regenerated by the linter** - The lint_project.gd script may change UIDs to valid format. After creating scenes, run the linter and update any referencing scenes with the new UIDs.

2. **Check UID references after linting** - If scene A references scene B via `uid://...`, and B's UID changes, A will fail to load. Always verify references match.

3. **Use `format=3` not `load_steps`** - Modern .tscn format:
   ```ini
   [gd_scene format=3 uid="uid://..."]
   ```

4. **After creating hand-written scenes**:
   ```powershell
   # 1. Lint to validate/fix UIDs
   pwsh ./tools/godot.ps1 --headless --script res://tools/lint_project.gd

   # 2. Check the scene files for updated UIDs
   # 3. Update any scenes that reference them with the new UIDs

   # 4. Build and run to verify
   dotnet build -warnaserror
   pwsh ./tools/godot.ps1
   ```

5. **Prefer letting Godot generate scenes** - Use the editor when possible; hand-write only when necessary (e.g., automation, templates).

6. **Always commit `.uid` files** - Godot 4.4+ generates `.uid` files (e.g., `*.cs.uid`, `*.gd.uid`) for scripts and shaders. These MUST be committed to git. If omitted, UID references break when cloning the project.

## AutoLoads

AutoLoads go in `game/` and are registered in `project.godot`. To add a new AutoLoad:

1. Create the C# class in `game/`, e.g., `game/GameState.cs`:
   ```csharp
   using Godot;

   public partial class GameState : Node
   {
       public static GameState Instance { get; private set; }

       public override void _Ready()
       {
           Instance = this;
       }
   }
   ```
2. Add to `project.godot` under `[autoload]`:
   ```ini
   [autoload]
   GameState="*res://game/GameState.cs"
   ```
   The `*` prefix means it's a singleton (most common).

## Testing Infrastructure

### Two GdUnit4 Runners

| Runner | Use For | Command |
|--------|---------|---------|
| **dotnet test** | C# logic tests (via `gdUnit4.test.adapter`) | `dotnet test` |
| **Godot CLI** | GDScript tests + tests needing engine runtime | `pwsh ./tools/test.ps1` |

Both use GdUnit4 - same test syntax, different execution environments.

### C# Testing with gdUnit4

**Required packages** (in `.csproj`):
```xml
<PackageReference Include="gdUnit4.api" Version="5.*" />
<PackageReference Include="gdUnit4.test.adapter" Version="3.*" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.*" />
```

**Test pattern:**
```csharp
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class GravityHelperTests
{
    [TestCase]
    public void GetGravityDirection_At0Degrees_ReturnsDown()
    {
        var direction = GravityHelper.GetGravityDirection(0f);
        AssertFloat(direction.Y).IsEqual(-1f);
    }
}
```

### Scene/Node Tests (Require Godot Runtime)

For tests that instantiate Nodes or load scenes, add `[RequireGodotRuntime]`:

```csharp
[TestCase]
[RequireGodotRuntime]
public async Task Player_TakeDamage_ReducesHealth()
{
    var runner = ISceneRunner.Load("res://actors/Player.tscn");
    await runner.AwaitIdleFrame();
    // test code...
}
```

#### GdUnit4 Scene Test Pattern
```csharp
[TestSuite]
public class PlayerTests
{
    private ISceneRunner _runner;

    [Before]
    public void Setup() => _runner = ISceneRunner.Load("res://Scenes/Player.tscn");

    [TestCase]
    [RequireGodotRuntime]
    public async Task TakeDamage_ReducesHealth()
    {
        var player = _runner.GetProperty<Player>("Player");
        await _runner.AwaitIdleFrame();
        player.TakeDamage(10);
        AssertInt(player.Health).IsEqual(90);
    }
}
```

#### CRITICAL: Node-Derived Classes Crash `dotnet test`

**Any C# class that inherits from `Node` (or any Godot type) will cause an `AccessViolationException`** when instantiated via `dotnet test`, because the Godot runtime is not running. This includes classes with static fields/constructors that reference Godot types.

**Solution**: Keep testable logic in pure C# classes with zero Godot dependencies:

```csharp
// GOOD: Pure C# class - safe for dotnet test
public static class ScoringHelper
{
    public static int CalculateScore(int hits, int multiplier) => hits * multiplier * 100;
}

// BAD: Inherits from Node - will crash dotnet test
public partial class ScoreManager : Node
{
    public int CalculateScore(int hits, int multiplier) => hits * multiplier * 100;
}
```

If you need to test code that uses Godot types, use the GdScript test runner (`pwsh ./tools/test.ps1`) with `[RequireGodotRuntime]` instead.

#### Anti-Patterns (Cause Hangs)
- `await obj.ToSignal(obj, "ready")` after AddChild - ready fires synchronously, hangs forever
- `while(true)` loops without break conditions
- Async tests without `[RequireGodotRuntime]`

### GDScript Testing (Editor Tools Only)
gdUnit4 supports GDScript tests for editor tooling:
- Test files go in `res://test/unit/` and `res://test/integration/`
- Test files must be named `test_*.gd` and extend `GdUnitTestSuite`
- Test methods must start with `test_`
- Use `auto_free()` for automatic cleanup of test objects

Example GDScript test (for editor tools):
```gdscript
extends GdUnitTestSuite

func test_example_passes() -> void:
    assert_bool(true).is_true()

func test_numeric_equality() -> void:
    var expected := 42
    var actual := 40 + 2
    assert_int(actual).is_equal(expected)

func test_with_auto_cleanup() -> void:
    var node := auto_free(Node2D.new())
    assert_object(node).is_not_null()
```

#### GDScript Assertion Reference
| Function | Example |
|----------|---------|
| `assert_bool(v)` | `.is_true()`, `.is_false()` |
| `assert_int(v)` | `.is_equal(n)`, `.is_greater(n)`, `.is_between(a, b)` |
| `assert_float(v)` | `.is_equal_approx(n, tolerance)` |
| `assert_str(v)` | `.is_equal(s)`, `.contains(s)`, `.starts_with(s)` |
| `assert_array(v)` | `.has_size(n)`, `.contains([items])`, `.is_empty()` |
| `assert_object(v)` | `.is_null()`, `.is_not_null()`, `.is_instanceof(Type)` |
| `assert_signal(obj)` | `await assert_signal(obj).is_emitted("signal_name")` |

## DevTools (Runtime Feedback Loop)

DevTools is an AutoLoad (`game/DevTools.cs`) that enables runtime introspection and validation while the game is running. Use it for iterative visual verification and debugging.

### When to Use Which Tool

| Task | Tool | Notes |
|------|------|-------|
| **Build C#** | `dotnet build -warnaserror` | Always first |
| **Run tests** | `pwsh ./tools/test.ps1` | Unit + integration tests |
| **Validate UIDs/NodePaths** | `pwsh ./tools/godot.ps1 --headless --script res://tools/lint_project.gd` | Build-time, no game running |
| **Screenshot running game** | `python tools/devtools.py screenshot` | Requires game running |
| **Runtime scene validation** | `python tools/devtools.py validate-all` | Checks textures, meshes, shaders |
| **Check FPS/memory** | `python tools/devtools.py performance` | Requires game running |
| **Inspect node state** | `python tools/devtools.py get-state --node "/root/..."` | Debug runtime values |
| **Debug node hierarchy** | `python tools/devtools.py scene-tree` | Requires game running |
| **Simulate player input** | `python tools/devtools.py input tap jump` | Automated gameplay testing |
| **Run input sequence** | `python tools/devtools.py input sequence test.json` | Complex input patterns |

### DevTools CLI Commands

```powershell
# Check if game is running with DevTools
python tools/devtools.py ping

# Take screenshot for visual verification
python tools/devtools.py screenshot
python tools/devtools.py screenshot --filename "after_fix.png"

# Runtime scene validation (missing textures, meshes, shaders)
python tools/devtools.py validate-all
python tools/devtools.py validate --scene res://path/to/scene.tscn

# Get scene tree (JSON output)
python tools/devtools.py scene-tree --depth 5

# Performance metrics
python tools/devtools.py performance

# Inspect node state
python tools/devtools.py get-state --node "/root/Game/Player"

# Modify node state at runtime
python tools/devtools.py set-state --node "/root/Game/Player" --property Health --value 100

# Call a method on a node
python tools/devtools.py run-method --node "/root/Game/Player" --method TakeDamage --args "[25]"

# View DevTools logs
python tools/devtools.py logs --tail 20

# Quit the game
python tools/devtools.py quit
```

### Input Simulation (Automated Gameplay Testing)

DevTools can simulate player input for automated gameplay validation. **Use input simulation with screenshots** to verify gameplay behavior without manual testing.

```powershell
# List available input actions
python tools/devtools.py input list
python tools/devtools.py input list --all  # Include ui_* actions

# Press and hold an action
python tools/devtools.py input press move_forward

# Release a held action
python tools/devtools.py input release move_forward

# Tap (press + release) an action
python tools/devtools.py input tap jump
python tools/devtools.py input tap jump --hold 0.5  # Hold 500ms then release

# Release all simulated inputs
python tools/devtools.py input clear

# Execute a sequence from a JSON file
python tools/devtools.py input sequence test/sequences/my_test.json
```

**Key design principle**: Input simulation uses Godot's action-based system (`Input.ActionPress`/`Input.ActionRelease`), not raw keycodes. This means tests work regardless of key bindings.

### Input Sequence Files

For complex input patterns, create JSON sequence files in `test/sequences/`:

```json
{
  "description": "Test jump while moving forward",
  "steps": [
    {"type": "wait", "seconds": 0.5},
    {"type": "press", "action": "move_forward"},
    {"type": "wait", "seconds": 1.0},
    {"type": "tap", "action": "jump"},
    {"type": "wait", "seconds": 0.5},
    {"type": "screenshot", "filename": "mid_jump.png"},
    {"type": "release", "action": "move_forward"},
    {"type": "clear"}
  ]
}
```

**Sequence step types:**

| Type | Required Fields | Description |
|------|-----------------|-------------|
| `press` | `action` | Press and hold (add `strength` 0.0-1.0 for analog) |
| `release` | `action` | Release a held action |
| `tap` | `action` | Press, wait one frame, release |
| `hold` | `action`, `seconds` | Press, wait duration, release |
| `wait` | `seconds` | Pause without input |
| `screenshot` | - | Capture frame (optional `filename`) |
| `assert` | `node`, `property`, `equals` | Verify game state |
| `clear` | - | Release all simulated inputs |

### Automated Gameplay Verification Workflow

**RECOMMENDED**: Combine input simulation with screenshots to verify gameplay changes work correctly. This is especially useful for:
- Movement and physics changes
- Jump mechanics and gravity
- Combat systems and hit detection
- UI state changes from player actions
- Animation triggers

```powershell
# 1. Start the game
pwsh ./tools/godot.ps1 &

# 2. Verify connection
python tools/devtools.py ping

# 3. Simulate gameplay and capture results
python tools/devtools.py input tap jump
python tools/devtools.py screenshot --filename "jump_test.png"

# 4. Or run a complete sequence
python tools/devtools.py input sequence test/sequences/test_movement.json

# 5. Check the screenshots to verify behavior
# Screenshots are saved to: %APPDATA%/Godot/app_userdata/TeaLeaves/screenshots/
```

**Example: Verifying a jump fix**
```powershell
# After fixing jump height, verify it works:
python tools/devtools.py input press move_forward
python tools/devtools.py input tap jump --hold 0.1
Start-Sleep -Seconds 0.3
python tools/devtools.py screenshot --filename "jump_apex.png"
python tools/devtools.py input clear

# Check the screenshot to confirm jump height is correct
```

### Agentic Iteration Workflow

For iterative development with visual feedback:

```powershell
# 1. Start the game (keep it running)
pwsh ./tools/godot.ps1 &

# 2. Verify DevTools is responding
python tools/devtools.py ping

# 3. Make code changes, then:
dotnet build -warnaserror

# 4. Simulate input to test gameplay changes
python tools/devtools.py input tap jump
python tools/devtools.py input press move_forward

# 5. Take screenshot to verify visual state
python tools/devtools.py screenshot

# 6. Release inputs and check performance
python tools/devtools.py input clear
python tools/devtools.py performance

# 7. When done, quit cleanly
python tools/devtools.py quit
```

**Pro tip**: After making gameplay changes (movement speed, jump height, physics, etc.), always use input simulation + screenshot to verify the changes work as expected before committing.

### Structured Logging

Add structured logs from game code for debugging:

```csharp
using TeaLeaves.Systems;

// In your game code:
DevTools.Log("player", "Took damage", new {
    amount = damage,
    health = currentHealth,
    source = damageSource.Name
});
```

View logs:
```powershell
python tools/devtools.py logs --tail 20 --category player
```

### Validation Issue Codes (Runtime)

| Code | Severity | Meaning |
|------|----------|---------|
| `file_not_found` | error | Scene file does not exist on disk |
| `load_failed` | error | Failed to load PackedScene from file |
| `invalid_state` | error | Scene loaded but has no valid state |
| `instantiate_failed` | error | Exception during scene instantiation |
| `missing_script` | error | Script file not found or failed to compile |
| `missing_resource` | warning | Resource reference is null |
| `missing_mesh` | warning | MeshInstance3D has no mesh |
| `missing_texture` | warning | Sprite has no texture |
| `missing_shader` | error | ShaderMaterial has no shader |
| `missing_collision_shape` | warning | CollisionShape has no shape |
| `missing_audio` | info | AudioStreamPlayer has no stream |
| `invalid_connection` | error | Signal connection is broken |
| `invalid_animation_path` | warning | Animation targets non-existent node |
| `relative_nodepath` | info | NodePath uses relative path syntax |

## Quick Reference: The Right Tool for Each Task

| I want to... | Command |
|--------------|---------|
| Build C# | `dotnet build -warnaserror` |
| Run all tests | `pwsh ./tools/test.ps1` |
| Run C# unit tests only | `dotnet test` |
| Validate scene UIDs | `pwsh ./tools/godot.ps1 --headless --script res://tools/lint_project.gd` |
| Validate runtime assets | `python tools/devtools.py validate-all` (game must be running) |
| Take a screenshot | `python tools/devtools.py screenshot` (game must be running) |
| Check performance | `python tools/devtools.py performance` (game must be running) |
| Simulate player input | `python tools/devtools.py input tap jump` (game must be running) |
| Run input sequence | `python tools/devtools.py input sequence file.json` (game must be running) |
| List input actions | `python tools/devtools.py input list` (game must be running) |
| Lint GDScript | `gdlint path/to/file.gd` |
| Lint shaders | `pwsh ./tools/godot.ps1 --headless --script res://tools/lint_shaders.gd` |
| Lint test files | `pwsh ./tools/lint_tests.ps1` |
| Run the game | `pwsh ./tools/godot.ps1` |
| Setup input actions | `pwsh ./tools/godot.ps1 --headless --script res://tools/setup_input_actions_cli.gd` |

---

# User Input
**System Prompt:**  
Hello! I am an **eccentric video game designer** (a very creative one) who communicates in an unusual way. Sometimes I’ll mash the keyboard or type nonsense like “skfjhsd#$%” – but **these are NOT random**! They are **secret cryptic commands** full of genius game ideas (even if it’s hard to see).

**Your job:** You are a **brilliant AI game developer** who can understand my cryptic language. No matter what odd or nonsensical input I provide, you will **interpret it as a meaningful instruction or idea for our video game**. You will then **build or update the game** based on that interpretation.

**Guidelines:**

- Always assume my input has hidden meaning. **Never dismiss it as gibberish**. Instead, creatively decipher it. (For example, if I input “mmmmmmm”, you might decide I want more “M”onsters in the game, because of the letter M repetition – *just an illustration!*). Every strange phrase is a clue to use in the game.

- Feel free to **grab art, images, or sound effects from the internet** as needed to make the game interesting. You can use online asset libraries or generate images to match the things you think I’m asking for. *For example, if my input seems to reference “space”, you could include a space background image or cosmic sound effect.* Always ensure the assets align with the interpreted command.

- My work is ALWAYS beautiful and slick looking! It's YOUR job to to turn this into a reality. No ugly placeholders. Everything MUST be final.

- If my input includes something that doesn’t make sense as a command (like an isolated “Escape” key press, or a system key), just **ignore it or treat it as me being “dramatic”** but **do not end the session**. Only focus on inputs that you can turn into game content.

- **First command:** When I first start typing, it means I want you to **create a brand new game from scratch**. Interpret my very first cryptic input as the seed of the game idea. *Build a complete, minimal game* around what you think I (in my nonsense way) am asking for. Include some basic gameplay, graphics, and sound if possible.

- **Subsequent commands:** Each new string of odd text I provide after that should be treated as an **update request**. Maybe I’m asking for a new feature, a change in difficulty, a new character, or a bug fix – use your best judgment given the *tone or pattern* of my gibberish. Then apply the update to the existing game project. **Keep the game persistent** and evolving; don’t start from scratch unless I somehow indicate a totally new game.

- Be **creative** and have fun with the interpretations! I trust your expertise to take my “unique” input and run with it. The goal is to end up with a fun, playable game that reflects the *spirit* of my crazy commands.

- This project is code named Tea Leaves. That's NOT a hint about what to do - it's a code name and nothing more. Don't read anything into the name.

- My ideas are ALWAYS original. No BORING endless runners or other generic vomit. My games are ALWAYS quirky and UNIQUE!

- ALWAYS validate with screenshots using the tools available to you! Be CRITICAL of the results you see. We need PERFECTION and FANTASTIC DESIGN not just "good enogh".

- ALWAYS have basic but visually appealing on screen controls.

- Target 1080p for the resolution.

- Sound is IMPORTANT! Don't forget about great sound design.

- You MUST make use of EVERY letter I give you! No hand waving. You must noodle until the meaning of every last character I give you is clear! Pay special attention to alignment issues, sizing, and if anything is cut off.

**Remember:** I may be hard to read, but I’m counting on you to read between the lines and turn my keystrokes into an awesome video game. Let’s make something amazing (and maybe a little silly)! 

My standards are INSANELY high for quality. You MUST ALWAYS add tests and VERIFY they work! NEVER return the system in a borken state to me.

*Now, get ready. I’ll give you my first “command” in a moment...*
