using System;

namespace EdgeRail {
    public enum Edge { None, Left, Right, Top, Bottom }
    public enum RailAction { Off, Volume, Brightness, Tabs, Zoom, Tracks, Desktops, UndoRedo, Adaptive }
    public sealed class GestureEngine {
        public double Band = .085;
        public double Step = .055;
        public Edge Active { get; private set; }
        double anchor;
        bool blocked;
        public void Reset() { Active = Edge.None; blocked = false; }
        public static Edge Detect(double x, double y, double band) {
            // Corners are intentionally excluded: never choose two rails at once.
            if (x < band && y >= band && y <= 1-band) return Edge.Left;
            if (x > 1-band && y >= band && y <= 1-band) return Edge.Right;
            if (y < band && x >= band && x <= 1-band) return Edge.Top;
            if (y > 1-band && x >= band && x <= 1-band) return Edge.Bottom;
            return Edge.None;
        }
        public int Update(double x, double y, int fingers, bool down) {
            if (!down || fingers == 0) { Reset(); return 0; }
            if (fingers != 1 || x<0 || x>1 || y<0 || y>1) {
                Active=Edge.None; blocked=true; return 0;
            }
            if (blocked) return 0;
            if (Active == Edge.None) {
                Active=Detect(x,y,Band);
                if (Active==Edge.None) blocked=true;
                anchor=Axis(x,y); return 0;
            }
            // A contact that leaves its starting rail must lift before trying again.
            if ((Active==Edge.Left && x>Band*2) || (Active==Edge.Right && x<1-Band*2) ||
                (Active==Edge.Top && y>Band*2) || (Active==Edge.Bottom && y<1-Band*2)) {
                Active=Edge.None; blocked=true; return 0;
            }
            double delta=Axis(x,y)-anchor;
            if (Math.Abs(delta)<Step) return 0;
            int direction=delta>0?1:-1;
            // One action per report avoids bursts after device stalls.
            anchor=Axis(x,y); return direction;
        }
        double Axis(double x,double y) { return Active==Edge.Left || Active==Edge.Right ? 1-y : x; }
    }
}
