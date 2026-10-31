import React from 'react';
export function fieldRuns(text:string,fields:string[]=[]):{text:string;bold:boolean}[]{
 const ranges:{start:number;end:number}[]=[];
 for(const field of [...new Set(fields)].filter(Boolean).sort((a,b)=>b.length-a.length)){
  let at=text.lastIndexOf(field);
  while(at>=0){const end=at+field.length;const boundary=(!/[\p{L}\p{N}]/u.test(field[0])||at===0||!/[\p{L}\p{N}]/u.test(text[at-1]))&&(!/[\p{L}\p{N}]/u.test(field.at(-1)!)||end===text.length||!/[\p{L}\p{N}]/u.test(text[end]));
   if(boundary&&!ranges.some(r=>at<r.end&&end>r.start)){ranges.push({start:at,end});break;}if(at===0)break;at=text.lastIndexOf(field,at-1);
  }
 }
 let at=0;const result:{text:string;bold:boolean}[]=[];for(const r of ranges.sort((a,b)=>a.start-b.start)){if(r.start>at)result.push({text:text.slice(at,r.start),bold:false});result.push({text:text.slice(r.start,r.end),bold:true});at=r.end;}if(at<text.length)result.push({text:text.slice(at),bold:false});return result;
}
export function FormattedText({text,fields}:{text:string;fields?:string[]}){return <>{fieldRuns(text,fields).map((r,i)=>r.bold?<strong key={i}>{r.text}</strong>:<React.Fragment key={i}>{r.text}</React.Fragment>)}</>;}
