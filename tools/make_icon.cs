using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

class MakeIcon
{
    // 生成多尺寸 ICO：<=64 用传统 BMP 帧（.NET 的 Icon 类只能解析 BMP 帧），
    // 128/256 用 PNG 帧（Vista+ 惯例，资源管理器/任务栏支持）
    static void Main(string[] args)
    {
        string outPath = args.Length > 0 ? args[0] : "app.ico";
        int[] bmpSizes = new int[] { 16, 24, 32, 48, 64 };
        int[] pngSizes = new int[] { 128, 256 };
        List<int> sizes = new List<int>();
        List<byte[]> frames = new List<byte[]>();
        List<bool> isPng = new List<bool>();

        for (int i = 0; i < bmpSizes.Length; i++)
        {
            using (Bitmap b = Render(bmpSizes[i]))
            {
                sizes.Add(bmpSizes[i]);
                frames.Add(ToBmpFrame(b));
                isPng.Add(false);
            }
        }
        for (int i = 0; i < pngSizes.Length; i++)
        {
            using (Bitmap b = Render(pngSizes[i]))
            using (MemoryStream ms = new MemoryStream())
            {
                b.Save(ms, ImageFormat.Png);
                sizes.Add(pngSizes[i]);
                frames.Add(ms.ToArray());
                isPng.Add(true);
            }
        }

        using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write((short)0);
            w.Write((short)1);
            w.Write((short)sizes.Count);
            int offset = 6 + 16 * sizes.Count;
            for (int i = 0; i < sizes.Count; i++)
            {
                int s = sizes[i];
                w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((short)1);
                w.Write((short)32);
                w.Write(frames[i].Length);
                w.Write(offset);
                offset += frames[i].Length;
            }
            for (int i = 0; i < frames.Count; i++) w.Write(frames[i]);
        }
        Console.WriteLine("icon written: " + outPath + "  frames=" + sizes.Count
            + " (BMP " + bmpSizes.Length + " / PNG " + pngSizes.Length + ")");
    }

    // 传统 ICO 帧：BITMAPINFOHEADER + XOR(32bpp BGRA, 自下而上) + AND 掩码
    static byte[] ToBmpFrame(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        int xorStride = w * 4;
        int andStride = ((w + 31) / 32) * 4;
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter w2 = new BinaryWriter(ms))
        {
            w2.Write(40);                 // biSize
            w2.Write(w);                  // biWidth
            w2.Write(h * 2);              // biHeight = XOR + AND
            w2.Write((short)1);           // biPlanes
            w2.Write((short)32);          // biBitCount
            w2.Write(0);                  // biCompression
            w2.Write(xorStride * h);      // biSizeImage
            w2.Write(0); w2.Write(0); w2.Write(0); w2.Write(0);   // 分辨率/调色板

            for (int y = h - 1; y >= 0; y--)          // 自下而上
                for (int x = 0; x < w; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    w2.Write(c.B); w2.Write(c.G); w2.Write(c.R); w2.Write(c.A);
                }

            for (int y = h - 1; y >= 0; y--)          // AND 掩码：0=不透明
            {
                byte[] row = new byte[andStride];
                for (int x = 0; x < w; x++)
                {
                    if (bmp.GetPixel(x, y).A < 128) row[x / 8] |= (byte)(0x80 >> (x % 8));
                }
                w2.Write(row);
            }
            return ms.ToArray();
        }
    }

    static Bitmap Render(int size)
    {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            float pad = size * 0.055f;
            float r = size * 0.22f;
            RectangleF rect = new RectangleF(pad, pad, size - pad * 2, size - pad * 2);
            using (GraphicsPath path = RoundRect(rect, r))
            using (LinearGradientBrush br = new LinearGradientBrush(rect,
                    Color.FromArgb(255, 60, 130, 246), Color.FromArgb(255, 22, 58, 120), 90f))
            {
                g.FillPath(br, path);
                using (Pen p = new Pen(Color.FromArgb(120, 150, 200, 255), Math.Max(1f, size / 48f)))
                    g.DrawPath(p, path);
            }

            string txt = "V";
            float fs2 = size * 0.66f;
            using (FontFamily fam = PickFont())
            using (Font f = new Font(fam, fs2, FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                RectangleF tr = new RectangleF(0, size * 0.02f, size, size * 0.96f);
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
                    g.DrawString(txt, f, sh, new RectangleF(tr.X + size * 0.012f, tr.Y + size * 0.012f, tr.Width, tr.Height), sf);
                using (SolidBrush fg = new SolidBrush(Color.White))
                    g.DrawString(txt, f, fg, tr, sf);
            }
        }
        return bmp;
    }

    static FontFamily PickFont()
    {
        string[] want = new string[] { "Segoe UI", "Arial", "Tahoma" };
        FontFamily[] fams = FontFamily.Families;
        for (int i = 0; i < want.Length; i++)
            for (int k = 0; k < fams.Length; k++)
                if (string.Equals(fams[k].Name, want[i], StringComparison.OrdinalIgnoreCase)) return fams[k];
        return FontFamily.GenericSansSerif;
    }

    static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        GraphicsPath p = new GraphicsPath();
        float d = radius * 2f;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
