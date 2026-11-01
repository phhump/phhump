// Serves the project root over HTTP (textures/fonts must be same-origin for canvas pixel access) and renders previews.
const { chromium } = require('playwright');
const http = require('http'), fs = require('fs'), path = require('path');
const root = path.resolve(__dirname, '../..');
const types = { '.html': 'text/html', '.jpg': 'image/jpeg', '.ttf': 'font/ttf', '.json': 'application/json', '.js': 'text/javascript' };
const server = http.createServer((req, res) => {
  const p = path.join(root, decodeURIComponent(req.url.split('?')[0]));
  fs.readFile(p, (e, d) => { if (e) { res.writeHead(404); res.end(); } else { res.writeHead(200, { 'Content-Type': types[path.extname(p)] || 'application/octet-stream' }); res.end(d); } });
}).listen(8765, async () => {
  const b = await chromium.launch(); const pg = await b.newPage({ viewport: { width: 1600, height: 900 } });
  const errs = []; pg.on('pageerror', e => { errs.push(e.message); console.log('ERR', e.message); });
  const out = process.argv[2] || path.join(root, 'Docs/preview');
  for (const s of ['menu', 'industries', 'moon', 'map', 'research', 'galaxy', 'proxima']) {
    console.log('render', s); await pg.goto('http://localhost:8765/Tools/PreviewRender/render.html?s=' + s);
    await pg.waitForFunction(() => document.getElementById('ui').innerHTML.length > 100, null, { timeout: 120000 });
    await pg.waitForTimeout(400);
    await pg.screenshot({ path: path.join(out, s + '.png') });
  }
  console.log('errors:', errs); await b.close(); server.close();
});
