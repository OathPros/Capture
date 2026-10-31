import {ImageCut,Region} from './model';
export interface Segment {source:number;length:number;output:number}
export interface Tile {sourceX:number;sourceY:number;width:number;height:number;x:number;y:number}
function segments(size:number,start:number,end:number,cuts:ImageCut[]):Segment[]{
 const from=Math.max(0,Math.min(size-1,Math.round(start*size))),to=Math.max(from+1,Math.min(size,Math.round(end*size)));
 const intervals=cuts.map(c=>({start:Math.max(from,Math.min(to,Math.round(c.start*size))),end:Math.max(from,Math.min(to,Math.round(c.end*size)))})).filter(c=>c.end>c.start).sort((a,b)=>a.start-b.start);
 const result:Segment[]=[];let position=from,output=0;
 for(const cut of intervals){if(cut.start>position){result.push({source:position,length:cut.start-position,output});output+=cut.start-position;}position=Math.max(position,cut.end);}
 if(position<to)result.push({source:position,length:to-position,output});return result;
}
export function imageLayout(width:number,height:number,crop?:Region|null,cuts:ImageCut[]=[]){
 const c=crop||{x:0,y:0,width:1,height:1};
 const columns=segments(width,c.x,c.x+c.width,cuts.filter(c=>c.axis==='vertical')),rows=segments(height,c.y,c.y+c.height,cuts.filter(c=>c.axis==='horizontal'));
 const tiles:Tile[]=rows.flatMap(y=>columns.map(x=>({sourceX:x.source,sourceY:y.source,width:x.length,height:y.length,x:x.output,y:y.output})));
 const left=Math.max(0,Math.min(width-1,Math.round(c.x*width))),top=Math.max(0,Math.min(height-1,Math.round(c.y*height)));
 return{columns,rows,tiles,left,top,right:Math.max(left+1,Math.min(width,Math.round((c.x+c.width)*width))),bottom:Math.max(top+1,Math.min(height,Math.round((c.y+c.height)*height))),width:columns.reduce((n,s)=>n+s.length,0),height:rows.reduce((n,s)=>n+s.length,0)};
}
export function toOriginal(value:number,segments:Segment[]):number {
 for(const s of segments)if(value<=s.output+s.length)return s.source+Math.max(0,value-s.output);
 const last=segments.at(-1);return last?last.source+last.length:0;
}
function toOutput(value:number,segments:Segment[],from:number,to:number):number {
 if(value<from)return value-from;if(value>to)return segments.reduce((n,s)=>n+s.length,0)+value-to;
 let output=0;for(const s of segments){if(value<=s.source)return output;if(value<s.source+s.length)return output+value-s.source;output+=s.length;}return output;
}
export function projectRegion(region:Region,width:number,height:number,layout:ReturnType<typeof imageLayout>):Region {
 const x=toOutput(region.x*width,layout.columns,layout.left,layout.right),y=toOutput(region.y*height,layout.rows,layout.top,layout.bottom);
 return{x,y,width:toOutput((region.x+region.width)*width,layout.columns,layout.left,layout.right)-x,height:toOutput((region.y+region.height)*height,layout.rows,layout.top,layout.bottom)-y};
}
