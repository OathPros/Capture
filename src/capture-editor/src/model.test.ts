import { describe, expect, it } from 'vitest';
import { moveStep, moveStepTo, Guide } from './model';
describe('step order',()=>{
 const guide: Guide={id:'guide',schemaVersion:'1.0.0',title:'Synthetic fixture',summary:'',createdAt:'',updatedAt:'',assets:[],steps:['first','second','third'].map((id,i)=>({id,order:i+1,title:id,instruction:'',notes:'',assetId:null,annotations:[],needsReview:true}))};
 it('moves without mutating the original or changing identities',()=>{const moved=moveStep(guide,1,-1);expect(moved.steps.map(s=>s.id)).toEqual(['second','first','third']);expect(moved.steps.map(s=>s.order)).toEqual([1,2,3]);expect(guide.steps[0].id).toBe('first');});
 it('moves any step to an exact destination without changing its identity',()=>{const moved=moveStepTo(guide,'first',2);expect(moved.steps.map(s=>s.id)).toEqual(['second','third','first']);expect(moved.steps.map(s=>s.order)).toEqual([1,2,3]);expect(moveStepTo(guide,'missing',0)).toBe(guide);expect(moveStepTo(guide,'first',-1)).toBe(guide);});
 it('leaves out-of-bounds movement unchanged',()=>{expect(moveStep(guide,0,-1)).toBe(guide);expect(moveStep(guide,2,1)).toBe(guide);});
});
