import { Annotation, AnnotationKind, Region } from './model';
export interface Point {x:number;y:number}
export const clamp=(value:number,min=0,max=1)=>Math.max(min,Math.min(max,value));
export function regionBetween(a:Point,b:Point):Region {
 const x=clamp(Math.min(a.x,b.x),0,.9999), y=clamp(Math.min(a.y,b.y),0,.9999);
 return {x,y,width:Math.max(.0001,Math.min(1-x,Math.abs(b.x-a.x))),height:Math.max(.0001,Math.min(1-y,Math.abs(b.y-a.y)))};
}
export function moveAnnotation(a:Annotation,dx:number,dy:number):Annotation {return {...a,x:clamp(a.x+dx,0,1-a.width),y:clamp(a.y+dy,0,1-a.height)};}
export function resizeAnnotation(a:Annotation,handle:string,p:Point):Annotation {
 let left=a.x,top=a.y,right=a.x+a.width,bottom=a.y+a.height;
 if(handle.includes('w'))left=clamp(p.x,0,right-.001);
 if(handle.includes('e'))right=clamp(p.x,left+.001,1);
 if(handle.includes('n'))top=clamp(p.y,0,bottom-.001);
 if(handle.includes('s'))bottom=clamp(p.y,top+.001,1);
 return {...a,x:left,y:top,width:right-left,height:bottom-top};
}
export function newAnnotation(kind:AnnotationKind,start:Point,end:Point,style:Partial<Annotation>):Annotation {
 return {...style,...regionBetween(start,end),id:crypto.randomUUID(),kind,flipX:end.x<start.x,flipY:end.y<start.y,
  fill:kind==='redact'?'#202833':kind==='callout'&&(!style.fill||style.fill==='transparent')?'#ffffff':style.fill||'transparent',
  text:kind==='callout'?'Add a callout':kind==='text'?'Your text':''};
}
