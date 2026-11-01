const { chromium } = require('playwright');const http=require('http'),fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'../..');const types={'.html':'text/html','.jpg':'image/jpeg','.ttf':'font/ttf','.json':'application/json','.js':'text/javascript'};
const srv=http.createServer((q,r)=>{const p=path.join(root,decodeURIComponent(q.url.split('?')[0]));fs.readFile(p,(e,d)=>{if(e){r.writeHead(404);r.end()}else{r.writeHead(200,{'Content-Type':types[path.extname(p)]||'application/octet-stream'});r.end(d)}})}).listen(8767,async()=>{
  const b=await chromium.launch({args:['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader']});const pg=await b.newPage({viewport:{width:1600,height:900}});
  pg.on('pageerror',e=>console.log('ERR',e.message));const out=process.argv[2]||path.join(root,'Docs/preview');
  for(const s of ['earth','earth_build','moon']){console.log('render',s);await pg.goto('http://localhost:8767/Tools/PreviewRender/surface.html?s='+s);
    await pg.waitForFunction(()=>document.body.dataset.done=='1',null,{timeout:180000});await pg.waitForTimeout(2500);
    await pg.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>r())));await pg.screenshot({path:path.join(out,'surface_'+s+'.png')})}
  await b.close();srv.close()});
