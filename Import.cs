using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenSlideCs
{
    unsafe class Import
    {
        #region windows
        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_detect_vendor")]
        private static extern IntPtr Wopenslide_detect_vendor([MarshalAs(UnmanagedType.LPStr)]string filename);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_open")]
        private static extern int* Wopenslide_open([MarshalAs(UnmanagedType.LPStr)]string filename);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_get_level_count")]
        private static extern int Wopenslide_get_level_count(int* osr);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_get_level0_dimensions")]
        private static extern void Wopenslide_get_level0_dimensions(int* osr, out long w, out long h);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_get_level_dimensions")]
        private static extern void Wopenslide_get_level_dimensions(int* osr, int level, out long w, out long h);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_get_level_downsample")]
        private static extern double Wopenslide_get_level_downsample(int* osr, int level);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_get_best_level_for_downsample")]
        private static extern int Wopenslide_get_best_level_for_downsample(int* osr, double downsample);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_read_region")]
        private static extern void Wopenslide_read_region(int* osr,
               void* dest,
               long x, long y,
               int level,
               long w, long h);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_close")]
        private static extern void Wopenslide_close(int* osr);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_get_error")]
        private static extern IntPtr Wopenslide_get_error(int* osr);

        [DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_get_property_value")]
        private static extern IntPtr Wopenslide_get_property_value(int* osr, [MarshalAs(UnmanagedType.LPStr)]string name);
        #endregion

        #region linux
        [DllImport("libopenslide.so.0", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_detect_vendor")]
        private static extern IntPtr Lopenslide_detect_vendor([MarshalAs(UnmanagedType.LPStr)]string filename);

        [DllImport("libopenslide.so.0", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_open")]
        private static extern int* Lopenslide_open([MarshalAs(UnmanagedType.LPStr)]string filename);

        [DllImport("libopenslide.so.0", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_get_level_count")]
        private static extern int Lopenslide_get_level_count(int* osr);

        [DllImport("libopenslide.so.0", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_get_level0_dimensions")]
        private static extern void Lopenslide_get_level0_dimensions(int* osr, out long w, out long h);

        [DllImport("libopenslide.so.0", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_get_level_dimensions")]
        private static extern void Lopenslide_get_level_dimensions(int* osr, int level, out long w, out long h);

        [DllImport("libopenslide.so.0", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_get_level_downsample")]
        private static extern double Lopenslide_get_level_downsample(int* osr, int level);

        [DllImport("libopenslide.so.0", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_get_best_level_for_downsample")]
        private static extern int Lopenslide_get_best_level_for_downsample(int* osr, double downsample);

        [DllImport("libopenslide.so.0", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_read_region")]
        private static extern void Lopenslide_read_region(int* osr,
               void* dest,
               long x, long y,
               int level,
               long w, long h);

        [DllImport("libopenslide.so.0", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_close")]
        private static extern void Lopenslide_close(int* osr);

        [DllImport("libopenslide.so.0", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_get_error")]
        private static extern IntPtr Lopenslide_get_error(int* osr);

        [DllImport("libopenslide.so.0", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "openslide_get_property_value")]
        private static extern IntPtr Lopenslide_get_property_value(int* osr, [MarshalAs(UnmanagedType.LPStr)]string name);

        #endregion 

        private static bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);


        public static Func<string, IntPtr> openslide_detect_vendor =
            (x => isWindows ? Wopenslide_detect_vendor(x) : Lopenslide_detect_vendor(x));

        public static Func<string, IntPtr> openslide_open =
            (x => isWindows ? new IntPtr(Wopenslide_open(x)) : new IntPtr(Lopenslide_open(x)));

        public static Func<IntPtr, int> openslide_get_level_count =
            (x => isWindows ? Wopenslide_get_level_count((int*)x.ToPointer()) : Lopenslide_get_level_count((int*)x.ToPointer()));

        public static void openslide_get_level0_dimensions(IntPtr osr, out long w, out long h)
        {
            if (isWindows)
                Wopenslide_get_level0_dimensions((int*)osr.ToPointer(), out w, out h);
            else
                Lopenslide_get_level0_dimensions((int*)osr.ToPointer(), out w, out h);
        }

        public static void openslide_get_level_dimensions(IntPtr osr, int level, out long w, out long h)
        {

            if (isWindows)
                Wopenslide_get_level_dimensions((int*)osr.ToPointer(), level, out w, out h);
            else
                Lopenslide_get_level_dimensions((int*)osr.ToPointer(), level, out w, out h);
        }

        public static Func<IntPtr, int, double> openslide_get_level_downsample =
            ((x, y) => isWindows ? Wopenslide_get_level_downsample((int*)x.ToPointer(), y) : Lopenslide_get_level_downsample((int*)x.ToPointer(), y));

        public static Func<IntPtr, double, int> openslide_get_best_level_for_downsample =
            ((x, y) => isWindows ? Wopenslide_get_best_level_for_downsample((int*)x.ToPointer(), y) : Lopenslide_get_best_level_for_downsample((int*)x.ToPointer(), y));

        public static void openslide_read_region(IntPtr osr,
               void* dest,
               long x, long y,
               int level,
               long w, long h)
        {
            if (isWindows)
                Wopenslide_read_region((int*)osr.ToPointer(), dest, x, y, level, w, h);
            else
                Lopenslide_read_region((int*)osr.ToPointer(), dest, x, y, level, w, h);
        }

        public static Action<IntPtr> openslide_close =
            (x => { if (isWindows) Wopenslide_close((int*)x.ToPointer()); else Lopenslide_close((int*)x.ToPointer()); });

        public static Func<IntPtr, IntPtr> openslide_get_error =
            (x => isWindows ? Wopenslide_get_error((int*)x.ToPointer()) : Lopenslide_get_error((int*)x.ToPointer()));

        public static Func<IntPtr, string, IntPtr> openslide_get_property_value =
            ((x, y) => isWindows ? Wopenslide_get_property_value((int*)x.ToPointer(), y) : Lopenslide_get_property_value((int*)x.ToPointer(), y));
    }
}
