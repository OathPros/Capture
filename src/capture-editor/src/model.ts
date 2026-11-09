export type AnnotationKind = 'highlight' | 'rectangle' | 'ellipse' | 'line' | 'arrow' | 'text' | 'callout' | 'blur' | 'redact';
export interface ImageCut {axis:'horizontal'|'vertical';start:number;end:number}
export interface Region { x:number; y:number; width:number; height:number }
export interface Annotation extends Region { id: string; kind: AnnotationKind; stroke?:string; fill?:string; textColor?:string; strokeWidth?:number; fontSize?:number; shadow?:boolean; text?:string; flipX?:boolean; flipY?:boolean; blurRadius?:number }
export interface Step { id: string; order: number; title: string; instruction: string; notes: string; assetId: string | null; annotations: Annotation[]; needsReview: boolean; crop?:Region|null; cuts?:ImageCut[];titleFields?:string[] }
export interface Asset { id: string; relativePath: string; mimeType: string; width: number; height: number; sha256: string }
export interface Guide { schemaVersion: string; id: string; title: string; summary: string; createdAt: string; updatedAt: string; steps: Step[]; assets: Asset[]; pendingAssetDeletes?:string[] }
export function moveStepTo(guide: Guide, id:string, to:number):Guide {
 const from=guide.steps.findIndex(s=>s.id===id);
 if(from<0||!Number.isInteger(to)||to<0||to>=guide.steps.length||to===from)return guide;
 const steps=[...guide.steps];const [step]=steps.splice(from,1);steps.splice(to,0,step);
 return {...guide,steps:steps.map((s,i)=>({...s,order:i+1}))};
}
export function moveStep(guide:Guide,index:number,delta:number):Guide {
 return guide.steps[index]?moveStepTo(guide,guide.steps[index].id,index+delta):guide;
}
