using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

[assembly:System.Reflection.AssemblyTitle("NetPeep")]
[assembly:System.Reflection.AssemblyProduct("NetPeep")]
[assembly:System.Reflection.AssemblyCompany("MD SOWROVE HOSSAIN")]
[assembly:System.Reflection.AssemblyVersion("2.0.0.0")]
[assembly:System.Reflection.AssemblyFileVersion("2.0.0.0")]

public class UsageDay { public double rx,tx; }
public class Ledger {
  public int version=1;
  public string since=DateTime.UtcNow.ToString("o"),selected="auto",unit="auto",theme="system";
  public bool compact=true,pinned=true;
  public double? taskbarFraction=null;
  public Dictionary<string,UsageDay> days=new Dictionary<string,UsageDay>();
  public static void Validate(Ledger d) {
    if(d==null||d.version!=1||d.days==null)throw new Exception("Invalid history format");
    foreach(var pair in d.days){DateTime date;if(!DateTime.TryParseExact(pair.Key,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date)||pair.Value==null||!Finite(pair.Value.rx)||!Finite(pair.Value.tx)||pair.Value.rx<0||pair.Value.tx<0)throw new Exception("Invalid history totals");}
    if(!new[]{"auto","B","KB","MB","GB"}.Contains(d.unit))d.unit="auto";
    if(!new[]{"system","light","dark"}.Contains(d.theme))d.theme="system";
    if(d.taskbarFraction.HasValue&&(!Finite(d.taskbarFraction.Value)||d.taskbarFraction.Value<0||d.taskbarFraction.Value>1))d.taskbarFraction=null;
  }
  public static bool Finite(double n){return !double.IsNaN(n)&&!double.IsInfinity(n);}
  public void Add(double rx,double tx,DateTime now){var key=now.ToString("yyyy-MM-dd");UsageDay day;if(!days.TryGetValue(key,out day)){day=new UsageDay();days[key]=day;}day.rx+=rx;day.tx+=tx;}
  public UsageDay Totals(string period,DateTime now){var start=now.Date;if(period=="week")start=start.AddDays(-(((int)start.DayOfWeek+6)%7));if(period=="month")start=new DateTime(now.Year,now.Month,1);string lower=period=="all"?"":start.ToString("yyyy-MM-dd"),upper=now.ToString("yyyy-MM-dd");var total=new UsageDay();foreach(var p in days)if(String.CompareOrdinal(p.Key,lower)>=0&&String.CompareOrdinal(p.Key,upper)<=0){total.rx+=p.Value.rx;total.tx+=p.Value.tx;}return total;}
}

public static class DisplayUnits {
  public static string Format(double value,string unit) {
    if(!Ledger.Finite(value))return "—";value=Math.Max(0,value);string[] names={"B","KB","MB","GB"};int i=Array.IndexOf(names,unit);
    if(i<0)i=Math.Min(3,(int)Math.Floor(Math.Log10(Math.Max(1,value))/3));double n=value/Math.Pow(1000,i);int digits=i==0?0:n==0?1:n<.001?9:n<1?4:n<100?2:1;
    return n.ToString("F"+digits,CultureInfo.InvariantCulture).TrimEndFraction()+" "+names[i];
  }
  static string TrimEndFraction(this string text){return text.Contains(".")?text.TrimEnd('0').TrimEnd('.'):text;}
}

