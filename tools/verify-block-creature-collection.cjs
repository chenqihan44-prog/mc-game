const fs=require('node:fs/promises');
const path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require(path.resolve(__dirname,'../.tools/lux3d/browser-runtime/node_modules/playwright'));
(async()=>{
 const dir=path.resolve(__dirname,'../docs/previews/block-creature-collection-2026-10-13');
 const models=JSON.parse(await fs.readFile(path.join(dir,'manifest.json'),'utf8'));
 const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1000},offline:true});
  const errors=[];page.on('pageerror',error=>errors.push(error.message));
  await page.goto(pathToFileURL(path.join(dir,'index.html')).href);
  if(await page.locator('.card').count()!==26)throw Error('Expected 26 cards');
  for(const [group,count] of [['land',8],['small_aquatic',6],['enemies',7],['bosses',5],['new',23]]){
   await page.locator(`[data-group="${group}"]`).click();
   if(await page.locator('.card:visible').count()!==count)throw Error('Category filter failed: '+group);
  }
  await page.locator('[data-group="all"]').click();await page.locator('#search').fill('wolf');
  if(await page.locator('.card:visible').count()!==1)throw Error('Search failed');await page.locator('#search').fill('');
  for(const item of models){
   await page.locator(`.card[data-id="${item.id}"]`).click();
   await page.locator('#model').evaluate(img=>img.decode());
   for(const view of ['side','front','three-quarter']){
    await page.locator(`[data-view="${view}"]`).click();await page.locator('#model').evaluate(img=>img.decode());
    if(!(await page.locator('#model').getAttribute('src')).endsWith(`-${view}.png`))throw Error('View failed: '+item.id);
   }
   await page.locator('#compare').click();await page.locator('#reference').evaluate(img=>img.decode());
   if(!await page.locator('#referencewrap').isVisible())throw Error('Reference toggle failed');
   await page.waitForFunction(()=>!document.getElementById('motion').disabled);
   await page.locator('#motion').click();const before=await page.locator('#model').getAttribute('src');
   await page.waitForTimeout(160);if(before===await page.locator('#model').getAttribute('src'))throw Error('Playback failed: '+item.id);
   await page.locator('#motion').click();const stopped=await page.locator('#model').getAttribute('src');
   await page.waitForTimeout(130);if(stopped!==await page.locator('#model').getAttribute('src'))throw Error('Pause failed');
   await page.locator('#timeline').fill('12');if(!(await page.locator('#model').getAttribute('src')).endsWith('/12.png'))throw Error('Scrub failed');
   await page.locator('#close').click();
  }
  await page.locator('.card[data-id="colossus"]').click();await page.locator('#next').click();
  if(await page.locator('#name').textContent()!=='牛')throw Error('Model navigation failed');await page.locator('#close').click();
  await page.setViewportSize({width:390,height:844});
  if(await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth))throw Error('Mobile page overflow');
  await page.locator('.card[data-id="wolf"]').click();
  if(await page.locator('#detail').evaluate(el=>el.scrollWidth>el.clientWidth+2))throw Error('Mobile dialog overflow');
  await page.locator('#close').click();
  await page.setViewportSize({width:1440,height:1000});
  await page.goto(pathToFileURL(path.join(dir,'overview.html')).href);
  for(const img of await page.locator('article img').all())await img.evaluate(el=>el.decode());
  await page.screenshot({path:path.join(dir,'all-26-overview.png'),fullPage:true});
  const report={passed:errors.length===0,models:26,offline:true,allViewsAndReferenceImages:true,allMotionPreviewsPlayPauseScrub:true,searchAndCategories:true,modelNavigation:true,mobileLayout:true,pageErrors:errors};
  await fs.writeFile(path.join(dir,'gallery-verification.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
  if(!report.passed)process.exitCode=1;
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
