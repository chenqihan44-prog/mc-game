const fs=require('node:fs/promises');
const path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require(path.resolve(__dirname,'../.tools/lux3d/browser-runtime/node_modules/playwright'));
(async()=>{
 const dir=path.resolve(__dirname,'../docs/previews/blender-creature-renders-2026-10-13');
 const out=path.join(dir,'inspection');await fs.mkdir(out,{recursive:true});
 const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:1500,height:1450},offline:true});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(path.join(dir,'index.html')).href);
  const cards=page.locator('article');const count=await cards.count();const checks=[];
  for(let n=0;n<count;n++){
   const card=cards.nth(n);await card.scrollIntoViewIfNeeded();
   const id=(await card.getAttribute('id')).replace('card-','');
   for(const angle of ['side','three-quarter']){
    await card.locator('button[data-angle="'+angle+'"]').click();
    await card.locator('img').evaluate(async img=>{await img.decode();if(img.naturalWidth!==1536||img.naturalHeight!==1536)throw Error('wrong dimensions');});
   }
   const work=await card.locator('a').getAttribute('href');await fs.access(path.join(dir,work));
   checks.push({id,bothImagesLoaded:true,dimensions:[1536,1536],workfileExists:true});
   if(n%6===5||n===count-1)await page.screenshot({path:path.join(out,'gallery-'+Math.floor(n/6)+'.png')});
  }
  for(let n=0;n<count;n++)await cards.nth(n).locator('img').evaluate(img=>img.loading='eager');
  await page.screenshot({path:path.join(out,'gallery-all.png'),fullPage:true});
  const report={models:count,checks,pageErrors:errors,passed:count===26&&errors.length===0};
  await fs.writeFile(path.join(out,'gallery-verification.json'),JSON.stringify(report,null,2));console.log(JSON.stringify({models:count,passed:report.passed}));
  if(!report.passed)process.exitCode=1;
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
