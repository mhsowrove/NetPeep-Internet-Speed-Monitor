const {contextBridge,ipcRenderer}=require('electron');
contextBridge.exposeInMainWorld('netpeep',{
  snapshot:()=>ipcRenderer.invoke('snapshot'),
  subscribe:callback=>{const handler=(_event,data)=>callback(data);ipcRenderer.on('sample',handler);return()=>ipcRenderer.removeListener('sample',handler);},
  selectInterface:id=>ipcRenderer.invoke('select-interface',id),
  compact:value=>ipcRenderer.invoke('compact',value),
  pinMeter:value=>ipcRenderer.invoke('pin-meter',value),
  setPreferences:patch=>ipcRenderer.invoke('preferences',patch),
  resetPosition:()=>ipcRenderer.invoke('reset-position'),
  external:key=>ipcRenderer.invoke('external',key),
  copyDonation:()=>ipcRenderer.invoke('copy-donation'),
  open:()=>ipcRenderer.invoke('open'),restart:()=>ipcRenderer.invoke('restart'),export:()=>ipcRenderer.invoke('export')
});
