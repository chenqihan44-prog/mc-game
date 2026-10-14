const fs=require('node:fs/promises');
const path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require(path.resolve(__dirname,'../.tools/lux3d/browser-runtime/node_modules/playwright'));
(async()=>{
 const dir=path.resolve(__dirname,'../docs/previews/creature-game-integration-2026-10-13');
 const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:1500,height:1100},offline:true});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(path.join(dir,'index.html')).href);
  const cards=page.locator('.grid article');const count=await cards.count();
  for(let n=0;n<count;n++){
   const img=cards.nth(n).locator('img');await img.scrollIntoViewIfNeeded();
   await img.evaluate(async el=>{await el.decode();if(el.naturalWidth!==512||el.naturalHeight!==416)throw Error('wrong capture size');});
  }
  for(const id of ['horse','guardian']){
   const img=page.locator('#'+id+'-demo'),button=page.locator('[data-demo='+id+']');await button.scrollIntoViewIfNeeded();
   const before=await img.getAttribute('src');await page.waitForTimeout(230);
   if(before===await img.getAttribute('src'))throw Error('demo did not advance');
   await button.click();const stopped=await img.getAttribute('src');await page.waitForTimeout(200);
   if(stopped!==await img.getAttribute('src'))throw Error('pause failed');
   await button.click();
  }
  await page.screenshot({path:path.join(dir,'gallery-browser.png'),fullPage:true});
  const report={passed:count===26&&errors.length===0,models:count,offline:true,bothDemosPlayAndPause:true,pageErrors:errors};
  await fs.writeFile(path.join(dir,'gallery-verification.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
  if(!report.passed)process.exitCode=1;
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
