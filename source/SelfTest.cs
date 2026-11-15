using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace EdgeRail {
    internal static class SelfTest {
        static readonly List<string> results=new List<string>();
        static void Check(string name,bool passed) {results.Add((passed?"PASS ":"FAIL ")+name);}
        public static int Run(string path) {
            results.Clear();
            try {
                var g=new GestureEngine();
                Check("corner is excluded",GestureEngine.Detect(.01,.01,.08)==Edge.None);
                g.Update(.99,.6,1,true);Check("right edge starts",g.Active==Edge.Right);
                Check("up increases",g.Update(.99,.5,1,true)==1);
                Check("down decreases",g.Update(.99,.65,1,true)==-1);
                Check("noise is ignored",g.Update(.99,.64,1,true)==0);
                g.Update(0,0,0,false);Check("lift resets",g.Active==Edge.None);
                g.Update(.5,.5,1,true);g.Update(.99,.5,1,true);Check("center contact cannot enter rail",g.Active==Edge.None);
                g.Reset();g.Update(.99,.5,1,true);g.Update(.99,.4,2,true);Check("two fingers cancel",g.Active==Edge.None);
                Check("multi-finger rejection persists",g.Update(.99,.2,1,true)==0 && g.Active==Edge.None);
                g.Update(0,0,0,false);g.Update(.5,.01,1,true);Check("top edge starts",g.Active==Edge.Top);
                Check("right increases",g.Update(.6,.01,1,true)==1);
                g.Update(.6,.5,1,true);Check("leaving rail cancels",g.Active==Edge.None);
                Check("leaving rail requires lift",g.Update(.8,.01,1,true)==0 && g.Active==Edge.None);
                g.Reset();g.Update(.01,.6,1,true);Check("left up increases",g.Update(.01,.5,1,true)==1);
                g.Reset();g.Update(.6,.99,1,true);Check("bottom left decreases",g.Update(.4,.99,1,true)==-1);
                g.Reset();g.Update(-1,.5,1,true);Check("out of range rejected",g.Active==Edge.None);
                g.Reset();g.Update(.99,.8,1,true);Check("stalled report produces one step",g.Update(.99,.1,1,true)==1);
                Check("browser adaptive mapping",ActionRunner.Adaptive("chrome")==RailAction.Tabs);
                Check("music adaptive mapping",ActionRunner.Adaptive("spotify")==RailAction.Tracks);
                Check("default adaptive mapping",ActionRunner.Adaptive("explorer")==RailAction.Desktops);
                var settings=new Settings {Top=RailAction.Zoom,Bottom=RailAction.Off,Mode=1,Width=12,Sensitivity=4};
                var xml=new XmlSerializer(typeof(Settings));Settings copy;
                using(var stream=new MemoryStream()){xml.Serialize(stream,settings);stream.Position=0;copy=(Settings)xml.Deserialize(stream);}
                Check("settings XML roundtrip",copy.Top==RailAction.Zoom && copy.Bottom==RailAction.Off && copy.Mode==1 && copy.Width==12 && copy.Sensitivity==4);
                Check("Win32 INPUT ABI",Marshal.SizeOf(typeof(Native.INPUT))==(IntPtr.Size==8?40:28));
            } catch(Exception ex) {results.Add("FAIL unexpected exception: "+ex);}
            bool failed=results.Exists(s=>s.StartsWith("FAIL"));
            results.Add(failed?"FAILED":"ALL TESTS PASSED");
            if(path!=null)File.WriteAllLines(path,results.ToArray());
            return failed?1:0;
        }
    }
}
