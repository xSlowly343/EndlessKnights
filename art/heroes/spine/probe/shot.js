const {chromium}=require('playwright');
(async()=>{const b=await chromium.launch({args:['--use-gl=angle','--use-angle=swiftshader']});const p=await b.newPage({viewport:{width:640,height:200}});
p.on('console',m=>console.log('console',m.text()));
await p.goto('http://localhost:8765/render.html');await p.waitForFunction(()=>document.title.length>0,{timeout:15000});
console.log(await p.title());await p.screenshot({path:'probe-result.png'});await b.close();})();
