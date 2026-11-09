import {test,expect,Page} from '@playwright/test';
import {readFileSync} from 'node:fs';
import {resolve} from 'node:path';
const png=readFileSync(resolve(process.cwd(),'../../tests/fixtures/landscape.png')).toString('base64');
async function host(page:Page,missing=false,stepCount=1){
 await page.addInitScript(({png,missing,portrait,stepCount})=>{
  const listeners:((event:any)=>void)[]=[];
  let guide:any={schemaVersion:'1.1.0',id:'00000000-0000-0000-0000-000000000001',title:'Synthetic screenshot workflow',summary:'',createdAt:new Date().toISOString(),updatedAt:new Date().toISOString(),pendingAssetDeletes:[],assets:[{id:'image',relativePath:'assets/image.png',mimeType:'image/png',width:1600,height:800,sha256:'0'.repeat(64)}],steps:[{id:'step',order:1,title:'Annotate the screenshot',instruction:'Use synthetic content.',notes:'',assetId:'image',annotations:[],needsReview:true,crop:null}]};
  if(stepCount>1){guide.assets.push({id:'portrait',relativePath:'assets/portrait.png',mimeType:'image/png',width:800,height:1600,sha256:'0'.repeat(64)});const base=guide.steps[0];guide.steps=Array.from({length:stepCount},(_,i)=>({...base,id:'step-'+i,order:i+1,title:'Action '+(i+1),assetId:i%2?'portrait':'image',annotations:[]}));}
  let preferences:any={hotkey:'R',screenshotHotkey:'S',exclusions:['keepass']};
  (window as any).__hostEvent=(data:any)=>listeners.forEach(fn=>fn({data}));
  (window as any).__guide=guide;(window as any).__imageMissing=missing;(window as any).__commands=[];(window as any).__copies=[];
  (window as any).chrome={webview:{addEventListener:(_:string,fn:any)=>listeners.push(fn),postMessage:(m:any)=>{
   (window as any).__commands.push(m.type);let result:any={},error='';if(m.type==='editor.ready')result={guide};
   if(m.type==='settings.get')result=preferences;
   if(m.type==='settings.update'){preferences=m.payload;(window as any).__preferences=preferences;}
   if(m.type==='app.flushComplete')(window as any).__selection=m.payload;
   if(m.type==='screenshot.capture'){
    if((window as any).__failScreenshot)error='Screenshot could not be saved.';
    else {
     const asset={...guide.assets[0],id:crypto.randomUUID()};
     let step=guide.steps.find((s:any)=>s.id===m.payload.stepId);
     if(!step){step={id:crypto.randomUUID(),order:guide.steps.length+1,title:'Screenshot',instruction:'',notes:''};guide={...guide,steps:[...guide.steps,step]};}
     guide={...guide,assets:[...guide.assets,asset],steps:guide.steps.map((s:any)=>s.id===step.id?{...s,assetId:asset.id,annotations:[],crop:null,cuts:[],needsReview:true}:s)};
     (window as any).__guide=guide;
     setTimeout(()=>listeners.forEach(fn=>fn({data:{type:'project.changed',guide,stepId:step.id}})),0);
    }
   }
   if(m.type==='asset.applyAllPixelEdits'){
    if((window as any).__failBulk)error='Image edits could not be saved.';
    else {
     const affected=new Set(guide.steps.filter((s:any)=>s.annotations.some((a:any)=>['blur','redact'].includes(a.kind))).map((s:any)=>s.assetId));
     const ids=new Map([...affected].map(id=>[id,crypto.randomUUID()]));
     guide={...guide,assets:guide.assets.map((a:any)=>ids.has(a.id)?{...a,id:ids.get(a.id)}:a),steps:guide.steps.map((s:any)=>ids.has(s.assetId)?{...s,assetId:ids.get(s.assetId),annotations:s.annotations.filter((a:any)=>!['blur','redact'].includes(a.kind))}:s),pendingAssetDeletes:[]};
     if((window as any).__failCleanup)guide.pendingAssetDeletes=['assets/old.png'];
     (window as any).__guide=guide;result={guide,warning:(window as any).__failCleanup?'Finish privacy cleanup before exporting.':undefined};
    }
   }
   if(m.type==='asset.read'){if((window as any).__imageMissing)error='This screenshot file is missing.';else {const a=guide.assets.find((a:any)=>a.id===m.payload.assetId);result={url:'data:image/png;base64,'+(a.width===800?portrait:png),width:a.width,height:a.height};}}
   if(m.type==='clipboard.copy'){if((window as any).__failCopy)error='The Windows clipboard is busy. Try copying again.';else{(window as any).__copies.push({format:m.payload.format,title:guide.steps[0].title,fields:guide.steps[0].titleFields||[]});result={copied:true};}}
   if(m.type==='asset.import'){if((window as any).__failImport)error='This image could not be decoded.';else if((window as any).__cancelImport)result=null;else{result={...guide.assets[0],id:crypto.randomUUID()};guide={...guide,assets:[...guide.assets,result]};}}
   if(m.type==='project.save' && JSON.stringify(m.payload.assets)!==JSON.stringify(guide.assets))error='Project or asset identity changed.';
   if(m.type==='project.save' && (window as any).__failSave)error='Save failed — retry Save';
   if(m.type==='project.save' && !error){guide=m.payload;(window as any).__guide=guide;result={saved:true};}if(m.type==='project.list')result=[guide];if(m.type==='project.open')result=guide;
   if(m.type==='asset.applyPixelEdits'){guide={...guide,assets:guide.assets.map((a:any)=>({...a,id:'sanitized',sha256:'1'.repeat(64)})),steps:guide.steps.map((s:any)=>({...s,assetId:'sanitized',annotations:s.annotations.filter((a:any)=>!['blur','redact'].includes(a.kind))}))};(window as any).__guide=guide;result={guide};}
   setTimeout(()=>listeners.forEach(fn=>fn({data:{requestId:m.requestId,ok:!error,result,error}})),m.type==='project.save'?((window as any).__saveDelay||0):0);
  }}};
 },{png,missing,stepCount,portrait:readFileSync(resolve(process.cwd(),'../../tests/fixtures/portrait.png')).toString('base64')});await page.goto('/');
}
async function draw(page:Page,tool:string,start={x:.15,y:.15},end={x:.4,y:.4}){
 await page.getByRole('button',{name:tool,exact:true}).click();const canvas=page.getByRole('img',{name:'Screenshot annotation canvas'});await canvas.scrollIntoViewIfNeeded();const b=(await canvas.boundingBox())!;
 await page.mouse.move(b.x+b.width*start.x,b.y+b.height*start.y);await page.mouse.down();await page.mouse.move(b.x+b.width*end.x,b.y+b.height*end.y,{steps:5});await page.mouse.up();
}
async function saved(page:Page){await page.getByRole('button',{name:'Save',exact:true}).click();await expect(page.getByRole('button',{name:'Save',exact:true})).toBeEnabled();return page.evaluate(()=>(window as any).__guide);}
test('native screenshot loads and supports drawing, moving, resizing, styling and clipboard undo',async({page})=>{
 await host(page);await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toBeVisible();await expect.poll(()=>page.evaluate(()=>(window as any).__commands.includes('asset.previewReady'))).toBe(true);
 await draw(page,'Rectangle');await expect(page.locator('[data-kind=rectangle]')).toHaveCount(1);await page.getByLabel('Outline width',{exact:true}).fill('8');await page.getByLabel('Shadow',{exact:true}).check();
 const canvas=page.getByRole('img',{name:'Screenshot annotation canvas'});await canvas.scrollIntoViewIfNeeded();const b=(await canvas.boundingBox())!;await page.mouse.move(b.x+b.width*.25,b.y+b.height*.25);await page.mouse.down();await page.mouse.move(b.x+b.width*.35,b.y+b.height*.35,{steps:5});await page.mouse.up();
 const h=(await page.locator('[data-handle=se]').boundingBox())!;await page.mouse.move(h.x+h.width/2,h.y+h.height/2);await page.mouse.down();await page.mouse.move(h.x+h.width/2+40,h.y+h.height/2+20,{steps:5});await page.mouse.up();
 const a=(await saved(page)).steps[0].annotations[0];expect(a.x).toBeCloseTo(.25,1);expect(a.width).toBeGreaterThan(.25);expect(a.strokeWidth).toBe(8);expect(a.shadow).toBe(true);
 await page.getByRole('button',{name:'Copy',exact:true}).click();await page.getByRole('button',{name:'Paste',exact:true}).click();await expect(page.locator('[data-kind=rectangle]')).toHaveCount(2);await page.getByRole('button',{name:'Cut selection',exact:true}).click();await expect(page.locator('[data-kind=rectangle]')).toHaveCount(1);await page.getByRole('button',{name:'Undo',exact:true}).click();await expect(page.locator('[data-kind=rectangle]')).toHaveCount(2);await page.getByRole('button',{name:'Redo',exact:true}).click();await expect(page.locator('[data-kind=rectangle]')).toHaveCount(1);
});
test('text, callouts, arrows, ellipse, line and crop save and reopen',async({page})=>{
 await host(page);await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toBeVisible();await draw(page,'Text');await page.getByLabel('Annotation text',{exact:true}).fill('Save your draft');await page.getByLabel('Font size',{exact:true}).fill('48');await expect(page.locator('svg text')).toHaveAccessibleName('Save your draft');
 await draw(page,'Callout',{x:.5,y:.1},{x:.85,y:.45});await page.getByLabel('Annotation text',{exact:true}).fill('Review this control');await draw(page,'Arrow',{x:.8,y:.8},{x:.4,y:.5});await draw(page,'Ellipse',{x:.1,y:.6},{x:.3,y:.85});await draw(page,'Line',{x:.2,y:.55},{x:.6,y:.55});
 await draw(page,'Crop',{x:.1,y:.1},{x:.9,y:.9});await page.getByRole('button',{name:'Apply crop',exact:true}).click();const g=await saved(page);expect(g.steps[0].annotations.map((a:any)=>a.kind)).toEqual(['text','callout','arrow','ellipse','line']);expect(g.steps[0].crop.width).toBeCloseTo(.8,1);expect(g.steps[0].annotations[0].fontSize).toBe(48);
 await page.getByRole('button',{name:'Reset crop',exact:true}).click();expect((await saved(page)).steps[0].crop).toBeNull();await page.getByRole('button',{name:'Projects',exact:true}).click();await page.getByRole('button',{name:/Synthetic screenshot workflow/}).click();await expect(page.locator('[data-kind=callout]')).toHaveCount(1);
});
test('missing screenshot has an error and retry',async({page})=>{
 await host(page,true);await expect(page.getByRole('alert')).toContainText('Screenshot unavailable');await expect(page.getByRole('alert')).toContainText('file is missing');await page.evaluate(()=>(window as any).__imageMissing=false);await page.getByRole('button',{name:'Retry screenshot',exact:true}).click();await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toBeVisible();
});
test('permanent blur and redaction clear undo history',async({page})=>{
 await host(page);await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toBeVisible();await draw(page,'Blur');await page.getByLabel('Blur strength',{exact:true}).press('ArrowRight');await draw(page,'Redact',{x:.5,y:.5},{x:.8,y:.8});await page.getByRole('button',{name:'Apply blur/redaction permanently',exact:true}).click();await expect(page.locator('[data-kind=blur], [data-kind=redact]')).toHaveCount(0);await expect(page.getByRole('button',{name:'Undo',exact:true})).toBeDisabled();expect(await page.evaluate(()=>(window as any).__guide.assets[0].id)).toBe('sanitized');
});

