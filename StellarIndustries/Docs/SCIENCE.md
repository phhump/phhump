# Scientific realism in Stellar Industries

Goal: real physics where it creates interesting decisions, simplifications where realism would only add tedium.
Every number below is either taken from the cited source or explicitly marked as a game assumption.

## 1. Rocket equation with staging
`Δv = Isp · g0 · ln(m0 / mf)` (Tsiolkovsky), evaluated per stage; everything above a stage (upper stages + payload)
counts as its payload. Implemented in `Core/Systems/SpacePhysics.cs`, verified against the analytic formula in the tests.

**Gameplay effect:** cargo has mass. Max cargo = min(cargo bay, payload the rocket equation allows for the mission's Δv).
Light, valuable goods (helium-3: 1 unit = 1 kg) ship well; shipping propellant (1 unit = 10 t) is expensive.
Heavy rockets need hydrogen upper stages — like the Saturn V — because a two-stage kerosene rocket cannot reach the
lunar surface (the tests confirm this).

| Rocket | Stages (dry + propellant, Isp) | Δv empty | Max payload |
|---|---|---|---|
| Orbital | 25+400 t @300 s, 4+90 t @345 s | 15.0 km/s | ~19 t to LEO |
| Lunar | 130+2100 t @300 s, 36+440 t @420 s, 8+60 t @440 s | 20.2 km/s | ~19 t to the lunar surface |
| Lunar shuttle | 8+22 t hydrolox @450 s | 5.8 km/s | ~17 t Moon → Earth |

*Game assumptions:* stage masses are game designs; Isp values are typical (kerosene/LOX ~300–345 s, hydrogen/LOX ~420–450 s);
mixture ratios O/F 2.56 (kerolox) and 6.0 (hydrolox); one propellant unit = 10 t; all stages are always fully fuelled;
recovery propellant of reusable rockets is abstracted.

## 2. Delta-v budgets
Source: Wikipedia, *Delta-v budget* (read 2026-11-01).

| Mission | Δv used | Composition |
|---|---|---|
| Orbital launch | 9.4 km/s | "about 9.4 km/s" to LEO (7.8 km/s orbital speed + 1.5–2 km/s drag and gravity losses) |
| Lunar landing / supply | 15.4 km/s | 9.4 + TLI 3.20 + lunar orbit insertion 0.90 + landing ~1.87 |
| Lunar export (Moon → Earth) | 2.8 km/s | ascent ~1.87 + trans-Earth injection ~0.9; aerobraking at Earth (page: aerocapture can remove up to 3.2 km/s) |
| Mars landing / supply | 14.7 km/s (best window) | 9.4 + LEO → Mars transfer 4.3 ("typical") + ~1.0 powered landing after aerobraking (*game assumption*) |
| Suborbital test | 2.0 km/s | *game assumption*: ~1.4 km/s for 100 km apogee plus losses |

## 3. Launch windows (Mars)
Earth and Mars line up every **779.94 days** (synodic period, NASA Mars fact sheet); a Hohmann transfer takes
**~8.5 months** (Wikipedia table). The page notes "the delta-v budget changes with launch time".
*Game simplification:* the cycle is compressed to 20 minutes; the extra Δv rises smoothly from 0 at the optimal
window to +5 km/s at the worst point. Automated routes wait for the window. Game time also runs while offline.

## 4. Planet data and solar power
Source: NASA NSSDCA planetary fact sheets (Earth comparison table, Moon and Mars fact sheets).

| | Radius | Gravity | Escape velocity | Day | Distance | Irradiance |
|---|---|---|---|---|---|---|
| Earth | 6371.0 km | 9.80 m/s² | 11.19 km/s | 24.0 h | 1 AU | 1361.0 W/m² |
| Moon | 1737.4 km | 1.62 m/s² | 2.38 km/s | 708.7 h | 0.3844·10⁶ km from Earth | 1361.0 W/m² |
| Mars | 3389.5 km | 3.73 m/s² | 5.03 km/s | 24.66 h | 1.524 AU | 586.2 W/m² |

586.2 / 1361 = 0.43 ≈ 1 / 1.524² — the inverse-square law (checked in the tests).
Solar output = irradiance × surface transmission, relative to Earth's surface.
*Game assumptions:* transmission Earth 0.75 (clear-sky average), Moon 1.0 (no air), Mars 0.85 (dust) →
solar factors 1.00 / 1.33 / 0.49. Day/night cycles (two-week lunar nights) are not simulated yet.

## 5. Other realism
- **Liquid oxygen on Earth** is made by cryogenic air separation (Linde process): power only, no raw material — but only
  where the atmosphere contains oxygen. On the Moon and Mars LOX comes from water electrolysis or CO₂ electrolysis (MOXIE).
- **LOX dominates propellant mass** (~72 % of kerolox, ~86 % of hydrolox), so oxygen production is the big industry.
- **Flight heritage:** each successful flight of a design raises its reliability (up to +6 %, capped at 99 %) — reliability
  growth as seen in real launch vehicles. *Game assumption* for the curve.
- **Helium-3:** ~10 ppb in lunar regolith (order of magnitude often cited); harvesting needs huge regolith throughput. Fusion yield is not modelled realistically (it would be millions of times more energy per kg).

## Not (yet) realistic, on purpose
Compressed time; abstract item masses for factories; no orbital mechanics simulation (Δv budgets instead); no crew,
radiation dose or life-support model; no day/night cycle.

## 6. Nearby stars and exoplanets (Era 6)
Sources (read 2026-11-01): Wikipedia *List of nearest exoplanets*, *Proxima Centauri b*, *Barnard's Star*, *Epsilon Eridani*,
*TRAPPIST-1e*, *Alpha Centauri*, *Breakthrough Starshot*, *Project Daedalus*.

Only **confirmed** planets can be colonised. Candidates and disputed objects are mentioned as notes, not as destinations.

| System | Distance | Star | Playable world(s) | Status |
|---|---|---|---|---|
| Alpha Centauri | 4.2465 ly (Proxima) | M5.5V, 0.00155 L☉; A/B: G2V/K1V | Proxima b (≥1.055 M⊕, 0.04848 AU, T_eq 234 K, habitable zone), Proxima d (≥0.26 M⊕, 5.122 d) | b confirmed 2016, d confirmed; c disputed; α Cen Ab unconfirmed candidate |
| Barnard's Star | 5.9629 ly | M4.0V, 0.0034 L☉ | Barnard e (≥0.193 M⊕, 0.0381 AU) | b, c, d, e confirmed 2024–2025; all inside the inner edge of the habitable zone |
| Epsilon Eridani | 10.475 ly | K2V, 0.326 L☉ | Ægir (b): ~318 M⊕, 3.53 AU — orbital He-3 harvesting | confirmed; debris belts at 3–20 AU and 70 AU |
| TRAPPIST-1 | 40.7 ly | ultra-cool red dwarf, 0.0566 % L☉ | TRAPPIST-1e: 0.692 M⊕, 0.920 R⊕, 0.02925 AU, 60.4 % of Earth's flux, T_eq 249.7 K | confirmed; JWST 2025: atmosphere undecided |

**Consistency checks (in the tests):** the inverse-square law L/a² gives 66 % of Earth's flux for Proxima b (sources: ~65–70 %) and
66 % for TRAPPIST-1e (source: 60.4 %, within 15 %). Note: Proxima's luminosity is 0.155 **%** of the Sun's (0.00155 L☉) — the
value "0.155 L☉" seen in a summary would give 66× Earth's flux and contradict the published 65–70 %.

**Derived values:** Proxima d's orbit 0.0288 AU from Kepler's third law (star 0.122 M☉); equilibrium temperatures
`T = 278.6 K · flux^0.25` (zero albedo) where no published value was available; positions from right ascension / declination.
**Assumptions:** radius from the rocky mass-radius relation R ≈ M^0.27 when only the (minimum) mass is known; surface deposits of
all exoplanets (nobody knows their composition); 20 % lower solar yield around red dwarfs (light is mostly infrared).

### Propulsion
- **Laser-sail probes** (Breakthrough Starshot concept): gram-scale probes, 15–20 % of c, phased laser array of up to 100 GW,
  20–30 years to Alpha Centauri. In the game: survey missions; real flight time + light-speed signal delay is shown.
  (The real project is on hold since 2025.)
- **Fusion seed ships** (Project Daedalus heritage): D-He3 fusion; Daedalus' exhaust velocities were 9,210–10,600 km/s, and
  helium-3 was to come from Jupiter's atmosphere or the Moon. The game uses Isp 10⁶ s (9,807 km/s). Unlike Daedalus (flyby at 12 % c),
  seed ships cruise at **3 % c and brake** at the target: Δv = 2 × 0.03 c ≈ 18,000 km/s, mass ratio ≈ 6.3. Proxima b takes ~142 years.
  Chemical rockets cannot do this (tested).
- **No supply lines** across light-years: colonies must be self-sufficient from local deposits — realistic and a design rule.

*Game simplifications:* deuterium separation yield (10 water → 1 deuterium) is far higher than the natural D/H ratio would allow;
helium-3 harvesting is enormously faster than real estimates; flight times are compressed (minutes instead of centuries).

## 7. Gravity and atmosphere on the surface (engineer mode)
- **Walking and jumping** use the real surface gravity (Earth 9.81, Mars 3.73, Moon 1.62 m/s²): the same jump
  (3 m/s) reaches ~0.5 m on Earth, ~1.2 m on Mars and ~2.8 m on the Moon. Lower gravity means less traction.
- **Jetpack** only on airless low-gravity worlds (g < 4 m/s²): thrust 1.5 g, ~3 s of gas (game values).
- **Drones:** with an atmosphere, quadcopters. Mars' air has about 1 % of Earth's density, so rotor craft need larger,
  much faster rotors (as NASA's Ingenuity helicopter did) — the game shows bigger blades spinning faster. Without air (Moon)
  rotors are useless: drones **hop ballistically**, thrusting only to take off and land.
  Flight time t = (vy + sqrt(vy² − 2 g Δh)) / g with vy = sqrt(2 g h) — the same hop lasts sqrt(9.81/1.62) ≈ 2.46× longer on
  the Moon (tested).
- **Dust** is emitted with the exact ballistic lifetime 2·vy/g. On the Moon it flies in clean parabolas and falls straight
  back (the "rooster tails" seen behind the Apollo rover); with air, drag slows it and the puff expands (weaker on Mars).
- **Footprints** stay on airless worlds (no wind), and fade on Earth and Mars.
*Simplifications:* drone masses/thrust are not simulated, only kinematics; Mars air density figure is approximate.
