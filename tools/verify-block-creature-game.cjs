const fs=require('node:fs/promises'),path=require('node:path'),{pathToFileURL}=require('node:url');
const {chromium}=require(path.resolve(__dirname,'../.tools/lux3d/browser-runtime/node_modules/playwright'));
(async()=>{
 const dir=path.resolve(__dirname,'../docs/previews/block-creature-game-2026-10-13');
 const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1000},offline:true}),errors=[];
  page.on('pageerror',error=>errors.push(error.message));await page.goto(pathToFileURL(path.join(dir,'index.html')).href);
  const cards=page.locator('.grid article');if(await cards.count()!==26)throw Error('26 game models required');
  for(const img of await cards.locator('img').all())await img.evaluate(async el=>{await el.decode();if(el.naturalWidth!==512||el.naturalHeight!==416)throw Error('Bad native render dimensions');});
  await page.locator('.hero').evaluate(async el=>{await el.decode();if(el.naturalWidth!==1280||el.naturalHeight!==720)throw Error('Missing world capture');});
  for(const id of ['horse','guardian']){
   const img=page.locator('#'+id+'-demo'),button=page.locator(`[data-demo="${id}"]`);
   await button.click();const initial=await img.getAttribute('src');await page.waitForTimeout(230);
   if(initial===await img.getAttribute('src'))throw Error('Playback failed');
   await button.click();const paused=await img.getAttribute('src');await page.waitForTimeout(160);
   if(paused!==await img.getAttribute('src'))throw Error('Pause failed');
  }
  await page.setViewportSize({width:390,height:844});
  if(await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth))throw Error('Mobile overflow');
  const report={passed:errors.length===0,models:26,nativeWorldCapture:true,bothDemosPlayPause:true,offline:true,mobileLayout:true,pageErrors:errors};
  await fs.writeFile(path.join(dir,'gallery-verification.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
  if(!report.passed)process.exitCode=1;
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