test('cut removes horizontal and vertical strips, joins the image and supports undo/reset',async({page})=>{
 await host(page);await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toBeVisible();
 await draw(page,'Cut strip',{x:.1,y:.1},{x:.8,y:.2});await page.getByRole('button',{name:'Apply cut',exact:true}).click();
 await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toHaveAttribute('viewBox','0 0 1600 720');
 await page.getByRole('button',{name:'Cut strip',exact:true}).click();await page.getByLabel('Cut direction',{exact:true}).selectOption('vertical');
 await draw(page,'Cut strip',{x:.2,y:.2},{x:.3,y:.8});await page.getByRole('button',{name:'Apply cut',exact:true}).click();
 await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toHaveAttribute('viewBox','0 0 1440 720');
 const g=await saved(page);expect(g.steps[0].cuts.map((c:any)=>c.axis)).toEqual(['horizontal','vertical']);
 await page.getByRole('button',{name:'Undo edit',exact:true}).click();await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toHaveAttribute('viewBox','0 0 1600 720');
 await page.getByRole('button',{name:'Reset cuts',exact:true}).click();await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toHaveAttribute('viewBox','0 0 1600 800');
});
test('bold fields and both clipboard buttons save first without opening a file dialog',async({page})=>{
 await host(page);await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toBeVisible();
 const title=page.getByLabel('Step title',{exact:true});await title.fill('Select Save draft');await title.press('End');for(let i=0;i<10;i++)await title.press('Shift+ArrowLeft');
 await page.getByRole('button',{name:'Bold field',exact:true}).click();await page.locator('.formatted-preview summary').click();await expect(page.locator('.title-preview strong')).toHaveText('Save draft');
 await page.locator('.export-menu summary').click();await page.getByRole('button',{name:'Copy HTML code',exact:true}).click();await expect(page.locator('.save-status')).toHaveText('HTML code copied');
 await page.locator('.export-menu summary').click();await page.getByRole('button',{name:'Copy steps for Word',exact:true}).click();await expect(page.locator('.save-status')).toHaveText('Word steps copied — paste into Word');
 expect(await page.evaluate(()=>(window as any).__copies)).toEqual(['html','word'].map(format=>({format,title:'Select Save draft',fields:['Save draft']})));
 expect(await page.evaluate(()=>(window as any).__commands.includes('export.request'))).toBe(false);
});
test('clipboard failure retains the edited guide',async({page})=>{
 await host(page);await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toBeVisible();await page.getByLabel('Step title',{exact:true}).fill('Preserve this edit');await page.evaluate(()=>(window as any).__failCopy=true);
 await page.locator('.export-menu summary').click();await page.getByRole('button',{name:'Copy steps for Word',exact:true}).click();await expect(page.getByRole('alert')).toContainText('clipboard is busy');await expect(page.getByLabel('Step title',{exact:true})).toHaveValue('Preserve this edit');
});

