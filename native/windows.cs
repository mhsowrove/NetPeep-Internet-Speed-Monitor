using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;

public class PeepInterface { public string id,name; public bool physical; public long rx,tx; }
public class PeepApp { public int pid,connections; public string name; public long rx,tx; public bool measured; }
public class PeepRect { public int x,y,width,height; }
public class PeepTaskbar { public PeepRect bounds,notification; }
public class PeepSample { public List<PeepInterface> interfaces=new List<PeepInterface>(); public List<PeepApp> apps=new List<PeepApp>(); public string status; public bool elevated; public PeepTaskbar taskbar; }
public static class NetPeepNative {
  [StructLayout(LayoutKind.Sequential)] struct Rect { public int left,top,right,bottom; }
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string name);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string name);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
  [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
  static PeepRect ToRect(Rect r) { return new PeepRect{x=r.left,y=r.top,width=r.right-r.left,height=r.bottom-r.top}; }
  static PeepTaskbar Taskbar() {
    IntPtr old=SetThreadDpiAwarenessContext(new IntPtr(-4));
    try { IntPtr hwnd=FindWindow("Shell_TrayWnd",null); Rect r,n; if(hwnd==IntPtr.Zero || !GetWindowRect(hwnd,out r))return null;
      var result=new PeepTaskbar{bounds=ToRect(r)}; IntPtr notify=FindWindowEx(hwnd,IntPtr.Zero,"TrayNotifyWnd",null);
      if(notify!=IntPtr.Zero && GetWindowRect(notify,out n))result.notification=ToRect(n);return result;
    } finally { if(old!=IntPtr.Zero)SetThreadDpiAwarenessContext(old); }
  }
  [StructLayout(LayoutKind.Sequential)] struct Row { public uint state,localAddr,localPort,remoteAddr,remotePort; }
  [StructLayout(LayoutKind.Sequential)] struct OwnerRow { public uint state,localAddr,localPort,remoteAddr,remotePort,pid; }
  [DllImport("iphlpapi.dll")] static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);
  [DllImport("iphlpapi.dll")] static extern uint SetPerTcpConnectionEStats(ref Row row,int type,byte[] rw,uint version,uint size,uint offset);
  [DllImport("iphlpapi.dll")] static extern uint GetPerTcpConnectionEStats(ref Row row,int type,byte[] rw,uint rwVersion,uint rwSize,IntPtr ros,uint rosVersion,uint rosSize,byte[] rod,uint rodVersion,uint rodSize);
  static Dictionary<string,long[]> previous=new Dictionary<string,long[]>();
  public static PeepSample Sample() {
    PeepSample result=new PeepSample();
    result.taskbar=Taskbar();
    result.elevated=new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    foreach(NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces()) {
      if(nic.OperationalStatus!=OperationalStatus.Up || nic.NetworkInterfaceType==NetworkInterfaceType.Loopback) continue;
      try { var s=nic.GetIPStatistics(); string desc=(nic.Name+" "+nic.Description).ToLowerInvariant();
        bool physical=(nic.NetworkInterfaceType==NetworkInterfaceType.Ethernet || nic.NetworkInterfaceType==NetworkInterfaceType.Wireless80211) && !desc.Contains("virtual") && !desc.Contains("vpn") && !desc.Contains("tap-") && !desc.Contains("tunnel") && !desc.Contains("hyper-v");
        result.interfaces.Add(new PeepInterface{id=nic.Id,name=nic.Name,physical=physical,rx=s.BytesReceived,tx=s.BytesSent});
      } catch(NetworkInformationException) {}
    }
    Dictionary<int,PeepApp> apps=new Dictionary<int,PeepApp>(); var next=new Dictionary<string,long[]>(); int size=0;
    GetExtendedTcpTable(IntPtr.Zero,ref size,false,2,5,0); IntPtr ptr=Marshal.AllocHGlobal(Math.Max(size,4));
    try {
      uint error=GetExtendedTcpTable(ptr,ref size,false,2,5,0);
      if(error!=0) { result.status="TCP table unavailable ("+error+")"; return result; }
      int count=Marshal.ReadInt32(ptr); int rowSize=Marshal.SizeOf(typeof(OwnerRow));
      for(int i=0;i<count;i++) {
        OwnerRow o=(OwnerRow)Marshal.PtrToStructure(IntPtr.Add(ptr,4+i*rowSize),typeof(OwnerRow));
        if(o.state!=5) continue;
        int pid=(int)o.pid; PeepApp a;
        if(!apps.TryGetValue(pid,out a)) { string name="Process "+pid; try{name=Process.GetProcessById(pid).ProcessName;}catch(ArgumentException){}catch(System.ComponentModel.Win32Exception){} a=new PeepApp{pid=pid,name=name}; apps[pid]=a; }
        a.connections++;
        if(!result.elevated) continue;
        Row r=new Row{state=o.state,localAddr=o.localAddr,localPort=o.localPort,remoteAddr=o.remoteAddr,remotePort=o.remotePort};
        string key=pid+":"+o.localAddr+":"+o.localPort+":"+o.remoteAddr+":"+o.remotePort;
        byte[] rw=new byte[1]; byte[] rod=new byte[96];
        uint code=GetPerTcpConnectionEStats(ref r,1,rw,0,1,IntPtr.Zero,0,0,rod,0,96);
        if(code!=0 || rw[0]==0) { rw[0]=1; SetPerTcpConnectionEStats(ref r,1,rw,0,1,0); continue; }
        long tx=(long)BitConverter.ToUInt64(rod,0), rx=(long)BitConverter.ToUInt64(rod,16); next[key]=new long[]{rx,tx}; long[] old;
        if(previous.TryGetValue(key,out old)) { a.rx+=Math.Max(0,rx-old[0]); a.tx+=Math.Max(0,tx-old[1]); a.measured=true; }
      }
    } finally { Marshal.FreeHGlobal(ptr); }
    previous=next; result.apps.AddRange(apps.Values);
    result.status=result.elevated ? "IPv4 TCP payload samples only · UDP/QUIC, IPv6 and short-lived connections excluded" : "Connection activity only · launch as administrator for partial IPv4 TCP byte counts";
    return result;
  }
}
