'use strict';
const {app,BrowserWindow,Tray,Menu,ipcMain,nativeImage,screen,powerMonitor,dialog,nativeTheme,shell,clipboard}=require('electron');
const path=require('node:path');
const {performance}=require('node:perf_hooks');
const {Meter,Store,totals,bytes}=require('./core');
const {Collector}=require('./collectors');
const {taskbarBounds,positionFraction}=require('./taskbar');
const {format,preferences}=require('./format');
const developer=require('../assets/developer.json');
let main,compact,tray,collector,store,meter,last,saveTimer,watchdog,quitting=false;
let taskbarGeometry=null;
const icon=path.join(__dirname,'../assets/icon.png');
function syncTrayMenu(){tray?.setContextMenu(Menu.buildFromTemplate([{label:'Open NetPeep',click:openMain},{label:'Show taskbar meter',type:'checkbox',checked:store.data.compact,click:item=>setCompact(item.checked)},...(process.platform==='win32'?[{label:store.data.pinned?'Unlock meter to move':'Pin meter to taskbar',click:()=>setPinned(!store.data.pinned)},{label:'Reset taskbar position',click:()=>{store.data.taskbarFraction=null;store.save();placeCompact();}}]:[]),{label:'Restart counters',click:startCollector},{type:'separator'},{label:'Quit NetPeep',click:()=>app.quit()}]));}
function setPinned(value){if(value&&!store.data.pinned&&compact&&taskbarGeometry)store.data.taskbarFraction=positionFraction(taskbarGeometry.bounds,compact.getBounds());store.data.pinned=Boolean(value);store.save();compact?.setIgnoreMouseEvents(process.platform==='win32'&&store.data.pinned);if(store.data.pinned)placeCompact();else if(store.data.compact)compact?.showInactive();syncTrayMenu();publish();}
function placeCompact(){
  if(!compact||compact.isDestroyed()||!store.data.compact||process.platform!=='win32'||!store.data.pinned)return;
  if(!taskbarGeometry){compact.hide();return;}
  const bar=taskbarGeometry.bounds,display=screen.getDisplayMatching(bar).bounds;
  const visibleWidth=Math.max(0,Math.min(bar.x+bar.width,display.x+display.width)-Math.max(bar.x,display.x));
  const visibleHeight=Math.max(0,Math.min(bar.y+bar.height,display.y+display.height)-Math.max(bar.y,display.y));
  if(Math.min(visibleWidth,visibleHeight)<16){compact.hide();return;}
  compact.setBounds(taskbarBounds(bar,taskbarGeometry.notification,store.data.taskbarFraction));
  compact.setAlwaysOnTop(true,'screen-saver');compact.showInactive();
}
function openMain(){if(!main||main.isDestroyed())createMain();main.show();main.focus();}
function updateTray(){if(!last)return;const text=`↓ ${format(last.down,store.data.unit)}/s   ↑ ${format(last.up,store.data.unit)}/s`;tray.setToolTip('NetPeep • '+text);if(process.platform==='darwin')tray.setTitle(text);}
function createMain(){
  main=new BrowserWindow({width:436,height:575,minWidth:400,minHeight:520,title:'NetPeep',backgroundColor:nativeTheme.shouldUseDarkColors?'#121a24':'#f3f6f9',icon,autoHideMenuBar:true,webPreferences:{preload:path.join(__dirname,'preload.js'),contextIsolation:true,nodeIntegration:false,sandbox:true}});
  main.loadFile(path.join(__dirname,'app.html'));
  main.webContents.setWindowOpenHandler(()=>({action:'deny'}));
  main.webContents.on('will-navigate',e=>e.preventDefault());
  main.on('close',e=>{if(!quitting){e.preventDefault();main.hide();}});
}
function setCompact(visible){
  store.data.compact=visible;store.save();syncTrayMenu();
  if(!visible){compact?.hide();return;}
  if(!compact||compact.isDestroyed()){
    const area=screen.getPrimaryDisplay().workArea;
    compact=new BrowserWindow({width:112,height:38,x:area.x+area.width-300,y:area.y+area.height-50,show:false,frame:false,resizable:false,maximizable:false,minimizable:false,hasShadow:false,alwaysOnTop:true,skipTaskbar:true,transparent:true,backgroundColor:'#00000000',webPreferences:{preload:path.join(__dirname,'preload.js'),contextIsolation:true,nodeIntegration:false,sandbox:true}});
    compact.loadFile(path.join(__dirname,'compact.html'));compact.setVisibleOnAllWorkspaces(true,{visibleOnFullScreen:true});
    compact.webContents.setWindowOpenHandler(()=>({action:'deny'}));compact.webContents.on('will-navigate',e=>e.preventDefault());
    compact.webContents.on('context-menu',()=>Menu.buildFromTemplate([{label:'Pin to taskbar',click:()=>setPinned(true)},{label:'Open NetPeep',click:openMain}]).popup({window:compact}));
  }
  compact.setIgnoreMouseEvents(process.platform==='win32'&&store.data.pinned);
  if(process.platform==='win32'&&store.data.pinned)placeCompact();else compact.showInactive();
}
function snapshot(){
  return {...last,periods:Object.fromEntries(['day','week','month','all'].map(p=>[p,totals(store.data.days,p)])),since:store.data.since,selected:store.data.selected,compact:store.data.compact,pinned:store.data.pinned,unit:store.data.unit,theme:store.data.theme,appVersion:app.getVersion(),developer,storageError:store.error,platform:process.platform};
}
function publish(){const data=snapshot();for(const w of [main,compact])if(w&&!w.isDestroyed())w.webContents.send('sample',data);}
function startCollector(){
  collector?.stop();meter.reset();
  last={down:0,up:0,apps:[],interfaces:[],status:'Connecting to native counters…',error:'',observedAt:Date.now()};
  collector=new Collector(sample=>{
    if(process.platform==='win32'){taskbarGeometry=sample.taskbar?{bounds:screen.screenToDipRect(null,sample.taskbar.bounds),notification:sample.taskbar.notification?screen.screenToDipRect(null,sample.taskbar.notification):null}:null;placeCompact();}
    const at=performance.now(),previousAt=last.monotonic;
    const measured=meter.sample(sample.interfaces,store.data.selected,at);
    const elapsed=previousAt?(at-previousAt)/1000:0;
    store.add(measured.rx,measured.tx);
    last={...measured,apps:sample.apps.map(a=>({...a,down:elapsed>0&&elapsed<15&&a.measured?a.rx/elapsed:null,up:elapsed>0&&elapsed<15&&a.measured?a.tx/elapsed:null})),interfaces:sample.interfaces,status:sample.status,error:'',observedAt:Date.now(),monotonic:at};
    updateTray();
    publish();
  },error=>{last={...last,down:0,up:0,error,apps:[]};tray.setToolTip('NetPeep • counters unavailable');if(process.platform==='darwin')tray.setTitle('↓ — ↑ —');publish();});collector.start();
}
if(!app.requestSingleInstanceLock())app.quit();
else {
  app.on('second-instance',openMain);
  app.whenReady().then(()=>{
    store=new Store(app.getPath('userData'));meter=new Meter();nativeTheme.themeSource=store.data.theme;
    const trayImage=nativeImage.createFromPath(path.join(__dirname,process.platform==='darwin'?'../assets/tray.png':'../assets/icon.png')).resize({width:20,height:20});
    if(process.platform==='darwin')trayImage.setTemplateImage(true);
    tray=new Tray(trayImage);tray.setToolTip('NetPeep • starting');
    syncTrayMenu();
    tray.on('click',openMain);createMain();if(store.data.compact)setCompact(true);startCollector();
    saveTimer=setInterval(()=>{store.save();publish();},5000);
    watchdog=setInterval(()=>{if(Date.now()-last.observedAt>8000&&!last.error){last={...last,down:0,up:0,apps:[],error:'No fresh samples. Use Restart counters to reconnect.'};tray.setToolTip('NetPeep • counters unavailable');if(process.platform==='darwin')tray.setTitle('↓ — ↑ —');publish();}},2000);
    powerMonitor.on('resume',()=>startCollector());
    powerMonitor.on('suspend',()=>{collector.stop();meter.reset();store.save();last={...last,down:0,up:0,error:'Paused while this computer sleeps.'};publish();});
  });
}
ipcMain.handle('snapshot',()=>snapshot());
ipcMain.handle('select-interface',(_e,id)=>{if(id!=='auto'&&!last.interfaces.some(i=>i.id===id))throw Error('Unknown interface');store.data.selected=id;meter.reset();store.save();publish();});
ipcMain.handle('compact',(_e,value)=>{setCompact(Boolean(value));publish();});
ipcMain.handle('pin-meter',(_e,value)=>setPinned(Boolean(value)));
ipcMain.handle('preferences',(_e,patch)=>{Object.assign(store.data,preferences(patch));nativeTheme.themeSource=store.data.theme;store.save();updateTray();publish();return true;});
ipcMain.handle('reset-position',()=>{store.data.taskbarFraction=null;store.save();placeCompact();});
ipcMain.handle('external',(_e,key)=>{if(!['email','facebook','paypal'].includes(key))throw Error('Unsupported link');return shell.openExternal((key==='email'?'mailto:':'')+developer[key]);});
ipcMain.handle('copy-donation',()=>{clipboard.writeText(developer.bkash);return true;});
ipcMain.handle('open',()=>openMain());
ipcMain.handle('restart',()=>startCollector());
ipcMain.handle('export',async()=>{const result=await dialog.showSaveDialog(main,{title:'Export local usage',defaultPath:'NetPeep-usage.json',filters:[{name:'JSON',extensions:['json']}]});if(!result.canceled){require('node:fs').writeFileSync(result.filePath,JSON.stringify(store.data,null,2));return true;}return false;});
app.on('activate',openMain);
app.on('before-quit',()=>{quitting=true;collector?.stop();clearInterval(saveTimer);clearInterval(watchdog);store?.save();});
