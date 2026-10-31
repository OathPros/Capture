import { readFileSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { test, expect } from '@playwright/test';
test('host bridge: create, edit, save, reorder, undo, export and reopen',async({page})=>{
 await page.addInitScript(()=>{
  let guide: any=null; const callbacks: ((e:any)=>void)[]=[];
  (window as any).__commands=[]; (window as any).__exports=[];
  (window as any).chrome={webview:{addEventListener:(_:string,fn:any)=>callbacks.push(fn),postMessage:(m:any)=>{
   (window as any).__commands.push(m.type); if(m.type==='export.request')(window as any).__exports.push({format:m.payload.format,title:guide?.title});
   let result:any={};
   if(m.type==='editor.ready')result={guide:null};
   if(m.type==='project.new') {guide={schemaVersion:'1.0.0',id:crypto.randomUUID(),title:'Untitled guide',summary:'',createdAt:new Date().toISOString(),updatedAt:new Date().toISOString(),steps:[],assets:[]};result=guide;}
   if(m.type==='project.save'){guide=m.payload;result={saved:true};}
   if(m.type==='project.list')result=guide?[guide]:[];
   if(m.type==='project.open')result=guide;
   setTimeout(()=>callbacks.forEach(fn=>fn({data:{requestId:m.requestId,ok:true,result}})),0);
  }}};
 });
 await page.goto('/');await expect(page.getByRole('heading',{name:'Your guides'})).toBeVisible();
 await page.getByRole('button',{name:'New text guide'}).click();await page.getByLabel('Guide title').fill('Synthetic training');
 await page.getByRole('button',{name:'+ Add text step'}).click();await page.getByLabel('Step title',{exact:true}).fill('First action');await page.getByLabel('Instruction',{exact:true}).fill('Select a fictional button.');
 await page.getByRole('button',{name:'+ Add text step'}).click();await page.getByLabel('Step title',{exact:true}).fill('Second action');
 await page.getByRole('button',{name:'Move up',exact:true}).click();await expect(page.locator('aside li').first()).toContainText('Second action');
 await page.getByRole('button',{name:'Undo',exact:true}).click();await expect(page.locator('aside li').first()).toContainText('First action');
 await page.getByRole('button',{name:'Save',exact:true}).click();await expect(page.getByRole('status')).toHaveText('Saved locally');
 await page.getByLabel('Guide title').fill('Saved before export');
 await page.getByRole('button',{name:'Word DOCX',exact:true}).click();
 await expect(page.getByRole('status')).toHaveText('Word export finished');
 await page.getByRole('button',{name:'HTML guide',exact:true}).click();
 await expect(page.getByRole('status')).toHaveText('HTML export finished');
 await page.getByRole('button',{name:'Markdown ZIP',exact:true}).click();
 expect(await page.evaluate(()=>(window as any).__exports)).toEqual(['docx','html','markdown'].map(format=>({format,title:'Saved before export'})));
 expect(await page.evaluate(()=>(window as any).__commands)).toContain('export.request');
 await page.getByRole('button',{name:'Projects',exact:true}).click();await page.getByRole('button',{name:/Saved before export/}).click();
 await expect(page.getByLabel('Guide title')).toHaveValue('Saved before export');await expect(page.locator('aside li')).toHaveCount(2);
});
test('browser without host explains unavailable native actions',async({page})=>{await page.goto('/');await expect(page.getByRole('alert')).toContainText('Open the Windows Capture application');});

test('standalone HTML export works offline on desktop and mobile',async({page})=>{
 const sample=resolve(process.cwd(),'../../artifacts/export-samples/synthetic-guide.html');
 test.skip(!existsSync(sample),'Run scripts/test.ps1 or generate samples with CAPTURE_EXPORT_SAMPLES before this integration check.');
 const external:string[]=[];await page.route(/^https?:/,route=>{external.push(route.request().url());return route.abort();});
 await page.setContent(readFileSync(sample,'utf8'));
 await expect(page.getByRole('heading',{level:1})).toHaveText('Create a training article');
 await expect(page.locator('article.step')).toHaveCount(3);
 for(const image of await page.locator('img').all()){await image.scrollIntoViewIfNeeded();await expect.poll(()=>image.evaluate(i=>(i as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);}
 await page.setViewportSize({width:390,height:844});
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 await page.emulateMedia({media:'print'});await expect(page.getByRole('navigation')).toBeHidden();
 expect(external).toEqual([]);
});
