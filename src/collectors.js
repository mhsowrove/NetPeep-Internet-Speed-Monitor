'use strict';
const {spawn,execFile}=require('node:child_process');
const path=require('node:path');
const {promisify}=require('node:util');
const exec=promisify(execFile);
function parseNetstat(text) {
  const lines=text.trim().split(/\r?\n/); const header=lines.shift().trim().split(/\s+/); const iRx=header.indexOf('Ibytes'),iTx=header.indexOf('Obytes');
  if(iRx<0||iTx<0) throw Error('Unsupported netstat output: byte columns missing');
  const found=new Map();
  for(const line of lines){const p=line.trim().split(/\s+/); if(!p[2]?.startsWith('<Link#'))continue; const id=p[0].replace(/\*$/,''); if(id==='lo0')continue; const rx=Number(p[iRx]),tx=Number(p[iTx]); if(!Number.isFinite(rx)||!Number.isFinite(tx))continue; found.set(id,{id,name:id,physical:/^en\d+$/.test(id),rx,tx});}
  return [...found.values()];
}
function parseNettop(text) {
  const rows=text.trim().split(/\r?\n/).map(line=>line.split(','));
  const h=rows.findIndex(row=>row.includes('bytes_in')&&row.includes('bytes_out'));
  if(h<0)throw Error('Unsupported nettop output: byte columns missing');
  const rxIndex=rows[h].indexOf('bytes_in'),txIndex=rows[h].indexOf('bytes_out');
  const result=[];
  for(const row of rows.slice(h+1)){if(/^\s/.test(row[0]))continue;const match=row[0]?.match(/^(.+)\.(\d+)$/); if(!match)continue; const rx=Number(row[rxIndex]),tx=Number(row[txIndex]); if(!Number.isFinite(rx)||!Number.isFinite(tx))continue; result.push({pid:Number(match[2]),name:match[1],rx,tx,measured:true});}
  return result;
}
class Collector {
  constructor(onSample,onError){this.onSample=onSample;this.onError=onError;this.stopped=false;this.child=null;this.timer=null;this.appPrevious=new Map();}
  start(){
    if(process.platform==='win32'){
      const base=path.join(__dirname,'..','native','windows.ps1').replace('app.asar'+path.sep,'app.asar.unpacked'+path.sep);
      this.child=spawn('powershell.exe',['-NoLogo','-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',base],{windowsHide:true}); let buffer='',err='';
      this.child.stdout.setEncoding('utf8'); this.child.stdout.on('data',s=>{buffer+=s;if(buffer.length>4e6){this.onError('Collector output exceeded limit');this.stop();return;}let i;while((i=buffer.indexOf('\n'))>=0){const line=buffer.slice(0,i);buffer=buffer.slice(i+1);try{const sample=JSON.parse(line);sample.error?this.onError(sample.error):this.onSample({...sample,interfaces:sample.interfaces||[],apps:sample.apps||[]});}catch(e){this.onError('Collector response invalid: '+e.message);}}});
      this.child.stderr.on('data',s=>{err=(err+s).slice(-2000);});this.child.on('error',e=>this.onError(e.message));this.child.on('exit',code=>{if(!this.stopped)this.onError(`Windows collector stopped (${code}). ${err}`);});
    } else if(process.platform==='darwin') this.macTick();
    else this.onError('NetPeep supports Windows and macOS.');
  }
  async macTick(){
    try {
      const {stdout}=await exec('/usr/sbin/netstat',['-ibn'],{timeout:5000,maxBuffer:2e6});
      const sample={interfaces:parseNetstat(stdout),apps:[],status:'macOS nettop · best-effort process attribution; shared services may own app traffic'};
      try {
        const {stdout:out}=await exec('/usr/bin/nettop',['-P','-L','1','-x','-J','bytes_in,bytes_out'],{timeout:5000,maxBuffer:4e6});
        const apps=parseNettop(out),next=new Map();
        for(const a of apps){const old=this.appPrevious.get(a.pid+':'+a.name);next.set(a.pid+':'+a.name,{rx:a.rx,tx:a.tx});if(old){a.rx=Math.max(0,a.rx-old.rx);a.tx=Math.max(0,a.tx-old.tx);}else{a.rx=0;a.tx=0;a.measured=false;}}
        this.appPrevious=next;sample.apps=apps;
      } catch(e){sample.status='Per-app bytes unavailable. nettop may require permission on this macOS version. '+e.message;}
      if(!this.stopped)this.onSample(sample);
    }catch(e){if(!this.stopped)this.onError(e.message);}
    if(!this.stopped)this.timer=setTimeout(()=>this.macTick(),1000);
  }
  stop(){this.stopped=true;clearTimeout(this.timer);if(this.child)this.child.kill();}
}
module.exports={Collector,parseNetstat,parseNettop};