test('add and replace images preserve text, undo history, manifest and saved projects',async({page})=>{
 await host(page);
 await page.getByLabel('Step title',{exact:true}).fill('Keep my instructions');
 await draw(page,'Rectangle');
 await page.getByRole('button',{name:'Replace image',exact:true}).click();
 await expect(page.locator('.save-status')).toHaveText('Saved locally');
 let g=await saved(page);const replacement=g.steps[0].assetId;
 expect(replacement).not.toBe('image');expect(g.assets).toHaveLength(2);expect(g.steps[0].annotations).toEqual([]);
 expect(g.steps[0].title).toBe('Keep my instructions');expect(g.steps[0].instruction).toBe('Use synthetic content.');
 await page.getByRole('button',{name:'Undo',exact:true}).click();
 g=await saved(page);expect(g.steps[0].assetId).toBe('image');expect(g.steps[0].annotations).toHaveLength(1);expect(g.assets).toHaveLength(2);
 await page.getByRole('button',{name:'Redo',exact:true}).click();
 g=await saved(page);expect(g.steps[0].assetId).toBe(replacement);
 await page.getByRole('button',{name:'+ Add image step',exact:true}).click();
 await expect(page.locator('aside li')).toHaveCount(2);
 g=await saved(page);expect(g.steps[1].title).toBe('New image step');expect(g.steps[1].needsReview).toBe(true);expect(g.assets).toHaveLength(3);
 await page.getByRole('button',{name:'Remove image',exact:true}).click();
 g=await saved(page);expect(g.steps[1].assetId).toBeNull();
 await page.getByRole('button',{name:'Add image',exact:true}).click();
 g=await saved(page);expect(g.steps[1].assetId).toBeTruthy();expect(g.assets).toHaveLength(4);
 await page.getByRole('button',{name:'Projects',exact:true}).click();
 await page.getByRole('button',{name:/Synthetic screenshot workflow/}).click();
 await expect(page.locator('aside li')).toHaveCount(2);await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toBeVisible();
 await expect(page.getByRole('alert')).toHaveCount(0);
});
test('cancelled and invalid image imports retain edits and undo',async({page})=>{
 await host(page);await page.getByLabel('Step title',{exact:true}).fill('Unsaved title');
 await page.evaluate(()=>(window as any).__cancelImport=true);
 await page.getByRole('button',{name:'+ Add image step',exact:true}).click();
 await expect(page.locator('.save-status')).toHaveText('Image selection cancelled');
 await expect(page.locator('aside li')).toHaveCount(1);
 await page.evaluate(()=>{(window as any).__cancelImport=false;(window as any).__failImport=true;});
 await page.getByRole('button',{name:'Replace image',exact:true}).click();
 await expect(page.getByRole('alert')).toContainText('could not be decoded');
 expect((await saved(page)).steps[0].title).toBe('Unsaved title');
 await page.getByRole('button',{name:'Undo',exact:true}).click();
 expect((await saved(page)).steps[0].title).toBe('Annotate the screenshot');
});

