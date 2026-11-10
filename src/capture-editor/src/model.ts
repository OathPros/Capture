export type AnnotationKind = 'highlight' | 'rectangle' | 'ellipse' | 'line' | 'arrow' | 'text' | 'callout' | 'blur' | 'redact';
export interface ImageCut {axis:'horizontal'|'vertical';start:number;end:number}
export interface Region { x:number; y:number; width:number; height:number }
export interface Annotation extends Region { id: string; kind: AnnotationKind; stroke?:string; fill?:string; textColor?:string; strokeWidth?:number; fontSize?:number; shadow?:boolean; text?:string; flipX?:boolean; flipY?:boolean; blurRadius?:number }
export interface Section { id: string; title: string; restartNumbering: boolean }
export interface Step { id: string; order: number; title: string; instruction: string; notes: string; assetId: string | null; sectionId?: string | null; annotations: Annotation[]; needsReview: boolean; crop?:Region|null; cuts?:ImageCut[];titleFields?:string[] }
export interface Asset { id: string; relativePath: string; mimeType: string; width: number; height: number; sha256: string }
export interface Guide { schemaVersion: string; id: string; title: string; summary: string; createdAt: string; updatedAt: string; steps: Step[]; assets: Asset[]; sections?: Section[]; pendingAssetDeletes?:string[] }
export function sectionsOf(guide: {sections?: Section[]}): Section[] { return guide.sections || []; }
// Display number per step: sections with restartNumbering begin again at 1 for each contiguous run; other steps share the running count.
export function stepNumbers(guide: Guide): number[] {
 const restart = new Set(sectionsOf(guide).filter(s=>s.restartNumbering).map(s=>s.id));
 const numbers: number[] = []; let shared = 0, run = 0, previous: string | null | undefined;
 for (const step of guide.steps) {
  const section = step.sectionId || null;
  if (section && restart.has(section)) { run = previous === section ? run + 1 : 1; numbers.push(run); }
  else numbers.push(++shared);
  previous = section;
 }
 return numbers;
}
export interface StepRun { section: Section | null; start: number; count: number }
export function stepRuns(guide: {steps: Step[]; sections?: Section[]}): StepRun[] {
 const runs: StepRun[] = [];
 for (let i = 0; i < guide.steps.length;) {
  const sectionId = guide.steps[i].sectionId || null; const start = i;
  while (i < guide.steps.length && (guide.steps[i].sectionId || null) === sectionId) i++;
  runs.push({ section: sectionsOf(guide).find(s=>s.id===sectionId) || null, start, count: i - start });
 }
 return runs;
}
export function moveStepTo(guide: Guide, id:string, to:number):Guide {
 const from=guide.steps.findIndex(s=>s.id===id);
 if(from<0||!Number.isInteger(to)||to<0||to>=guide.steps.length||to===from)return guide;
 const steps=[...guide.steps];const [step]=steps.splice(from,1);steps.splice(to,0,step);
 return {...guide,steps:steps.map((s,i)=>({...s,order:i+1}))};
}
export function moveStep(guide:Guide,index:number,delta:number):Guide {
 return guide.steps[index]?moveStepTo(guide,guide.steps[index].id,index+delta):guide;
}
