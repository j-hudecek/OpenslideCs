using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSlideCs
{
    public sealed class OpenSlide
    {
        private class Openslide : IDisposable
        {
            const int TILE_SIZE = 512;
            /// <summary>
            /// TILE_SIZE - overlaps (1px on each side)
            /// </summary>
            const int TILE_DOWNSAMPLE = TILE_SIZE - 2;
            /*
             * For DZ we need the levels to be always twice more zoomed in. The slide file can have levels (os_levels) with ratio 1000 between them. We need to recalculate our levels.
             * based on https://github.com/openslide/openslide-python/blob/master/openslide/deepzoom.py
             */
            public IntPtr handle;
            public int max_os_level;
            /// <summary>
            /// dimensions per os_level
            /// </summary>
            List<SizeL> dimensions = new List<SizeL>();
            /// <summary>
            /// downsample per os_level (how many original pixels are represented by one pixel) usually first level is 1?
            /// </summary>
            List<double> downsamples = new List<double>();
            /// <summary>
            ///dimensions per dz_level (in pixels)
            /// </summary>
            List<SizeL> z_dimensions = new List<SizeL>();
            /// <summary>
            ///dimensions per dz_level (in tiles)
            /// </summary>
            SizeL[] t_dimensions;

            /// <summary>
            ///Total downsamples for each dz_level (powers of 2) 
            /// </summary>
            int[] l0_z_downsamples;

            /// <summary>
            /// Deep zoom levels
            /// </summary>
            int max_dz_level;

            /// <summary>
            /// Best os_level for dz_level based on downsample(slide_from_dz_level)
            /// </summary>
            int[] os_level_for_dz_level;

            /// <summary>
            /// 
            /// </summary>
            double[] l_z_downsamples;

            private void InitZDimensions()
            {
                ///size on a current dz_level
                var z_size = dimensions[0];
                z_dimensions.Add(z_size);
                while (z_size.Width > 1 || z_size.Height > 1)
                {
                    z_size = new SizeL((long)Math.Max(1, Math.Ceiling(z_size.Width / 2.0)),
                                        (long)Math.Max(1, Math.Ceiling(z_size.Height / 2.0)));
                    z_dimensions.Add(z_size);
                }
                z_dimensions.Reverse();
            }

            public string GetLastError()
            {
                var error = CheckForLastError();
                if (error != null)
                    throw new ArgumentException("openslide error: " + error);
                else
                    throw new ArgumentException("openslide error, but error is empty?");
            }

            public string CheckForLastError()
            {
                var lastError = openslide_get_error(handle);
                if (lastError.ToInt32() == 0)
                    return null;
                else
                    return Marshal.PtrToStringAnsi(lastError);
            }

            public Openslide(string filename)
            {
                handle = openslide_open(filename);
                if (handle.ToInt32() == 0)
                    GetLastError();
                max_os_level = openslide_get_level_count(handle);
                if (max_os_level == -1)
                    GetLastError();
                for (int level = 0; level < max_os_level; level++)
                {
                    long w = 0, h = 0;
                    openslide_get_level_dimensions(handle, level, out w, out h);
                    if (w == -1 || h == -1)
                        GetLastError();
                    dimensions.Add(new SizeL(w, h));
                    var downsample = openslide_get_level_downsample(handle, level);
                    if (downsample == -1.0)
                        GetLastError();
                    downsamples.Add(downsample);
                }
                InitZDimensions();
                t_dimensions = z_dimensions.Select(x => new SizeL((long)Math.Ceiling(x.Width  / (double)TILE_DOWNSAMPLE),
                                                                  (long)Math.Ceiling(x.Height / (double)TILE_DOWNSAMPLE))).ToArray();

                max_dz_level = z_dimensions.Count;
                var l0_z_downsamples = Enumerable.Range(0, max_dz_level).Select(x => (int)Math.Pow(2, max_dz_level - x - 1)).ToArray();
                os_level_for_dz_level = l0_z_downsamples.Select(x =>
                {
                    var best_level = openslide_get_best_level_for_downsample(handle, x);
                    if (best_level == -1)
                        GetLastError();
                    return best_level;
                }).ToArray();
                l_z_downsamples = Enumerable.Range(0, max_dz_level).Select(l => l0_z_downsamples[l] / downsamples[os_level_for_dz_level[l]]).ToArray();
            }

            public MemoryStream GetTile(int level, long row, long col)
            {
                if (level < 0 || level >= max_dz_level)
                    throw new ArgumentException($"wrong level level {level}, row {row}, col {col}");
                if (t_dimensions[level].Width <= col || t_dimensions[level].Height <= row ||
                    0 > col || 0 > row)
                    throw new ArgumentException($"wrong address level {level}, row {row}, col {col}");
                var os_level = os_level_for_dz_level[level];
                //Calculate top/ left and bottom/ right overlap
                var z_overlap_tl = new SizeL(col == 0 ? 0 : 1, 
                                             row == 0 ? 0 : 1);
                var z_overlap_br = new SizeL(col == t_dimensions[level].Width ? 0 : 1, 
                                             row == t_dimensions[level].Height ? 0 : 1);

                var z_size = new SizeL(Math.Min(TILE_DOWNSAMPLE, z_dimensions[level].Width  - TILE_DOWNSAMPLE * col) + z_overlap_tl.Width  + z_overlap_br.Width,
                                       Math.Min(TILE_DOWNSAMPLE, z_dimensions[level].Height - TILE_DOWNSAMPLE * row) + z_overlap_tl.Height + z_overlap_br.Height);
                if (z_size.Width < 0 || z_size.Height < 0)
                    throw new ArgumentException($"out of bounds level {level}, row {row}, col {col}");
                var z_location = new SizeL(TILE_DOWNSAMPLE * col, TILE_DOWNSAMPLE * row);
                var l_location = new SizeF((float)l_z_downsamples[level] * (z_location.Width  - z_overlap_tl.Width),
                                           (float)l_z_downsamples[level] * (z_location.Height - z_overlap_tl.Height));
                //Round location down and size up, and add offset of active area
                var l0_location = new SizeL((long)(downsamples[os_level] * l_location.Width), 
                                            (long)(downsamples[os_level] * l_location.Height));
                var l_size = new SizeL((long)Math.Min(Math.Ceiling(l_z_downsamples[level] * z_size.Width ), dimensions[os_level].Width),
                                       (long)Math.Min(Math.Ceiling(l_z_downsamples[level] * z_size.Height), dimensions[os_level].Height));
                var bmp = ReadRegion(l0_location, os_level, l_size);
                var resizedbmp = new Bitmap(bmp, (int)z_size.Width, (int)z_size.Height);
                var stream = new MemoryStream();
                resizedbmp.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                stream.Position = 0;
                return stream;
            }

            string dzitemplate = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Image xmlns=\"http://schemas.microsoft.com/deepzoom/2008\"  Format=\"jpeg\"  Overlap=\"1\"  TileSize=\"@TileSize\">" +
                        "<Size Height=\"@Height\" Width=\"@Width\"/></Image>";

            public MemoryStream GetDZI()
            {
                var s = dzitemplate.Replace("@Width", dimensions[0].Width.ToString()).Replace("@Height", dimensions[0].Height.ToString()).Replace("@TileSize", (TILE_SIZE - 2).ToString());
                MemoryStream stream = new MemoryStream();
                StreamWriter writer = new StreamWriter(stream);
                writer.Write(s);
                writer.Flush();
                stream.Position = 0;
                return stream;
            }

            private Bitmap ReadRegion(SizeL location, int level, SizeL size)
            {
                Bitmap bmp = new Bitmap((int)size.Width, (int)size.Height);
                bmp.SetPixel(0, 0, Color.AliceBlue);
                var bmpdata = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                openslide_read_region(handle, bmpdata.Scan0, location.Width, location.Height, level, size.Width, size.Height);
                bmp.UnlockBits(bmpdata);
                if (bmp.GetPixel(0, 0) == Color.Black)
                {
                    var error = CheckForLastError();
                    if (error != null)
                        throw new ArgumentException($"error reading region loc:{location}, level:{level}, size:{size}" + error);
                    //else just a black image?
                }
                return bmp;
            }

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        // TODO: dispose managed state (managed objects).
                    }
                    openslide_close(handle);
                    // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                    // TODO: set large fields to null.

                    disposedValue = true;
                }
            }

            // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
            ~Openslide()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(false);
            }

            // This code added to correctly implement the disposable pattern.
            public void Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(true);
                // TODO: uncomment the following line if the finalizer is overridden above.
                GC.SuppressFinalize(this);
            }
            #endregion
        }

        struct SizeL
        {
            public long Width;
            public long Height;
            public SizeL(long w, long h)
            {
                Width = w;
                Height = h;
            }

            public override string ToString()
            {
                return "w:" + Width + " h:" + Height;
            }
        }



        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr openslide_open([MarshalAs(UnmanagedType.LPStr)]string filename);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr openslide_detect_vendor([MarshalAs(UnmanagedType.LPStr)]string filename);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int openslide_get_level_count(IntPtr osr);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void openslide_get_level0_dimensions(IntPtr osr, out long w, out long h);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void openslide_get_level_dimensions(IntPtr osr, int level, out long w, out long h);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern double openslide_get_level_downsample(IntPtr osr, int level);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int openslide_get_best_level_for_downsample(IntPtr osr, double downsample);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void openslide_read_region(IntPtr osr,
               IntPtr dest,
               long x, long y,
               int level,
               long w, long h);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void openslide_close(IntPtr osr);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr openslide_get_error(IntPtr osr);

        static Dictionary<string, Openslide> slides = new Dictionary<string, Openslide>();

        private Openslide GetOpenSlide(string filename)
        {
            if (!slides.ContainsKey(filename))
            {
                slides.Add(filename, new Openslide(filename));
            }
            return slides[filename];
        }

        public MemoryStream GetDZI(string filename)
        {
            var ost = GetOpenSlide(filename);
            return ost.GetDZI();
        }

        public MemoryStream GetJpg(string filename, string path)
        {
            var rx = new Regex("_files/([0-9]*)/([0-9]*)_([0-9]*).jpeg");
            var m = rx.Match(path);
            if (m.Success)
            {
                var ost = GetOpenSlide(filename);
                var level = Int32.Parse(m.Groups[1].Value);
                var col = Int64.Parse(m.Groups[2].Value);
                var row = Int64.Parse(m.Groups[3].Value);
                return ost.GetTile(level, row, col);
            }
            return null;
        }
    }
}
