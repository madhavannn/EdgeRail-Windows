using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace EdgeRail {
    // Read-only Raw Input + the Windows HID parser; no device handles or vendor driver.
    public sealed class TouchpadReader : IDisposable {
        const int Success=0x00110000;
        [StructLayout(LayoutKind.Sequential)] struct DEVICE { public ushort page,usage; public uint flags; public IntPtr target; }
        [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterRawInputDevices(DEVICE[] dev,uint count,uint size);
        [DllImport("user32.dll",SetLastError=true)] static extern uint GetRawInputData(IntPtr raw,uint command,IntPtr data,ref uint size,uint header);
        [DllImport("user32.dll",EntryPoint="GetRawInputDeviceInfoW",SetLastError=true)] static extern uint GetRawInputDeviceInfo(IntPtr device,uint cmd,IntPtr data,ref uint size);
        [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr prep,IntPtr caps);
        [DllImport("hid.dll")] static extern int HidP_GetValueCaps(int type,IntPtr caps,ref ushort count,IntPtr prep);
        [DllImport("hid.dll")] static extern int HidP_GetButtonCaps(int type,IntPtr caps,ref ushort count,IntPtr prep);
        [DllImport("hid.dll")] static extern int HidP_GetUsageValue(int type,ushort page,ushort link,ushort usage,out uint value,IntPtr prep,byte[] report,uint length);
        [DllImport("hid.dll")] static extern int HidP_GetUsages(int type,ushort page,ushort link,[Out] ushort[] list,ref uint length,IntPtr prep,byte[] report,uint size);
        class Value { public ushort page,link,usage; public byte report; public int min,max; }
        class Contact { public Value x,y; public ushort link; public byte report; public bool confidence; }
        class Descriptor : IDisposable {
            public IntPtr prep; public Value count; public List<Contact> contacts=new List<Contact>();
            public bool valid;
            public void Dispose() { if(prep!=IntPtr.Zero) { Marshal.FreeHGlobal(prep); prep=IntPtr.Zero; } }
        }
        readonly Dictionary<IntPtr,Descriptor> devices=new Dictionary<IntPtr,Descriptor>();
        IntPtr currentDevice;
        public bool Registered { get; private set; }
        public int Packets { get; private set; }
        public int Decoded { get; private set; }
        public string Status { get; private set; }
        public event Action<double,double,int,bool> ContactChanged;
        public TouchpadReader(IntPtr window) {
            Registered=RegisterRawInputDevices(new[]{new DEVICE {page=0x0D,usage=5,flags=0x2100,target=window}},1,(uint)Marshal.SizeOf(typeof(DEVICE)));
            Status=Registered?"Waiting for compatible touchpad data":"Touchpad registration unavailable ("+Marshal.GetLastWin32Error()+")";
        }
        static ushort U16(IntPtr p,int n) { return unchecked((ushort)Marshal.ReadInt16(p,n)); }
        Descriptor Load(IntPtr handle) {
            var d=new Descriptor();
            try {
                uint size=0;
                if(GetRawInputDeviceInfo(handle,0x20000005,IntPtr.Zero,ref size)==uint.MaxValue || size==0 || size>1024*1024) return d;
                d.prep=Marshal.AllocHGlobal((int)size);
                if(GetRawInputDeviceInfo(handle,0x20000005,d.prep,ref size)==uint.MaxValue) return d;
                IntPtr caps=Marshal.AllocHGlobal(64);
                ushort values,buttons;
                try {
                    if(HidP_GetCaps(d.prep,caps)!=Success || U16(caps,0)!=5 || U16(caps,2)!=0x0D) return d;
                    buttons=U16(caps,46); values=U16(caps,48);
                } finally { Marshal.FreeHGlobal(caps); }
                if(values>1024 || buttons>1024) return d;
                var list=new List<Value>();
                IntPtr buffer=Marshal.AllocHGlobal(Math.Max(1,(int)values)*72);
                try {
                    if(HidP_GetValueCaps(0,buffer,ref values,d.prep)!=Success) return d;
                    for(int i=0;i<values;i++) {
                        IntPtr p=IntPtr.Add(buffer,i*72);
                        // Ranged X/Y values cannot be treated as one scalar safely.
                        if(Marshal.ReadByte(p,12)!=0) continue;
                        list.Add(new Value {page=U16(p,0),report=Marshal.ReadByte(p,2),link=U16(p,6),usage=U16(p,56),min=Marshal.ReadInt32(p,40),max=Marshal.ReadInt32(p,44)});
                    }
                } finally { Marshal.FreeHGlobal(buffer); }
                var tip=new Dictionary<ushort,byte>();
                var confidence=new HashSet<ushort>();
                buffer=Marshal.AllocHGlobal(Math.Max(1,(int)buttons)*72);
                try {
                    if(buttons>0 && HidP_GetButtonCaps(0,buffer,ref buttons,d.prep)==Success) {
                        for(int i=0;i<buttons;i++) {
                            IntPtr p=IntPtr.Add(buffer,i*72);
                            if(U16(p,0)!=0x0D) continue;
                            ushort lo=U16(p,56),hi=Marshal.ReadByte(p,12)!=0?U16(p,58):lo,link=U16(p,6);
                            if(lo<=0x42 && hi>=0x42) tip[link]=Marshal.ReadByte(p,2);
                            if(lo<=0x47 && hi>=0x47) confidence.Add(link);
                        }
                    }
                } finally { Marshal.FreeHGlobal(buffer); }
                foreach(Value v in list) {
                    if(v.page==0x0D && v.usage==0x54) d.count=v;
                    if(v.page!=1 || v.usage!=0x30 || v.max<=v.min || !tip.ContainsKey(v.link)) continue;
                    Value y=list.Find(w=>w.page==1 && w.usage==0x31 && w.link==v.link && w.report==v.report && w.max>w.min);
                    if(y!=null && tip[v.link]==v.report) d.contacts.Add(new Contact{x=v,y=y,link=v.link,report=v.report,confidence=confidence.Contains(v.link)});
                }
                // Require an explicit total contact count to reject multi-finger frames.
                d.valid=d.count!=null && d.contacts.Count>0;
                return d;
            } catch { return d; }
        }
        static bool Read(Descriptor d,Value v,byte[] report,out uint result) {
            result=0;
            return v!=null && (v.report==0 || report[0]==v.report) && HidP_GetUsageValue(0,v.page,v.link,v.usage,out result,d.prep,report,(uint)report.Length)==Success;
        }
        void Emit(double x,double y,int n,bool down) { var e=ContactChanged; if(e!=null)e(x,y,n,down); }
        public void ClearDevices() { foreach(var d in devices.Values)d.Dispose(); devices.Clear();currentDevice=IntPtr.Zero; Emit(0,0,0,false); }
        public void Process(IntPtr input) {
            uint header=(uint)(8+IntPtr.Size*2),size=0;
            if(GetRawInputData(input,0x10000003,IntPtr.Zero,ref size,header)==uint.MaxValue || size<header+8 || size>1024*1024) return;
            IntPtr buffer=Marshal.AllocHGlobal((int)size);
            try {
                if(GetRawInputData(input,0x10000003,buffer,ref size,header)==uint.MaxValue || Marshal.ReadInt32(buffer)!=2)return;
                IntPtr handle=Marshal.ReadIntPtr(buffer,8);
                if(currentDevice!=handle) { Emit(0,0,0,false);currentDevice=handle; }
                int reportSize=Marshal.ReadInt32(buffer,(int)header),count=Marshal.ReadInt32(buffer,(int)header+4);
                if(reportSize<1 || count<1 || (long)reportSize*count+header+8>size)return;
                Descriptor d;
                if(!devices.TryGetValue(handle,out d)) { d=Load(handle); devices.Add(handle,d); }
                Packets++;
                if(!d.valid) { Status="Touchpad reports received; layout unsupported. Use compatibility mode."; return; }
                for(int i=0;i<count;i++) {
                    byte[] report=new byte[reportSize];
                    Marshal.Copy(IntPtr.Add(buffer,(int)header+8+i*reportSize),report,0,reportSize);
                    uint fingers;
                    if(!Read(d,d.count,report,out fingers)) { Emit(0,0,2,true); continue; }
                    if(fingers!=1) { Emit(0,0,(int)Math.Min(fingers,10),fingers!=0); continue; }
                    bool found=false;
                    foreach(Contact c in d.contacts) {
                        if(c.report!=0 && report[0]!=c.report)continue;
                        ushort[] usages=new ushort[64]; uint n=64;
                        if(HidP_GetUsages(0,0x0D,c.link,usages,ref n,d.prep,report,(uint)report.Length)!=Success)continue;
                        bool down=false,confident=!c.confidence;
                        for(int j=0;j<n;j++) { if(usages[j]==0x42)down=true; if(usages[j]==0x47)confident=true; }
                        uint x,y;
                        if(!down || !confident || !Read(d,c.x,report,out x) || !Read(d,c.y,report,out y))continue;
                        double nx=((double)x-c.x.min)/(c.x.max-c.x.min),ny=((double)y-c.y.min)/(c.y.max-c.y.min);
                        Decoded++; Status="Native touchpad detected - single-finger coordinates received";
                        Emit(nx,ny,1,true); found=true; break;
                    }
                    if(!found) Emit(0,0,0,false);
                }
            } catch { Status="Touchpad report could not be decoded. Use compatibility mode."; Emit(0,0,2,true); }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        public void Dispose() {
            if(Registered)RegisterRawInputDevices(new[]{new DEVICE {page=0x0D,usage=5,flags=1,target=IntPtr.Zero}},1,(uint)Marshal.SizeOf(typeof(DEVICE)));
            ClearDevices();
        }
    }
}
