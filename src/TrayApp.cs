using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VarNamer
{
    public class HotkeyWindow : NativeWindow
    {
        public event EventHandler Pressed;
        public event EventHandler ShotPressed;
        private const int WM_HOTKEY = 0x0312;
        private const int ID_SHOT = 0xB19;
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_NOREPEAT = 0x4000;
        private const int ID = 0xB17;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams());
        }

        public bool Register(int mods, int vk)
        {
            return RegisterHotKey(Handle, ID, mods | MOD_NOREPEAT, vk);
        }

        public bool RegisterShot(int mods, int vk)
        {
            return RegisterHotKey(Handle, ID_SHOT, mods | MOD_NOREPEAT, vk);
        }

        public void Unregister()
        {
            try { UnregisterHotKey(Handle, ID); }
            catch (Exception) { }
        }

        public static int ModsCtrlAlt { get { return MOD_CONTROL | MOD_ALT; } }
        public static int ModsCtrlShift { get { return MOD_CONTROL | MOD_SHIFT; } }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == ID && Pressed != null) Pressed(this, EventArgs.Empty);
                else if (id == ID_SHOT && ShotPressed != null) ShotPressed(this, EventArgs.Empty);
            }
            base.WndProc(ref m);
        }
    }

    public class TrayApp : ApplicationContext
    {
        [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hWnd);
        [DllImport("shcore.dll")] private static extern int GetProcessDpiAwareness(IntPtr h, out int value);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);

        private NotifyIcon tray;
        private MainForm main;
        private FloatForm floater;
        private HotkeyWindow hk;
        private AppState state;
        private string hotkeyInfo = "";
        private IntPtr prevWindow = IntPtr.Zero;

        // 复制后切回上一个窗口（新手最省事：不用手点回编辑器）
        public void ReturnToPrevWindow()
        {
            try
            {
                if (!state.Cfg.ReturnToPrevWindow) return;
                if (prevWindow == IntPtr.Zero || !IsWindow(prevWindow)) return;
                SetForegroundWindow(prevWindow);
            }
            catch (Exception) { }
        }

        public TrayApp(string initialInput)
        {
            state = new AppState();
            hk = new HotkeyWindow();
            hk.Pressed += OnHotkey;
            hk.ShotPressed += OnShotHotkey;
            int sm, sv;
            if (Hotkey.TryParse(state.Cfg.ShotHotkey, out sm, out sv)) hk.RegisterShot(sm, sv);
            string hkErr;
            if (!ApplyHotkey(state.Cfg.Hotkey, out hkErr))
            {
                string err2;
                if (!ApplyHotkey("Ctrl+Shift+V", out err2)) hotkeyInfo = "(注册失败)";
            }

            tray = new NotifyIcon();
            tray.Icon = LoadIcon();
            tray.Text = "VarNamer - 中文变量取名";
            tray.Visible = true;
            tray.DoubleClick += delegate(object s, EventArgs e) { ShowMain(); };
            tray.ContextMenuStrip = BuildMenu();

            // 启动时自动检测 exe 同级目录的外置词库并导入
            string dictFile;
            int dictAdded = AutoDict.ImportIfNeeded(state, out dictFile);
            if (dictAdded > 0 && tray != null)
                tray.ShowBalloonTip(2500, "VarNamer 词库", "已自动导入词库 " + dictAdded + " 条：" + System.IO.Path.GetFileName(dictFile), ToolTipIcon.Info);

            WriteRunningInfo();
            floater = new FloatForm(state);
            floater.OpenMain = delegate() { ShowMain(); };
            floater.AfterCopy = delegate() { ReturnToPrevWindow(); };
            floater.VisibilityChanged = delegate() { RaiseFloatChanged(); };

            if (initialInput != null && initialInput.Length > 0)
            {
                state.LastInput = initialInput;
                ShowMain();
                floater.SetInput(initialInput);
                floater.ShowFloat();
            }
            else
            {
                floater.ShowFloat();     // 启动时总是显示悬浮窗（此前会沿用上次隐藏状态，导致“重启后窗口不见了”）
            }

            if (!state.Cfg.GuideShown)
            {
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                t.Interval = 900;
                t.Tick += delegate(object s, EventArgs e)
                {
                    t.Stop();
                    t.Dispose();
                    state.Cfg.GuideShown = true;    // 先标记已读，避免中途退出后反复弹
                    state.Cfg.Save();
                    ShowGuide();
                };
                t.Start();
            }

        }

        // 托盘图标（按托盘小图标尺寸取帧）
        public static Icon LoadIcon()
        {
            int small = SystemInformation.SmallIconSize.Width;
            if (small <= 0) small = 16;
            return LoadAppIcon(small);
        }

        // 按指定尺寸从内嵌 app.ico 取帧；渲染为空则回退（.NET 不认 PNG 压缩帧，必须校验）
        public static Icon LoadAppIcon(int size)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                string[] names = asm.GetManifestResourceNames();
                for (int i = 0; i < names.Length; i++)
                {
                    if (!names[i].EndsWith("app.ico", StringComparison.OrdinalIgnoreCase)) continue;
                    using (Stream s = asm.GetManifestResourceStream(names[i]))
                    {
                        if (s == null) continue;
                        byte[] data = new byte[s.Length];
                        s.Read(data, 0, data.Length);
                        int[] wants = new int[] { size, 32, 48, 16 };
                        for (int k = 0; k < wants.Length; k++)
                        {
                            Icon ic = null;
                            try
                            {
                                using (MemoryStream ms = new MemoryStream(data))
                                    ic = new Icon(ms, wants[k], wants[k]);
                            }
                            catch (Exception) { ic = null; }
                            if (ic != null && RendersNonEmpty(ic)) return ic;
                        }
                    }
                }
            }
            catch (Exception) { }
            try
            {
                Icon exe = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (exe != null && RendersNonEmpty(exe)) return exe;
            }
            catch (Exception) { }
            return SystemIcons.Application;
        }

        // 校验：图标渲染到托盘尺寸后必须真的有像素（.NET 不认 PNG 帧时会得到空白图）
        private static bool RendersNonEmpty(Icon ic)
        {
            try
            {
                int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
                using (Bitmap bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent);
                        g.DrawIcon(ic, new Rectangle(0, 0, size, size));
                    }
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                            if (bmp.GetPixel(x, y).A > 40) return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        private void OnHotkey(object sender, EventArgs e)
        {
            ConvertClipboard();
        }

        private void OnShotHotkey(object sender, EventArgs e)
        {
            StartScreenshot();
        }

        // 截图（全屏选区 + 标注 + 复制/保存/钉在桌面）
        public void StartScreenshot()
        {
            try
            {
                ShotForm f = new ShotForm(state);
                if (f.Start()) return;      // 失败时 Start 内部已提示
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message, "截图失败", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        private void ConvertClipboard()
        {
            try { prevWindow = GetForegroundWindow(); } catch (Exception) { }   // 记住你正在用的窗口
            string text = "";
            try
            {
                if (Clipboard.ContainsText()) text = Clipboard.GetText();
            }
            catch (Exception) { }
            if (text != null && text.Trim().Length > 0) floater.SetInput(text.Trim());
            floater.ShowFloat();
            floater.Activate();
        }

        public void ShowMain()
        {
            if (main == null || main.IsDisposed)
            {
                main = new MainForm(state, this, state.LastInput);
            }
            main.Show();
            if (main.WindowState == FormWindowState.Minimized) main.WindowState = FormWindowState.Normal;
            main.Activate();
        }

        private void WriteRunningInfo()
        {
            try
            {
                string txt = "版本 v" + AppVersion.Value
                    + "\r\nPID " + System.Diagnostics.Process.GetCurrentProcess().Id
                    + "\r\n路径 " + Application.ExecutablePath
                    + "\r\n启动 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                System.IO.File.WriteAllText(System.IO.Path.Combine(Config.DataDir, "running.txt"), txt, new System.Text.UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        public void ShowGuide()
        {
            try
            {
                using (TextInfoForm f = new TextInfoForm("新手引导 · 3 步开始用", "首次启动自动弹出，可随时从托盘菜单再看", Guide.Text))
                { f.ShowDialog(main != null && !main.IsDisposed ? (IWin32Window)main : null); }
            }
            catch (Exception) { }
        }

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    object v = k.GetValue("VarNamer");
                    return v != null;
                }
            }
            catch (Exception) { return false; }
        }

        public static void SetAutoStart(bool on)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue("VarNamer", "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue("VarNamer", false);
                }
            }
            catch (Exception) { }
        }

        public static bool TrySetAutoStart(bool on)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return false;
                    if (on) k.SetValue("VarNamer", "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue("VarNamer", false);
                }
                return IsAutoStartEnabled() == on;
            }
            catch (Exception) { return false; }
        }

        public void Quit()
        {
            try
            {
                if (floater != null) floater.SavePosition();
                state.Cfg.Save();
                try { System.IO.File.Delete(System.IO.Path.Combine(Config.DataDir, "running.txt")); } catch (Exception) { }
                if (hk != null) hk.Unregister();
            }
            catch (Exception) { }
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            if (main != null && !main.IsDisposed) main.Dispose();
            if (floater != null && !floater.IsDisposed) floater.Dispose();
            Application.Exit();
        }

        public MainForm Main { get { return main; } }

        // 绑定全局热键（切换时用）。失败时返回 false 并给出原因，调用方负责回滚配置。
        public bool ApplyHotkey(string text, out string error)
        {
            error = "";
            int mods, vk;
            if (!Hotkey.TryParse(text, out mods, out vk))
            {
                error = "格式无效：" + text;
                return false;
            }
            hk.Unregister();
            bool ok = hk.Register(mods, vk);
            if (!ok)
            {
                error = "已被其它程序占用";
                int m2, v2;
                if (Hotkey.TryParse(state.Cfg.Hotkey, out m2, out v2)) hk.Register(m2, v2);
                return false;
            }
            hotkeyInfo = text;
            return true;
        }

        // 重新绑定截图热键（设置里改过之后调用）
        public bool ApplyShotHotkey(string text, out string error)
        {
            error = "";
            int mods, vk;
            if (!Hotkey.TryParse(text, out mods, out vk)) { error = "格式无效"; return false; }
            if (!hk.RegisterShot(mods, vk)) { error = "已被占用"; return false; }
            return true;
        }

        // 应用主题：重建已打开的窗口（状态保存在 AppState 里，不丢）
        public void ApplyTheme(bool light)
        {
            Theme.ApplyPalette(!light);
            string typed = null;
            bool mainVisible = main != null && !main.IsDisposed && main.Visible;
            if (main != null && !main.IsDisposed)
            {
                typed = state.LastInput;
                main.Hide();
                main.Dispose();
                main = null;
            }
            if (floater != null && !floater.IsDisposed)
            {
                if (typed == null) typed = floater.CurrentInput();
                floater.SavePosition();
                floater.Hide();
                floater.Dispose();
            }
            floater = new FloatForm(state);
            floater.OpenMain = delegate() { ShowMain(); };
            floater.AfterCopy = delegate() { ReturnToPrevWindow(); };
            floater.VisibilityChanged = delegate() { RaiseFloatChanged(); };
            if (typed != null && typed.Length > 0) floater.SetInput(typed);
            floater.ShowFloat();
            if (mainVisible) ShowMain();
            if (tray != null) tray.ContextMenuStrip = BuildMenu();
        }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("打开主窗口(&M)", null, delegate(object s, EventArgs e) { ShowMain(); });
            menu.Items.Add("显示/隐藏悬浮窗(&F)  " + hotkeyInfo, null, delegate(object s, EventArgs e) { ToggleFloater(); });
            menu.Items.Add("设置(&S)…", null, delegate(object s, EventArgs e) { ShowSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出(&X)", null, delegate(object s, EventArgs e) { Quit(); });
            return menu;
        }

        public void ShowSettings()
        {
            try
            {
                using (SettingsForm f = new SettingsForm(state, this))
                    f.ShowDialog(main != null && !main.IsDisposed ? (IWin32Window)main : null);
            }
            catch (Exception) { }
        }

        // 设置里改了「悬浮窗默认透明度」后即时生效
        public void ApplyFloatIdleOpacity()
        {
            if (floater == null || floater.IsDisposed) return;
            floater.ApplyIdleOpacity();
        }

        // 悬浮窗显示/隐藏状态变化（主界面上的按钮要靠它同步）
        public event EventHandler FloatVisibilityChanged;

        public bool FloaterVisible
        {
            get { return floater != null && !floater.IsDisposed && floater.Visible; }
        }

        public void ShowFloater()
        {
            if (floater == null || floater.IsDisposed) return;
            floater.ShowFloat();
            floater.Activate();
            RaiseFloatChanged();
        }

        public void HideFloater()
        {
            if (floater == null || floater.IsDisposed) return;
            floater.HideFloat();
            RaiseFloatChanged();
        }

        // 主界面「悬浮窗」按钮：一下显示、再一下隐藏
        public void ToggleFloater()
        {
            if (floater == null || floater.IsDisposed) return;
            if (floater.Visible) HideFloater();
            else ShowFloater();
        }

        private void RaiseFloatChanged()
        {
            if (FloatVisibilityChanged != null) FloatVisibilityChanged(this, EventArgs.Empty);
        }
    }
}