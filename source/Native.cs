using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace EdgeRail {
    internal static class Native {
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);
        [DllImport("user32.dll")] internal static extern uint SendInput(uint count, INPUT[] inputs,int size);
        [StructLayout(LayoutKind.Sequential)] internal struct MOUSEINPUT { public int x,y; public uint data,flags,time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] internal struct KEYBDINPUT { public ushort key,scan; public uint flags,time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Explicit)] internal struct UNION { [FieldOffset(0)] public MOUSEINPUT mouse; [FieldOffset(0)] public KEYBDINPUT key; }
        [StructLayout(LayoutKind.Sequential)] internal struct INPUT { public uint type; public UNION data; }
        static INPUT Key(int key,bool up) {
            bool extended=(key>=0x21 && key<=0x2E) || (key>=0xA6 && key<=0xB7) || key==0xA3 || key==0xA5 || key==0x5B || key==0x5C;
            return new INPUT { type=1, data=new UNION { key=new KEYBDINPUT { key=(ushort)key,flags=(up?2u:0u)|(extended?1u:0u) } } };
        }
        internal static void Chord(int[] mods,int key) {
            var seq=new List<INPUT>();
            // Preserve physically-held left/right modifiers around injected shortcuts.
            int[] modifiers={0xA0,0xA1,0xA2,0xA3,0xA4,0xA5,0x5B,0x5C};
            var held=new List<int>();
            foreach(int m in modifiers) if((GetAsyncKeyState(m)&0x8000)!=0) held.Add(m);
            if(held.Count>0) { seq.Add(Key(0xFF,false)); seq.Add(Key(0xFF,true)); }
            foreach(int m in held) seq.Add(Key(m,true));
            foreach(int m in mods) seq.Add(Key(m,false));
            seq.Add(Key(key,false)); seq.Add(Key(key,true));
            for(int i=mods.Length-1;i>=0;i--) seq.Add(Key(mods[i],true));
            foreach(int m in held) seq.Add(Key(m,false));
            INPUT[] inputs=seq.ToArray();
            if(SendInput((uint)inputs.Length,inputs,Marshal.SizeOf(typeof(INPUT)))!=inputs.Length)
                throw new InvalidOperationException("Windows blocked the shortcut. Use the target app at the same permission level.");
        }
    }
}
