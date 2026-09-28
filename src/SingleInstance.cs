using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace VarNamer
{
    // 单实例保护：
    //   · 互斥体必须由静态字段持有到进程结束 —— 之前用局部变量 + GC.KeepAlive，
    //     方法返回后对象被回收、句柄释放，单实例就失效了（表现：双击能开出第二个）
    //   · 第二个实例不再弹“已在运行”对话框，而是发一个命名事件让已有实例把窗口弹到前台
    internal static class Single
    {
        private const string MutexName = "VarNamer.SingleInstance.v1";
        private const string ShowSignalName = "VarNamer.ShowWindow.v1";

        private static Mutex mutex;      // 故意用静态字段：活到进程退出

        // 返回 true 表示本进程是第一个实例（可以继续启动）
        public static bool IsFirstInstance()
        {
            try
            {
                bool created;
                mutex = new Mutex(true, MutexName, out created);
                if (!created)
                {
                    mutex.Dispose();
                    mutex = null;
                    return false;
                }
                return true;
            }
            catch (Exception) { return true; }
        }

        // 通知已在运行的实例：把窗口弹出来
        public static void NotifyExisting()
        {
            try
            {
                bool createdNew;
                using (EventWaitHandle ev = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName, out createdNew))
                    ev.Set();
            }
            catch (Exception) { }
            // 兜底：万一信号线程没起来，按窗口标题找出来直接置前
            try
            {
                IntPtr h = FindWindow(null, "VarNamer  中文变量取名");
                if (h == IntPtr.Zero) h = FindWindow(null, "VarNamer 悬浮窗");
                if (h != IntPtr.Zero) SetForegroundWindow(h);
            }
            catch (Exception) { }
        }

        // 已有实例用它等待“第二个实例启动”的信号
        public static EventWaitHandle OpenSignal()
        {
            try
            {
                bool createdNew;
                return new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName, out createdNew);
            }
            catch (Exception) { return null; }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
