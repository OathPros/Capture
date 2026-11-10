import { describe, expect, it } from 'vitest';
import { moveStep, moveStepTo, stepNumbers, stepRuns, Guide, Step } from './model';
function step(id:string,sectionId:string|null=null):Step{return {id,order:0,title:id,instruction:'',notes:'',assetId:null,sectionId,annotations:[],needsReview:true};}
describe('section numbering',()=>{
 const sections=[{id:'a',title:'A',restartNumbering:false},{id:'b',title:'B',restartNumbering:true}];
 it('numbers steps continuously without sections',()=>{
  const guide={id:'g',schemaVersion:'1.3.0',title:'',summary:'',createdAt:'',updatedAt:'',assets:[],steps:[step('one'),step('two'),step('three')]};
  expect(stepNumbers(guide)).toEqual([1,2,3]);
 });
 it('keeps guide numbering for sections that inherit it',()=>{
  const guide={id:'g',schemaVersion:'1.3.0',title:'',summary:'',createdAt:'',updatedAt:'',assets:[],sections,steps:[step('one'),step('two','a'),step('three','a'),step('four')]};
  expect(stepNumbers(guide)).toEqual([1,2,3,4]);
 });
 it('restarts numbering inside restart sections and resumes the shared count after',()=>{
  const guide={id:'g',schemaVersion:'1.3.0',title:'',summary:'',createdAt:'',updatedAt:'',assets:[],sections,steps:[step('one'),step('two','b'),step('three','b'),step('four')]};
  expect(stepNumbers(guide)).toEqual([1,1,2,2]);
 });
 it('restarts again for a non-contiguous run of the same restart section',()=>{
  const guide={id:'g',schemaVersion:'1.3.0',title:'',summary:'',createdAt:'',updatedAt:'',assets:[],sections,steps:[step('one','b'),step('two'),step('three','b')]};
  expect(stepNumbers(guide)).toEqual([1,1,1]);
 });
 it('groups contiguous steps by section',()=>{
  const guide={id:'g',schemaVersion:'1.3.0',title:'',summary:'',createdAt:'',updatedAt:'',assets:[],sections,steps:[step('one'),step('two','a'),step('three','a'),step('four','b')]};
  expect(stepRuns(guide).map(r=>[r.section?.id||null,r.start,r.count])).toEqual([[null,0,1],['a',1,2],['b',3,1]]);
 });
});
describe('step order',()=>{
 const guide: Guide={id:'guide',schemaVersion:'1.0.0',title:'Synthetic fixture',summary:'',createdAt:'',updatedAt:'',assets:[],steps:['first','second','third'].map((id,i)=>({id,order:i+1,title:id,instruction:'',notes:'',assetId:null,annotations:[],needsReview:true}))};
 it('moves without mutating the original or changing identities',()=>{const moved=moveStep(guide,1,-1);expect(moved.steps.map(s=>s.id)).toEqual(['second','first','third']);expect(moved.steps.map(s=>s.order)).toEqual([1,2,3]);expect(guide.steps[0].id).toBe('first');});
 it('moves any step to an exact destination without changing its identity',()=>{const moved=moveStepTo(guide,'first',2);expect(moved.steps.map(s=>s.id)).toEqual(['second','third','first']);expect(moved.steps.map(s=>s.order)).toEqual([1,2,3]);expect(moveStepTo(guide,'missing',0)).toBe(guide);expect(moveStepTo(guide,'first',-1)).toBe(guide);});
 it('leaves out-of-bounds movement unchanged',()=>{expect(moveStep(guide,0,-1)).toBe(guide);expect(moveStep(guide,2,1)).toBe(guide);});
});
