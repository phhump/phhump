# Architecture

## Assemblies
| Assembly | Folder | Depends on | Purpose |
|---|---|---|---|
| `StellarIndustries.Core` | `Scripts/Core` | nothing (`noEngineReferences: true`) | Complete game simulation. Pure C#, deterministic, unit-testable outside Unity. |
| `StellarIndustries.Unity` | `Scripts/Unity` | Core | Composition root, ScriptableObject content, 3D views, UI. |
| `StellarIndustries.Editor` | `Editor` | Core, Unity | Menu tools: scene setup, content export, validation. |
| `StellarIndustries.Tests` | `Tests` | Core, NUnit | Shared test suite + Unity Test Runner wrapper. |

The hard rule: **gameplay logic lives only in Core**. The Unity layer reads state, listens to events and
calls system APIs. This keeps the simulation testable (25 headless tests incl. a full playthrough bot)
and lets the UI be replaced (e.g. by UI Toolkit) without touching gameplay.

## Core layout
```
Core/
  Data/        Definitions (ResourceDef, BuildingDef, RecipeDef, TechDef, RocketDef, LocationDef, MissionDef, AchievementDef, StartDef)
               GameDatabase (indexed lookup + Validate()), DefaultContent (built-in content)
  State/       GameState and its parts — ids and numbers only, serializable
  Systems/     GameEvents, ModifierSystem, InventorySystem, ProductionSystem (incl. energy),
               ConstructionSystem, ResearchSystem, MissionSystem, LogisticsSystem, AchievementSystem,
               SpacePhysics (rocket equation, launch windows, solar factor, heritage)
  Simulation/  GameSimulation (facade, wiring, Tick/Step), OfflineProgression
  Save/        SaveSerializer (format/version/checksum/migrations), SaveSanitizer (repair), SaveManager (+ storages)
  Util/        Json, JsonMapper (reflection), Rng (xorshift, state saved), NumberFormat
```
Mapping to the suggested architecture of the brief: GameManager → `GameController`; GameClock → `GameSimulation.Tick`;
EventManager → `GameEvents`; ResourceManager/InventoryManager → `InventorySystem`; ProductionManager/RecipeManager/EnergyManager/
FactoryManager/MiningManager → `ProductionSystem` (one data-driven code path for all of them); BuildingManager → `ConstructionSystem`;
RocketManager/SpacecraftManager/MissionManager → `MissionSystem`; TransportManager/TradeRouteManager → `LogisticsSystem`;
ResearchManager/TechnologyTreeManager/UnlockManager → `ResearchSystem` + `RequiredTech` fields; AchievementManager → `AchievementSystem`;
PlanetManager → `LocationDef` data + `IndustriesPanel`. Managers were merged where they would only have been thin wrappers.

## Key decisions
- **One source of truth for unlocks.** Buildings, recipes and missions declare `RequiredTech`; a tech's "Unlocks" list is derived. No dangling unlock lists.
- **Rockets are inventory items.** Assembly is just a recipe; launches consume the item. Automation, storage and logistics work for rockets with no special code.
- **Batched production.** Each tick a building completes `floor(progress)` cycles at once, limited by inputs and free storage. Cost is O(buildings) per step independent of `dt`, which makes offline simulation cheap (≤ 2000 steps for any duration).
- **Offline = the normal simulation.** `OfflineProgression` re-runs `Step(dt, efficiency)` in coarse steps; storage, energy, inputs and rocket availability are respected automatically. Missions in flight advance in real time.
- **Energy per location.** Supply (wind/solar/fuel × location factors) vs. demand; consumers run at `min(1, supply/demand)`. Blocked buildings draw no power.
- **Priority** sorts buildings per location (high first) so important consumers get scarce inputs first.
- **Determinism.** RNG state is part of the save; same seed + same inputs ⇒ identical state (tested).
- **Save robustness.** Header line + body line; FNV-1a checksum; temp-file + rename; `.bak` of the previous save; `SaveSanitizer` drops unknown ids, clamps values, restores invariants; versions newer than the game are rejected; `Migrations` dictionary for future format changes.
- **No singletons.** `GameController` is the composition root and passes itself to `SpaceView` and `GameUI`. `GameBootstrap` only creates a controller if a scene has none.
- **IMGUI for the test build.** No prefabs/scene serialization required, works in every render pipeline. Structural UI changes call `GUIUtility.ExitGUI()` to keep IMGUI's layout passes consistent.
- **Placeholder art via `VisualFactory`.** Procedural planet textures, primitives, starfield as one point mesh. Supports built-in RP and URP. Replace by real assets in one place.
- **Scalable visuals.** At most 160 building visuals per location (pooled rockets, shared materials); the simulation itself has no such limit.

## Performance notes
- ~100 buildings simulate in microseconds per tick. For thousands, the next step is aggregating identical buildings (same def/recipe/level) into counted stacks — `ProductionSystem` is the only place to change.
- Building lists per location are cached and re-sorted only when buildings change.

## Engineer mode (surface)
- **Core:** `PlacementSystem` owns all spatial rules — deterministic deposit fields per location, footprints + 2.5 m clearance,
  landing-pad exclusion, survey bounds, "extractors on matching field". `ConstructionSystem.BuildAt` is the player's build call;
  `Place`/`Build` without coordinates auto-place (start kit, quick-build UI, bot, old saves). Placed extractors can only run
  recipes of the field under them; `ProductionSystem` reports `NoDeposit` otherwise. All covered by tests.
- **Unity (`Scripts/Unity/Surface`):** `SurfaceWorld` (terrain, sky, lighting, fields, structure models, interaction),
  `EngineerController` (CharacterController, real gravity, low-g traction), `RoverController` (terrain-following vehicle),
  `SurfaceCamera`, `BuildMode` (ghost preview, raycast on terrain, rules from the core), `StructureFactory` (models),
  `SurfaceHud` (IMGUI HUD). `SpaceView` is suspended while the surface is active.
- **Input decision:** `SurfaceInput` is fed from IMGUI events at the end of `OnGUI` (after UI controls consumed theirs), so world
  clicks never go through HUD buttons and the game works with both the legacy Input Manager and the new Input System
  (`UnityEngine.Input` is never called). Held keys are cleared whenever the command center opens or the mode switches.
- **Layers:** buildings, engineer and rover use layer 2 (Ignore Raycast) so placement raycasts only hit terrain, while the
  CharacterController still collides with buildings.
