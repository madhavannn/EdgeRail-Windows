using System;
using System.Diagnostics;
using System.Management;
using System.Threading;

namespace EdgeRail {
    public sealed class ActionRunner {
        int brightnessBusy;
        public Action<string> Notify;
        void Say(string message) { if(Notify!=null)Notify(message); }
        public static string ForegroundProcess() {
            try { uint pid; Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out pid); return Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant(); }
            catch { return ""; }
        }
        public static RailAction Adaptive(string name) {
            if(name=="chrome" || name=="msedge" || name=="firefox" || name=="brave" || name=="opera" || name=="vivaldi")return RailAction.Tabs;
            if(name=="spotify" || name=="wmplayer" || name=="vlc" || name=="music.ui" || name=="zuneMusic".ToLowerInvariant())return RailAction.Tracks;
            if(name=="figma" || name=="acrord32" || name=="acrobat" || name=="photos")return RailAction.Zoom;
            return RailAction.Desktops;
        }
        public void Run(RailAction action,int direction) {
            if(action==RailAction.Off)return;
            if(action==RailAction.Adaptive)action=Adaptive(ForegroundProcess());
            try {
                if(action==RailAction.Brightness) { Brightness(direction*5); return; }
                if(action==RailAction.Volume)Native.Chord(new int[0],direction>0?0xAF:0xAE);
                else {
                    if(ForegroundProcess()==Process.GetCurrentProcess().ProcessName.ToLowerInvariant()) { Say("Focus another app to use "+action+"."); return; }
                    switch(action) {
                        case RailAction.Tabs: Native.Chord(direction>0?new[]{0x11}:new[]{0x11,0x10},0x09);break;
                        case RailAction.Zoom: Native.Chord(new[]{0x11},direction>0?0xBB:0xBD);break;
                        case RailAction.Tracks: Native.Chord(new int[0],direction>0?0xB0:0xB1);break;
                        case RailAction.Desktops: Native.Chord(new[]{0x11,0x5B},direction>0?0x27:0x25);break;
                        case RailAction.UndoRedo: Native.Chord(new[]{0x11},direction>0?0x59:0x5A);break;
                    }
                }
                Say(action+"  "+(direction>0?"+":"-"));
            } catch(Exception ex) { Say(ex.Message); }
        }
        void Brightness(int delta) {
            if(Interlocked.CompareExchange(ref brightnessBusy,1,0)!=0)return;
            ThreadPool.QueueUserWorkItem(delegate {
                try {
                    bool changed=false;
                    var values=new System.Collections.Generic.Dictionary<string,int>();
                    var options=new EnumerationOptions { Timeout=TimeSpan.FromSeconds(2),ReturnImmediately=false };
                    using(var query=new ManagementObjectSearcher(new ManagementScope(@"root\WMI"),new ObjectQuery("SELECT InstanceName, CurrentBrightness FROM WmiMonitorBrightness WHERE Active=TRUE"),options))
                    using(var found=query.Get()) foreach(ManagementObject b in found) using(b) { values[Convert.ToString(b["InstanceName"])]=Convert.ToInt32(b["CurrentBrightness"]); }
                    using(var query=new ManagementObjectSearcher(new ManagementScope(@"root\WMI"),new ObjectQuery("SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=TRUE"),options))
                    using(var found=query.Get()) foreach(ManagementObject m in found) using(m) {
                        int current;
                        if(!values.TryGetValue(Convert.ToString(m["InstanceName"]),out current))continue;
                        int target=Math.Max(0,Math.Min(100,current+delta));
                        m.InvokeMethod("WmiSetBrightness",new object[]{1u,(byte)target}); changed=true;
                    }
                    Say(changed?"Brightness "+(delta>0?"+5%":"-5%"):"This display does not expose Windows brightness control.");
                } catch { Say("Brightness unavailable. Internal displays with WMI support are required."); }
                finally { Interlocked.Exchange(ref brightnessBusy,0); }
            });
        }
    }
}
