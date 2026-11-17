// The same UI runs inside Windows WebView2 and Electron on macOS.
if(!window.netpeep&&window.chrome?.webview){
  const pending=new Map(),listeners=new Set();let sequence=0;
  function request(method,...args){return new Promise((resolve,reject)=>{const id=++sequence;pending.set(id,{resolve,reject});window.chrome.webview.postMessage({id,method,args});});}
  window.chrome.webview.addEventListener('message',event=>{const m=event.data;if(m.type==='sample'){for(const fn of listeners)fn(m.data);}else if(pending.has(m.id)){const p=pending.get(m.id);pending.delete(m.id);m.error?p.reject(Error(m.error)):p.resolve(m.value);}});
  window.netpeep={snapshot:()=>request('snapshot'),subscribe:fn=>{listeners.add(fn);return()=>listeners.delete(fn);},selectInterface:id=>request('select-interface',id),compact:v=>request('compact',v),pinMeter:v=>request('pin-meter',v),resetPosition:()=>request('reset-position'),setPreferences:p=>request('preferences',p),external:key=>request('external',key),copyDonation:()=>request('copy-donation'),open:()=>request('open'),restart:()=>request('restart'),export:()=>request('export')};
}
