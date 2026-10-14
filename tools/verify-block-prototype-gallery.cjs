const fs=require('node:fs/promises');
const path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require(path.resolve(__dirname,'../.tools/lux3d/browser-runtime/node_modules/playwright'));
(async()=>{
 const revisionTwo=process.argv.includes('--v2');
 const dir=path.resolve(__dirname,revisionTwo?'../docs/previews/block-creature-prototypes-v2-2026-10-13':'../docs/previews/block-creature-prototypes-2026-10-13');
 const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:1500,height:1000},offline:true});
  const errors=[];page.on('pageerror',error=>errors.push(error.message));
  await page.goto(pathToFileURL(path.join(dir,'index.html')).href);
  const cards=page.locator('article');
  if(await cards.count()!==3)throw Error('Expected three prototype cards');
  for(const id of ['cow','pig','guardian']){
   const card=page.locator(`article[data-id="${id}"]`),img=card.locator('.model');
   await img.evaluate(el=>el.decode());
   for(const view of ['side','front','three-quarter']){
    await card.locator(`[data-view="${view}"]`).click();
    await img.evaluate(el=>el.decode());
    if(!(await img.getAttribute('src')).endsWith(`-${view}.png`))throw Error('Wrong angle');
   }
   await card.locator('[data-concept]').click();
   if(!await card.locator('.concept').isVisible())throw Error('Concept toggle failed');
   await card.locator('.concept').evaluate(el=>el.decode());
   if(revisionTwo){await card.locator('[data-concept]').click();await card.locator('.concept').evaluate(el=>el.decode());}
   await card.locator('[data-motion]').click();
   const start=await img.getAttribute('src');await page.waitForTimeout(180);
   if(start===await img.getAttribute('src'))throw Error('Motion did not advance');
   await card.locator('[data-motion]').click();
   const paused=await img.getAttribute('src');await page.waitForTimeout(150);
   if(paused!==await img.getAttribute('src'))throw Error('Motion did not pause');
   await card.locator('.timeline').fill('10');
   if(!(await img.getAttribute('src')).endsWith('/10.png'))throw Error('Frame control failed');
   await card.locator('[data-view="three-quarter"]').click();
   await img.evaluate(el=>el.decode());
  }
  await page.screenshot({path:path.join(dir,'overview.png'),fullPage:true});
  if(revisionTwo){
   for(const id of ['cow','pig','guardian'])await page.locator(`article[data-id="${id}"]`).screenshot({path:path.join(dir,`${id}-comparison.png`)});
  }
  await page.setViewportSize({width:390,height:844});
  if(await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth))throw Error('Mobile overflow');
  const report={passed:errors.length===0,offline:true,models:3,angleControls:true,conceptComparison:true,motionPlayPauseAndSeek:true,mobileLayout:true,pageErrors:errors};
  await fs.writeFile(path.join(dir,'gallery-verification.json'),JSON.stringify(report,null,2));
  console.log(JSON.stringify(report));
  if(!report.passed)process.exitCode=1;
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
