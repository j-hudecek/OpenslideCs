using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public unsafe sealed class OpenSlide
    {
        private class Openslide : IDisposable
        {
            internal const int TILE_SIZE = 512;
            /// <summary>
            /// TILE_SIZE - overlaps (1px on each side)
            /// </summary>
            const int TILE_DOWNSAMPLE = TILE_SIZE - 2;
            /*
             * For DZ we need the levels to be always twice more zoomed in. The slide file can have levels (os_levels) with ratio 1000 between them. We need to recalculate our levels.
             * based on https://github.com/openslide/openslide-python/blob/master/openslide/deepzoom.py
             */
            public int* handle;
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

            public int[] EasyLevels;


            private string origfile;

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
                OpenSlide.TraceMsg( "start openslide " + filename);
                origfile = filename;
                if (!File.Exists(filename))
                    throw new ArgumentException($"File '{filename}' can't be opened");
                handle = openslide_open(filename);
                unsafe
                {
                    if (handle == null || handle[0] == 0)
                    {
                        var vendor = openslide_detect_vendor(filename);
                        //GetLastError();
                        if (vendor.ToInt32() != 0)
                            throw new ArgumentException("Vendor " + Marshal.PtrToStringAnsi(vendor) + " unsupported?");
                        else
                            throw new ArgumentException("File unrecognized");
                    }
                }
                OpenSlide.TraceMsg( "opened openslide " + filename);
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
                l0_z_downsamples = Enumerable.Range(0, max_dz_level).Select(x => (int)Math.Pow(2, max_dz_level - x - 1)).ToArray();
                os_level_for_dz_level = l0_z_downsamples.Select(x =>
                {
                    var best_level = openslide_get_best_level_for_downsample(handle, x * 1.01);
                    if (best_level == -1)
                        GetLastError();
                    return best_level;
                }).ToArray();
                l_z_downsamples = Enumerable.Range(0, max_dz_level).Select(l => l0_z_downsamples[l] / downsamples[os_level_for_dz_level[l]]).ToArray();
                InitEasyLevels();
                OpenSlide.TraceMsg( "end openslide " + filename);
            }

            public MemoryStream GetTile(int level, long row, long col)
            {
                OpenSlide.TraceMsg( "start gettile " + level+"/"+ col + "_" + row);
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
                OpenSlide.TraceMsg("calcs done " + level + "/" + col + "_" + row);
                var bmp = ReadRegion(l0_location, os_level, l_size);
                if (l_size.Width != z_size.Width || l_size.Height != z_size.Height)
                { //only resize when necessary
                    OpenSlide.TraceMsg("resize " + level + "/" + col + "_" + row);
                    bmp = new Bitmap(bmp, (int)z_size.Width, (int)z_size.Height);
                }
                OpenSlide.TraceMsg( "new bmp " + level + "/" + col + "_" + row);
                var stream = new MemoryStream();
                //Prints tile coords for testing
                //var g = Graphics.FromImage(resizedbmp);
                //g.DrawString(level + "/" + col + "_" + row, new Font(FontFamily.GenericSansSerif, 18), new SolidBrush(Color.Black), resizedbmp.Width / 2, resizedbmp.Height / 2);
                bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                OpenSlide.TraceMsg( "end gettile " + level + "/" + col + "_" + row);
                stream.Position = 0;
                return stream;
            }

            public long Height
            {
                get
                {
                    return dimensions[0].Height;
                }
            }

            public long Width
            {
                get
                {
                    return dimensions[0].Width;
                }
            }
            private static string GetBytesReadable(long i)
            {
                // Get absolute value
                long absolute_i = (i < 0 ? -i : i);
                // Determine the suffix and readable value
                string suffix;
                double readable;
                if (absolute_i >= 0x1000000000000000) // Exabyte
                {
                    suffix = "EB";
                    readable = (i >> 50);
                }
                else if (absolute_i >= 0x4000000000000) // Petabyte
                {
                    suffix = "PB";
                    readable = (i >> 40);
                }
                else if (absolute_i >= 0x10000000000) // Terabyte
                {
                    suffix = "TB";
                    readable = (i >> 30);
                }
                else if (absolute_i >= 0x40000000) // Gigabyte
                {
                    suffix = "GB";
                    readable = (i >> 20);
                }
                else if (absolute_i >= 0x100000) // Megabyte
                {
                    suffix = "MB";
                    readable = (i >> 10);
                }
                else if (absolute_i >= 0x400) // Kilobyte
                {
                    suffix = "KB";
                    readable = i;
                }
                else
                {
                    return i.ToString("0 B"); // Byte
                }
                // Divide by 1024 to get fractional value
                readable = (readable / 1024);
                // Return formatted number with suffix
                return readable.ToString("0.### ") + suffix;
            }

            private Bitmap ReadRegion(SizeL location, int level, SizeL size)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                OpenSlide.TraceMsg( "start ReadRegion " + level + "/" + location.Height + "_" + location.Width+": "+ GetBytesReadable(size.Width*size.Height*3));
                Bitmap bmp = new Bitmap((int)size.Width, (int)size.Height);
                bmp.SetPixel(0, 0, Color.AliceBlue);
                var bmpdata = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                OpenSlide.TraceMsg("bmp locked " + level + "/" + location.Height + "_" + location.Width);
                unsafe
                {
                    void* p = bmpdata.Scan0.ToPointer();
                    openslide_read_region(handle, p, location.Width, location.Height, level, size.Width, size.Height);
                }
                OpenSlide.TraceMsg("read finished " + level + "/" + location.Height + "_" + location.Width + ": " + GetBytesReadable(size.Width * size.Height * 3 / Math.Max(sw.ElapsedMilliseconds, 1)) + "/ms");
                bmp.UnlockBits(bmpdata);
                OpenSlide.TraceMsg( "unlock bits " + level + "/" + location.Height + "_" + location.Width);
                if (bmp.GetPixel(0, 0) == Color.Black)
                {
                    var error = CheckForLastError();
                    if (error != null)
                        throw new ArgumentException($"error reading region loc:{location}, level:{level}, size:{size}" + error);
                    //else just a black image?
                }
                OpenSlide.TraceMsg( "end ReadRegion " + level + "/" + location.Height + "_" + location.Width);
                return bmp;
            }

            public MemoryStream ReadThumbnail(int minsize)
            {
                for (int i = 0; i < z_dimensions.Count; i++)
                {
                    var d = z_dimensions[i];
                    if (d.Width >= minsize || d.Height >= minsize)
                        return GetTile(i, 0, 0);
                }
                return null;
            }

            public double GetMPP()
            {
                double DEFAULT_MPP = 0.19872813990461;
                var prop = openslide_get_property_value(handle, OPENSLIDE_PROPERTY_NAME_MPP_X);
                if (prop.ToInt32() == 0)
                    GetLastError();
                var propstring = Marshal.PtrToStringAnsi(prop);
                double ret = DEFAULT_MPP;
                Double.TryParse(propstring.Replace(",", "."), out ret);
                if (ret < 1e-10 || ret > 1000)
                {
                    ret = DEFAULT_MPP;
                    Double.TryParse(propstring.Replace(".",","), out ret);
                }
                return ret;
            }

            /// <summary>
            /// Returns levels without scaling
            /// </summary>
            /// <returns></returns>
            internal void InitEasyLevels()
            {
                var ret = new List<int>();
                for (int i = 0; i < max_dz_level; i++)
                {
                    if (Math.Abs(l0_z_downsamples[i] - 1) < 0.01)
                        ret.Add(i);
                }
                EasyLevels = ret.ToArray();
            }

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                    }
                    unsafe
                    {
                        if (handle != null && handle[0] != 0)
                        {
                            openslide_close(handle);
                        }
                    }

                    disposedValue = true;
                }
            }

            ~Openslide()
            {
                Dispose(false);
            }

            public void Dispose()
            {
                Dispose(true);
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
        /// <summary>
        /// microns per pixel
        /// </summary>
        const string OPENSLIDE_PROPERTY_NAME_MPP_X = "openslide.mpp-x";
        const string OPENSLIDE_PROPERTY_NAME_MPP_Y = "openslide.mpp-y";

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int* openslide_open([MarshalAs(UnmanagedType.LPStr)]string filename);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr openslide_detect_vendor([MarshalAs(UnmanagedType.LPStr)]string filename);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int openslide_get_level_count(int* osr);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void openslide_get_level0_dimensions(int* osr, out long w, out long h);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void openslide_get_level_dimensions(int* osr, int level, out long w, out long h);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern double openslide_get_level_downsample(int* osr, int level);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int openslide_get_best_level_for_downsample(int* osr, double downsample);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void openslide_read_region(int* osr,
               void* dest,
               long x, long y,
               int level,
               long w, long h);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void openslide_close(int* osr);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr openslide_get_error(int* osr);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr openslide_get_property_value(int* osr, [MarshalAs(UnmanagedType.LPStr)]string name);

        static Dictionary<string, Openslide> slides = new Dictionary<string, Openslide>();

        static object slides_lock = new Object();

        private Openslide GetOpenSlide(string filename, bool canOpenFile = true)
        {
            if (!slides.ContainsKey(filename))
            {
                lock (slides_lock)
                {
                    if (!slides.ContainsKey(filename))
                    {
                        if (!canOpenFile)
                            return null;
                        slides.Add(filename, new Openslide(filename));
                        //If we want to clean up the cache
                        //Task.Run(async delegate
                        //{
                        //    await Task.Delay(1000 * 60 * 60 * 6);
                        //    lock (slides_lock)
                        //    {
                        //        if (slides.ContainsKey(filename))
                        //        {
                        //            slides[filename].Dispose();
                        //            slides.Remove(filename);
                        //        }
                        //    }
                        //    return 42; //gotta return something
                        //});
                    }
                }
            }
            return slides[filename];
        }

        public MemoryStream GetDZI(string filename, out long width, out long height)
        {
            var ost = GetOpenSlide(filename);
            width = ost.Width;
            height = ost.Height;
            return GetDZI(width, height);
        }


        static string dzitemplate = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Image xmlns=\"http://schemas.microsoft.com/deepzoom/2008\"  Format=\"jpeg\"  Overlap=\"1\"  TileSize=\"@TileSize\">" +
                    "<Size Height=\"@Height\" Width=\"@Width\"/></Image>";

        public static MemoryStream GetDZI(long width, long height)
        {
            var s = dzitemplate.Replace("@Width", width.ToString()).Replace("@Height", height.ToString()).Replace("@TileSize", (Openslide.TILE_SIZE - 2).ToString());
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        public MemoryStream GetJpg(string filename, string path)
        {
            //url = '/<ID>_files/<int:level>/<int:col>_<int:row>.<format>'
            OpenSlide.TraceMsg( "start getjpg " + path);
            var rx = new Regex("_files/([0-9]*)/([0-9]*)_([0-9]*).jpeg");
            var m = rx.Match(path);
            if (m.Success)
            {
                var ost = GetOpenSlide(filename);
                OpenSlide.TraceMsg( "open os " + path);
                var level = Int32.Parse(m.Groups[1].Value);
                var col = Int64.Parse(m.Groups[2].Value);
                var row = Int64.Parse(m.Groups[3].Value);
                var ret = ost.GetTile(level, row, col);
                OpenSlide.TraceMsg( "end getjpg " + path);
                return ret;
            }
            return null;
        }

        public void EnsureOpen(string filename)
        {
            GetOpenSlide(filename);
        }

        public bool IsEasyLevel(string filename, int level)
        {
            var ost = GetOpenSlide(filename, false);
            if (ost == null)
                return true; //this can happen before the background thread opens all images. Default to showing all levels
            return ost.EasyLevels.Contains(level);
        }

        public MemoryStream GetThumbnail(string filemame, int minsize)
        {
            var ost = GetOpenSlide(filemame);
            return ost.ReadThumbnail(minsize);
        }

        public double GetMPP(string filemame)
        {
            var ost = GetOpenSlide(filemame);
            return ost.GetMPP();
        }

        public static Action<String> OnTrace;

        private static void TraceMsg(string m)
        {
            if (OnTrace != null)
                OnTrace(m);
        }

        public static Tuple<long, long> TestPerf(string testpath)
        {
            var ret = new Tuple<long, long>(0,0);
            var buffer = new byte[4096];
            var sw = new Stopwatch();
            sw.Start();
            using (var f = File.OpenRead(testpath))
            {
                f.Read(buffer, 0, 4096);
            }
            var TTfirstpage = sw.ElapsedMilliseconds;
            var copyto = Path.GetTempFileName();
            File.Delete(copyto);
            sw.Restart();
            File.Copy(testpath, copyto);
            var TTcompletefile = sw.ElapsedMilliseconds;
            File.Delete(copyto);
            return new Tuple<long, long>(TTfirstpage, TTcompletefile);
        }
    }
}
