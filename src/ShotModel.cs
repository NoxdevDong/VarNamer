using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;

namespace VarNamer
{
    public enum ShotTool { None, Rect, Ellipse, Arrow, Pen, Highlight, Mosaic, Text }

    public class ShotShape
    {
        public ShotTool Tool;
        public List<Point> Points = new List<Point>();
        public Color Color = Color.Red;
        public int Width = 3;
        public string Text = "";
        public int FontSize = 18;
    }

    // 抓屏（抽成独立函数：便于自测与优雅报错）
    public static class ShotCapture
    {
        public static Bitmap Grab(out string error)
        {
            error = "";
            try
            {
                Rectangle vs = SystemInformation.VirtualScreen;
                Bitmap bmp = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(vs.Left, vs.Top, 0, 0, new Size(vs.Width, vs.Height), CopyPixelOperation.SourceCopy);
                return bmp;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return null;
            }
        }
    }

    // 截图标注引擎：形状保持屏幕坐标，合成时按选区裁剪平移
    public static class ShotDraw
    {
        public static readonly Color[] Palette = new Color[]
        {
            Color.FromArgb(232, 61, 61), Color.FromArgb(255, 145, 32), Color.FromArgb(255, 214, 51),
            Color.FromArgb(60, 190, 100), Color.FromArgb(60, 140, 255), Color.FromArgb(20, 20, 20),
            Color.White
        };

