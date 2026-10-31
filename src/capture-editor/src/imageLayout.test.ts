import {describe,it,expect} from 'vitest';
import {imageLayout,projectRegion,toOriginal} from './imageLayout';
import {fieldRuns} from './FormattedText';
describe('strip cuts and source coordinates',()=>{
 it('joins strips from both axes and keeps source coordinate mapping',()=>{const layout=imageLayout(1000,800,null,[{axis:'horizontal',start:0,end:.1},{axis:'horizontal',start:.3,end:.45},{axis:'vertical',start:.2,end:.3}]);expect(layout.width).toBe(900);expect(layout.height).toBe(600);expect(toOriginal(300,layout.columns)).toBe(400);const box=projectRegion({x:.4,y:.5,width:.2,height:.1},1000,800,layout);for(const [key,value] of Object.entries({x:300,y:200,width:200,height:80}))expect(box[key as keyof typeof box]).toBeCloseTo(value,8);});
 it('coalesces overlapping cuts and keeps crop-relative mapping',()=>{const l=imageLayout(100,100,{x:.1,y:.1,width:.8,height:.8},[{axis:'horizontal',start:.2,end:.4},{axis:'horizontal',start:.3,end:.5}]);expect(l.height).toBe(50);expect(projectRegion({x:0,y:0,width:.2,height:.2},100,100,l)).toEqual({x:-10,y:-10,width:20,height:20});});
 it('bolds literal field labels rather than generated verbs or substrings',()=>{expect(fieldRuns('Select Select',['Select'])).toEqual([{text:'Select ',bold:false},{text:'Select',bold:true}]);expect(fieldRuns('Select GUID',['ID'])).toEqual([{text:'Select GUID',bold:false}]);});
});
