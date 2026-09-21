using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

static class MakeIcon
{
    static int Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "calc.ico";
        int[] sizes = { 256, 64, 48, 32, 16 };
        var bitmaps = new Bitmap[sizes.Length];
        for (int i = 0; i < sizes.Length; i++)
            bitmaps[i] = Draw(sizes[i]);
        WriteIco(path, bitmaps);
        foreach (Bitmap bitmap in bitmaps)
            bitmap.Dispose();
        return 0;
    }

    static Bitmap Draw(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float pad = Math.Max(1f, size * 0.06f);
            float side = size - pad * 2f;
            float radius = side * 0.28f;
            using (GraphicsPath tile = RoundRect(pad, pad, side, side, radius))
            using (var fill = new SolidBrush(Color.FromArgb(255, 140, 153, 141)))
                g.FillPath(fill, tile);

            float displayX = pad + side * 0.16f;
            float displayY = pad + side * 0.16f;
            float displayW = side * 0.68f;
            float displayH = side * 0.22f;
            using (GraphicsPath screen = RoundRect(displayX, displayY, displayW, displayH, displayH * 0.4f))
            using (var cream = new SolidBrush(Color.FromArgb(255, 244, 242, 236)))
                g.FillPath(cream, screen);

            float dot = side * 0.12f;
            float gap = side * 0.08f;
            float gridW = dot * 2f + gap;
            float originX = pad + (side - gridW) / 2f;
            float originY = pad + side * 0.52f;
            using (var cream = new SolidBrush(Color.FromArgb(255, 244, 242, 236)))
            {
                for (int row = 0; row < 2; row++)
                {
                    for (int col = 0; col < 2; col++)
                    {
                        float x = originX + col * (dot + gap);
                        float y = originY + row * (dot + gap);
                        g.FillEllipse(cream, x, y, dot, dot);
                    }
                }
            }
        }
        return bmp;
    }

    static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
    {
        float d = r * 2f;
        if (d > w) d = w;
        if (d > h) d = h;
        var path = new GraphicsPath();
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    static void WriteIco(string path, Bitmap[] bitmaps)
    {
        using (FileStream fs = File.Create(path))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write((ushort)0);
            bw.Write((ushort)1);
            bw.Write((ushort)bitmaps.Length);
            var blobs = new byte[bitmaps.Length][];
            for (int i = 0; i < bitmaps.Length; i++)
                blobs[i] = Dib(bitmaps[i]);

            int offset = 6 + 16 * bitmaps.Length;
            for (int i = 0; i < bitmaps.Length; i++)
            {
                int w = bitmaps[i].Width;
                int h = bitmaps[i].Height;
                bw.Write((byte)(w >= 256 ? 0 : w));
                bw.Write((byte)(h >= 256 ? 0 : h));
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write((uint)blobs[i].Length);
                bw.Write((uint)offset);
                offset += blobs[i].Length;
            }
            foreach (byte[] blob in blobs)
                bw.Write(blob);
        }
    }

    static byte[] Dib(Bitmap bmp)
    {
        int w = bmp.Width;
        int h = bmp.Height;
        int xorSize = w * h * 4;
        int stride = ((w + 31) / 32) * 4;
        int andSize = stride * h;
        var data = new byte[40 + xorSize + andSize];
        WriteInt(data, 0, 40);
        WriteInt(data, 4, w);
        WriteInt(data, 8, h * 2);
        data[12] = 1;
        data[14] = 32;
        WriteInt(data, 20, xorSize + andSize);

        BitmapData bits = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int srcStride = bits.Stride;
            var raw = new byte[srcStride * h];
            Marshal.Copy(bits.Scan0, raw, 0, raw.Length);
            for (int y = 0; y < h; y++)
            {
                int dstY = h - 1 - y;
                for (int x = 0; x < w; x++)
                {
                    int si = y * srcStride + x * 4;
                    byte b = raw[si];
                    byte g = raw[si + 1];
                    byte r = raw[si + 2];
                    byte a = raw[si + 3];
                    int di = 40 + (dstY * w + x) * 4;
                    data[di] = (byte)(b * a / 255);
                    data[di + 1] = (byte)(g * a / 255);
                    data[di + 2] = (byte)(r * a / 255);
                    data[di + 3] = a;
                    if (a < 128)
                    {
                        int ai = 40 + xorSize + dstY * stride + (x / 8);
                        data[ai] |= (byte)(0x80 >> (x % 8));
                    }
                }
            }
        }
        finally
        {
            bmp.UnlockBits(bits);
        }
        return data;
    }

    static void WriteInt(byte[] data, int offset, int value)
    {
        data[offset] = (byte)value;
        data[offset + 1] = (byte)(value >> 8);
        data[offset + 2] = (byte)(value >> 16);
        data[offset + 3] = (byte)(value >> 24);
    }
}
