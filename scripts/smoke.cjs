// Real Electron + native collector integration; isolated from normal user history.
const {app,BrowserWindow}=require('electron');const fs=require('node:fs');const path=require('node:path');const assert=require('node:assert/strict');
const directory=path.resolve(process.env.NETPEEP_SMOKE_DIR||path.join(__dirname,'../test/smoke-data'));
fs.mkdirSync(directory,{recursive:true});app.setPath('userData',directory);
require('../src/main');
app.whenReady().then(()=>setTimeout(async()=>{
  try {
    const main=BrowserWindow.getAllWindows().find(w=>w.getTitle()==='NetPeep');assert.ok(main,'dashboard window exists');
    const errors=[];main.webContents.on('console-message',(_e,level,message)=>{if(level>=3)errors.push(message);});
    const first=await main.webContents.executeJavaScript('window.netpeep.snapshot()');
    assert.ok(first.interfaces.length>0,'native interfaces');assert.equal(first.error,'','native collector healthy');assert.ok(first.periods.day.rx>=0);
    await main.webContents.executeJavaScript("document.querySelector('[data-period=week]').click()");
    assert.equal(await main.webContents.executeJavaScript("document.querySelector('[data-period=week]').getAttribute('aria-pressed')"),'true');
    await main.webContents.executeJavaScript('window.netpeep.selectInterface('+JSON.stringify(first.interfaces[0].id)+')');
    assert.equal((await main.webContents.executeJavaScript('window.netpeep.snapshot()')).selected,first.interfaces[0].id);
    await main.webContents.executeJavaScript("window.netpeep.selectInterface('auto')");
    await main.webContents.executeJavaScript('window.netpeep.compact(false)');assert.equal((await main.webContents.executeJavaScript('window.netpeep.snapshot()')).compact,false);
    await main.webContents.executeJavaScript('window.netpeep.compact(true)');
    if(process.platform==='win32'){
      const meter=BrowserWindow.getAllWindows().find(w=>w.getTitle()==='NetPeep live meter');assert.ok(meter,'taskbar meter exists');
      const {screen}=require('electron');
      await main.webContents.executeJavaScript('window.netpeep.pinMeter(false)');
      const unlocked=meter.getBounds(),display=screen.getPrimaryDisplay();
      meter.setPosition(display.bounds.x+230,unlocked.y);
      await new Promise(resolve=>setTimeout(resolve,200));
      await main.webContents.executeJavaScript('window.netpeep.pinMeter(true)');
      await new Promise(resolve=>setTimeout(resolve,1200));
      const pinned=meter.getBounds();assert.equal(pinned.width,112);assert.ok(Math.abs(pinned.x-(display.bounds.x+230))<=2,'dragged horizontal position retained');
      assert.ok(pinned.y>=display.workArea.y+display.workArea.height,'meter inside bottom taskbar');
      assert.equal(await meter.webContents.executeJavaScript('getComputedStyle(document.body).backgroundColor'),'rgba(0, 0, 0, 0)');
      assert.equal(await meter.webContents.executeJavaScript("document.querySelector('.readings').firstElementChild.querySelector('[id]').id"),'up');
      assert.equal((await main.webContents.executeJavaScript('window.netpeep.snapshot()')).pinned,true);
      const bitmap=(await meter.webContents.capturePage()).toBitmap();assert.equal(bitmap[3],0,'transparent corner has zero alpha');
      if(process.env.NETPEEP_SCREENSHOT)fs.writeFileSync(path.resolve(process.env.NETPEEP_SCREENSHOT).replace(/\.png$/, '-taskbar.png'),(await meter.webContents.capturePage()).toPNG());
    }
    await main.webContents.executeJavaScript("document.getElementById('interface').options[0].dataset.testMarker='kept'");
    main.close();assert.equal(main.isDestroyed(),false,'close retains background monitoring');main.show();
    await new Promise(resolve=>setTimeout(resolve,2500));
    const final=await main.webContents.executeJavaScript('window.netpeep.snapshot()');assert.ok(final.observedAt>=first.observedAt,'samples continue');
    assert.equal(await main.webContents.executeJavaScript("document.getElementById('interface').options[0].dataset.testMarker"),'kept','sampling preserves adapter dropdown options');
    await main.webContents.executeJavaScript("window.netpeep.setPreferences({unit:'GB',theme:'dark'})");
    await new Promise(resolve=>setTimeout(resolve,200));
    assert.equal((await main.webContents.executeJavaScript('window.netpeep.snapshot()')).unit,'GB');
    assert.equal(await main.webContents.executeJavaScript('document.documentElement.dataset.theme'),'dark');
    await main.webContents.executeJavaScript("window.netpeep.setPreferences({unit:'auto',theme:'light'})");
    main.setSize(436,575);await new Promise(resolve=>setTimeout(resolve,500));
    if(process.env.NETPEEP_SCREENSHOT)fs.writeFileSync(path.resolve(process.env.NETPEEP_SCREENSHOT),(await main.webContents.capturePage()).toPNG());
    assert.equal(errors.length,0,'renderer console');
    fs.writeFileSync(path.join(directory,'smoke-result.json'),JSON.stringify({passed:true,interfaces:final.interfaces.length,processes:final.apps.length,status:final.status,periods:final.periods,checks:['native collector','calendar period UI','adapter selection','floating meter toggle','close-to-tray','continued sampling','renderer console','screenshot']},null,2));
    console.log('PASS: Electron/native integration',final.status);app.quit();
  }catch(e){console.error(e);app.exit(1);}
},10000));
