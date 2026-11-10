import React,{useLayoutEffect,useRef,useState} from 'react';
import {Section,Step,stepRuns} from './model';
import {FormattedText} from './FormattedText';
interface Props{
 steps:Step[];sections:Section[];numbers:number[];selectedId?:string;busy:boolean;
 onSelect(id:string):void;onDuplicate(id:string):void;onDelete(id:string):void;onMove(id:string,to:number):void;
 onAddText():void;onAddImage():void;onAddSection():void;
 onRenameSection(id:string,title:string):void;onDeleteSection(id:string):void;onToggleRestart(id:string):void;
}
export function StepSidebar({steps,sections,numbers,selectedId,busy,onSelect,onDuplicate,onDelete,onMove,onAddText,onAddImage,onAddSection,onRenameSection,onDeleteSection,onToggleRestart}:Props){
 const list=useRef<HTMLOListElement>(null),dragging=useRef<string|null>(null),restoreFocus=useRef(false);
 const [drop,setDrop]=useState<{id:string;after:boolean}|null>(null),[announcement,announce]=useState('');
 const [collapsed,setCollapsed]=useState<Set<string>>(new Set());
 useLayoutEffect(()=>{
  const row=Array.from(list.current?.querySelectorAll<HTMLElement>('[data-step-id]')||[]).find(e=>e.dataset.stepId===selectedId);
  row?.scrollIntoView({block:'nearest'});
  if(restoreFocus.current){row?.querySelector<HTMLElement>('.step-select')?.focus({preventScroll:true});restoreFocus.current=false;}
 },[selectedId,steps]);
 function keyboard(e:React.KeyboardEvent){
  if(busy)return;
  const target=e.target as HTMLElement;if(!target.matches('.step-select,.step-drag'))return;
  const id=target.closest<HTMLElement>('[data-step-id]')?.dataset.stepId,index=steps.findIndex(s=>s.id===id);if(index<0||!id)return;
  if(e.key==='Delete'){e.preventDefault();e.stopPropagation();restoreFocus.current=true;onDelete(id);announce('Step deleted. Undo restores it.');}
  if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='d'){e.preventDefault();restoreFocus.current=true;onDuplicate(id);announce('Step duplicated.');}
  if(['ArrowUp','ArrowDown','Home','End'].includes(e.key)){
   e.preventDefault();const to=Math.max(0,Math.min(steps.length-1,e.key==='Home'?0:e.key==='End'?steps.length-1:index+(e.key==='ArrowUp'?-1:1)));
   restoreFocus.current=true;
   if(e.altKey){onMove(id,to);announce(`Step moved to position ${to+1}.`);}else onSelect(steps[to].id);
  }
 }
 function toggleCollapse(id:string){setCollapsed(old=>{const next=new Set(old);if(next.has(id))next.delete(id);else next.add(id);return next;});}
 const runs=stepRuns({steps,sections});
 const rows:React.ReactNode[]=[];
 for(const run of runs){
  if(run.section){
   const section=run.section,hidden=collapsed.has(section.id);
   rows.push(<li key={`section-${section.id}-${run.start}`} className="section-heading" data-section-id={section.id}>
    <button className="section-collapse" aria-expanded={!hidden} aria-label={`${hidden?'Expand':'Collapse'} section ${section.title}`} title={hidden?'Expand section':'Collapse section'} disabled={busy} onClick={()=>toggleCollapse(section.id)}>{hidden?'▸':'▾'}</button>
    <input className="section-title" aria-label="Section title" value={section.title} maxLength={1000} disabled={busy} onChange={e=>onRenameSection(section.id,e.target.value)}/>
    <span className="section-count" title={`${run.count} ${run.count===1?'step':'steps'}`}>{run.count}</span>
    <button className={`section-restart ${section.restartNumbering?'active':''}`} aria-pressed={section.restartNumbering} aria-label={`Restart numbering for section ${section.title}`} title={section.restartNumbering?'Numbering restarts in this section — use guide numbering':'Restart step numbering in this section'} disabled={busy} onClick={()=>{onToggleRestart(section.id);announce(section.restartNumbering?'Section uses guide numbering.':'Section numbering restarts.');}}>↺</button>
    <button className="section-delete" aria-label={`Delete section ${section.title}`} title="Delete section — steps stay in the guide" disabled={busy} onClick={()=>{onDeleteSection(section.id);announce('Section removed. Steps stay in the guide.');}}>×</button>
   </li>);
   if(hidden)continue;
  }
  for(let i=run.start;i<run.start+run.count;i++){
   const s=steps[i];
   rows.push(<li key={s.id} data-step-id={s.id} className={`${s.id===selectedId?'selected':''} ${run.section?'in-section':''} ${drop?.id===s.id?(drop.after?'drop-after':'drop-before'):''}`}
    onDragOver={e=>{if(!dragging.current||busy)return;e.preventDefault();e.dataTransfer.dropEffect='move';const b=e.currentTarget.getBoundingClientRect();setDrop({id:s.id,after:e.clientY>b.top+b.height/2});const box=list.current?.getBoundingClientRect();if(box&&list.current){if(e.clientY<box.top+30)list.current.scrollTop-=12;if(e.clientY>box.bottom-30)list.current.scrollTop+=12;}}}
    onDrop={e=>{e.preventDefault();const id=dragging.current,from=steps.findIndex(x=>x.id===id);if(id&&from>=0){const box=e.currentTarget.getBoundingClientRect();const insertion=i+(e.clientY>box.top+box.height/2?1:0);onMove(id,Math.max(0,insertion-(from<insertion?1:0)));announce('Step reordered.');}dragging.current=null;setDrop(null);}}>
    <button className="step-drag" aria-label={`Reorder step ${i+1}`} title="Drag to reorder · Alt+↑/↓" disabled={busy} draggable={!busy} onDragStart={e=>{dragging.current=s.id;e.dataTransfer.effectAllowed='move';e.dataTransfer.setData('text/plain',s.id);}} onDragEnd={()=>{dragging.current=null;setDrop(null);}}>⠿</button>
    <button className="step-select" aria-current={s.id===selectedId?'step':undefined} title={s.title} disabled={busy} onClick={()=>onSelect(s.id)}><span className="step-number">{numbers[i]}.</span><span className="step-name"><FormattedText text={s.title} fields={s.titleFields}/></span>{s.needsReview&&<span className="review-dot" title="Needs review" aria-label="Needs review"/>}</button>
    <div className="step-row-actions"><button disabled={busy||steps.length>=10000} aria-label={`Duplicate step ${i+1}`} title="Duplicate step" onClick={()=>{restoreFocus.current=true;onDuplicate(s.id);}}><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M8 8h12v12H8zM4 16V4h12"/></svg></button><button disabled={busy} aria-label={`Delete step ${i+1}`} title="Delete step · Delete" onClick={()=>{restoreFocus.current=true;onDelete(s.id);}}><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 6h16M9 6V3h6v3M6 6l1 15h10l1-15M10 10v7m4-7v7"/></svg></button></div>
   </li>);
  }
 }
 return <aside className="step-sidebar" aria-label="Guide steps" onKeyDown={keyboard}>
  <div className="steps-heading"><h2>Steps</h2><span>{steps.length}</span></div>
  <div className="step-add"><button disabled={busy||steps.length>=10000} onClick={onAddText}>+ Add text step</button><button disabled={busy||steps.length>=10000} onClick={onAddImage}>+ Add image step</button></div>
  <div className="section-add"><button disabled={busy||!steps.length||sections.length>=1000} onClick={onAddSection} title="Start a titled section at the selected step">+ Add section</button></div>
  <ol ref={list}>{rows}</ol><span className="sr-only" aria-live="polite">{announcement}</span>
 </aside>;
}
