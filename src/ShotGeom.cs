using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VarNamer
{
    // 选区几何运算（纯函数，便于单测）：8 个手柄的命中、拖拽缩放、移动、边界钳制
    public static class ShotGeom
    {
        // 手柄索引：0=左上 1=上 2=右上 3=右 4=右下 5=下 6=左下 7=左
        public const int HandleNone = -1;

        public static Point[] HandlePoints(Rectangle r)
        {
            return new Point[]
            {
                new Point(r.Left, r.Top), new Point(r.Left + r.Width / 2, r.Top), new Point(r.Right, r.Top),
                new Point(r.Right, r.Top + r.Height / 2), new Point(r.Right, r.Bottom),
                new Point(r.Left + r.Width / 2, r.Bottom), new Point(r.Left, r.Bottom),
                new Point(r.Left, r.Top + r.Height / 2)
            };
        }

        public static int HitHandle(Rectangle r, Point p, int tol)
        {
            if (r.Width <= 0 || r.Height <= 0) return HandleNone;
            Point[] h = HandlePoints(r);
            int best = HandleNone;
            long bestD = long.MaxValue;
            for (int i = 0; i < h.Length; i++)
            {
                int dx = Math.Abs(h[i].X - p.X), dy = Math.Abs(h[i].Y - p.Y);
                if (dx <= tol && dy <= tol)
                {
                    long d = (long)dx * dx + (long)dy * dy;
                    if (d < bestD) { bestD = d; best = i; }
                }
            }
            return best;
        }

        public static Rectangle ResizeFromHandle(Rectangle r, int handle, Point p)
        {
            int left = r.Left, top = r.Top, right = r.Right, bottom = r.Bottom;
            switch (handle)
            {
                case 0: left = p.X; top = p.Y; break;
                case 1: top = p.Y; break;
                case 2: right = p.X; top = p.Y; break;
                case 3: right = p.X; break;
                case 4: right = p.X; bottom = p.Y; break;
                case 5: bottom = p.Y; break;
                case 6: left = p.X; bottom = p.Y; break;
                case 7: left = p.X; break;
                default: return r;
            }
            if (right < left) { int t = left; left = right; right = t; }
            if (bottom < top) { int t = top; top = bottom; bottom = t; }
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        public static Rectangle MoveTo(Rectangle r, Point from, Point to)
        {
            return new Rectangle(r.X + (to.X - from.X), r.Y + (to.Y - from.Y), r.Width, r.Height);
        }

        public static Rectangle ClampInside(Rectangle r, Rectangle bounds)
        {
            int x = r.X, y = r.Y;
            if (x < bounds.Left) x = bounds.Left;
            if (y < bounds.Top) y = bounds.Top;
            if (x + r.Width > bounds.Right) x = Math.Max(bounds.Left, bounds.Right - r.Width);
            if (y + r.Height > bounds.Bottom) y = Math.Max(bounds.Top, bounds.Bottom - r.Height);
            return new Rectangle(x, y, r.Width, r.Height);
        }

        public static Rectangle ClampResize(Rectangle r, Rectangle bounds, int minSize)
        {
            int left = Math.Max(bounds.Left, r.Left), top = Math.Max(bounds.Top, r.Top);
            int right = Math.Min(bounds.Right, r.Right), bottom = Math.Min(bounds.Bottom, r.Bottom);
            if (right - left < minSize) right = Math.Min(bounds.Right, left + minSize);
            if (bottom - top < minSize) bottom = Math.Min(bounds.Bottom, top + minSize);
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        // 等比缩放（保持长宽比）：锚定“手柄对面的边/角”不动，按鼠标屏幕位置算新矩形
        public static Rectangle ResizeProportional(Rectangle r0, int handle, Point mouse, int minWidth, double aspect)
        {
            if (aspect <= 0.01) aspect = (double)r0.Width / Math.Max(1, r0.Height);
            bool left = (handle == 0 || handle == 6 || handle == 7);
            bool right = (handle == 2 || handle == 3 || handle == 4);
            bool top = (handle == 0 || handle == 1 || handle == 2);
            bool bottom = (handle == 4 || handle == 5 || handle == 6);

            // 锚点：与手柄相对的边/角保持不动
            int anchorX = left ? r0.Right : (right ? r0.Left : r0.Left);
            int anchorY = top ? r0.Bottom : (bottom ? r0.Top : r0.Top);

            int w, h;
            if (left || right)
            {
                w = Math.Abs(mouse.X - anchorX);
                h = (int)Math.Round(w / aspect);
            }
            else
            {
                h = Math.Abs(mouse.Y - anchorY);
                w = (int)Math.Round(h * aspect);
            }
            if (w < minWidth) { w = minWidth; h = (int)Math.Round(w / aspect); }
            if (h < Math.Max(4, (int)(minWidth / Math.Max(0.01, aspect)))) h = Math.Max(4, (int)Math.Round(minWidth / aspect));

            // 锚点：手柄所在一侧的“对边/对角”固定；边缘手柄的另一轴也固定在起始位置，
            // 避免缩放时窗口在垂直方向漂移（此前表现为“像被裁过/放大后位置乱”））
            int x, y;
            if (left) x = r0.Right - w;                       // 拖左边 → 右边不动
            else if (right) x = r0.Left;                      // 拖右边 → 左边不动
            else x = r0.Left + (r0.Width - w) / 2;            // 上下边缘 → 水平居中
            if (top) y = r0.Bottom - h;                       // 拖上边 → 下边不动
            else if (bottom) y = r0.Top;                      // 拖下边 → 上边不动
            else y = r0.Top;                                  // 左右边缘 → 顶边不动
            return new Rectangle(x, y, w, h);
        }

        public static double AspectOf(Rectangle r) { return (double)r.Width / Math.Max(1, r.Height); }
        // 把窗口矩形拉回指定屏幕范围内（保持尺寸，仅平移）
        public static Rectangle ClampToScreen(Rectangle r, Rectangle screen)
        {
            int x = r.X, y = r.Y;
            if (x + r.Width > screen.Right) x = screen.Right - r.Width;
            if (y + r.Height > screen.Bottom) y = screen.Bottom - r.Height;
            if (x < screen.Left) x = screen.Left;
            if (y < screen.Top) y = screen.Top;
            return new Rectangle(x, y, r.Width, r.Height);
        }

        public static Cursor CursorForHandle(int handle)
        {
            switch (handle)
            {
                case 0: case 4: return Cursors.SizeNWSE;
                case 2: case 6: return Cursors.SizeNESW;
                case 1: case 5: return Cursors.SizeNS;
                case 3: case 7: return Cursors.SizeWE;
                default: return Cursors.Cross;
            }
        }
    }
}
