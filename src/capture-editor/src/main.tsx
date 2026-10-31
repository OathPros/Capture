import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { request, onProjectChanged, onFlush } from './bridge';
import { Guide, Step, moveStep } from './model';
import './style.css';
function App() {
 const [guide, setGuide] = useState<Guide | null>(null), [projects, setProjects] = useState<Guide[]>([]), [selected, select] = useState(0);
 const [status, setStatus] = useState('Connecting…'), [error, setError] = useState(''), [busy, setBusy] = useState(false);
 const [history, setHistory] = useState<Guide[]>([]), [future, setFuture] = useState<Guide[]>([]);
 const [settings, setSettings] = useState<{hotkey: string; exclusions: string[]} | null>(null);
 const latest = useRef(guide), saved = useRef(''), saveQueue = useRef(Promise.resolve());
 latest.current = guide;
 function load(g: Guide | null) { saved.current = JSON.stringify(g); setGuide(g); select(0); setHistory([]); setFuture([]); setStatus('Saved locally'); }
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
 onFlush(flush);
 async function list() { await flush(); const p = await request<Guide[]>('project.list'); setProjects(p); load(null); }
 useEffect(() => {
  void act(async () => { const ready=await request<{guide: Guide|null}>('editor.ready'); if(ready.guide) load(ready.guide); else setProjects(await request<Guide[]>('project.list')); setStatus('Ready'); });
  return onProjectChanged(g => { load(g as Guide); });
 }, []);
 useEffect(() => {
  if(!guide || saved.current===JSON.stringify(guide)) return;
  setStatus('Unsaved changes');
  const timer=setTimeout(() => { void save(guide).catch(e=>{setStatus('Save failed — retry Save');setError(e.message);}); },500);
  return () => clearTimeout(timer);
 },[guide]);
 useEffect(() => {
  const handler=(e: KeyboardEvent) => { if((e.ctrlKey||e.metaKey) && e.key==='s') { e.preventDefault(); void act(flush); } };
  window.addEventListener('keydown',handler); return () => window.removeEventListener('keydown',handler);
 });
 function edit(g: Guide) { if(!guide) return; setHistory(h=>[...h.slice(-49),guide]); setFuture([]); setGuide(g); }
 function stepEdit(change: Partial<Step>) { if(guide) edit({...guide,steps:guide.steps.map((s,i)=>i===selected?{...s,...change}:s)}); }
 function undo() { if(!guide || !history.length) return; setFuture(f=>[guide,...f]); setGuide(history.at(-1)!); setHistory(h=>h.slice(0,-1)); }
 function redo() { if(!guide || !future.length) return; setHistory(h=>[...h,guide]); setGuide(future[0]); setFuture(f=>f.slice(1)); }
 const step=guide?.steps[selected], asset=guide?.assets.find(a=>a.id===step?.assetId);
 return <><header><strong>CAPTURE <small>PREVIEW</small></strong><span>Local guides. Your work stays on this computer.</span><button disabled={busy} onClick={()=>void act(async()=>{await flush();setSettings(await request('settings.get'));})}>Settings</button></header>
 <div className="notice">Early Windows preview · Review every screenshot. Sensitive information may be visible. DOCX, PDF, crop and redaction are not available in this version.</div>
 {error && <div role="alert" className="error">{error}<button onClick={()=>setError('')}>Dismiss</button></div>}
 <nav><button disabled={busy} onClick={()=>void act(list)}>Projects</button><button disabled={busy} onClick={()=>void act(async()=>{await flush();load(await request('project.new'));})}>New text guide</button><button disabled={busy} onClick={()=>void act(async()=>{await flush();const g=await request<Guide|null>('project.import');if(g)load(g);})}>Import package</button><button className="record" disabled={busy} onClick={()=>void act(async()=>{await flush();await request('recording.toggle');})}>● Record workflow</button><span role="status" aria-live="polite">{status}</span></nav>
 <div inert={busy}>
 {settings ? <main className="settings"><h1>Settings</h1><label>Start / finish shortcut: Ctrl + Shift + <select value={settings.hotkey} onChange={e=>setSettings({...settings,hotkey:e.target.value})}>{'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split('').map(k=><option key={k}>{k}</option>)}</select></label><label>Excluded process names (one per line, case-insensitive partial matches)<textarea value={settings.exclusions.join('\n')} onChange={e=>setSettings({...settings,exclusions:e.target.value.split('\n')})}/></label><p>Pause before opening sensitive material. Private browser windows are not automatically detected. Do not record credentials or institutional personal records.</p><button disabled={busy} onClick={()=>void act(async()=>{await request('settings.update',settings);setSettings(null);})}>Save settings</button><button onClick={()=>setSettings(null)}>Back</button></main>
 : !guide ? <main className="projects"><h1>Your guides</h1><p>Record a workflow across your Windows applications, then edit the captured steps here.</p>{!projects.length && <p className="empty">No guides yet. Choose Record workflow to start, or New text guide to write instructions.</p>}<div className="cards">{projects.map(g=><button disabled={busy} key={g.id} className="card" onClick={()=>void act(async()=>load(await request('project.open',{id:g.id})))}><strong>{g.title}</strong><span>{g.steps.length} steps · {new Date(g.updatedAt).toLocaleString()}</span></button>)}</div></main>
 : <><section className="document-header"><label>Guide title<input value={guide.title} maxLength={1000} onChange={e=>edit({...guide,title:e.target.value})}/></label><div><button disabled={busy} onClick={()=>void act(flush)}>Save</button><button disabled={!history.length} onClick={undo}>Undo</button><button disabled={!future.length} onClick={redo}>Redo</button><button disabled={busy} onClick={()=>void act(async()=>{await flush();const r=await request<{cancelled?:boolean}>('export.request',{format:'markdown'});setStatus(r.cancelled?'Export cancelled':'Markdown export finished');})}>Markdown ZIP</button><button disabled={busy} onClick={()=>void act(async()=>{await flush();const r=await request<{cancelled?:boolean}>('export.request',{format:'portable'});setStatus(r.cancelled?'Export cancelled':'Package export finished');})}>Portable package</button></div></section>
 <div className="workspace"><aside><h2>Steps</h2><button onClick={()=>{edit({...guide,steps:[...guide.steps,{id:crypto.randomUUID(),order:guide.steps.length+1,title:'New step',instruction:'',notes:'',assetId:null,annotations:[],needsReview:false}]});select(guide.steps.length);}}>+ Add text step</button><ol>{guide.steps.map((s,i)=><li key={s.id}><button aria-current={i===selected?'step':undefined} onClick={()=>select(i)}>{i+1}. {s.title}{s.needsReview?' · Review':''}</button></li>)}</ol></aside>
 <main className="editor"><label>Introduction<textarea value={guide.summary} onChange={e=>edit({...guide,summary:e.target.value})}/></label>{step ? <><div className="step-actions"><button disabled={selected===0} onClick={()=>{edit(moveStep(guide,selected,-1));select(selected-1);}}>Move up</button><button disabled={selected===guide.steps.length-1} onClick={()=>{edit(moveStep(guide,selected,1));select(selected+1);}}>Move down</button><button onClick={()=>{const steps=[...guide.steps];steps.splice(selected+1,0,{...structuredClone(step),id:crypto.randomUUID()});edit({...guide,steps});select(selected+1);}}>Duplicate</button><button onClick={()=>{edit({...guide,steps:guide.steps.filter((_,i)=>i!==selected)});select(Math.max(0,selected-1));}}>Delete step</button></div><label>Step title<input value={step.title} onChange={e=>stepEdit({title:e.target.value})}/></label><label>Instruction<textarea value={step.instruction} onChange={e=>stepEdit({instruction:e.target.value})}/></label><label className="check"><input type="checkbox" checked={!step.needsReview} onChange={e=>stepEdit({needsReview:!e.target.checked})}/>I reviewed this step and screenshot</label>
 {asset && <><div className="screenshot"><img src={`https://project.capture.local/${asset.relativePath}`} alt={`Screenshot for step ${selected+1}`}/>{step.annotations.map(a=><div key={a.id} className="highlight" style={{left:`${a.x*100}%`,top:`${a.y*100}%`,width:`${a.width*100}%`,height:`${a.height*100}%`}}/>)}</div>{step.annotations.map((a,index)=><fieldset key={a.id}><legend>Highlight position and size (%)</legend>{(['x','y','width','height'] as const).map(key=><label key={key}>{key}<input type="number" min="0" max="100" step="0.1" value={Math.round(a[key]*1000)/10} onChange={e=>{const value=Number(e.target.value)/100; if(!Number.isFinite(value))return;const next={...a,[key]:value};next.width=Math.max(.001,Math.min(next.width,1));next.height=Math.max(.001,Math.min(next.height,1));next.x=Math.max(0,Math.min(next.x,1-next.width));next.y=Math.max(0,Math.min(next.y,1-next.height));stepEdit({annotations:step.annotations.map((old,i)=>i===index?next:old)});}}/></label>)}</fieldset>)}</>}
 <label>Notes<textarea value={step.notes} onChange={e=>stepEdit({notes:e.target.value})}/></label></> : <p>Add a step to start writing your guide.</p>}</main></div></>}
 </div></>;
}
createRoot(document.getElementById('root')!).render(<App/>);
