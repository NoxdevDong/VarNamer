using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VarNamer
{
    // 缓动与插值
    // 曲线取自 Fluent 2 官方设计令牌 packages/tokens/src/global/curves.ts（Apache-2.0）
    public static class Curves
    {
        public static readonly Func<double, double> DecelerateMax = Bezier(0.1, 0.9, 0.2, 1.0);    // 入场用
        public static readonly Func<double, double> DecelerateMin = Bezier(0.33, 0.0, 0.1, 1.0);   // 小范围入场
        public static readonly Func<double, double> AccelerateMax = Bezier(0.9, 0.1, 1.0, 0.2);    // 退场用
        public static readonly Func<double, double> EasyEase = Bezier(0.33, 0.0, 0.67, 1.0);       // 状态变化

        // cubic-bezier(x1,y1,x2,y2)：二分求 t 使 Bx(t)=x，再取 By(t)
        public static Func<double, double> Bezier(double x1, double y1, double x2, double y2)
        {
            return delegate(double x)
            {
                if (x <= 0) return 0.0;
                if (x >= 1) return 1.0;
                double lo = 0, hi = 1, t = x;
                for (int i = 0; i < 24; i++)
                {
                    t = (lo + hi) / 2;
                    double bx = 3 * (1 - t) * (1 - t) * t * x1 + 3 * (1 - t) * t * t * x2 + t * t * t;
                    if (bx < x) lo = t; else hi = t;
                }
                return 3 * (1 - t) * (1 - t) * t * y1 + 3 * (1 - t) * t * t * y2 + t * t * t;
            };
        }
    }

    public static class Anim
    {
        // 兼容旧调用：默认入场曲线
        public static double EaseOut(double t) { return Curves.DecelerateMin(t); }
        public static double EaseInOut(double t) { return Curves.EasyEase(t); }

        public static int Lerp(int a, int b, double t)
        {
            if (t < 0) t = 0; else if (t > 1) t = 1;
            return (int)Math.Round(a + (b - a) * t);
        }

        public static Color Lerp(Color a, Color b, double t)
        {
            if (t < 0) t = 0; else if (t > 1) t = 1;
            return Color.FromArgb(Lerp(a.A, b.A, t), Lerp(a.R, b.R, t), Lerp(a.G, b.G, t), Lerp(a.B, b.B, t));
        }
    }

    // 补间：全程序共用一个 15ms 计时器，空闲时自动停掉（不占 CPU、不泄漏）
    public class Tween
    {
        private static readonly List<Tween> live = new List<Tween>();
        private static readonly Timer pump = new Timer();
        private static int lastTick;

        private int duration;
        private int delay;
        private double elapsed;
        private Action<double> tick;
        private Action done;
        private bool stopped;

        static Tween()
        {
            pump.Interval = 15;
            pump.Tick += delegate(object s, EventArgs e) { Pump(); };
        }

        public static Tween Run(int ms, Action<double> onTick, Action onDone)
        {
            Tween tw = new Tween();
            tw.duration = ms < 1 ? 1 : ms;
            tw.delay = 0;
            tw.tick = onTick;
            tw.done = onDone;
            live.Add(tw);
            if (!pump.Enabled) { lastTick = Environment.TickCount; pump.Start(); }
            if (tw.tick != null) { try { tw.tick(0); } catch (Exception) { } }
            return tw;
        }

        // 延迟若干毫秒后启动（用于列表逐行错峰出现）
        public static Tween RunDelayed(int delayMs, int ms, Action<double> onTick, Action onDone)
        {
            Tween tw = new Tween();
            tw.duration = ms < 1 ? 1 : ms;
            tw.tick = onTick;
            tw.done = onDone;
            tw.delay = delayMs < 0 ? 0 : delayMs;
            live.Add(tw);
            if (!pump.Enabled) { lastTick = Environment.TickCount; pump.Start(); }
            return tw;    // 注意：延迟期间不触发回调，否则“错峰”就没意义了
        }

        public void Stop() { stopped = true; }

        private static void Pump()
        {
            int now = Environment.TickCount;
            int dt = now - lastTick;
            lastTick = now;
            if (dt < 0) dt = 0;
            if (dt > 60) dt = 60;
            Tween[] arr = live.ToArray();
            for (int i = 0; i < arr.Length; i++) arr[i].Step(dt);
            if (live.Count == 0) pump.Stop();
        }

        private void Step(int dt)
        {
            if (stopped) { live.Remove(this); return; }
            if (delay > 0)
            {
                delay -= dt;
                if (delay > 0) return;
                elapsed = 0;
                try { if (tick != null) tick(0); } catch (Exception) { }   // 延迟结束这一拍只负责起跑
                return;
            }
            elapsed += dt;
            double t = elapsed / duration;
            if (t > 1) t = 1;
            try { if (tick != null) tick(t); } catch (Exception) { }
            if (t >= 1)
            {
                live.Remove(this);
                try { if (done != null) done(); } catch (Exception) { }
            }
        }

        // 窗体淡入（0 → to），窗体销毁时自动停止
        public static void FadeIn(Form f, double to, int ms)
        {
            if (f == null || f.IsDisposed) return;
            double from = f.Opacity <= 0.02 ? 0 : f.Opacity;
            Run(ms, delegate(double t)
            {
                if (f.IsDisposed) return;
                f.Opacity = from + (to - from) * Curves.DecelerateMax(t);
            }, null);
        }
    }
}
