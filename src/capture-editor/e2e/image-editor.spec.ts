import {test,expect,Page} from '@playwright/test';
import {readFileSync} from 'node:fs';
import {resolve} from 'node:path';
const png=readFileSync(resolve(process.cwd(),'../../tests/fixtures/landscape.png')).toString('base64');
async function host(page:Page,missing=false){
 await page.addInitScript(({png,missing})=>{
  const listeners:((event:any)=>void)[]=[];
  let guide:any={schemaVersion:'1.1.0',id:'00000000-0000-0000-0000-000000000001',title:'Synthetic screenshot workflow',summary:'',createdAt:new Date().toISOString(),updatedAt:new Date().toISOString(),pendingAssetDeletes:[],assets:[{id:'image',relativePath:'assets/image.png',mimeType:'image/png',width:1600,height:800,sha256:'0'.repeat(64)}],steps:[{id:'step',order:1,title:'Annotate the screenshot',instruction:'Use synthetic content.',notes:'',assetId:'image',annotations:[],needsReview:true,crop:null}]};
  (window as any).__guide=guide;(window as any).__imageMissing=missing;(window as any).__commands=[];(window as any).__copies=[];
  (window as any).chrome={webview:{addEventListener:(_:string,fn:any)=>listeners.push(fn),postMessage:(m:any)=>{
   (window as any).__commands.push(m.type);let result:any={},error='';if(m.type==='editor.ready')result={guide};
   if(m.type==='asset.read'){if((window as any).__imageMissing)error='This screenshot file is missing.';else result={url:'data:image/png;base64,'+png,width:1600,height:800};}
   if(m.type==='clipboard.copy'){if((window as any).__failCopy)error='The Windows clipboard is busy. Try copying again.';else{(window as any).__copies.push({format:m.payload.format,title:guide.steps[0].title,fields:guide.steps[0].titleFields||[]});result={copied:true};}}
   if(m.type==='project.save'){guide=m.payload;(window as any).__guide=guide;result={saved:true};}if(m.type==='project.list')result=[guide];if(m.type==='project.open')result=guide;
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
 await page.getByRole('button',{name:'Copy HTML code',exact:true}).click();await expect(page.getByRole('status')).toHaveText('HTML code copied');
 await page.getByRole('button',{name:'Copy steps for Word',exact:true}).click();await expect(page.getByRole('status')).toHaveText('Word steps copied — paste into Word');
 expect(await page.evaluate(()=>(window as any).__copies)).toEqual(['html','word'].map(format=>({format,title:'Select Save draft',fields:['Save draft']})));
 expect(await page.evaluate(()=>(window as any).__commands.includes('export.request'))).toBe(false);
});
test('clipboard failure retains the edited guide',async({page})=>{
 await host(page);await expect(page.getByRole('img',{name:'Screenshot annotation canvas'})).toBeVisible();await page.getByLabel('Step title',{exact:true}).fill('Preserve this edit');await page.evaluate(()=>(window as any).__failCopy=true);
 await page.getByRole('button',{name:'Copy steps for Word',exact:true}).click();await expect(page.getByRole('alert')).toContainText('clipboard is busy');await expect(page.getByLabel('Step title',{exact:true})).toHaveValue('Preserve this edit');
});
