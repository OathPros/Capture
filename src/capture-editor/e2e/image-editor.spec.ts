import {test,expect,Page} from '@playwright/test';
import {readFileSync} from 'node:fs';
import {resolve} from 'node:path';
const png=readFileSync(resolve(process.cwd(),'../../tests/fixtures/landscape.png')).toString('base64');
async function host(page:Page,missing=false){
 await page.addInitScript(({png,missing})=>{
  const listeners:((event:any)=>void)[]=[];
  let guide:any={schemaVersion:'1.1.0',id:'00000000-0000-0000-0000-000000000001',title:'Synthetic screenshot workflow',summary:'',createdAt:new Date().toISOString(),updatedAt:new Date().toISOString(),pendingAssetDeletes:[],assets:[{id:'image',relativePath:'assets/image.png',mimeType:'image/png',width:1600,height:800,sha256:'0'.repeat(64)}],steps:[{id:'step',order:1,title:'Annotate the screenshot',instruction:'Use synthetic content.',notes:'',assetId:'image',annotations:[],needsReview:true,crop:null}]};
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
   if(m.type==='asset.read'){if((window as any).__imageMissing)error='This screenshot file is missing.';else result={url:'data:image/png;base64,'+png,width:1600,height:800};}
   if(m.type==='clipboard.copy'){if((window as any).__failCopy)error='The Windows clipboard is busy. Try copying again.';else{(window as any).__copies.push({format:m.payload.format,title:guide.steps[0].title,fields:guide.steps[0].titleFields||[]});result={copied:true};}}
   if(m.type==='asset.import'){if((window as any).__failImport)error='This image could not be decoded.';else if((window as any).__cancelImport)result=null;else{result={...guide.assets[0],id:crypto.randomUUID()};guide={...guide,assets:[...guide.assets,result]};}}
   if(m.type==='project.save' && JSON.stringify(m.payload.assets)!==JSON.stringify(guide.assets))error='Project or asset identity changed.';
   if(m.type==='project.save' && (window as any).__failSave)error='Save failed — retry Save';
   if(m.type==='project.save' && !error){guide=m.payload;(window as any).__guide=guide;result={saved:true};}if(m.type==='project.list')result=[guide];if(m.type==='project.open')result=guide;
   if(m.type==='asset.applyPixelEdits'){guide={...guide,assets:guide.assets.map((a:any)=>({...a,id:'sanitized',sha256:'1'.repeat(64)})),steps:guide.steps.map((s:any)=>({...s,assetId:'sanitized',annotations:s.annotations.filter((a:any)=>!['blur','redact'].includes(a.kind))}))};(window as any).__guide=guide;result={guide};}
   setTimeout(()=>listeners.forEach(fn=>fn({data:{requestId:m.requestId,ok:!error,result,error}})),0);
  }}};
 },{png,missing});await page.goto('/');
}
async function draw(page:Page,tool:string,start={x:.15,y:.15},end={x:.4,y:.4}){
 await page.getByRole('button',{name:tool,exact:true}).click();const canvas=page.getByRole('img',{name:'Screenshot annotation canvas'});await canvas.scrollIntoViewIfNeeded();const b=(await canvas.boundingBox())!;
 await page.mouse.move(b.x+b.width*start.x,b.y+b.height*start.y);await page.mouse.down();await page.mouse.move(b.x+b.width*end.x,b.y+b.height*end.y,{steps:5});await page.mouse.up();
}
async function saved(page:Page){await page.getByRole('button',{name:'Save',exact:true}).click();return page.evaluate(()=>(window as any).__guide);}
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
 await page.getByRole('button',{name:'Bold field',exact:true}).click();await expect(page.locator('.title-preview strong')).toHaveText('Save draft');
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
 await host(page);await page.getByRole('button',{name:'Duplicate',exact:true}).click();
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
