# Balance notes

Measured with the progression bot (`dotnet run --project Tools/HeadlessTests/Runner/HeadlessTests.csproj -- --bot-trace`).

## Problem found (before)
All 24 Sol-era technologies were finished after ~2 h — exactly when the Moon was reached — and ~156,000 unused science
piled up by hour 6. "Martian Industry" was researched at 1 h 45 min, long before Mars was reachable.

## Fix
Technology costs now follow an exponential curve that tracks industrial progress
(T1 15–25 · T2 30–60 · T3 80–200 · T4 300–600 · T5 1.2–1.5K · T6 3–6K · T7 9–15K · T8 20–30K · T9 50K · T10 80K · T11 120K · T12 200K).

| Bot time | Techs done | Research running |
|---|---|---|
| 1 h | 14 | Launch Automation |
| 2 h | 20 | Titanium Metallurgy |
| 3 h 30 | 22 | Interplanetary Navigation |
| 4 h 30–5 h | 23 | Martian Industry |
| 5 h 30+ | 24 | idle - saving science for the interstellar era (Laser Sails 80K) |

Moon landing at ~2 h, helium-3 at ~4 h (bot, no player optimisation). A regression test asserts that at least 8
technologies remain when the Moon is reached.

## Other fixes
- Coal / fusion plants burn fuel only for the power the grid actually needs (they used to burn at full rate).
- Returning reusable rockets are never lost to full vehicle storage; research refunds always fit.
- "Interstellar Network" achievement counts star systems (it used to check a single mission).
- Massless laser charge can no longer be selected as rocket cargo.
