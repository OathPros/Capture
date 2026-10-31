import React,{useEffect,useId,useRef,useState} from 'react';
import {Annotation,AnnotationKind,Asset,Region,Step,ImageCut} from './model';
import {request} from './bridge';
import {Point,clamp,moveAnnotation,newAnnotation,regionBetween,resizeAnnotation} from './imageGeometry';
import {imageLayout,projectRegion,toOriginal} from './imageLayout';

type Tool='select'|'crop'|'cut'|AnnotationKind;
const tools:{id:Tool;name:string;icon:string}[]=[
 {id:'select',name:'Select',icon:'↖'},{id:'crop',name:'Crop',icon:'⌗'},{id:'cut',name:'Cut strip',icon:'✂'},{id:'blur',name:'Blur',icon:'▦'},
 {id:'redact',name:'Redact',icon:'■'},{id:'text',name:'Text',icon:'T'},{id:'line',name:'Line',icon:'╱'},
 {id:'arrow',name:'Arrow',icon:'↗'},{id:'callout',name:'Callout',icon:'▱'},{id:'rectangle',name:'Rectangle',icon:'□'},
 {id:'ellipse',name:'Ellipse',icon:'○'},{id:'highlight',name:'Highlight',icon:'▣'}];
const defaults:Partial<Annotation>={stroke:'#e31837',fill:'transparent',textColor:'#202833',strokeWidth:3,fontSize:28,shadow:false,blurRadius:12};
interface Props {projectId:string;asset:Asset;step:Step;onChange(change:Partial<Step>):void;onApplyPixels():void;onUndo():void;onRedo():void;canUndo:boolean;canRedo:boolean}
interface Drag {type:'draw'|'move'|'resize';start:Point;base?:Annotation;handle?:string;value?:Annotation|Region;kind:Tool}

function textLines(text:string,width:number,fontSize:number) {
 const limit=Math.max(1,Math.floor((width-12)/(fontSize*.55)));const lines:string[]=[];
 for(const paragraph of text.split('\n')){
  let line='';for(const word of paragraph.split(' ')){if(line.length&&line.length+word.length+1>limit){lines.push(line);line=word;}else line+=(line?' ':'')+word;}lines.push(line);
 }
 return lines;
}
function Shape({a,width,height,source,id,onPointerDown}:{a:Annotation;width:number;height:number;source:string;id:string;onPointerDown?:(e:React.PointerEvent<SVGGElement>)=>void}) {
 const x=a.x*width,y=a.y*height,w=a.width*width,h=a.height*height;
 const stroke=a.stroke||'#e31837',fill=a.fill||'transparent',fontSize=a.fontSize||24,weight=a.strokeWidth||3;
 const filter=a.shadow?`url(#${id}-shadow)`:undefined;
 const start={x:a.flipX?x+w:x,y:a.flipY?y+h:y},end={x:a.flipX?x:x+w,y:a.flipY?y:y+h};
 const common={stroke,fill,strokeWidth:weight};
 return <g data-annotation-id={a.id} data-kind={a.kind} onPointerDown={onPointerDown} filter={filter}>
  <defs><filter id={`${id}-shadow`} x="-40%" y="-40%" width="180%" height="180%"><feDropShadow dx="3" dy="4" stdDeviation="2" floodOpacity=".28"/></filter>
  <marker id={`${id}-arrow`} markerWidth={Math.max(12,weight*4)} markerHeight={Math.max(12,weight*4)} refX="10" refY="5" orient="auto" markerUnits="userSpaceOnUse"><path d="M 0 0 L 10 5 L 0 10 Z" fill={stroke}/></marker>
  <clipPath id={`${id}-clip`}><rect x={x} y={y} width={w} height={h}/></clipPath>
  <filter id={`${id}-blur`} filterUnits="userSpaceOnUse" x="0" y="0" width={width} height={height}><feGaussianBlur stdDeviation={(a.blurRadius||12)/2}/></filter></defs>
  {a.kind==='blur'?<g clipPath={`url(#${id}-clip)`}><image href={source} width={width} height={height} filter={`url(#${id}-blur)`}/></g>:
   a.kind==='redact'?<rect x={x} y={y} width={w} height={h} fill={fill==='transparent'?'#202833':fill}/>:
   a.kind==='ellipse'?<ellipse cx={x+w/2} cy={y+h/2} rx={w/2} ry={h/2} {...common}/>:
   a.kind==='line'||a.kind==='arrow'?<line x1={start.x} y1={start.y} x2={end.x} y2={end.y} stroke={stroke} strokeWidth={weight} strokeLinecap="round" markerEnd={a.kind==='arrow'?`url(#${id}-arrow)`:undefined}/>:
   a.kind==='callout'?<path d={`M${x},${y}h${w}v${h*.72}h${-w*.6}l${-w*.2},${h*.28}l${w*.04},${-h*.28}H${x}Z`} {...common}/>:
   <rect x={x} y={y} width={w} height={h} {...common} stroke={a.kind==='text'?'none':stroke}/>}
  {(a.kind==='text'||a.kind==='callout')&&<svg x={x+6} y={y+3} width={Math.max(1,w-12)} height={Math.max(1,h*(a.kind==='callout'?.72:1)-6)} overflow="hidden"><text aria-label={a.text||''} fill={a.textColor||'#202833'} fontSize={fontSize} fontFamily="Arial,sans-serif">{textLines(a.text||'',w,fontSize).map((line,i)=><tspan key={i} x="0" y={fontSize*(i*1.2+1)}>{line}</tspan>)}</text></svg>}
  {/* A transparent hit area makes outlines and text easy to select without changing the export. */}
  <rect x={x} y={y} width={w} height={h} fill="transparent" stroke="none"/>
 </g>;
}