public static class Program {
  [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
  [STAThread] public static void Main(string[] args) {
    try {
      SetProcessDpiAwarenessContext(new IntPtr(-4));Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
      string smoke=null;for(int i=0;i+1<args.Length;i++)if(args[i]=="--smoke")smoke=Path.GetFullPath(args[i+1]);
      if(args.Contains("--selftest")){SelfTest(args.Last());return;}
      bool created;using(var mutex=new Mutex(true,smoke==null?"Local\\NetPeep.Native.2":"Local\\NetPeep.Smoke."+Process.GetCurrentProcess().Id,out created)){
        if(!created){MessageBox.Show("NetPeep is already running. Open it from its tray icon.","NetPeep");return;}
        Application.Run(new PeepContext(smoke,args.Contains("--startup")));
      }
    }catch(Exception e){if(args.Contains("--smoke")||args.Contains("--selftest")){File.WriteAllText(args.Last()+".error.txt",e.ToString());Environment.ExitCode=1;}else MessageBox.Show(e.Message,"NetPeep couldn't start");}
  }
  static void SelfTest(string destination){
    var l=new Ledger();l.Add(1000,500,new DateTime(2026,11,16));l.Add(2000,1000,new DateTime(2026,11,17));if(l.Totals("week",new DateTime(2026,11,17)).rx!=3000||l.Totals("day",new DateTime(2026,11,17)).rx!=2000)throw new Exception("Calendar totals failed");
    if(DisplayUnits.Format(1500,"KB")!="1.5 KB"||DisplayUnits.Format(1000000000,"GB")!="1 GB"||DisplayUnits.Format(12,"B")!="12 B")throw new Exception("Unit formatting failed");
    l.unit="MB";l.theme="dark";var json=new JavaScriptSerializer();var restored=json.Deserialize<Ledger>(json.Serialize(l));Ledger.Validate(restored);if(restored.unit!="MB"||restored.theme!="dark")throw new Exception("Preference roundtrip failed");
    File.WriteAllText(destination,"PASS: calendar periods, unit conversion, preference persistence, ledger validation");
  }
}

public class PeepContext:ApplicationContext {
  const string Version="2.0.0";
  readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=16*1024*1024};
  readonly string root=AppDomain.CurrentDomain.BaseDirectory,dataDir,smokeDir;
  readonly NotifyIcon tray;readonly Dashboard dashboard;readonly TaskbarMeter meter;readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer{Interval=1000};
  readonly Stopwatch clock=Stopwatch.StartNew();readonly Dictionary<string,PeepInterface> previous=new Dictionary<string,PeepInterface>();
  Ledger ledger;Dictionary<string,object> developer;PeepSample sample=new PeepSample();List<Dictionary<string,object>> apps=new List<Dictionary<string,object>>();
  double lastAt=-1,down,up;string error="",storageError="";DateTime observed=DateTime.Now;bool busy,quitting;int ticks;
  public PeepContext(string smoke,bool startup){
    smokeDir=smoke;dataDir=smoke??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"NetPeep");Directory.CreateDirectory(dataDir);
    developer=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,"assets","developer.json")));ledger=Load();
    dashboard=new Dashboard(this,Path.Combine(root,"assets","icon.ico"),smoke!=null);
    // A hidden startup still needs a UI-thread handle for suspend/resume messages.
    var dashboardHandle=dashboard.Handle;
    meter=new TaskbarMeter(delegate{Pin(true);},delegate{ShowDashboard();});
    tray=new NotifyIcon{Icon=new Icon(Path.Combine(root,"assets","icon.ico")),Text="NetPeep",Visible=true};tray.MouseClick+=delegate(object s,MouseEventArgs e){if(e.Button==MouseButtons.Left)ShowDashboard();};BuildMenu();
    timer.Tick+=async delegate{if(!busy){busy=true;try{await Collect();}finally{busy=false;}}};timer.Start();
    SystemEvents.PowerModeChanged+=PowerChanged;
    if(!startup)ShowDashboard();if(smokeDir!=null){Directory.CreateDirectory(smokeDir);RunSmoke();}
  }
  Ledger Load(){string file=Path.Combine(dataDir,"usage.json");if(!File.Exists(file))return new Ledger();try{var d=json.Deserialize<Ledger>(File.ReadAllText(file));Ledger.Validate(d);return d;}catch{string backup=file+".corrupt-"+DateTime.UtcNow.Ticks;File.Copy(file,backup);storageError="Unreadable history was backed up.";return new Ledger();}}
  void Save(){try{string file=Path.Combine(dataDir,"usage.json"),tmp=file+".tmp";File.WriteAllText(tmp,json.Serialize(ledger));if(File.Exists(file))File.Replace(tmp,file,file+".last-good");else File.Move(tmp,file);storageError="";}catch(Exception e){storageError="Could not save history: "+e.Message;}}
  async Task Collect(){
    try {
      var next=await Task.Run(()=>NetPeepNative.Sample());if(quitting)return;double at=clock.Elapsed.TotalSeconds,elapsed=lastAt<0?0:at-lastAt;double rx=0,tx=0;
      foreach(var nic in next.interfaces){PeepInterface old;if(previous.TryGetValue(nic.id,out old)&&(ledger.selected=="auto"?nic.physical:nic.id==ledger.selected)&&elapsed>0&&elapsed<15){rx+=Math.Max(0,nic.rx-old.rx);tx+=Math.Max(0,nic.tx-old.tx);}}
      previous.Clear();foreach(var nic in next.interfaces)previous[nic.id]=nic;lastAt=at;down=elapsed>0?rx/elapsed:0;up=elapsed>0?tx/elapsed:0;ledger.Add(rx,tx,DateTime.Now);sample=next;apps=new List<Dictionary<string,object>>();
      foreach(var a in sample.apps)apps.Add(new Dictionary<string,object>{{"pid",a.pid},{"name",a.name},{"connections",a.connections},{"down",a.measured&&elapsed>0&&elapsed<15?(object)(a.rx/elapsed):null},{"up",a.measured&&elapsed>0&&elapsed<15?(object)(a.tx/elapsed):null}});
      observed=DateTime.Now;error="";if(++ticks%5==0)Save();UpdateMeter();Publish();
    }catch(Exception e){error=e.Message;down=up=0;apps.Clear();previous.Clear();lastAt=-1;UpdateMeter();Publish();}
  }
  public object Snapshot(){return new{down=down,up=up,apps=apps,interfaces=sample.interfaces,status=sample.status??"Connecting to native counters…",error=error,observedAt=new DateTimeOffset(observed).ToUnixTimeMilliseconds(),periods=new{day=ledger.Totals("day",DateTime.Now),week=ledger.Totals("week",DateTime.Now),month=ledger.Totals("month",DateTime.Now),all=ledger.Totals("all",DateTime.Now)},since=ledger.since,selected=ledger.selected,compact=ledger.compact,pinned=ledger.pinned,unit=ledger.unit,theme=ledger.theme,storageError=storageError,platform="win32",appVersion=Version,developer=developer};}
  void Publish(){dashboard.Post(new{type="sample",data=Snapshot()});}
  void ShowDashboard(){if(dashboard.IsDisposed)return;dashboard.Show();dashboard.WindowState=FormWindowState.Normal;dashboard.Activate();}
  void BuildMenu(){var menu=new ContextMenuStrip();menu.Items.Add("Open NetPeep",null,delegate{ShowDashboard();});var show=new ToolStripMenuItem("Show taskbar meter"){Checked=ledger.compact};show.Click+=delegate{ledger.compact=!ledger.compact;Save();UpdateMeter();BuildMenu();Publish();};menu.Items.Add(show);menu.Items.Add(ledger.pinned?"Unlock meter to move":"Pin meter to taskbar",null,delegate{Pin(!ledger.pinned);});menu.Items.Add("Reset taskbar position",null,delegate{ledger.taskbarFraction=null;Save();UpdateMeter();});menu.Items.Add(new ToolStripSeparator());var units=new ToolStripMenuItem("Units");foreach(var value in new[]{"auto","B","KB","MB","GB"}){string chosen=value;var item=new ToolStripMenuItem(value=="auto"?"Auto":value){Checked=ledger.unit==value};item.Click+=delegate{ledger.unit=chosen;Save();UpdateMeter();Publish();BuildMenu();};units.DropDownItems.Add(item);}menu.Items.Add(units);menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Quit NetPeep",null,delegate{Quit();});var old=tray.ContextMenuStrip;tray.ContextMenuStrip=menu;if(old!=null)old.Dispose();}
  void Pin(bool value){if(value&&!ledger.pinned&&sample.taskbar!=null){var b=sample.taskbar.bounds;bool vertical=b.height>b.width;double span=vertical?b.height-meter.Height:b.width-meter.Width;ledger.taskbarFraction=span>0?Math.Max(0,Math.Min(1,(vertical?meter.Top-b.y:meter.Left-b.x)/span)):0;}ledger.pinned=value;Save();UpdateMeter();BuildMenu();Publish();}
  void UpdateMeter(){
    string text="NetPeep: ↓ "+DisplayUnits.Format(down,ledger.unit)+"/s ↑ "+DisplayUnits.Format(up,ledger.unit)+"/s";tray.Text=error.Length>0?"NetPeep: counters unavailable":text.Substring(0,Math.Min(63,text.Length));
    dashboard.ApplyTheme(DarkTheme());
    if(!ledger.compact){meter.Hide();return;}if(sample.taskbar==null){if(ledger.pinned)meter.Hide();return;}
    var bar=sample.taskbar.bounds;var bounds=new Rectangle(bar.x,bar.y,bar.width,bar.height);var screen=Screen.FromRectangle(bounds);var intersection=Rectangle.Intersect(bounds,screen.Bounds);if(ledger.pinned&&Math.Min(intersection.Width,intersection.Height)<16){meter.Hide();return;}
    float scale=TaskbarMeter.TaskbarDpi()/96f;int width=(int)Math.Round(112*scale),height=(int)Math.Round(38*scale);bool vertical=bar.height>bar.width;if(vertical)width=Math.Min(width,bar.width);else height=Math.Min(height,bar.height);
    if(ledger.pinned){double span=vertical?bar.height-height:bar.width-width;var n=sample.taskbar.notification;double suggested=vertical?(n!=null?n.y-bar.y-height-10*scale:span-180*scale):(n!=null?n.x-bar.x-width-12*scale:span-180*scale);int offset=(int)Math.Round(ledger.taskbarFraction.HasValue?ledger.taskbarFraction.Value*span:Math.Max(0,Math.Min(span,suggested)));meter.SetBounds(bar.x+(vertical?(bar.width-width)/2:offset),bar.y+(vertical?offset:(bar.height-height)/2),width,height);}
    else if(!meter.Visible){meter.SetBounds(bar.x+230,bar.y+(bar.height-height)/2,width,height);}
    meter.Pinned=ledger.pinned;meter.DpiScale=scale;meter.Upload=error.Length>0?"—":DisplayUnits.Format(up,ledger.unit)+"/s";meter.Download=error.Length>0?"—":DisplayUnits.Format(down,ledger.unit)+"/s";meter.Light=ledger.theme=="light";meter.Show();meter.Draw();
  }
  bool DarkTheme(){if(ledger.theme=="dark")return true;if(ledger.theme=="light")return false;try{return Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1))==0;}catch{return false;}}
  public void Dispatch(string raw){
    int id=0;try{
      var request=json.Deserialize<Dictionary<string,object>>(raw);id=Convert.ToInt32(request["id"]);string method=Convert.ToString(request["method"]);var values=request["args"] as IEnumerable;object[] args=values==null?new object[0]:values.Cast<object>().ToArray();object result=null;
      switch(method){
        case "snapshot":result=Snapshot();break;
        case "preferences":var prefs=args[0] as Dictionary<string,object>;if(prefs==null)throw new Exception("Invalid preferences");if(prefs.ContainsKey("unit")){string unit=Convert.ToString(prefs["unit"]);if(!new[]{"auto","B","KB","MB","GB"}.Contains(unit))throw new Exception("Invalid unit");ledger.unit=unit;}if(prefs.ContainsKey("theme")){string theme=Convert.ToString(prefs["theme"]);if(!new[]{"system","light","dark"}.Contains(theme))throw new Exception("Invalid theme");ledger.theme=theme;}Save();UpdateMeter();BuildMenu();Publish();result=true;break;
        case "select-interface":string selected=Convert.ToString(args[0]);if(selected!="auto"&&!sample.interfaces.Any(n=>n.id==selected))throw new Exception("Unknown interface");ledger.selected=selected;previous.Clear();lastAt=-1;Save();Publish();break;
        case "compact":ledger.compact=Convert.ToBoolean(args[0]);Save();UpdateMeter();BuildMenu();Publish();break;
        case "pin-meter":Pin(Convert.ToBoolean(args[0]));break;
        case "reset-position":ledger.taskbarFraction=null;Save();UpdateMeter();break;
        case "restart":previous.Clear();lastAt=-1;error="";break;
        case "open":ShowDashboard();break;
        case "external":string key=Convert.ToString(args[0]);if(!new[]{"email","facebook","paypal"}.Contains(key))throw new Exception("Unsupported link");string url=(key=="email"?"mailto:":"")+Convert.ToString(developer[key]);Process.Start(new ProcessStartInfo(url){UseShellExecute=true});break;
        case "copy-donation":Clipboard.SetText(Convert.ToString(developer["bkash"]));result=true;break;
        case "export":using(var save=new SaveFileDialog{FileName="NetPeep-usage.json",Filter="JSON history|*.json"})if(save.ShowDialog(dashboard)==DialogResult.OK){File.WriteAllText(save.FileName,json.Serialize(ledger));result=true;}break;
        default:throw new Exception("Unsupported request");
      }dashboard.Post(new{id=id,value=result});
    }catch(Exception e){dashboard.Post(new{id=id,error=e.Message});}
  }
  void PowerChanged(object sender,PowerModeChangedEventArgs e){if(dashboard.IsDisposed)return;dashboard.BeginInvoke((Action)delegate{previous.Clear();lastAt=-1;if(e.Mode==PowerModes.Suspend){timer.Stop();Save();}if(e.Mode==PowerModes.Resume)timer.Start();});}
  public void Quit(){if(quitting)return;quitting=true;timer.Stop();Save();SystemEvents.PowerModeChanged-=PowerChanged;tray.Visible=false;tray.Dispose();meter.Close();dashboard.AllowClose=true;dashboard.Close();ExitThread();}
  async void RunSmoke(){
    try{
      await Task.Delay(9000);if(!dashboard.Ready)throw new Exception("WebView2 did not initialize");if(sample.interfaces.Count==0||error.Length>0)throw new Exception("Native sampling failed: "+error);
      foreach(string chosen in new[]{"B","KB","MB","GB"}){await dashboard.Js("document.getElementById('unit').value='"+chosen+"';document.getElementById('unit').dispatchEvent(new Event('change'))");await Task.Delay(200);if(ledger.unit!=chosen||!meter.Upload.EndsWith(chosen+"/s"))throw new Exception("Unit selector failed: "+chosen);}
      await dashboard.Js("window.netpeep.setPreferences({unit:'MB',theme:'dark'})");await Task.Delay(300);
      if(ledger.unit!="MB"||ledger.theme!="dark")throw new Exception("Preference bridge failed");
      string theme=await dashboard.Js("document.documentElement.dataset.theme");if(theme!="\"dark\"")throw new Exception("Dark theme did not apply");
      await dashboard.Js("document.querySelector('[data-tab=settings]').click()");if(await dashboard.Js("document.documentElement.scrollHeight<=innerHeight")!="true")throw new Exception("Settings exceed compact window height");await Capture("settings-dark.png");
      await dashboard.Js("window.netpeep.setPreferences({unit:'KB',theme:'light'})");await Task.Delay(200);await dashboard.Js("document.querySelector('[data-tab=overview]').click()");if(await dashboard.Js("document.documentElement.scrollHeight<=innerHeight")!="true")throw new Exception("Overview exceeds compact window height");await Capture("overview-light.png");
      await dashboard.Js("document.querySelector('[data-tab=about]').click()");await Capture("about-light.png");
      string developerText=await dashboard.Js("document.getElementById('developer').textContent");if(!developerText.Contains("MD SOWROVE HOSSAIN"))throw new Exception("Developer info missing");
      string paypal=await dashboard.Js("document.getElementById('paypal').textContent");if(!paypal.Contains("PayPal"))throw new Exception("Donation section missing");
      Pin(false);meter.Left=230;Pin(true);if(Math.Abs(meter.Left-230)>3)throw new Exception("Taskbar position not retained");
      using(var bitmap=meter.RenderBitmap()){if(bitmap.GetPixel(0,0).A!=0)throw new Exception("Meter transparency failed");bitmap.Save(Path.Combine(smokeDir,"taskbar.png"),ImageFormat.Png);}
      dashboard.Close();if(dashboard.IsDisposed||dashboard.Visible)throw new Exception("Close-to-tray failed");
      Save();var restored=json.Deserialize<Ledger>(File.ReadAllText(Path.Combine(dataDir,"usage.json")));Ledger.Validate(restored);if(restored.unit!="KB"||restored.theme!="light")throw new Exception("Persisted settings failed");
      File.WriteAllText(Path.Combine(smokeDir,"result.json"),json.Serialize(new{passed=true,version=Version,interfaces=sample.interfaces.Count,processes=sample.apps.Count,checks=new[]{"native collection","WebView2 bridge","unit selection","light/dark themes","developer profile","donation section","transparent taskbar","saved position","close to tray","persisted settings"}}));Quit();
    }catch(Exception e){File.WriteAllText(Path.Combine(smokeDir,"error.txt"),e.ToString());Environment.ExitCode=1;Quit();}
  }
  async Task Capture(string name){await Task.Delay(300);using(var file=File.Create(Path.Combine(smokeDir,name)))await dashboard.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);}
}