test('image attachment remains editable when saving fails and can be retried',async({page})=>{
 await host(page);
 await page.evaluate(()=>(window as any).__failSave=true);
 await page.getByRole('button',{name:'+ Add image step',exact:true}).click();
 await expect(page.getByRole('alert')).toContainText('Save failed');
 await expect(page.locator('aside li')).toHaveCount(2);
 await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toBeVisible();
 await page.evaluate(()=>(window as any).__failSave=false);
 const g=await saved(page);expect(g.steps).toHaveLength(2);expect(g.steps[1].assetId).toBe(g.assets[1].id);
});

test('screenshots attach to a selected step, create a new step and retain undo',async({page})=>{
 await host(page);await page.getByLabel('Step title',{exact:true}).fill('Keep this title');
 await draw(page,'Rectangle');
 await page.getByRole('button',{name:'Capture screenshot for step',exact:true}).click();
 await expect(page.locator('.save-status')).toHaveText('Screenshot captured');
 let g=await saved(page);expect(g.steps).toHaveLength(1);expect(g.steps[0].title).toBe('Keep this title');expect(g.steps[0].annotations).toEqual([]);expect(g.assets).toHaveLength(2);
 await page.getByRole('button',{name:'Undo',exact:true}).click();
 g=await saved(page);expect(g.steps[0].assetId).toBe('image');expect(g.steps[0].annotations).toHaveLength(1);
 await page.getByRole('button',{name:/^Capture screenshot Ctrl/}).click();
 await expect(page.locator('aside li')).toHaveCount(2);g=await saved(page);expect(g.steps[1].title).toBe('Screenshot');
 await page.evaluate(()=>(window as any).__hostEvent({type:'editor.flush'}));
 await expect.poll(()=>page.evaluate(()=>(window as any).__selection?.stepId)).toBe(g.steps[1].id);
 await page.evaluate(()=>(window as any).__failScreenshot=true);
 await page.getByRole('button',{name:'Capture screenshot for step',exact:true}).click();
 await expect(page.getByRole('alert')).toContainText('Screenshot could not be saved');
 await expect(page.locator('aside li')).toHaveCount(2);
});
test('bulk apply handles multiple images and shared steps before continuing export',async({page})=>{
 await host(page);await page.getByRole('button',{name:'Duplicate step 1',exact:true}).click();
 await draw(page,'Blur');
 await page.getByRole('button',{name:'+ Add image step',exact:true}).click();
 await draw(page,'Redact');
 await expect(page.getByRole('region',{name:'Apply image edits'})).toContainText('2 images');
 await page.locator('.export-menu summary').click();await page.getByRole('button',{name:'Word DOCX',exact:true}).click();
 expect(await page.evaluate(()=>(window as any).__commands.includes('export.request'))).toBe(false);
 await page.getByRole('button',{name:'Apply all and continue',exact:true}).click();
 await expect(page.locator('.save-status')).toHaveText('Word export finished');
 const g=await saved(page);expect(g.steps).toHaveLength(3);expect(g.steps.every((s:any)=>s.annotations.length===0)).toBe(true);
 expect(g.steps[0].assetId).toBe(g.steps[1].assetId);
 await expect(page.getByRole('button',{name:'Undo',exact:true})).toBeDisabled();
 const commands:string[]=await page.evaluate(()=>(window as any).__commands);
 expect(commands.indexOf('asset.applyAllPixelEdits')).toBeLessThan(commands.indexOf('export.request'));
});
test('bulk failure and cleanup failure block export with retry',async({page})=>{
 await host(page);await draw(page,'Redact');
 await page.locator('.export-menu summary').click();await page.getByRole('button',{name:'Copy HTML code',exact:true}).click();
 await page.evaluate(()=>(window as any).__failBulk=true);
 await page.getByRole('button',{name:'Apply all and continue',exact:true}).click();
 await expect(page.getByRole('alert')).toContainText('could not be saved');
 expect(await page.evaluate(()=>(window as any).__commands.includes('clipboard.copy'))).toBe(false);
 expect((await saved(page)).steps[0].annotations).toHaveLength(1);
 await page.evaluate(()=>{(window as any).__failBulk=false;(window as any).__failCleanup=true;});
 await page.getByRole('button',{name:'Apply all and continue',exact:true}).click();
 await expect(page.getByRole('alert').first()).toContainText('Finish privacy cleanup');
 expect(await page.evaluate(()=>(window as any).__commands.includes('clipboard.copy'))).toBe(false);
 await page.evaluate(()=>(window as any).__failCleanup=false);
 await page.locator('.export-menu summary').click();await page.getByRole('button',{name:'Copy HTML code',exact:true}).click();
 await page.getByRole('button',{name:'Apply all and continue',exact:true}).click();
 await expect(page.locator('.save-status')).toHaveText('HTML code copied');
});
test('separate screenshot shortcut settings and persistent light/dark appearance',async({page})=>{
 await page.setViewportSize({width:1440,height:1000});await host(page);
 await expect(page.locator('html')).toHaveAttribute('data-theme','dark');
 await expect(page.locator('.document-actions>button')).toHaveCSS('background-color','rgb(27, 31, 39)');
 await expect(page.locator('.notice')).toHaveCount(0);
 await expect.poll(()=>page.locator('.brand img').evaluate((img:HTMLImageElement)=>img.naturalWidth)).toBe(96);
 await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toBeVisible();await page.evaluate(()=>scrollTo(0,0));
 if(process.env.CAPTURE_UI_PREVIEW)await page.screenshot({path:process.env.CAPTURE_UI_PREVIEW+'/dark.png',fullPage:true});
 await page.getByRole('button',{name:'Switch to light mode',exact:true}).click();
 await expect(page.locator('html')).toHaveAttribute('data-theme','light');
 await expect(page.locator('.document-actions>button')).toHaveCSS('background-color','rgb(255, 255, 255)');
 await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toBeVisible();await page.evaluate(()=>scrollTo(0,0));
 if(process.env.CAPTURE_UI_PREVIEW)await page.screenshot({path:process.env.CAPTURE_UI_PREVIEW+'/light.png',fullPage:true});
 await page.reload();await expect(page.locator('html')).toHaveAttribute('data-theme','light');
 await page.getByRole('button',{name:'Settings',exact:true}).click();
 await expect(page.getByLabel('Recording shortcut',{exact:true})).toHaveValue('R');
 await expect(page.getByLabel('Screenshot shortcut',{exact:true})).toHaveValue('S');
 await page.getByLabel('Screenshot shortcut',{exact:true}).selectOption('R');
 await expect(page.getByRole('button',{name:'Save settings',exact:true})).toBeDisabled();
 await page.getByLabel('Screenshot shortcut',{exact:true}).selectOption('P');
 await page.getByRole('button',{name:'Save settings',exact:true}).click();
 expect(await page.evaluate(()=>(window as any).__preferences.screenshotHotkey)).toBe('P');
 await expect(page.getByRole('button',{name:/^Capture screenshot Ctrl/})).toHaveAttribute('title','Ctrl + Shift + P');
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
});

