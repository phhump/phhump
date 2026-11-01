# Adding content without code

Two ways:

1. **In code** — add entries in `Scripts/Core/Data/DefaultContent.cs` (fast for programmers, covered by tests).
2. **As assets** — menu **Stellar Industries → Export Built-in Content to Assets** writes every definition as a
   ScriptableObject under `Assets/StellarIndustries/Content/…` and a database at `Resources/StellarContent.asset`.
   From then on the game loads the assets. Create new ones via *Assets → Create → Stellar Industries → …* and add
   them to the database lists. With `IncludeBuiltInContent = true` assets only need to contain additions/overrides
   (same `Id` overrides the built-in entry).

Run **Stellar Industries → Validate Content** afterwards. Validation reports unknown ids, recipes no building can run,
techs without a gameplay effect, resources that can never be obtained and missions no rocket can fly.

## Example: a new world (asteroid Ceres)
1. `LocationDef`: `Id = "ceres"`, `ParentId = ""`, `OrbitRadius = 60`, `Deposits = water_ice 1.2, iron_ore 0.8, rare_earths 0.6`, `SolarMultiplier = 0.25`.
2. `MissionDef`: `Id = "ceres_landing"`, `Origin = "earth"`, `Destination = "ceres"`, `RequiredRangeTier = 3`, `RequiresLanding = true`, `UnlockLocation = "ceres"`, `FirstTimeRewards = …`, `RequiredTech = "interplanetary_navigation"`.
3. Done — the map shows it, the mission appears after the tech, existing mines/solar arrays work there automatically.

## Example: a new production chain
1. `ResourceDef` `microchip` (Component).
2. `RecipeDef` `make_microchip`: inputs `silicon_wafer 1, copper_wire 2`, outputs `microchip 1`, `Duration 8`, `RequiredTech "electronics_2"`.
3. Add `make_microchip` to `component_factory.Recipes`.
4. Use `microchip` somewhere (another recipe, a build cost, a tech material cost).

## Field reference (most important)
- **Recipe**: `RequiredDeposit` makes it an extraction recipe (location must have the deposit; richness scales speed).
- **Building**: `PowerUse` / `PowerOutput` + `PowerSource` (Fuel uses its first recipe as fuel), `StorageBonus`, `AllowedLocations`, `CostScaling`, `UpgradeCostBase/Growth`, `SpeedPerLevel`, `Shape` (placeholder visual).
- **Tech**: `Tier` (tree column), `Prerequisites`, `ScienceCost`, `MaterialCost` (paid on Earth), `Modifiers`, `UnlockFeatures`.
- **Rocket**: `VehicleResourceId` (the produced item), `RangeTier`, `CanLand`, `Reusable`, `CargoCapacity`, `Reliability`, `FuelCost`.
- **Mission**: `Type`, `Origin`, `Destination`, `RequiredRangeTier`, `RequiresLanding`, `AllowsCargo`, rewards, `UnlockLocation`, `UnlockFeatures`.
