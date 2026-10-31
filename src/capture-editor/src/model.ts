export type AnnotationKind = 'highlight' | 'rectangle' | 'ellipse' | 'line' | 'arrow' | 'text' | 'callout' | 'blur' | 'redact';
export interface Region { x:number; y:number; width:number; height:number }
export interface Annotation extends Region { id: string; kind: AnnotationKind; stroke?:string; fill?:string; textColor?:string; strokeWidth?:number; fontSize?:number; shadow?:boolean; text?:string; flipX?:boolean; flipY?:boolean; blurRadius?:number }
export interface Step { id: string; order: number; title: string; instruction: string; notes: string; assetId: string | null; annotations: Annotation[]; needsReview: boolean; crop?:Region|null }
export interface Asset { id: string; relativePath: string; mimeType: string; width: number; height: number; sha256: string }
export interface Guide { schemaVersion: string; id: string; title: string; summary: string; createdAt: string; updatedAt: string; steps: Step[]; assets: Asset[]; pendingAssetDeletes?:string[] }
export function moveStep(guide: Guide, index: number, delta: number): Guide {
 const to = index + delta; if (to < 0 || to >= guide.steps.length) return guide;
 const steps = [...guide.steps]; [steps[index], steps[to]] = [steps[to], steps[index]];
 return { ...guide, steps: steps.map((s, i) => ({ ...s, order: i + 1 })) };
}