async function assertFits(page:Page){
 await expect.poll(()=>page.evaluate(()=>{
  const stage=document.querySelector('.image-stage'),image=stage?.querySelector('svg');if(!stage||!image)return false;const s=stage.getBoundingClientRect(),r=image.getBoundingClientRect();
  return r.width>10&&r.height>10&&r.left>=s.left-1&&r.right<=s.right+1&&r.top>=s.top-1&&r.bottom<=s.bottom+1&&stage.scrollHeight<=stage.clientHeight+1&&stage.scrollWidth<=stage.clientWidth+1&&s.bottom<=innerHeight+1&&document.documentElement.scrollHeight<=innerHeight+1;
 })).toBe(true);
}
test('Fit image contains wide and portrait screenshots with 21 steps at desktop and minimum window sizes',async({page})=>{
 await page.setViewportSize({width:1920,height:1057});await host(page,false,21);
 for(const size of [{width:1920,height:1057},{width:1366,height:768},{width:1400,height:920},{width:800,height:600}]){
  await page.setViewportSize(size);
  if(await page.getByRole('button',{name:'Step details',exact:true}).getAttribute('aria-expanded')==='true')await page.getByRole('button',{name:'Close step details',exact:true}).click();
  for(const index of [0,1,20]){
   await page.locator('aside li .step-select').nth(index).click();await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toBeVisible();await assertFits(page);
  }
  const sidebar=await page.locator('.step-sidebar').boundingBox();expect(sidebar!.y+sidebar!.height).toBeLessThanOrEqual(size.height+1);
 }
});
test('zoom preference survives step changes and reload, while crop and tool changes recalculate Fit',async({page})=>{
 await page.setViewportSize({width:1400,height:920});await host(page,false,21);
 await page.getByLabel('Screenshot zoom',{exact:true}).selectOption('0.5');
 await page.locator('aside li .step-select').nth(1).click();await expect(page.getByLabel('Screenshot zoom',{exact:true})).toHaveValue('0.5');
 await expect.poll(()=>page.getByRole('img',{name:'Screenshot annotation canvas'}).evaluate(e=>Math.round(e.getBoundingClientRect().width))).toBe(400);
 await page.reload();await expect(page.getByLabel('Screenshot zoom',{exact:true})).toHaveValue('0.5');
 await page.getByLabel('Screenshot zoom',{exact:true}).selectOption('fit');await assertFits(page);
 await draw(page,'Crop',{x:.1,y:.1},{x:.7,y:.65});await page.getByRole('button',{name:'Apply crop',exact:true}).click();await assertFits(page);
 await page.getByRole('button',{name:'Reset crop',exact:true}).click();await assertFits(page);
 await page.getByRole('button',{name:'Zoom in',exact:true}).click();await expect(page.getByLabel('Screenshot zoom',{exact:true})).not.toHaveValue('fit');
 const g=await saved(page);expect(g.steps[0].crop).toBeNull();expect(g).not.toHaveProperty('zoom');expect(g.steps[0]).not.toHaveProperty('zoom');
});
test('sidebar drag, keyboard reorder, duplicate and delete preserve selection, order and undo',async({page})=>{
 await page.setViewportSize({width:1440,height:1000});await host(page,false,6);await page.evaluate(()=>(window as any).__saveDelay=150);
 await page.locator('aside li .step-select').nth(1).click();
 await page.getByRole('button',{name:'Reorder step 1',exact:true}).dragTo(page.locator('aside li').nth(3),{targetPosition:{x:80,y:45}});
 let g=await saved(page);expect(g.steps.map((s:any)=>s.title)).toEqual(['Action 2','Action 3','Action 4','Action 1','Action 5','Action 6']);
 await expect(page.getByLabel('Step title',{exact:true})).toHaveValue('Action 2');
 await page.getByRole('button',{name:'Undo',exact:true}).click();await expect(page.getByLabel('Step title',{exact:true})).toHaveValue('Action 2');
 await page.locator('aside li .step-select').nth(1).focus();await page.keyboard.press('Alt+ArrowDown');
 g=await saved(page);expect(g.steps[2].title).toBe('Action 2');await expect(page.locator('aside [aria-current=step]')).toContainText('Action 2');
 await page.locator('aside li .step-select').nth(2).focus();await page.keyboard.press('Control+d');await expect(page.locator('aside li')).toHaveCount(7);
 await page.keyboard.press('Delete');await expect(page.locator('aside li')).toHaveCount(6);
 await page.getByRole('button',{name:'Undo',exact:true}).click();await expect(page.locator('aside li')).toHaveCount(7);await expect(page.locator('aside [aria-current=step]')).toContainText('Action 2');
 g=await saved(page);expect(g.steps.map((s:any)=>s.order)).toEqual([1,2,3,4,5,6,7]);
 await page.getByRole('button',{name:'Projects',exact:true}).click();await page.getByRole('button',{name:/Synthetic screenshot workflow/}).click();await expect(page.locator('aside li')).toHaveCount(7);
});
test('Delete in text or on an annotation never deletes a step, and zoomed canvas supports panning',async({page})=>{
 await page.setViewportSize({width:1440,height:900});await host(page,false,3);
 await page.getByLabel('Step title',{exact:true}).focus();await page.keyboard.press('Delete');await expect(page.locator('aside li')).toHaveCount(3);
 await draw(page,'Rectangle');const image=page.getByRole('img',{name:'Screenshot annotation canvas'});await image.focus();await page.keyboard.press('Delete');
 await expect(page.locator('[data-kind=rectangle]')).toHaveCount(0);await expect(page.locator('aside li')).toHaveCount(3);
 await page.getByLabel('Screenshot zoom',{exact:true}).selectOption('2');
 const stage=page.locator('.image-stage');const b=(await stage.boundingBox())!;
 await page.mouse.move(b.x+b.width/2,b.y+b.height/2);await page.mouse.down({button:'middle'});await page.mouse.move(b.x+b.width/2-80,b.y+b.height/2-60,{steps:5});await page.mouse.up({button:'middle'});
 expect(await stage.evaluate(e=>e.scrollLeft)).toBeGreaterThan(0);expect(await stage.evaluate(e=>e.scrollTop)).toBeGreaterThan(0);
});

