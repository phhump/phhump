# Performance

Goal: smooth on low-end PCs (integrated graphics, 4 GB RAM) without giving up the look — every preset keeps the real
planet textures, atmospheres and the modern UI.

## Simulation (measured: `dotnet run --project Tools/HeadlessTests/Runner/HeadlessTests.csproj -c Release -- --bench`)
| Buildings | Placement (before → after) | Tick | Garbage per tick (before → after) |
|---|---|---|---|
| 100 | 34 ms → 22 ms | 0.04 ms | 9.6 KB → 12 B |
| 1,000 | 10.4 s → 0.4 s | 0.10 ms | 55 KB → 12 B |
| 5,000 | 203 s → 0.33 s | 0.50 ms | 209 KB → 12 B |

Fixes: spatial hash (32 m cells) for overlap checks, ring-search memo for auto-placement, allocation-free deposit checks
(no LINQ/closures in the per-tick path), auto-research search throttled to once per second. Unity's garbage collector
stops the game while collecting, so "no garbage per frame" removes stutters on slow PCs. A regression test enforces
< 512 bytes per tick and fast placement for 2,000 buildings.

## Rendering — quality presets (Settings → Graphics, stored per device)
| | Low | Medium | High | Ultra |
|---|---|---|---|---|
| Texture resolution | half (8K Earth → 4K) | full | full | full |
| Shadows | off | hard, 70 m | soft, 140 m, 2 cascades | soft, 240 m, 4 cascades |
| MSAA | off | 2x | 4x | 8x |
| Terrain grid / horizon | 100 / 50 | 150 / 80 | 200 / 120 | 256 / 160 |
| Planet sphere segments | 48 | 72 | 112 | 160 |
| Decoration density | 35 % | 65 % | 100 % | 140 % |
| Drones / footprint decals | 6 / 60 | 12 / 150 | 24 / 400 | 40 / 800 |
| Particles | 30 % | 60 % | 100 % | 150 % |
| Cloud layer | off | on | on | on |
| Target fps | 30 | 60 (vsync) | 60 (vsync) | 60 (vsync) |

- **Auto-detection** on first start (VRAM, RAM, CPU threads, integrated GPU names).
- **Adaptive quality:** after ~16 s far below the target frame rate the preset steps down once (never up — no oscillation);
  loading hitches are ignored via a grace period.
- **Battery saver:** 10 fps in the background, 30 fps after one minute without input in orbit views. The simulation is
  time-based, so frame rate never changes game speed; `runInBackground` keeps the idle economy running.
- **LOD:** every building has a detailed and a simple model (same size/colour); thresholds follow the preset; tiny ones are culled.
- **GPU instancing** on all runtime materials (identical primitives draw together) and **static batching** of decoration
  (rocks, stakes, ore boulders: hundreds of objects → few draw calls).
- **Zero-allocation UI paths:** cached GUIStyles, HUD lists refreshed 4x per second instead of every IMGUI event, cached
  status components on buildings.
- **Smooth planet spheres** replace Unity's low-poly sphere (prettier, and the segment count scales with the preset).
