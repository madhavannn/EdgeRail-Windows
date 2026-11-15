using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Threading;
using System.Diagnostics;

namespace EdgeRail {
    public class Settings {
        public int Mode=0;
        public int Width=9;
        public int Sensitivity=6;
        public RailAction Left=RailAction.Brightness,Right=RailAction.Volume,Top=RailAction.Adaptive,Bottom=RailAction.Tracks;
        public RailAction For(Edge edge) { switch(edge) { case Edge.Left:return Left;case Edge.Right:return Right;case Edge.Top:return Top;case Edge.Bottom:return Bottom;default:return RailAction.Off; } }
        public static string PathName { get {return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EdgeRail","settings.xml");} }
        public static string LoadWarning="";
        public static Settings Load() {
            try { if(File.Exists(PathName)) using(var f=File.OpenRead(PathName)) {
                Settings s=(Settings)new XmlSerializer(typeof(Settings)).Deserialize(f);
                s.Mode=Math.Max(0,Math.Min(2,s.Mode)); s.Width=Math.Max(4,Math.Min(16,s.Width));s.Sensitivity=Math.Max(3,Math.Min(12,s.Sensitivity));
                if(!Enum.IsDefined(typeof(RailAction),s.Left) || !Enum.IsDefined(typeof(RailAction),s.Right) || !Enum.IsDefined(typeof(RailAction),s.Top) || !Enum.IsDefined(typeof(RailAction),s.Bottom))throw new InvalidDataException();
                return s;
            } } catch { LoadWarning="Saved settings could not be loaded. Defaults are in use."; }
            return new Settings();
        }
        public void Save() {
            Directory.CreateDirectory(Path.GetDirectoryName(PathName));
            string temp=PathName+".tmp";
            using(var f=File.Create(temp))new XmlSerializer(typeof(Settings)).Serialize(f,this);
            if(File.Exists(PathName))File.Replace(temp,PathName,null);else File.Move(temp,PathName);
        }
    }

    internal sealed class RailCombo : ComboBox {
        public RailCombo() {
            DrawMode=DrawMode.OwnerDrawFixed;DropDownStyle=ComboBoxStyle.DropDownList;FlatStyle=FlatStyle.Flat;
            BackColor=Color.FromArgb(31,43,60);ForeColor=Color.FromArgb(229,237,247);ItemHeight=23;
        }
        protected override void OnDrawItem(DrawItemEventArgs e) {
            using(var b=new SolidBrush((e.State&DrawItemState.Selected)!=0?Color.FromArgb(45,83,86):BackColor))e.Graphics.FillRectangle(b,e.Bounds);
            string text=e.Index>=0 && e.Index<Items.Count?GetItemText(Items[e.Index]):Text;
            TextRenderer.DrawText(e.Graphics,text,Font,new Rectangle(e.Bounds.X+7,e.Bounds.Y,e.Bounds.Width-10,e.Bounds.Height),ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        }
    }
    internal sealed class Preview : Control {
        public GestureEngine Engine=new GestureEngine();
        public Settings Settings;
        public string Caption="Drag from an edge to try a gesture. Preview only.";
        bool dragging;
        PointF finger;
        public Preview() { DoubleBuffered=true; BackColor=Color.FromArgb(21,29,42); Cursor=Cursors.Hand; }
        protected override void OnMouseDown(MouseEventArgs e) { if(e.Button!=MouseButtons.Left)return;dragging=true;Capture=true;Engine.Reset();UpdateFinger(e);base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if(dragging)UpdateFinger(e);base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { dragging=false;Capture=false;Engine.Reset();Invalidate();base.OnMouseUp(e); }
        protected override void OnMouseCaptureChanged(EventArgs e) { if(!Capture) { dragging=false;Engine.Reset();Invalidate(); }base.OnMouseCaptureChanged(e); }
        void UpdateFinger(MouseEventArgs e) {
            float x=(e.X-28f)/(Width-56),y=(e.Y-24f)/(Height-74);
            finger=new PointF(e.X,e.Y);
            int d=Engine.Update(x,y,1,true);
            if(d!=0)Caption=Engine.Active+" / "+Settings.For(Engine.Active)+" "+(d>0?"+":"-")+"   - preview only";
            Invalidate();
        }
        public void Pulse(Edge edge,string text) { Caption=text;Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            Rectangle box=new Rectangle(28,24,Width-56,Height-74);int band=Math.Max(9,(int)(Math.Min(box.Width,box.Height)*Engine.Band));
            using(var b=new SolidBrush(Color.FromArgb(31,43,60)))g.FillRectangle(b,box);
            using(var p=new Pen(Color.FromArgb(64,84,110),1))g.DrawRectangle(p,box);
            Fill(g,new Rectangle(box.Left,box.Top+band,band,box.Height-2*band),Edge.Left);
            Fill(g,new Rectangle(box.Right-band,box.Top+band,band,box.Height-2*band),Edge.Right);
            Fill(g,new Rectangle(box.Left+band,box.Top,box.Width-2*band,band),Edge.Top);
            Fill(g,new Rectangle(box.Left+band,box.Bottom-band,box.Width-2*band,band),Edge.Bottom);
            using(var font=new Font("Segoe UI",11))using(var b=new SolidBrush(Color.FromArgb(159,183,210))) {
                var fmt=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
                g.DrawString("START AT AN EDGE\nSlide up / down or left / right",font,b,box,fmt);
            }
            if(dragging)using(var b=new SolidBrush(Color.FromArgb(133,231,198)))g.FillEllipse(b,finger.X-6,finger.Y-6,12,12);
            using(var b=new SolidBrush(Color.FromArgb(170,188,210)))g.DrawString(Caption,Font,b,new RectangleF(16,Height-35,Width-32,32));
        }
        void Fill(Graphics g,Rectangle rect,Edge edge) {using(var b=new SolidBrush(Engine.Active==edge?Color.FromArgb(125,231,193):Color.FromArgb(56,117,117)))g.FillRectangle(b,rect);}
    }

    internal sealed class MainForm : Form {
        Settings settings;
        TouchpadReader reader;
        GestureEngine native=new GestureEngine(),pointer=new GestureEngine();
        ActionRunner runner=new ActionRunner();
        System.Windows.Forms.Timer timer;
        NotifyIcon tray;
        ToolStripMenuItem pauseMenu;
        Label status,deviceStatus,stateLabel;
        Button pause;
        ComboBox mode;
        Preview preview;
        bool paused,quitting,initializing=true;
        readonly bool testMode;
        Edge pointerEdge;
        Rectangle pointerBounds;
        long lastAction;
        readonly Stopwatch clock=Stopwatch.StartNew();
        long lastNative;
        public MainForm(bool testing) {
            testMode=testing;
            settings=testing?new Settings():Settings.Load();
            Text="EdgeRail for Windows";ClientSize=new Size(900,690);MinimumSize=new Size(800,680);StartPosition=FormStartPosition.CenterScreen;
            Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(13,20,31);ForeColor=Color.FromArgb(229,237,247);AutoScaleMode=AutoScaleMode.Dpi;
            BuildUI();
            runner.Notify=delegate(string s) { if(IsDisposed || !IsHandleCreated)return;try { BeginInvoke((Action)(()=>{status.Text=s;})); }catch(InvalidOperationException){} };
            if(!testing) {
                reader=new TouchpadReader(Handle);reader.ContactChanged+=OnContact;
                tray=new NotifyIcon {Icon=SystemIcons.Application,Text="EdgeRail - active",Visible=true};
                var menu=new ContextMenuStrip();menu.Items.Add("Open settings",null,delegate {OpenSettings();});
                pauseMenu=new ToolStripMenuItem("Pause",null,delegate {TogglePause();});menu.Items.Add(pauseMenu);
                menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Quit EdgeRail",null,delegate {quitting=true;Close();});tray.ContextMenuStrip=menu;
                tray.DoubleClick+=delegate {OpenSettings();};
                bool p=Native.RegisterHotKey(Handle,1,0x4003,0x50),o=Native.RegisterHotKey(Handle,2,0x4003,0x4F);
                if(!p || !o)status.Text="A hotkey is already in use. You can use the tray menu instead.";
                if(Settings.LoadWarning.Length>0)status.Text=Settings.LoadWarning;
                timer=new System.Windows.Forms.Timer {Interval=30};timer.Tick+=OnTick;timer.Start();
            }
            initializing=false;ConfigureEngines();
        }
        static Color Muted=Color.FromArgb(157,177,201),Card=Color.FromArgb(21,29,42),Mint=Color.FromArgb(133,231,198);
        Label Label(string text,int size,Color color) {return new Label {Text=text,Font=new Font("Segoe UI",size),ForeColor=color,AutoSize=true,Margin=new Padding(0,0,0,6)};}
        Button Button(string text) {return new Button {Text=text,AutoSize=true,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(31,48,63),ForeColor=ForeColor,Padding=new Padding(10,4,10,4),Cursor=Cursors.Hand};}
        void BuildUI() {
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(28,22,28,16),ColumnCount=1,RowCount=6};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,77));root.RowStyles.Add(new RowStyle(SizeType.Absolute,70));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,82));root.RowStyles.Add(new RowStyle(SizeType.Absolute,74));root.RowStyles.Add(new RowStyle(SizeType.Absolute,62));Controls.Add(root);
            var header=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2};header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170));
            var title=new FlowLayoutPanel {FlowDirection=FlowDirection.TopDown,Dock=DockStyle.Fill,WrapContents=false};title.Controls.Add(Label("EdgeRail",26,ForeColor));title.Controls.Add(Label("Your edges. Everyday controls. Built for Windows.",10,Muted));header.Controls.Add(title,0,0);
            pause=Button("Pause controls");pause.Anchor=AnchorStyles.Top|AnchorStyles.Right;pause.Click+=delegate{TogglePause();};header.Controls.Add(pause,1,0);root.Controls.Add(header);
            var modeRow=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2};modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,66));modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,34));
            var modeGroup=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};modeGroup.Controls.Add(Label("INPUT MODE",9,Mint));
            mode=new RailCombo {Width=460};mode.Items.AddRange(new object[]{"Automatic: native touchpad + Alt screen edges","Compatibility: Alt + screen edges","Native touchpad only (hardware dependent)"});mode.SelectedIndex=settings.Mode;
            mode.SelectedIndexChanged+=delegate {settings.Mode=mode.SelectedIndex;ConfigureEngines();Save();};modeGroup.Controls.Add(mode);modeRow.Controls.Add(modeGroup);
            stateLabel=Label("CONTROLS ACTIVE",10,Mint);stateLabel.Anchor=AnchorStyles.Right|AnchorStyles.Top;modeRow.Controls.Add(stateLabel);root.Controls.Add(modeRow);
            var middle=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2};middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));
            preview=new Preview {Dock=DockStyle.Fill,Margin=new Padding(0,0,18,0),Settings=settings,Font=new Font("Segoe UI",9)};middle.Controls.Add(preview);
            var actions=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=5,BackColor=Card,Padding=new Padding(16)};actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,76));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute,24));for(int row=0;row<4;row++)actions.RowStyles.Add(new RowStyle(SizeType.Percent,25));
            actions.Controls.Add(Label("EDGE",9,Mint),0,0);actions.Controls.Add(Label("CONTROL",9,Mint),1,0);
            AddMapping(actions,1,Edge.Left,settings.Left);AddMapping(actions,2,Edge.Right,settings.Right);AddMapping(actions,3,Edge.Top,settings.Top);AddMapping(actions,4,Edge.Bottom,settings.Bottom);middle.Controls.Add(actions);root.Controls.Add(middle);
            var tuning=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,Padding=new Padding(0,10,0,0)};tuning.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));tuning.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            tuning.Controls.Add(Slider("Edge width",4,16,settings.Width,delegate(int v){settings.Width=v;ConfigureEngines();Save();}));
            tuning.Controls.Add(Slider("Travel per step",3,12,settings.Sensitivity,delegate(int v){settings.Sensitivity=v;ConfigureEngines();Save();}));root.Controls.Add(tuning);
            var help=Label("COMPATIBILITY  Hold Alt, move the pointer to a screen edge, then slide along it. Release Alt to finish.\nNATIVE  Land one finger on a touchpad edge, then slide. Driver support is required; the pointer may still move.\nCtrl + Alt + P pauses controls  |  Ctrl + Alt + O opens settings  |  Closing this window keeps the tray app running.",9,Muted);help.Dock=DockStyle.Fill;help.AutoSize=false;root.Controls.Add(help);
            var footer=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};footer.RowStyles.Add(new RowStyle(SizeType.Percent,50));footer.RowStyles.Add(new RowStyle(SizeType.Percent,50));status=Label("Ready. The pad above is a safe preview; it does not change system settings.",9,Mint);status.AutoSize=false;status.Dock=DockStyle.Fill;
            deviceStatus=Label("Native touchpad: waiting for data. Compatibility mode works with a pointer.",9,Muted);deviceStatus.AutoSize=false;deviceStatus.Dock=DockStyle.Fill;footer.Controls.Add(status);footer.Controls.Add(deviceStatus);root.Controls.Add(footer);
        }
        Control Slider(string label,int min,int max,int value,Action<int> changed) {
            var panel=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};var text=Label(label+": "+value+"%",9,Muted);panel.Controls.Add(text);
            var track=new TrackBar {Minimum=min,Maximum=max,Value=value,TickStyle=TickStyle.None,Dock=DockStyle.Fill,Height=28};track.ValueChanged+=delegate{text.Text=label+": "+track.Value+"%";changed(track.Value);};panel.Controls.Add(track);return panel;
        }
        void AddMapping(TableLayoutPanel table,int row,Edge edge,RailAction selected) {
            var name=Label(edge.ToString(),11,ForeColor);name.Anchor=AnchorStyles.Left;table.Controls.Add(name,0,row);
            var box=new RailCombo {Dock=DockStyle.Fill,Anchor=AnchorStyles.Left|AnchorStyles.Right,Margin=new Padding(0,6,0,6),DataSource=Enum.GetValues(typeof(RailAction))};box.SelectedItem=selected;
            box.SelectedIndexChanged+=delegate {var v=(RailAction)box.SelectedItem;switch(edge){case Edge.Left:settings.Left=v;break;case Edge.Right:settings.Right=v;break;case Edge.Top:settings.Top=v;break;case Edge.Bottom:settings.Bottom=v;break;}Save();};table.Controls.Add(box,1,row);
        }
        void ConfigureEngines() {native.Reset();pointer.Reset();pointerEdge=Edge.None;native.Band=pointer.Band=settings.Width/100.0;native.Step=pointer.Step=settings.Sensitivity/100.0;if(preview!=null){preview.Engine.Band=native.Band;preview.Engine.Step=native.Step;preview.Engine.Reset();preview.Invalidate();}}
        void Save() {if(initializing || testMode)return;try{settings.Save();status.Text="Settings saved.";}catch{status.Text="Could not save settings. Current settings still work for this session.";}}
        internal void VerifyUI() {
            if(mode.SelectedIndex!=0 || mode.Text.Length==0)throw new InvalidOperationException("Input mode binding failed.");
            TogglePause();if(!paused || pause.Text!="Resume controls")throw new InvalidOperationException("Pause failed.");
            TogglePause();if(paused || pause.Text!="Pause controls")throw new InvalidOperationException("Resume failed.");
            int combos=0;VerifyMappings(Controls,ref combos);if(combos!=5)throw new InvalidOperationException("A mapping control is missing.");
            status.Text="Ready. The pad above is a safe preview; it does not change system settings.";
        }
        void VerifyMappings(Control.ControlCollection controls,ref int count) {
            foreach(Control c in controls) {
                ComboBox combo=c as ComboBox;
                if(combo!=null){count++;if(combo.SelectedItem==null || combo.Text.Length==0)throw new InvalidOperationException("An empty mapping was found.");}
                VerifyMappings(c.Controls,ref count);
            }
        }
        void TogglePause() {paused=!paused;ConfigureEngines();pause.Text=paused?"Resume controls":"Pause controls";stateLabel.Text=paused?"CONTROLS PAUSED":"CONTROLS ACTIVE";stateLabel.ForeColor=paused?Color.FromArgb(255,200,132):Mint;if(tray!=null){tray.Text=paused?"EdgeRail - paused":"EdgeRail - active";pauseMenu.Text=paused?"Resume":"Pause";}status.Text=paused?"Paused. All live gestures are disabled.":"Controls resumed.";}
        void OpenSettings() {Show();WindowState=FormWindowState.Normal;Activate();}
        void Fire(Edge edge,int direction) {
            if(paused || direction==0)return;RailAction a=settings.For(edge);if(a==RailAction.Off)return;
            RailAction effective=a==RailAction.Adaptive?ActionRunner.Adaptive(ActionRunner.ForegroundProcess()):a;
            int cooldown=(effective==RailAction.Volume || effective==RailAction.Brightness || effective==RailAction.Zoom)?90:320;
            if(clock.ElapsedMilliseconds-lastAction<cooldown)return;lastAction=clock.ElapsedMilliseconds;
            runner.Run(a,direction);
        }
        void OnContact(double x,double y,int count,bool down) {
            lastNative=clock.ElapsedMilliseconds;
            // Alt reserves control for compatibility mode, avoiding duplicate actions.
            if(paused || settings.Mode==1 || (Native.GetAsyncKeyState(0x12)&0x8000)!=0 || (Native.GetAsyncKeyState(1)&0x8000)!=0 || (Native.GetAsyncKeyState(2)&0x8000)!=0){native.Reset();return;}
            int step=native.Update(x,y,count,down);Fire(native.Active,step);
        }
        void OnTick(object sender,EventArgs e) {
            if(reader!=null)deviceStatus.Text=reader.Status+"  |  HID packets "+reader.Packets+" / contacts "+reader.Decoded;
            if(lastNative>0 && clock.ElapsedMilliseconds-lastNative>220)native.Reset();
            bool alt=(Native.GetAsyncKeyState(0x12)&0x8000)!=0;
            bool clicking=(Native.GetAsyncKeyState(1)&0x8000)!=0 || (Native.GetAsyncKeyState(2)&0x8000)!=0;
            if(paused || settings.Mode==2 || !alt || clicking){pointer.Reset();pointerEdge=Edge.None;return;}
            Point p=Cursor.Position;Rectangle r=Screen.FromPoint(p).Bounds;
            double x=(double)(p.X-r.Left)/Math.Max(1,r.Width-1),y=(double)(p.Y-r.Top)/Math.Max(1,r.Height-1);
            // Compatibility activation requires reaching the outer 12 physical pixels,
            // independent of the much wider physical-touchpad band.
            if(pointerEdge==Edge.None) {
                Edge edge=GestureEngine.Detect(x,y,Math.Max(12.0/r.Width,12.0/r.Height));
                if(edge==Edge.None)return;
                pointer.Reset();pointer.Band=Math.Max(18.0/r.Width,18.0/r.Height);pointerBounds=r;
                pointer.Update(x,y,1,true);pointerEdge=pointer.Active;return;
            }
            if(r!=pointerBounds){pointer.Reset();pointerEdge=Edge.None;return;}
            int d=pointer.Update(x,y,1,true);Fire(pointer.Active,d);
            // Leaving the rail requires releasing Alt; do not re-arm in the middle of a gesture.
        }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x00FF && reader!=null)reader.Process(m.LParam);
            else if(m.Msg==0x00FE && reader!=null)reader.ClearDevices();
            else if(m.Msg==0x0312) {if(m.WParam.ToInt32()==1)TogglePause();else if(m.WParam.ToInt32()==2)OpenSettings();}
            base.WndProc(ref m);
        }
        protected override void OnFormClosing(FormClosingEventArgs e) {
            if(!quitting && tray!=null && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();tray.ShowBalloonTip(2500,"EdgeRail is running","Use the tray icon to open settings, pause or quit.",ToolTipIcon.Info);return;}base.OnFormClosing(e);
        }
        protected override void Dispose(bool disposing) {
            if(disposing && !IsDisposed){quitting=true;if(timer!=null)timer.Dispose();if(reader!=null)reader.Dispose();if(IsHandleCreated){Native.UnregisterHotKey(Handle,1);Native.UnregisterHotKey(Handle,2);}if(tray!=null){tray.Visible=false;tray.Dispose();}}
            base.Dispose(disposing);
        }
    }
    static class Program {
        [STAThread] static int Main(string[] args) {
            if(args.Length>0 && args[0]=="--self-test")return SelfTest.Run(args.Length>1?args[1]:null);
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length>0 && args[0]=="--preview") {Application.Run(new MainForm(true));return 0;}
            if(args.Length>0 && args[0]=="--smoke-test") {
                try {
                    using(var f=new MainForm(true)) { f.Show();Application.DoEvents();f.VerifyUI();using(var bitmap=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bitmap,new Rectangle(0,0,f.Width,f.Height));if(args.Length>1)bitmap.Save(args[1]);} f.Close(); }
                    return 0;
                } catch(Exception ex) {if(args.Length>1)File.WriteAllText(args[1]+".error.txt",ex.ToString());return 1;}
            }
            bool created;using(var single=new Mutex(true,@"Local\EdgeRail.Windows.UserSession",out created)) {
                if(!created){MessageBox.Show("EdgeRail is already running. Open it from the system tray or press Ctrl + Alt + O.","EdgeRail");return 0;}
                try {Application.Run(new MainForm(false));return 0;}
                catch(Exception ex) {MessageBox.Show("EdgeRail could not start: "+ex.Message,"EdgeRail",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
            }
        }
    }
}
