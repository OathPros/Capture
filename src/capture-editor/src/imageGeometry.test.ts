import {describe,it,expect} from 'vitest';
import {moveAnnotation,newAnnotation,regionBetween,resizeAnnotation} from './imageGeometry';
import {Annotation} from './model';
const a:Annotation={id:'a',kind:'rectangle',x:.2,y:.2,width:.3,height:.3};
describe('image-space geometry',()=>{
 it('handles reversed drags and edge clicks without invalid regions',()=>{const r=regionBetween({x:.8,y:.9},{x:.2,y:.1});expect(r.x).toBe(.2);expect(r.width).toBeCloseTo(.6);const edge=regionBetween({x:1,y:1},{x:1,y:1});expect(edge.x+edge.width).toBeLessThanOrEqual(1.000001);});
 it('moves and resizes without leaving the image or mutating the original',()=>{expect(moveAnnotation(a,1,-1)).toMatchObject({x:.7,y:0});expect(resizeAnnotation(a,'nw',{x:.1,y:.1})).toMatchObject({x:.1,y:.1,width:.4,height:.4});expect(a.x).toBe(.2);expect(resizeAnnotation(a,'se',{x:2,y:2}).width).toBe(.8);});
 it('preserves reverse arrow direction and uses opaque redaction defaults',()=>{const arrow=newAnnotation('arrow',{x:.8,y:.8},{x:.2,y:.2},{});expect(arrow.flipX).toBe(true);expect(arrow.flipY).toBe(true);expect(newAnnotation('redact',{x:0,y:0},{x:.3,y:.3},{fill:'transparent'}).fill).toBe('#202833');});
});
