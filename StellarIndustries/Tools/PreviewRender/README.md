# Preview renders (not Unity screenshots)

Renders UI previews from **real simulation data** without Unity: the progression bot plays the game headless,
the state is exported as JSON and drawn in the game's layout and theme colours (from `Theme.cs`).
3D views (planet, star map) are approximations of `SpaceView`; the real game uses Unity primitives and lighting.

```bash
dotnet run --project ../HeadlessTests/Runner/HeadlessTests.csproj -- --export-preview data.json
python3 -c "d=open('data.json').read();h=open('render.html').read().replace('<script>\nconst D=window.DATA','<script>window.DATA='+d+';\nconst D=window.DATA');open('render_full.html','w').write(h)"
node shot.js        # needs Playwright + Chromium; writes industries/moon/research/galaxy.png
```