public class Dashboard:Form {
  public readonly WebView2 Browser=new WebView2{Dock=DockStyle.Fill};public bool Ready,AllowClose;readonly PeepContext context;readonly bool smoke;
  public Dashboard(PeepContext ctx,string icon,bool test){context=ctx;smoke=test;Text="NetPeep";ClientSize=new Size(420,530);MinimumSize=new Size(400,520);StartPosition=FormStartPosition.CenterScreen;Icon=new Icon(icon);Controls.Add(Browser);Shown+=Initialize;FormClosing+=delegate(object s,FormClosingEventArgs e){if(!AllowClose){e.Cancel=true;Hide();}};}
  async void Initialize(object sender,EventArgs e){Shown-=Initialize;try{
    string root=AppDomain.CurrentDomain.BaseDirectory;var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NetPeep","WebView2"));await Browser.EnsureCoreWebView2Async(environment);
    var core=Browser.CoreWebView2;core.Settings.AreDevToolsEnabled=smoke;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreBrowserAcceleratorKeysEnabled=false;core.Settings.IsStatusBarEnabled=false;
    core.SetVirtualHostNameToFolderMapping("netpeep.local",root,CoreWebView2HostResourceAccessKind.DenyCors);
    core.NavigationStarting+=delegate(object s,CoreWebView2NavigationStartingEventArgs n){if(!n.Uri.StartsWith("https://netpeep.local/",StringComparison.Ordinal))n.Cancel=true;};core.NewWindowRequested+=delegate(object s,CoreWebView2NewWindowRequestedEventArgs n){n.Handled=true;};
    core.WebMessageReceived+=delegate(object s,CoreWebView2WebMessageReceivedEventArgs m){if(m.Source.StartsWith("https://netpeep.local/",StringComparison.Ordinal))context.Dispatch(m.WebMessageAsJson);};
    core.NavigationCompleted+=delegate{Ready=true;};Browser.Source=new Uri("https://netpeep.local/src/app.html");
  }catch(WebView2RuntimeNotFoundException){Controls.Clear();var label=new Label{Dock=DockStyle.Fill,Text="Microsoft Edge WebView2 Runtime is required for the dashboard.\n\nYour taskbar meter can still run. Install WebView2 from Microsoft's official website, then restart NetPeep.",Padding=new Padding(24)};Controls.Add(label);var button=new Button{Text="Get Microsoft WebView2",Dock=DockStyle.Bottom,Height=40};button.Click+=delegate{Process.Start(new ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/"){UseShellExecute=true});};Controls.Add(button);}
  catch(Exception ex){MessageBox.Show(ex.Message,"Dashboard unavailable");}}
  public void Post(object value){if(Ready&&!IsDisposed)Browser.CoreWebView2.PostWebMessageAsJson(new JavaScriptSerializer{MaxJsonLength=16*1024*1024}.Serialize(value));}
  public Task<string> Js(string script){return Browser.CoreWebView2.ExecuteScriptAsync(script);}
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
  public void ApplyTheme(bool dark){BackColor=dark?Color.FromArgb(18,26,36):Color.FromArgb(243,246,249);if(IsHandleCreated){int value=dark?1:0;DwmSetWindowAttribute(Handle,20,ref value,4);}}
}

public class TaskbarMeter:Form {
  const int WS_EX_LAYERED=0x80000,WS_EX_TRANSPARENT=0x20,WS_EX_NOACTIVATE=0x08000000,WS_EX_TOOLWINDOW=0x80;
  public bool Pinned=true,Light;public float DpiScale=1;public string Upload="—",Download="—";readonly Action pin,open;
  [StructLayout(LayoutKind.Sequential)] struct Point {public int x,y;public Point(int x,int y){this.x=x;this.y=y;}}
  [StructLayout(LayoutKind.Sequential)] struct NativeSize {public int cx,cy;public NativeSize(int x,int y){cx=x;cy=y;}}
  [StructLayout(LayoutKind.Sequential,Pack=1)] struct Blend {public byte operation,flags,alpha,format;}
  [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
  [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
  [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
  [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
  [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
  [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
  [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr dest,ref Point location,ref NativeSize size,IntPtr source,ref Point origin,int key,ref Blend blend,int flags);
  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd,int index);
  [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd,int index,int value);
  [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int cx,int cy,uint flags);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string title);
  [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
  public static uint TaskbarDpi(){uint dpi=GetDpiForWindow(FindWindow("Shell_TrayWnd",null));return dpi==0?96:dpi;}
  public TaskbarMeter(Action pinAction,Action openAction){pin=pinAction;open=openAction;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;Size=new System.Drawing.Size(112,38);}
  protected override bool ShowWithoutActivation{get{return true;}}
  protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=WS_EX_LAYERED|WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE;return p;}}
  protected override void WndProc(ref Message m){if(!Pinned&&m.Msg==0x84){int x=(short)(m.LParam.ToInt64()&0xffff)-Left;m.Result=new IntPtr(x>Width-22*DpiScale?1:2);return;}if(!Pinned&&m.Msg==0xA5){var menu=new ContextMenuStrip();menu.Items.Add("Pin to taskbar",null,delegate{pin();});menu.Items.Add("Open NetPeep",null,delegate{open();});menu.Closed+=delegate{menu.Dispose();};menu.Show(Cursor.Position);return;}base.WndProc(ref m);}
  protected override void OnMouseClick(MouseEventArgs e){if(!Pinned&&e.X>Width-22*DpiScale)pin();base.OnMouseClick(e);}
  public Bitmap RenderBitmap(){
    var bitmap=new Bitmap(Math.Max(1,Width),Math.Max(1,Height),PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);g.ScaleTransform(DpiScale,DpiScale);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;if(!Pinned){using(var b=new SolidBrush(Color.FromArgb(150,24,37,58)))g.FillRectangle(b,0,0,112,38);}
      DrawText(g,"↑",4,2,Color.FromArgb(255,209,92));DrawText(g,"↓",4,18,Color.FromArgb(115,201,255));DrawText(g,Upload,16,2,Light?Color.FromArgb(30,45,60):Color.White);DrawText(g,Download,16,18,Light?Color.FromArgb(30,45,60):Color.White);if(!Pinned)DrawText(g,"✓",94,10,Color.FromArgb(130,212,255));
    }return bitmap;
  }
  void DrawText(Graphics g,string text,float x,float y,Color color){using(var family=new FontFamily("Segoe UI"))using(var path=new GraphicsPath()){path.AddString(text,family,(int)FontStyle.Regular,12,new PointF(x,y),StringFormat.GenericTypographic);using(var stroke=new Pen(Light?Color.FromArgb(120,255,255,255):Color.FromArgb(180,0,0,0),2))g.DrawPath(stroke,path);using(var brush=new SolidBrush(color))g.FillPath(brush,path);}}
  public void Draw(){if(!Visible)return;int style=GetWindowLong(Handle,-20);SetWindowLong(Handle,-20,Pinned?style|WS_EX_TRANSPARENT:style&~WS_EX_TRANSPARENT);using(var image=RenderBitmap()){
    IntPtr screen=GetDC(IntPtr.Zero),dc=CreateCompatibleDC(screen),bitmap=image.GetHbitmap(Color.FromArgb(0)),old=SelectObject(dc,bitmap);
    try{var location=new Point(Left,Top);var size=new NativeSize(Width,Height);var origin=new Point(0,0);var blend=new Blend{operation=0,flags=0,alpha=255,format=1};UpdateLayeredWindow(Handle,screen,ref location,ref size,dc,ref origin,0,ref blend,2);SetWindowPos(Handle,new IntPtr(-1),Left,Top,Width,Height,0x0010|0x0040);}finally{SelectObject(dc,old);DeleteObject(bitmap);DeleteDC(dc);ReleaseDC(IntPtr.Zero,screen);}
  }}
}
