export type ViewZoom = 'fit' | 'width' | number;
export function parseZoom(value: unknown): ViewZoom {
 if(value==='fit'||value==='width')return value;
 return typeof value==='number'&&Number.isFinite(value)&&value>=.1&&value<=4?value:'fit';
}
export function viewScale(zoom:ViewZoom,imageWidth:number,imageHeight:number,viewportWidth:number,viewportHeight:number):number {
 const width=Math.max(1,viewportWidth-32),height=Math.max(1,viewportHeight-32);
 if(typeof zoom==='number')return zoom;
 if(zoom==='width')return width/Math.max(1,imageWidth);
 return Math.min(1,width/Math.max(1,imageWidth),height/Math.max(1,imageHeight));
}
