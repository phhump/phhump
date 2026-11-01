const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1600, height: 900 } });
  const errs=[]; p.on('pageerror', e => errs.push(e.message));
  for (const s of ['industries', 'moon', 'research', 'galaxy']) {
    await p.goto('file://' + __dirname + '/render_full.html?s=' + s); await p.waitForTimeout(300);
    await p.screenshot({ path: __dirname + '/' + s + '.png' });
  }
  console.log('errors:', errs); await b.close();
})();
