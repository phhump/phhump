# Architecture

## Assemblies
| Assembly | Folder | Depends on | Purpose |
|---|---|---|---|
| `StellarIndustries.Core` | `Scripts/Core` | nothing (`noEngineReferences: true`) | Complete game simulation. Pure C#, deterministic, unit-testable outside Unity. |
| `StellarIndustries.Unity` | `Scripts/Unity` | Core | Composition root, ScriptableObject content, 3D views, UI. |
| `StellarIndustries.Editor` | `Editor` | Core, Unity | Menu tools: scene setup, content export, validation. |
| `StellarIndustries.Tests` | `Tests` | Core, NUnit | Shared test suite + Unity Test Runner wrapper. |

The hard rule: **gameplay logic lives only in Core**. The Unity layer reads state, listens to events and
calls system APIs. This keeps the simulation testable (19 headless tests incl. a full playthrough bot)
and lets the UI be replaced (e.g. by UI Toolkit) without touching gameplay.

## Core layout
```
Core/
  Data/        Definitions (ResourceDef, BuildingDef, RecipeDef, TechDef, RocketDef, LocationDef, MissionDef, AchievementDef, StartDef)
               GameDatabase (indexed lookup + Validate()), DefaultContent (built-in content)
  State/       GameState and its parts — ids and numbers only, serializable
  Systems/     GameEvents, ModifierSystem, InventorySystem, ProductionSystem (incl. energy),
               ConstructionSystem, ResearchSystem, MissionSystem, LogisticsSystem, AchievementSystem
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
