using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace IconBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            string inputPath = @"C:\Users\seojy\.gemini\antigravity\brain\e296da4b-754d-45e6-90fb-88e27e6cdd3b\mc_installer_icon_1788882827019.jpg";
            string clientDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @".."));
            string outDir = Path.Combine(clientDir, "src");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            string outIcoPath = Path.Combine(outDir, "app.ico");
            string outPngPath = Path.Combine(outDir, "app_icon.png");
            string outArtifactPng = @"C:\Users\seojy\.gemini\antigravity\brain\e296da4b-754d-45e6-90fb-88e27e6cdd3b\app_icon_preview.png";

            Console.WriteLine("Loading: " + inputPath);
            using (Bitmap raw = new Bitmap(inputPath))
            {
                // 1. Remove background via flood-fill from corners
                Console.WriteLine("Removing background...");
                Bitmap cleanBmp = RemoveBlackBackground(raw);

                // 2. Crop to bounding box and add 5% padding
                Console.WriteLine("Cropping to icon bounds...");
                Bitmap croppedBmp = CropAndPad(cleanBmp, 0.04f);
                cleanBmp.Dispose();

                // Save preview PNG
                croppedBmp.Save(outPngPath, ImageFormat.Png);
                try { croppedBmp.Save(outArtifactPng, ImageFormat.Png); } catch { }
                Console.WriteLine("Preview PNG saved: " + outPngPath);

                // 3. Generate multi-resolution ICO
                Console.WriteLine("Generating multi-res ICO (256, 64, 48, 32, 16)...");
                int[] sizes = new int[] { 256, 64, 48, 32, 16 };
                List<byte[]> imageBuffers = new List<byte[]>();
                List<IconEntryInfo> entries = new List<IconEntryInfo>();

                foreach (int size in sizes)
                {
                    using (Bitmap scaled = ResizeBitmap(croppedBmp, size, size))
                    {
                        if (size == 256)
                        {
                            // PNG compressed for 256x256 (Windows standard since Vista)
                            using (MemoryStream ms = new MemoryStream())
                            {
                                scaled.Save(ms, ImageFormat.Png);
                                byte[] pngBytes = ms.ToArray();
                                imageBuffers.Add(pngBytes);
                                entries.Add(new IconEntryInfo
                                {
                                    Width = 0, // 0 means 256
                                    Height = 0,
                                    BitCount = 32,
                                    BytesInRes = (uint)pngBytes.Length
                                });
                            }
                        }
                        else
                        {
                            // Standard DIB (BITMAPINFOHEADER + BGRA + AND mask)
                            byte[] dibBytes = CreateDibIconData(scaled);
                            imageBuffers.Add(dibBytes);
                            entries.Add(new IconEntryInfo
                            {
                                Width = (byte)size,
                                Height = (byte)size,
                                BitCount = 32,
                                BytesInRes = (uint)dibBytes.Length
                            });
                        }
                    }
                }

                // Write complete ICO file
                using (FileStream fs = new FileStream(outIcoPath, FileMode.Create, FileAccess.Write))
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    // ICONDIR
                    bw.Write((ushort)0); // idReserved
                    bw.Write((ushort)1); // idType = 1 (ICO)
                    bw.Write((ushort)entries.Count); // idCount

                    // Calculate offsets
                    // Header is 6 bytes + (16 bytes * count)
                    uint offset = (uint)(6 + (16 * entries.Count));

                    for (int i = 0; i < entries.Count; i++)
                    {
                        var ent = entries[i];
                        bw.Write(ent.Width);
                        bw.Write(ent.Height);
                        bw.Write((byte)0); // color count
                        bw.Write((byte)0); // reserved
                        bw.Write((ushort)1); // planes
                        bw.Write((ushort)ent.BitCount);
                        bw.Write(ent.BytesInRes);
                        bw.Write(offset);

                        offset += ent.BytesInRes;
                    }

                    // Write image payloads
                    for (int i = 0; i < imageBuffers.Count; i++)
                    {
                        bw.Write(imageBuffers[i]);
                    }
                }

                croppedBmp.Dispose();
                Console.WriteLine("ICO successfully generated: " + outIcoPath);
                FileInfo fi = new FileInfo(outIcoPath);
                Console.WriteLine("ICO File Size: " + fi.Length + " bytes");

                using (Icon testIcon = new Icon(outIcoPath))
                {
                    Console.WriteLine("Self-test passed! Icon loaded: " + testIcon.Width + "x" + testIcon.Height);
                }
            }
        }

        struct IconEntryInfo
        {
            public byte Width;
            public byte Height;
            public ushort BitCount;
            public uint BytesInRes;
        }

        static Bitmap RemoveBlackBackground(Bitmap src)
        {
            int w = src.Width;
            int h = src.Height;
            Bitmap dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            BitmapData srcData = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dstData = dst.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            int byteCount = srcData.Stride * h;
            byte[] srcBytes = new byte[byteCount];
            byte[] dstBytes = new byte[byteCount];
            Marshal.Copy(srcData.Scan0, srcBytes, 0, byteCount);
            Buffer.BlockCopy(srcBytes, 0, dstBytes, 0, byteCount);

            bool[,] visited = new bool[w, h];
            Queue<Point> q = new Queue<Point>();

            for (int x = 0; x < w; x++)
            {
                q.Enqueue(new Point(x, 0));
                visited[x, 0] = true;
                q.Enqueue(new Point(x, h - 1));
                visited[x, h - 1] = true;
            }
            for (int y = 0; y < h; y++)
            {
                q.Enqueue(new Point(0, y));
                visited[0, y] = true;
                q.Enqueue(new Point(w - 1, y));
                visited[w - 1, y] = true;
            }

            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            while (q.Count > 0)
            {
                Point pt = q.Dequeue();
                int px = pt.X;
                int py = pt.Y;

                int idx = (py * srcData.Stride) + (px * 4);
                byte b = srcBytes[idx];
                byte g = srcBytes[idx + 1];
                byte r = srcBytes[idx + 2];

                int maxVal = Math.Max(r, Math.Max(g, b));

                if (maxVal <= 18)
                {
                    dstBytes[idx + 3] = 0; // completely transparent
                }
                else if (maxVal <= 45)
                {
                    // soft antialiasing
                    dstBytes[idx + 3] = (byte)((maxVal - 18) * 255 / (45 - 18));
                }
                else
                {
                    // reached object edge
                    continue;
                }

                for (int i = 0; i < 4; i++)
                {
                    int nx = px + dx[i];
                    int ny = py + dy[i];
                    if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                    {
                        if (!visited[nx, ny])
                        {
                            visited[nx, ny] = true;
                            q.Enqueue(new Point(nx, ny));
                        }
                    }
                }
            }

            Marshal.Copy(dstBytes, 0, dstData.Scan0, byteCount);
            src.UnlockBits(srcData);
            dst.UnlockBits(dstData);

            return dst;
        }

        static Bitmap CropAndPad(Bitmap src, float padRatio)
        {
            int w = src.Width;
            int h = src.Height;

            int minX = w, minY = h, maxX = 0, maxY = 0;

            BitmapData data = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] bytes = new byte[data.Stride * h];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            src.UnlockBits(data);

            for (int y = 0; y < h; y++)
            {
                int rowOffset = y * data.Stride;
                for (int x = 0; x < w; x++)
                {
                    byte a = bytes[rowOffset + (x * 4) + 3];
                    if (a > 10)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (minX > maxX || minY > maxY)
            {
                return (Bitmap)src.Clone();
            }

            int contentW = maxX - minX + 1;
            int contentH = maxY - minY + 1;
            int maxDim = Math.Max(contentW, contentH);

            int padding = (int)(maxDim * padRatio);
            int finalDim = maxDim + (padding * 2);

            Bitmap result = new Bitmap(finalDim, finalDim, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                int destX = padding + (maxDim - contentW) / 2;
                int destY = padding + (maxDim - contentH) / 2;

                g.DrawImage(src,
                    new Rectangle(destX, destY, contentW, contentH),
                    new Rectangle(minX, minY, contentW, contentH),
                    GraphicsUnit.Pixel);
            }

            return result;
        }

        static Bitmap ResizeBitmap(Bitmap src, int width, int height)
        {
            Bitmap dst = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(src, 0, 0, width, height);
            }
            return dst;
        }

        static byte[] CreateDibIconData(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;

            int headerSize = 40; // BITMAPINFOHEADER
            int xorSize = w * h * 4; // 32-bit BGRA
            int maskRowBytes = ((w + 31) / 32) * 4;
            int andMaskSize = maskRowBytes * h;
            int totalSize = headerSize + xorSize + andMaskSize;

            byte[] buffer = new byte[totalSize];
            using (MemoryStream ms = new MemoryStream(buffer))
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                // BITMAPINFOHEADER
                bw.Write((uint)headerSize); // biSize
                bw.Write(w); // biWidth
                bw.Write(h * 2); // biHeight (Important: MUST be 2 * height)
                bw.Write((ushort)1); // biPlanes
                bw.Write((ushort)32); // biBitCount
                bw.Write((uint)0); // biCompression (BI_RGB)
                bw.Write((uint)xorSize); // biSizeImage
                bw.Write(0); // biXPelsPerMeter
                bw.Write(0); // biYPelsPerMeter
                bw.Write((uint)0); // biClrUsed
                bw.Write((uint)0); // biClrImportant

                // Lock bitmap for BGRA bottom-up readout
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                byte[] raw = new byte[data.Stride * h];
                Marshal.Copy(data.Scan0, raw, 0, raw.Length);
                bmp.UnlockBits(data);

                // Write XOR mask (bottom row first)
                for (int y = h - 1; y >= 0; y--)
                {
                    int rowOffset = y * data.Stride;
                    for (int x = 0; x < w; x++)
                    {
                        int px = rowOffset + (x * 4);
                        bw.Write(raw[px]);     // B
                        bw.Write(raw[px + 1]); // G
                        bw.Write(raw[px + 2]); // R
                        bw.Write(raw[px + 3]); // A
                    }
                }

                // Write 1-bit AND mask (bottom row first)
                // 1 means transparent, 0 means opaque
                for (int y = h - 1; y >= 0; y--)
                {
                    int rowOffset = y * data.Stride;
                    byte currentByte = 0;
                    int bitIndex = 7;
                    int bytesWrittenInRow = 0;

                    for (int x = 0; x < w; x++)
                    {
                        byte alpha = raw[rowOffset + (x * 4) + 3];
                        if (alpha < 128)
                        {
                            currentByte |= (byte)(1 << bitIndex);
                        }

                        bitIndex--;
                        if (bitIndex < 0)
                        {
                            bw.Write(currentByte);
                            bytesWrittenInRow++;
                            currentByte = 0;
                            bitIndex = 7;
                        }
                    }

                    if (bitIndex != 7)
                    {
                        bw.Write(currentByte);
                        bytesWrittenInRow++;
                    }

                    // Pad to 4-byte boundary
                    while (bytesWrittenInRow < maskRowBytes)
                    {
                        bw.Write((byte)0);
                        bytesWrittenInRow++;
                    }
                }
            }

            return buffer;
        }
    }
}