test.describe('scaled display geometry',()=>{
 test.use({viewport:{width:1280,height:720},deviceScaleFactor:1.5});
 test('Fit and annotation coordinates remain correct with scaled pixels',async({page})=>{
  await host(page,false,3);await page.locator('aside li .step-select').nth(1).click();await assertFits(page);
  await page.getByLabel('Screenshot zoom',{exact:true}).selectOption('width');
  await expect.poll(()=>page.locator('.image-stage').evaluate(e=>e.scrollHeight>e.clientHeight)).toBe(true);
  await page.getByLabel('Screenshot zoom',{exact:true}).selectOption('fit');await assertFits(page);
  await draw(page,'Rectangle',{x:.2,y:.2},{x:.6,y:.5});
  const a=(await saved(page)).steps[1].annotations[0];expect(a.x).toBeCloseTo(.2,1);expect(a.y).toBeCloseTo(.2,1);expect(a.width).toBeCloseTo(.4,1);expect(a.height).toBeCloseTo(.3,1);
 });
});
test('export menu remains usable above the narrow details drawer',async({page})=>{
 await page.setViewportSize({width:800,height:600});await host(page);
 await page.getByRole('button',{name:'Step details',exact:true}).click();
 await expect(page.getByRole('region',{name:'Step details',exact:true})).toBeVisible();
 await page.locator('.export-menu summary').click();await page.getByRole('button',{name:'Copy HTML code',exact:true}).click();
 await expect(page.locator('.save-status')).toHaveText('HTML code copied');
});
