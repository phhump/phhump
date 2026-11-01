# Roadmap

| Milestone | Status | Notes |
|---|---|---|
| 1 — Playable foundation | **done** (test build) | Menu, resources, production, construction, upgrades, rockets, research, save/load. |
| 2 — Moon expansion | **done** (test build) | Moon location, missions, landing, lunar resources/mining, local manufacturing (lunar shuttle), planet management UI. |
| 3 — Automation | **partly** | Done: automated production/mining, transport routes, offline progression, priorities, automated research. Open: production statistics graphs, per-route schedules, storage hubs. |
| 4 — Solar system | **started** | Mars (data, landing, supply, MOXIE, atmospheric processor). Open: asteroid belt (procedural asteroid compositions), Jupiter/Saturn & moons, gas harvesting, advanced spacecraft, larger tree. |
| 5 — Interstellar | **started** | Done: 4 real star systems / 5 confirmed exoplanets, laser-sail surveys, fusion seed ships, colonies, star map. Open: procedurally generated systems beyond ~50 ly, intra-system logistics at other stars, generation ships. |
| 6 — Endgame | open | Dyson swarms, planetary dismantling, habitats, megastructures. |

## Next tasks (suggested order)
1. Run in the Unity Editor, fix any runtime/layout issues, tune UI sizes.
2. **Excavation system**: depth levels per location (`ExcavationState`: depth, progress), deeper layers add deposits/richness; visual crater rings on the planet.
3. **Building stacks** for performance with thousands of buildings.
4. Energy storage (batteries) and day/night solar cycles on the Moon.
5. Terraforming meters per planet (atmosphere, temperature, water) driven by long-running recipes.
6. Asteroid belt with procedurally generated bodies; Jupiter/Saturn systems.
7. Replace IMGUI with UI Toolkit, placeholder art with models; audio.
8. Balance pass with the progression bot (`Tests/Shared/ProgressionBot.cs`) as a regression benchmark.

## Known limitations of the test build
- Not yet executed in the Unity Editor here (compiled against reference assemblies + headless tests only).
- Planet excavation, terraforming, batteries, asteroid belt and outer planets are not implemented yet.
- For standalone builds with the built-in pipeline, add *Standard* to *Project Settings → Graphics → Always Included Shaders* if objects appear pink (Shader.Find).
- Placeholder visuals; IMGUI interface.