        public static void DrawShapes(Graphics g, List<ShotShape> shapes, Bitmap pixellated, Point offset)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < shapes.Count; i++)
            {
                ShotShape s = shapes[i];
                if (s.Tool == ShotTool.Mosaic) { DrawMosaic(g, s, pixellated, offset); continue; }
                if (s.Tool == ShotTool.Text) { DrawText(g, s, offset); continue; }
                if (s.Points.Count < 2) continue;

                List<Point> pts = new List<Point>();
                for (int k = 0; k < s.Points.Count; k++)
                    pts.Add(new Point(s.Points[k].X - offset.X, s.Points[k].Y - offset.Y));
                Rectangle box = Bounds(pts);

                if (s.Tool == ShotTool.Highlight)
                {
                    using (Pen p = new Pen(Color.FromArgb(90, s.Color), Math.Max(8, s.Width * 6)))
                    {
                        p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                        using (GraphicsPath path = new GraphicsPath())
                        {
                            path.AddLines(pts.ToArray());
                            g.DrawPath(p, path);
                        }
                    }
                    continue;
                }

                using (Pen p = new Pen(s.Color, s.Width))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                    if (s.Tool == ShotTool.Rect) g.DrawRectangle(p, box.X, box.Y, box.Width, box.Height);
                    else if (s.Tool == ShotTool.Ellipse) g.DrawEllipse(p, box.X, box.Y, box.Width, box.Height);
                    else if (s.Tool == ShotTool.Pen)
                    {
                        using (GraphicsPath path = new GraphicsPath())
                        {
                            path.AddLines(pts.ToArray());
                            g.DrawPath(p, path);
                        }
                    }
                    else if (s.Tool == ShotTool.Arrow)
                    {
                        Point a = pts[0], b = pts[pts.Count - 1];
                        g.DrawLine(p, a, b);
                        double ang = Math.Atan2(b.Y - a.Y, b.X - a.X);
                        int head = Math.Max(10, s.Width * 5);
                        for (int k = 0; k < 2; k++)
                        {
                            double t = ang + Math.PI + (k == 0 ? 0.5 : -0.5);
                            g.DrawLine(p, b, new Point(b.X + (int)(head * Math.Cos(t)), b.Y + (int)(head * Math.Sin(t))));
                        }
                    }
                }
            }
        }

        private static void DrawText(Graphics g, ShotShape s, Point offset)
        {
            if (string.IsNullOrEmpty(s.Text) || s.Points.Count == 0) return;
            using (Font f = new Font("Microsoft YaHei UI", s.FontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush back = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            {
                Point p = new Point(s.Points[0].X - offset.X, s.Points[0].Y - offset.Y);
                Size sz = TextRenderer.MeasureText(s.Text, f);
                back.Color = Color.FromArgb(110, 0, 0, 0);
                g.FillRectangle(back, new Rectangle(p.X - 3, p.Y - 2, sz.Width + 6, sz.Height + 4));
                TextRenderer.DrawText(g, s.Text, f, new Point(p.X, p.Y), s.Color);
            }
        }

        private static void DrawMosaic(Graphics g, ShotShape s, Bitmap pixellated, Point offset)
        {
            if (s.Points.Count == 0) return;
            Rectangle box = Inflate(Bounds(s.Points), Math.Max(6, s.Width * 4));
            box.Intersect(new Rectangle(0, 0, pixellated.Width, pixellated.Height));
            if (box.Width <= 0 || box.Height <= 0) return;
            g.DrawImage(pixellated, new Rectangle(box.X - offset.X, box.Y - offset.Y, box.Width, box.Height),
                box, GraphicsUnit.Pixel);
        }

        // 标注的包围盒（用于命中测试与选中框）
        public static Rectangle ShapeBounds(ShotShape s)
        {
            if (s == null || s.Points.Count == 0) return Rectangle.Empty;
            Rectangle r = Bounds(s.Points);
            if (s.Tool == ShotTool.Text)
            {
                int w = Math.Max(30, s.Text == null ? 30 : s.Text.Length * s.FontSize);
                r = new Rectangle(r.X, r.Y, w, s.FontSize + 8);
            }
            int pad = Math.Max(4, s.Width * 2);
            return new Rectangle(r.X - pad, r.Y - pad, r.Width + pad * 2, r.Height + pad * 2);
        }

        // 命中测试：返回最上层命中的形状下标（后画的在上层）
        public static int HitTest(List<ShotShape> shapes, Point p)
        {
            if (shapes == null) return -1;
            for (int i = shapes.Count - 1; i >= 0; i--)
            {
                Rectangle b = ShapeBounds(shapes[i]);
                if (!b.IsEmpty && b.Contains(p)) return i;
            }
            return -1;
        }

        // 平移一个标注（所有点一起移动）
        public static void Translate(ShotShape s, int dx, int dy)
        {
            if (s == null || (dx == 0 && dy == 0)) return;
            for (int i = 0; i < s.Points.Count; i++)
                s.Points[i] = new Point(s.Points[i].X + dx, s.Points[i].Y + dy);
        }

        public static Rectangle Bounds(List<Point> pts)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int i = 0; i < pts.Count; i++)
            {
                if (pts[i].X < minX) minX = pts[i].X;
                if (pts[i].Y < minY) minY = pts[i].Y;
                if (pts[i].X > maxX) maxX = pts[i].X;
                if (pts[i].Y > maxY) maxY = pts[i].Y;
            }
            if (minX > maxX) return Rectangle.Empty;
            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        private static Rectangle Inflate(Rectangle r, int n)
        {
            return new Rectangle(r.X - n, r.Y - n, r.Width + n * 2, r.Height + n * 2);
        }

        // 马赛克底图：整屏打码一次，绘制时按需取块
        public static Bitmap Pixellate(Bitmap src, int block)
        {
            if (block < 2) block = 2;
            // 关键：用“整数块网格”（缩到 sw x sh 再最近邻放大 block 倍），
            // 否则块边界与像素不对齐，马赛克会出现块内不一致的杂色
            int sw = Math.Max(1, (src.Width + block - 1) / block);
            int sh = Math.Max(1, (src.Height + block - 1) / block);
            Bitmap outBmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(outBmp))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                using (Bitmap small = new Bitmap(sw, sh, PixelFormat.Format32bppArgb))
                {
                    using (Graphics gs = Graphics.FromImage(small))
                    {
                        gs.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        gs.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        gs.DrawImage(src, new Rectangle(0, 0, sw, sh));
                    }
                    g.DrawImage(small, new Rectangle(0, 0, sw * block, sh * block));
                }
            }
            return outBmp;
        }

        // 合成：底图 + 标注
        public static Bitmap Compose(Bitmap full, Rectangle region, List<ShotShape> shapes)
        {
            region.Intersect(new Rectangle(0, 0, full.Width, full.Height));
            if (region.Width <= 0) region = new Rectangle(0, 0, Math.Max(1, full.Width), Math.Max(1, full.Height));
            Bitmap outBmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(outBmp))
            {
                g.DrawImage(full, new Rectangle(0, 0, region.Width, region.Height), region, GraphicsUnit.Pixel);
                if (shapes.Count > 0)
                {
                    using (Bitmap pix = Pixellate(full, 12))
                        DrawShapes(g, shapes, pix, region.Location);
                }
            }
            return outBmp;
        }
    }
}