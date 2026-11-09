import React, { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { request, onProjectChanged, onFlush, onHostError } from './bridge';
import { Asset, Guide, Step, moveStep } from './model';
import './style.css';
import {ImageEditor} from './ImageEditor';
import {FormattedText} from './FormattedText';
type Preferences={hotkey:string;screenshotHotkey:string;exclusions:string[];hotkeyRegistered?:boolean;screenshotHotkeyRegistered?:boolean};
type OutputAction={type:'export.request'|'clipboard.copy';format:string;label:string};
function App() {
 const [guide, setGuide] = useState<Guide | null>(null), [projects, setProjects] = useState<Guide[]>([]), [selected, select] = useState(0);
 const [status, setStatus] = useState('Connecting…'), [error, setError] = useState(''), [busy, setBusy] = useState(false);
 const [history, setHistory] = useState<Guide[]>([]), [future, setFuture] = useState<Guide[]>([]);
 const [settings, setSettings] = useState<Preferences | null>(null);
 const [shortcuts,setShortcuts]=useState({hotkey:'R',screenshotHotkey:'S'});
 const [theme,setTheme]=useState<'light'|'dark'>(()=>{try{return localStorage.getItem('capture.theme')==='light'?'light':'dark';}catch{return 'dark';}});
 const [pendingOutput,setPendingOutput]=useState<OutputAction|null>(null);
 const selectedRef=useRef(selected);selectedRef.current=selected;
 useLayoutEffect(()=>{document.documentElement.dataset.theme=theme;try{localStorage.setItem('capture.theme',theme);}catch{}if(window.chrome?.webview)void request('app.theme',{theme}).catch(()=>{});},[theme]);
 useEffect(()=>{
  const close=(e:PointerEvent)=>{if(e.target instanceof Element&&!e.target.closest('.export-menu'))document.querySelector('details.export-menu')?.removeAttribute('open');};
  const escape=(e:KeyboardEvent)=>{if(e.key==='Escape'){const menu=document.querySelector<HTMLDetailsElement>('details.export-menu[open]');menu?.removeAttribute('open');menu?.querySelector<HTMLElement>('summary')?.focus();}};
  document.addEventListener('pointerdown',close);document.addEventListener('keydown',escape);
  return ()=>{document.removeEventListener('pointerdown',close);document.removeEventListener('keydown',escape);};
 },[]);
 const titleInput=useRef<HTMLInputElement>(null);const [titleSelection,setTitleSelection]=useState(false);
 const latest = useRef(guide), saved = useRef(''), saveQueue = useRef(Promise.resolve());
 latest.current = guide;
 function load(g: Guide | null) { latest.current=g;setPendingOutput(null);saved.current = JSON.stringify(g); setGuide(g); select(0); setHistory([]); setFuture([]); setStatus('Saved locally'); }
 async function act<T>(fn: () => Promise<T>): Promise<T | undefined> { setBusy(true); setError(''); try { return await fn(); } catch(e) { setError(String(e instanceof Error ? e.message : e)); } finally { setBusy(false); } }
 function save(g: Guide): Promise<void> {
  const value = JSON.stringify(g);
  const next = saveQueue.current.catch(() => {}).then(async () => {
   if(saved.current === value) return;
   setStatus('Saving…');
   await request('project.save', g);
   saved.current = value;
   if(JSON.stringify(latest.current)===value) setStatus('Saved locally');
  });
  saveQueue.current = next; return next;
 }
 async function flush() { if(latest.current) await save(latest.current); }
 onFlush(async()=>{await flush();return {projectId:latest.current?.id,stepId:latest.current?.steps[selectedRef.current]?.id};});
 async function list() { await flush(); const p = await request<Guide[]>('project.list'); setProjects(p); load(null); }
 useEffect(() => {
  void act(async () => { const ready=await request<{guide: Guide|null;issues?:string[]}>('editor.ready');if(ready.issues?.length)setError(ready.issues.join(' ')); if(ready.guide) load(ready.guide); else setProjects(await request<Guide[]>('project.list')); setStatus('Ready');const preferences=await request<Partial<Preferences>>('settings.get');setShortcuts({hotkey:preferences.hotkey||'R',screenshotHotkey:preferences.screenshotHotkey||'S'}); });
  const stopErrors=onHostError(message=>setError(message));
  const stopChanges=onProjectChanged((g,stepId) => {
   const next=g as Guide;const base=latest.current;
   if(stepId && base?.id===next.id && base.assets.every(a=>next.assets.some(n=>n.id===a.id))){
    const manifest=(snapshot:Guide)=>({...snapshot,assets:next.assets});
    setHistory(h=>[...h.slice(-49).map(manifest),manifest(base)]);setFuture([]);
    saved.current=JSON.stringify(next);latest.current=next;setGuide(next);setStatus('Screenshot captured');
   }else load(next);
   if(stepId){select(Math.max(0,next.steps.findIndex(s=>s.id===stepId)));setPendingOutput(null);setError('');setStatus('Screenshot captured');}
  });
  return ()=>{stopChanges();stopErrors();};
 }, []);
 useEffect(() => {
  if(!guide || saved.current===JSON.stringify(guide)) return;
  setStatus('Unsaved changes');
  const timer=setTimeout(() => { void save(guide).catch(e=>{setStatus('Save failed — retry Save');setError(e.message);}); },500);
  return () => clearTimeout(timer);
 },[guide]);
 useEffect(() => {
  const handler=(e: KeyboardEvent) => { if((e.ctrlKey||e.metaKey) && !(e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement || e.target instanceof HTMLSelectElement)) {if(e.key==='z'){e.preventDefault();if(e.shiftKey)redo();else undo();}if(e.key==='y'){e.preventDefault();redo();}} if((e.ctrlKey||e.metaKey) && !e.shiftKey && !e.altKey && e.key.toLowerCase()==='s') { e.preventDefault(); void act(flush); } };
  window.addEventListener('keydown',handler); return () => window.removeEventListener('keydown',handler);
 });
 function edit(g: Guide) { if(!guide) return; setHistory(h=>[...h.slice(-49),guide]); setFuture([]); setGuide(g); }
 function stepEdit(change: Partial<Step>) { if(guide) edit({...guide,steps:guide.steps.map((s,i)=>i===selected?{...s,...change}:s)}); }
 function undo() { if(!guide || !history.length) return; setFuture(f=>[guide,...f]); setGuide(history.at(-1)!); setHistory(h=>h.slice(0,-1)); }
 function redo() { if(!guide || !future.length) return; setHistory(h=>[...h,guide]); setGuide(future[0]); setFuture(f=>f.slice(1)); }
 async function importImage(newStep: boolean) {
  await act(async()=>{
   await flush();
   const base=latest.current;if(!base)return;
   const image=await request<Asset|null>('asset.import',{projectId:base.id});
   if(!image){setStatus('Image selection cancelled');return;}
   // Native asset identity is immutable. Keep the manifest in every undo snapshot.
   const withAsset=(g:Guide):Guide=>({...g,assets:[...g.assets,image]});
   const imported=withAsset(base);
   saved.current=JSON.stringify(imported);
   setHistory(h=>[...h.slice(-49).map(withAsset),imported]);setFuture([]);
   const change={assetId:image.id,annotations:[],crop:null,cuts:[],needsReview:true};
   const steps=[...imported.steps];
   if(newStep){
    const index=steps.length?selected+1:0;
    steps.splice(index,0,{id:crypto.randomUUID(),order:index+1,title:'New image step',instruction:'',notes:'',...change});
    select(index);
   } else steps[selected]={...steps[selected],...change};
   const next={...imported,steps:steps.map((s,i)=>({...s,order:i+1}))};
   latest.current=next;setGuide(next);
   await save(next);
  });
 }
 const pendingImages=new Set(guide?.steps.filter(s=>s.annotations.some(a=>a.kind==='blur'||a.kind==='redact')).map(s=>s.assetId)).size;
 async function applyAll():Promise<boolean>{
  await flush();const index=selected;
  const result=await request<{guide:Guide;warning?:string}>('asset.applyAllPixelEdits');
  load(result.guide);select(index);
  if(result.warning){setError(result.warning);return false;}
  setStatus('All blur/redaction edits applied');return true;
 }
 async function output(action:OutputAction,applied=false){
  document.querySelector('details.export-menu')?.removeAttribute('open');
  if(!applied && (pendingImages || guide?.pendingAssetDeletes?.length)){setPendingOutput(action);return;}
  await flush();
  const result=await request<{cancelled?:boolean}>(action.type,{format:action.format});
  setPendingOutput(null);setStatus(result.cancelled?'Export cancelled':action.label);
 }
 async function screenshot(stepId?:string){
  await act(async()=>{await flush();await request('screenshot.capture',stepId?{stepId}:{});});
 }
 const step=guide?.steps[selected], asset=guide?.assets.find(a=>a.id===step?.assetId);
 return <><header className="app-header"><div className="brand"><img className="brand-mark" src="/capture-icon.png" width="40" height="40" alt="" aria-hidden="true"/><strong>Capture</strong><span className="brand-divider"/><span className="brand-caption">Workflow studio</span></div><div className="header-actions"><button className="quiet" aria-label={theme==='dark'?'Switch to light mode':'Switch to dark mode'} onClick={()=>setTheme(theme==='dark'?'light':'dark')}>{theme==='dark'?'☀ Light mode':'☾ Dark mode'}</button><button className="quiet" disabled={busy} onClick={()=>void act(async()=>{await flush();const preferences=await request<Preferences>('settings.get');setSettings({...preferences,screenshotHotkey:preferences.screenshotHotkey||'S'});})}>Settings</button></div></header>
 {error && <div role="alert" className="error">{error}<button onClick={()=>setError('')}>Dismiss</button></div>}
 <nav className="app-nav"><div className="nav-group"><button disabled={busy} onClick={()=>void act(list)}>Projects</button><button disabled={busy} onClick={()=>void act(async()=>{await flush();load(await request('project.new'));})}>New text guide</button><button disabled={busy} onClick={()=>void act(async()=>{await flush();const g=await request<Guide|null>('project.import');if(g)load(g);})}>Import package</button></div><div className="nav-group capture-actions"><button className="primary" disabled={busy} title={`Ctrl + Shift + ${shortcuts.screenshotHotkey}`} onClick={()=>void screenshot()}>Capture screenshot <kbd>Ctrl ⇧ {shortcuts.screenshotHotkey}</kbd></button><button className="record" disabled={busy} title={`Ctrl + Shift + ${shortcuts.hotkey}`} onClick={()=>void act(async()=>{await flush();await request('recording.toggle');})}><span aria-hidden="true">●</span> Record workflow <kbd>Ctrl ⇧ {shortcuts.hotkey}</kbd></button></div><span className="save-status" role="status" aria-live="polite">{status}</span></nav>
 {guide?.pendingAssetDeletes?.length ? <div className="error" role="alert">Privacy cleanup is pending. Export is blocked until original image files have been removed.<button disabled={busy} onClick={()=>void act(async()=>{await flush();const index=selected;load(await request<Guide>('asset.finishCleanup'));select(index);})}>Finish privacy cleanup</button></div> : null}
 <div inert={busy}>
 {settings ? <main className="settings"><span className="eyebrow">YOUR WORKSPACE</span><h1>Settings</h1><p className="muted">Make Capture work the way you do.</p><section className="settings-section"><h2>Appearance</h2><label>Theme<select value={theme} onChange={e=>setTheme(e.target.value as 'light'|'dark')}><option value="dark">Dark</option><option value="light">Light</option></select></label></section><section className="settings-section"><h2>Keyboard shortcuts</h2><label>Start / finish recording: Ctrl + Shift + <select aria-label="Recording shortcut" value={settings.hotkey} onChange={e=>setSettings({...settings,hotkey:e.target.value})}>{'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split('').map(k=><option key={k}>{k}</option>)}</select></label><label>Capture screenshot: Ctrl + Shift + <select aria-label="Screenshot shortcut" value={settings.screenshotHotkey} onChange={e=>setSettings({...settings,screenshotHotkey:e.target.value})}>{'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split('').map(k=><option key={k}>{k}</option>)}</select></label>{settings.hotkey===settings.screenshotHotkey&&<p role="alert" className="inline-error">Choose different keys for recording and screenshots.</p>}</section><section className="settings-section"><h2>Recording exclusions</h2><label>Excluded process names (one per line)<textarea value={settings.exclusions.join('\n')} onChange={e=>setSettings({...settings,exclusions:e.target.value.split('\n').filter(Boolean)})}/></label></section><div className="step-actions"><button className="primary" disabled={busy||settings.hotkey===settings.screenshotHotkey} onClick={()=>void act(async()=>{await request('settings.update',settings);setShortcuts({hotkey:settings.hotkey,screenshotHotkey:settings.screenshotHotkey});setSettings(null);setStatus('Settings saved');})}>Save settings</button><button onClick={()=>setSettings(null)}>Back</button></div></main>
 : !guide ? <main className="projects"><span className="eyebrow">WORKSPACE</span><h1>Your guides</h1><p className="muted">Capture a moment. Turn a workflow into a guide.</p>{!projects.length && <div className="empty"><div className="empty-icon" aria-hidden="true">▣</div><h2>Your next great guide starts here</h2><p>Record a workflow, take a screenshot, or start with a blank guide.</p><button className="primary" onClick={()=>void screenshot()}>Take your first screenshot</button></div>}<div className="cards">{projects.map(g=><button disabled={busy} key={g.id} className="card" onClick={()=>void act(async()=>load(await request('project.open',{id:g.id})))}><strong>{g.title}</strong><span>{g.steps.length} steps · {new Date(g.updatedAt).toLocaleString()}</span></button>)}</div></main>
 : <><section className="document-header"><div className="document-identity"><span className="eyebrow">GUIDE · {guide.steps.length} {guide.steps.length===1?'STEP':'STEPS'}</span><label>Guide title<input value={guide.title} maxLength={1000} onChange={e=>edit({...guide,title:e.target.value})}/></label></div><div className="document-actions"><button disabled={busy} onClick={()=>void act(flush)}>Save</button><div className="undo-group"><button disabled={!history.length} onClick={undo}>Undo</button><button disabled={!future.length} onClick={redo}>Redo</button></div><details className="export-menu"><summary>Export &amp; copy <span aria-hidden="true">⌄</span></summary><div className="export-options"><span className="menu-label">EXPORT GUIDE</span>{[{format:'docx',name:'Word DOCX',label:'Word export finished'},{format:'html',name:'HTML guide',label:'HTML export finished'},{format:'markdown',name:'Markdown ZIP',label:'Markdown export finished'},{format:'portable',name:'Portable package',label:'Package export finished'}].map(item=><button key={item.format} disabled={busy} onClick={()=>void act(()=>output({type:'export.request',format:item.format,label:item.label}))}>{item.name}</button>)}<span className="menu-label">COPY TO CLIPBOARD</span><button disabled={busy} onClick={()=>void act(()=>output({type:'clipboard.copy',format:'html',label:'HTML code copied'}))}>Copy HTML code</button><button disabled={busy} onClick={()=>void act(()=>output({type:'clipboard.copy',format:'word',label:'Word steps copied — paste into Word'}))}>Copy steps for Word</button></div></details></div></section>
 {(pendingImages>0 || pendingOutput) && <section className="pixel-batch" aria-label="Apply image edits"><div><strong>{pendingOutput?'Finish edits and export':`${pendingImages} ${pendingImages===1?'image has':'images have'} blur/redaction edits`}</strong><p>Apply all edits permanently across this guide. This replaces original images and clears Undo.</p></div><button className="primary" disabled={busy} onClick={()=>void act(async()=>{const action=pendingOutput;if(await applyAll() && action)await output(action,true);})}>{pendingOutput?'Apply all and continue':'Apply all blur/redaction'}</button>{pendingOutput&&<button onClick={()=>setPendingOutput(null)}>Cancel export</button>}</section>}

 <div className="workspace"><aside><div className="steps-heading"><h2>Steps</h2><span>{guide.steps.length}</span></div><button onClick={()=>{edit({...guide,steps:[...guide.steps,{id:crypto.randomUUID(),order:guide.steps.length+1,title:'New step',instruction:'',notes:'',assetId:null,annotations:[],needsReview:false}]});select(guide.steps.length);}}>+ Add text step</button><button disabled={busy || guide.steps.length>=10000} onClick={()=>void importImage(true)}>+ Add image step</button><ol>{guide.steps.map((s,i)=><li key={s.id}><button aria-current={i===selected?'step':undefined} onClick={()=>select(i)}>{i+1}. <FormattedText text={s.title} fields={s.titleFields}/>{s.needsReview?' · Review':''}</button></li>)}</ol></aside>
 <main className="editor"><div className="editor-kicker"><span className="eyebrow">STEP {selected+1} OF {guide.steps.length}</span><span className="step-state">{step?.needsReview?'Draft':'Ready'}</span></div><details className="guide-introduction"><summary>Guide introduction</summary><label>Introduction<textarea value={guide.summary} onChange={e=>edit({...guide,summary:e.target.value})}/></label></details>{step ? <><div className="step-actions"><button disabled={selected===0} onClick={()=>{edit(moveStep(guide,selected,-1));select(selected-1);}}>Move up</button><button disabled={selected===guide.steps.length-1} onClick={()=>{edit(moveStep(guide,selected,1));select(selected+1);}}>Move down</button><button onClick={()=>{const steps=[...guide.steps];steps.splice(selected+1,0,{...structuredClone(step),id:crypto.randomUUID()});edit({...guide,steps});select(selected+1);}}>Duplicate</button><button onClick={()=>{edit({...guide,steps:guide.steps.filter((_,i)=>i!==selected)});select(Math.max(0,selected-1));}}>Delete step</button></div><label className="step-title-field">Step title<input ref={titleInput} value={step.title} onSelect={()=>setTitleSelection((titleInput.current?.selectionEnd||0)>(titleInput.current?.selectionStart||0))} onChange={e=>stepEdit({title:e.target.value})}/></label>
 <div className="title-format"><span>Use sentence case; bold field/control names.</span><button disabled={!titleSelection} onMouseDown={e=>e.preventDefault()} onClick={()=>{const input=titleInput.current;if(!input)return;const text=step.title.slice(input.selectionStart||0,input.selectionEnd||0).trim();if(text&&text.length<=120)stepEdit({titleFields:[...new Set([...(step.titleFields||[]),text])].slice(-32)});}}>Bold field</button><button disabled={!step.titleFields?.length} onClick={()=>stepEdit({titleFields:[]})}>Clear bold</button></div>
 {!!step.titleFields?.length && <div className="title-preview" aria-label="Formatted step title"><FormattedText text={step.title} fields={step.titleFields}/></div>}
 <div className="step-actions image-source-actions"><button className="primary" disabled={busy} onClick={()=>void screenshot(step.id)}>Capture screenshot for step</button><button disabled={busy} onClick={()=>void importImage(false)}>{asset?'Replace image':'Add image'}</button>{asset && <button onClick={()=>stepEdit({assetId:null,annotations:[],crop:null,cuts:[],needsReview:true})}>Remove image</button>}</div>
 {asset && <ImageEditor key={`${step.id}:${asset.id}`} projectId={guide.id} asset={asset} step={step} onChange={change=>stepEdit({...change,needsReview:true})} onUndo={undo} onRedo={redo} canUndo={history.length>0} canRedo={future.length>0} onApplyPixels={()=>void act(async()=>{
  await flush();const result=await request<{guide:Guide;warning?:string}|null>('asset.applyPixelEdits',{assetId:asset.id});
  if(result){const index=selected;load(result.guide);select(index);if(result.warning)setError(result.warning);}
 })}/>}

 <label>Instruction<textarea placeholder="Optional details — leave blank unless they add something beyond the title." value={step.instruction} onChange={e=>stepEdit({instruction:e.target.value})}/></label><label className="check"><input type="checkbox" checked={!step.needsReview} onChange={e=>stepEdit({needsReview:!e.target.checked})}/>I reviewed this step and screenshot</label>
 <label>Notes<textarea value={step.notes} onChange={e=>stepEdit({notes:e.target.value})}/></label></> : <p>Add a step to start writing your guide.</p>}</main></div></>}
 </div></>;
}
createRoot(document.getElementById('root')!).render(<App/>);
