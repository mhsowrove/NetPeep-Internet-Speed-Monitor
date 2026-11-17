'use strict';
const fs = require('node:fs');
const path = require('node:path');
function dayKey(date = new Date()) { return `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,'0')}-${String(date.getDate()).padStart(2,'0')}`; }
function bounds(period, now = new Date()) {
  const start = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  if (period === 'week') start.setDate(start.getDate() - (start.getDay()+6)%7);
  if (period === 'month') start.setDate(1);
  return period === 'all' ? '' : dayKey(start);
}
function totals(days, period, now = new Date()) {
  const lower = bounds(period,now), upper = dayKey(now);
  return Object.entries(days).reduce((a,[key,v]) => key >= lower && key <= upper ? {rx:a.rx+v.rx,tx:a.tx+v.tx} : a, {rx:0,tx:0});
}
function bytes(value) { if (!Number.isFinite(value)) return '—'; const n = Math.max(0,value); const units=['B','KiB','MiB','GiB','TiB']; const i=Math.min(4,Math.floor(Math.log(Math.max(1,n))/Math.log(1024))); return `${(n/1024**i).toFixed(i && n/1024**i<100 ? 1 : 0)} ${units[i]}`; }
class Meter {
  constructor() { this.previous=new Map(); this.at=null; }
  reset() { this.previous.clear(); this.at=null; }
  sample(interfaces, selected, at) {
    let rx=0,tx=0; const elapsed=this.at===null?0:(at-this.at)/1000; const next=new Map();
    for (const item of interfaces) {
      if (![item.rx,item.tx].every(n=>Number.isFinite(n)&&n>=0)) continue;
      next.set(item.id,item); const old=this.previous.get(item.id);
      if (old && (selected==='auto' ? item.physical : item.id===selected) && elapsed>0 && elapsed<15) {
        rx+=Math.max(0,item.rx-old.rx); tx+=Math.max(0,item.tx-old.tx);
      }
    }
    this.previous=next; this.at=at;
    return {rx,tx,down:elapsed>0?rx/elapsed:0,up:elapsed>0?tx/elapsed:0};
  }
}
class Store {
  constructor(directory) {
    this.file=path.join(directory,'usage.json'); this.error=''; this.data={version:1,since:new Date().toISOString(),days:{},selected:'auto',compact:true,pinned:true,taskbarFraction:null,unit:'auto',theme:'system'};
    fs.mkdirSync(directory,{recursive:true});
    try {
      const d=JSON.parse(fs.readFileSync(this.file,'utf8'));
      if(d.version!==1 || !d.days || typeof d.days!=='object' || !Object.entries(d.days).every(([k,v])=>/^\d{4}-\d{2}-\d{2}$/.test(k)&&v&&[v.rx,v.tx].every(n=>Number.isFinite(n)&&n>=0))) throw Error('Invalid history');
      this.data={...this.data,...d};
      if(!['auto','B','KB','MB','GB'].includes(this.data.unit))this.data.unit='auto';
      if(!['system','light','dark'].includes(this.data.theme))this.data.theme='system';
    } catch(e) { if(e.code!=='ENOENT') { this.error='History could not be read. A backup was kept.'; fs.copyFileSync(this.file,this.file+'.corrupt-'+Date.now()); } }
  }
  add(rx,tx,at=new Date()) { const key=dayKey(at); const d=this.data.days[key] ||= {rx:0,tx:0}; d.rx+=rx; d.tx+=tx; }
  save() { try { fs.writeFileSync(this.file+'.tmp',JSON.stringify(this.data)); fs.renameSync(this.file+'.tmp',this.file); this.error=''; return true; } catch(e) { this.error='History could not be saved: '+e.message; return false; } }
}
module.exports={dayKey,bounds,totals,bytes,Meter,Store};