export function ImageEditor({projectId,asset,step,onChange,onApplyPixels,onUndo,onRedo,canUndo,canRedo}:Props) {
 const [cutAxis,setCutAxis]=useState<'horizontal'|'vertical'>('horizontal'),[cutCandidate,setCutCandidate]=useState<Region|null>(null);
 const [tool,setTool]=useState<Tool>('select'),[selected,setSelected]=useState<string|null>(null),[style,setStyle]=useState(defaults);
 const [source,setSource]=useState<string|null>(null),[error,setError]=useState(''),[retry,setRetry]=useState(0),[zoom,setZoom]=useState(0);
 const [preview,setPreview]=useState<Annotation|Region|null>(null),[cropCandidate,setCropCandidate]=useState<Region|null>(null),[displayWidth,setDisplayWidth]=useState(800);
 const section=useRef<HTMLElement>(null);
 const svg=useRef<SVGSVGElement>(null),viewport=useRef<HTMLDivElement>(null),drag=useRef<Drag|null>(null),clipboard=useRef<Annotation|null>(null);
 const uid=useId().replace(/[^a-zA-Z0-9]/g,'');
 const crop=step.crop||{x:0,y:0,width:1,height:1};const layout=imageLayout(asset.width,asset.height,step.crop,step.cuts);const view={x:0,y:0,width:layout.width,height:layout.height};
 const unit=view.width/Math.max(1,zoom?view.width*zoom:displayWidth);
 const chosen=step.annotations.find(a=>a.id===selected);
 const properties={...defaults,...style,...chosen};
 const pendingPixels=step.annotations.filter(a=>a.kind==='blur'||a.kind==='redact').length;
 useEffect(()=>{
  let active=true;setSource(null);setError('');
  void request<{url:string;width:number;height:number}>('asset.read',{projectId,assetId:asset.id}).then(result=>{
   if(!result.url.startsWith('data:image/png;base64,'))throw new Error('The host returned an invalid screenshot.');
   const probe=new Image();probe.onload=()=>{if(active){if(probe.naturalWidth!==asset.width||probe.naturalHeight!==asset.height)setError('The screenshot dimensions do not match the project.');else setSource(result.url);}};
   probe.onerror=()=>{if(active)setError('This screenshot could not be decoded. Restore the original PNG from a project backup.');};probe.src=result.url;
  }).catch(e=>{if(active)setError(e.message);});
  return()=>{active=false;};
 },[projectId,asset.id,asset.sha256,retry]);
 useEffect(()=>{setSelected(null);setPreview(null);setCropCandidate(null);setCutCandidate(null);drag.current=null;setTool('select');},[step.id,asset.id]);
 useEffect(()=>{
  const observer=new ResizeObserver(()=>{if(viewport.current)setDisplayWidth(Math.max(1,viewport.current.clientWidth-24));});
  if(viewport.current)observer.observe(viewport.current);return()=>observer.disconnect();
 },[source]);
 function point(e:React.PointerEvent):Point {
  const matrix=svg.current?.getScreenCTM();if(!matrix)return{x:0,y:0};
  const p=new DOMPoint(e.clientX,e.clientY).matrixTransform(matrix.inverse());return{x:clamp(toOriginal(p.x,layout.columns)/asset.width),y:clamp(toOriginal(p.y,layout.rows)/asset.height)};
 }
 function capture(e:React.PointerEvent) {svg.current?.setPointerCapture(e.pointerId);svg.current?.focus();}
 function start(e:React.PointerEvent<SVGSVGElement>) {
  if(e.button!==0)return;
  if(tool==='select'){setSelected(null);return;}
  e.preventDefault();capture(e);setCropCandidate(null);setCutCandidate(null);const p=point(e);
  const value=tool==='crop'||tool==='cut'?regionBetween(p,p):newAnnotation(tool,p,p,style);
  drag.current={type:'draw',start:p,kind:tool,value};setPreview(value);
 }
 function selectShape(e:React.PointerEvent<SVGGElement>,a:Annotation) {
  if(tool!=='select'||e.button!==0)return;
  e.stopPropagation();e.preventDefault();capture(e);setSelected(a.id);
  drag.current={type:'move',start:point(e),base:a,kind:'select',value:a};
 }
 function resize(e:React.PointerEvent,handle:string) {
  if(!chosen)return;e.stopPropagation();e.preventDefault();capture(e);
  drag.current={type:'resize',start:point(e),base:chosen,handle,kind:'select',value:chosen};
 }
 function move(e:React.PointerEvent<SVGSVGElement>) {
  const active=drag.current;if(!active)return;const p=point(e);let value:Annotation|Region;
  if(active.type==='draw'&&active.kind==='cut'){const r=regionBetween(active.start,p);value=cutAxis==='horizontal'?{x:crop.x,y:r.y,width:crop.width,height:r.height}:{x:r.x,y:crop.y,width:r.width,height:crop.height};}
  else if(active.type==='draw')value=active.kind==='crop'?regionBetween(active.start,p):{...active.value,...regionBetween(active.start,p),flipX:p.x<active.start.x,flipY:p.y<active.start.y} as Annotation;
  else if(active.type==='move')value=moveAnnotation(active.base!,p.x-active.start.x,p.y-active.start.y);
  else value=resizeAnnotation(active.base!,active.handle!,p);
  active.value=value;setPreview(value);
 }
 function finish(e:React.PointerEvent<SVGSVGElement>) {
  const active=drag.current;if(!active)return;
  let value=active.value!;drag.current=null;setPreview(null);if(svg.current?.hasPointerCapture(e.pointerId))svg.current.releasePointerCapture(e.pointerId);
  if(active.kind==='cut'){if(value.width*asset.width>=2&&value.height*asset.height>=2)setCutCandidate(value);return;}
  if(active.kind==='crop'){if(value.width*asset.width>=2&&value.height*asset.height>=2)setCropCandidate(value);return;}
  if(active.type==='draw'){
   if(value.width*asset.width<4&&value.height*asset.height<4)value={...value,x:Math.min(value.x,.75),y:Math.min(value.y,.88),width:.25,height:.12};
   onChange({annotations:[...step.annotations,value as Annotation]});setSelected((value as Annotation).id);setTool('select');
  }else if(JSON.stringify(active.base)!==JSON.stringify(value))onChange({annotations:step.annotations.map(a=>a.id===(value as Annotation).id?value as Annotation:a)});
 }
 function patch(change:Partial<Annotation>) {setStyle(s=>({...s,...change}));if(chosen)onChange({annotations:step.annotations.map(a=>a.id===selected?{...a,...change}:a)});}
 function remove(){if(chosen){onChange({annotations:step.annotations.filter(a=>a.id!==selected)});setSelected(null);}}
 function copy(cut=false){if(!chosen)return;clipboard.current=structuredClone(chosen);if(cut)remove();}
 function paste(){if(!clipboard.current)return;const a=moveAnnotation({...clipboard.current,id:crypto.randomUUID()},.02,.02);onChange({annotations:[...step.annotations,a]});setSelected(a.id);setTool('select');}
 function keyboard(e:React.KeyboardEvent) {
  if(e.target instanceof HTMLInputElement||e.target instanceof HTMLTextAreaElement||e.target instanceof HTMLSelectElement)return;
  if(e.key==='Escape'){drag.current=null;setPreview(null);setCropCandidate(null);setCutCandidate(null);setSelected(null);setTool('select');}
  if((e.key==='Delete'||e.key==='Backspace')&&chosen){e.preventDefault();remove();}
  if(e.ctrlKey||e.metaKey){if(e.key==='c'){e.preventDefault();copy();}if(e.key==='x'){e.preventDefault();copy(true);}if(e.key==='v'){e.preventDefault();paste();}}
 }
 useEffect(()=>{if(source)section.current?.scrollIntoView({block:'start'});},[source]);
 const drawn=step.annotations.map(a=>(preview&&'id'in preview&&preview.id===a.id)?preview:a);
 const selection=(preview&&'id'in preview&&preview.id===selected?preview:chosen);
 const originalBox=cropCandidate||cutCandidate||(preview&&(drag.current?.kind==='crop'||drag.current?.kind==='cut')?preview:null);
 const cropBox=originalBox?projectRegion(originalBox,asset.width,asset.height,layout):null;
 const projectedSelection=selection?projectRegion(selection,asset.width,asset.height,layout):null;
 const selectionBox=projectedSelection&&projectedSelection.width>0&&projectedSelection.height>0?projectedSelection:null;
 const candidateCut:ImageCut|null=cutCandidate?{axis:cutAxis,start:cutAxis==='horizontal'?cutCandidate.y:cutCandidate.x,end:cutAxis==='horizontal'?cutCandidate.y+cutCandidate.height:cutCandidate.x+cutCandidate.width}:null;
 const planned=candidateCut?imageLayout(asset.width,asset.height,step.crop,[...(step.cuts||[]),candidateCut]):null;
 function projected(a:Annotation):Annotation|null {const b=projectRegion(a,asset.width,asset.height,layout);return b.width>0&&b.height>0?{...a,x:b.x/view.width,y:b.y/view.height,width:b.width/view.width,height:b.height/view.height}:null;}

 return <section ref={section} className="image-editor" aria-label="Screenshot tools" onKeyDown={keyboard}>
  <div className="image-editor-heading"><strong>Screenshot editor</strong><span>{Math.round(view.width)} × {Math.round(view.height)} px</span><label>Zoom<select aria-label="Screenshot zoom" value={zoom} onChange={e=>setZoom(Number(e.target.value))}><option value={0}>Fit</option>{[.5,.75,1,1.5,2,3].map(z=><option key={z} value={z}>{z*100}%</option>)}</select></label></div>
  <div role="toolbar" aria-label="Image tools" className="image-tools">{tools.map(t=><button type="button" key={t.id} aria-pressed={tool===t.id} title={t.name==='Blur'?'Blur softens pixels; use Redact to remove sensitive information.':`${t.name}: drag on the screenshot`} onClick={()=>{setTool(t.id);setCropCandidate(null);setCutCandidate(null);if(t.id!=='select')setSelected(null);}}><span aria-hidden="true">{t.icon}</span>{t.name}</button>)}</div>
  {tool==='cut'&&<label className="cut-direction">Cut direction<select aria-label="Cut direction" value={cutAxis} onChange={e=>{setCutAxis(e.target.value as 'horizontal'|'vertical');setCutCandidate(null);}}><option value="horizontal">Horizontal strip</option><option value="vertical">Vertical strip</option></select></label>}
  <div className="tool-properties">
   <label>Outline<input aria-label="Outline colour" type="color" value={properties.stroke} onChange={e=>patch({stroke:e.target.value})}/></label>
   <label>Width<input aria-label="Outline width" type="number" min="1" max="40" value={properties.strokeWidth} onChange={e=>patch({strokeWidth:clamp(Number(e.target.value)||1,1,40)})}/></label>
   <label>Fill<input aria-label="Fill colour" type="color" value={properties.fill==='transparent'?'#ffffff':properties.fill} onChange={e=>patch({fill:e.target.value})}/></label>
   <label className="inline-check"><input aria-label="No fill" type="checkbox" checked={properties.fill==='transparent'} onChange={e=>patch({fill:e.target.checked?'transparent':'#ffffff'})}/>No fill</label>
   <label>Text<input aria-label="Text colour" type="color" value={properties.textColor} onChange={e=>patch({textColor:e.target.value})}/></label>
   <label>Font size<input aria-label="Font size" type="number" min="8" max="256" value={properties.fontSize} onChange={e=>patch({fontSize:clamp(Number(e.target.value)||8,8,256)})}/></label>
   <label className="inline-check"><input aria-label="Shadow" type="checkbox" checked={properties.shadow} onChange={e=>patch({shadow:e.target.checked})}/>Shadow</label>
   {(tool==='blur'||chosen?.kind==='blur')&&<label>Blur strength<input aria-label="Blur strength" type="range" min="2" max="64" value={properties.blurRadius} onChange={e=>patch({blurRadius:Number(e.target.value)})}/></label>}
  </div>
  {(chosen?.kind==='text'||chosen?.kind==='callout')&&<label className="annotation-text">Annotation text<textarea aria-label="Annotation text" value={chosen.text||''} maxLength={4000} onChange={e=>patch({text:e.target.value})}/></label>}
  <div className="image-actions"><button disabled={!canUndo} onClick={onUndo}>Undo edit</button><button disabled={!canRedo} onClick={onRedo}>Redo edit</button><button disabled={!chosen} onClick={()=>copy()}>Copy</button><button disabled={!chosen} onClick={()=>copy(true)}>Cut selection</button><button onClick={paste}>Paste</button><button disabled={!chosen} onClick={remove}>Delete annotation</button>{step.crop&&<button onClick={()=>onChange({crop:null})}>Reset crop</button>}
   {!!step.cuts?.length&&<button onClick={()=>onChange({cuts:[]})}>Reset cuts</button>}
   {candidateCut&&<><button className="primary" disabled={!planned?.width||!planned?.height||(step.cuts?.length||0)>=32} onClick={()=>{onChange({cuts:[...(step.cuts||[]),candidateCut]});setCutCandidate(null);setSelected(null);setTool('select');}}>Apply cut</button><button onClick={()=>setCutCandidate(null)}>Cancel cut</button></>}
   {cropCandidate&&<><button className="primary" onClick={()=>{onChange({crop:cropCandidate});setCropCandidate(null);setTool('select');}}>Apply crop</button><button onClick={()=>setCropCandidate(null)}>Cancel crop</button></>}
   {pendingPixels>0&&<button className="pixel-apply" onClick={onApplyPixels}>Apply blur/redaction permanently</button>}
  </div>
  <p className="tool-hint">{tool==='select'?'Select an annotation to move it or resize its handles. Use Undo / Redo for reversible edits.':tool==='cut'?'Drag a full-width or full-height strip to remove, then Apply cut. The remaining image joins together. Cut is reversible, not redaction.':tool==='crop'?'Drag the region to keep, then choose Apply crop. Reset crop restores the full image.':tool==='blur'?'Drag to blur an area. Blur is not secure redaction; use Redact for secrets.':'Drag on the screenshot to add '+tool+'.'}{pendingPixels>0?' Apply pixel edits permanently before exporting. The original screenshot will be replaced and Undo cleared.':''}</p>
  {error?<div className="image-error" role="alert"><strong>Screenshot unavailable</strong><p>{error}</p><button onClick={()=>setRetry(r=>r+1)}>Retry screenshot</button></div>:!source?<div className="image-loading" role="status">Loading screenshot…</div>:
  <div className="image-stage" data-tool={tool} ref={viewport}><svg ref={svg} role="img" aria-label="Screenshot annotation canvas" tabIndex={0} viewBox={`${view.x} ${view.y} ${view.width} ${view.height}`} width={view.width} height={view.height} style={{width:zoom?view.width*zoom:'100%',height:'auto',cursor:tool==='select'?'default':'crosshair'}} onPointerDown={start} onPointerMove={move} onPointerUp={finish} onPointerCancel={()=>{drag.current=null;setPreview(null);}}>
   <defs><g id={`${uid}-source`}><image href={source} width={asset.width} height={asset.height} onLoad={()=>{void request('asset.previewReady',{projectId,assetId:asset.id,width:asset.width,height:asset.height}).catch(()=>{});}} onError={()=>setError('The PNG could not be decoded. Restore the screenshot from a complete project backup.')}/>
    {drawn.filter(a=>a.kind==='blur'||a.kind==='redact').map((a,i)=><Shape key={a.id} a={a} width={asset.width} height={asset.height} source={source} id={`${uid}-pixel-${i}`}/>)}
    {preview&&'id'in preview&&drag.current?.type==='draw'&&(preview.kind==='blur'||preview.kind==='redact')&&<Shape a={preview} width={asset.width} height={asset.height} source={source} id={`${uid}-pixel-draft`}/>}
   </g></defs>
   {layout.tiles.map((t,i)=><svg key={i} x={t.x} y={t.y} width={t.width} height={t.height} viewBox={`${t.sourceX} ${t.sourceY} ${t.width} ${t.height}`} overflow="hidden" pointerEvents="none"><use href={`#${uid}-source`}/></svg>)}
   {drawn.map((a,i)=>{const p=projected(a);if(!p)return null;return a.kind==='blur'||a.kind==='redact'?<rect key={a.id} x={p.x*view.width} y={p.y*view.height} width={p.width*view.width} height={p.height*view.height} fill="transparent" onPointerDown={e=>selectShape(e as unknown as React.PointerEvent<SVGGElement>,a)}/>:<Shape key={a.id} a={p} width={view.width} height={view.height} source={source} id={`${uid}-${i}`} onPointerDown={e=>selectShape(e,a)}/>;})}
   {preview&&'id'in preview&&drag.current?.type==='draw'&&preview.kind!=='blur'&&preview.kind!=='redact'&&projected(preview)&&<Shape a={projected(preview)!} width={view.width} height={view.height} source={source} id={`${uid}-draft`}/>}
   {cropBox&&<rect data-testid="crop-region" x={cropBox.x} y={cropBox.y} width={cropBox.width} height={cropBox.height} fill="#2366c222" stroke="#2366c2" strokeWidth={2*unit} strokeDasharray={`${6*unit} ${4*unit}`} pointerEvents="none"/>}
   {selectionBox&&tool==='select'&&<g data-testid="selection-handles"><rect x={selectionBox.x} y={selectionBox.y} width={selectionBox.width} height={selectionBox.height} fill="none" stroke="#2366c2" strokeWidth={1.5*unit} strokeDasharray={`${4*unit} ${3*unit}`} pointerEvents="none"/>{[['nw',0,0],['n',.5,0],['ne',1,0],['e',1,.5],['se',1,1],['s',.5,1],['sw',0,1],['w',0,.5]].map(([handle,x,y])=><rect key={handle} data-handle={handle} x={(selectionBox.x+selectionBox.width*Number(x))-5*unit} y={(selectionBox.y+selectionBox.height*Number(y))-5*unit} width={10*unit} height={10*unit} fill="white" stroke="#2366c2" strokeWidth={1.5*unit} style={{cursor:`${handle}-resize`}} onPointerDown={e=>resize(e,String(handle))}/>)}</g>}
  </svg></div>}
  {chosen&&<details className="annotation-geometry"><summary>Exact position and size (%)</summary><div>{(['x','y','width','height']as const).map(key=><label key={key}>{key}<input aria-label={`Annotation ${key}`} type="number" min="0" max="100" step=".1" value={Math.round(chosen[key]*1000)/10} onChange={e=>{let a={...chosen,[key]:Number(e.target.value)/100};a.width=clamp(a.width,.001,1);a.height=clamp(a.height,.001,1);a.x=clamp(a.x,0,1-a.width);a.y=clamp(a.y,0,1-a.height);onChange({annotations:step.annotations.map(old=>old.id===a.id?a:old)});}}/></label>)}</div></details>}
 </section>;
}
