import {describe,it,expect} from 'vitest';
import {parseZoom,viewScale} from './viewZoom';
describe('viewport zoom',()=>{
 it('contains landscape, portrait, cropped and small images within both dimensions without upscaling',()=>{
  for(const [iw,ih] of [[1920,1080],[800,1600],[320,200],[3840,2160],[800,80]])for(const [vw,vh] of [[1200,600],[400,250],[180,100]]){
   const s=viewScale('fit',iw,ih,vw,vh);expect(s).toBeLessThanOrEqual(1);expect(iw*s).toBeLessThanOrEqual(vw-32+.001);expect(ih*s).toBeLessThanOrEqual(vh-32+.001);
  }
 });
 it('distinguishes width fitting and literal source-pixel percentages',()=>{
  expect(viewScale('fit',800,1600,1032,532)).toBe(.3125);
  expect(viewScale('width',800,1600,1032,532)).toBe(1.25);
  expect(viewScale(.5,3840,2160,500,300)).toBe(.5);
 });
 it('rejects corrupt or unsafe saved preferences and handles zero-size mounting',()=>{
  for(const v of [null,{},'50',0,-1,100,NaN,Infinity])expect(parseZoom(v)).toBe('fit');
  expect(parseZoom(.5)).toBe(.5);expect(parseZoom('width')).toBe('width');expect(parseZoom('fit')).toBe('fit');
  expect(Number.isFinite(viewScale('fit',1920,1080,0,0))).toBe(true);
 });
});
