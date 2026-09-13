using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VTStudioToolBox.Services
{
    internal static class PawnIOLib
    {
        private const string DllName = "PawnIOLib";
        private static bool _dllLoaded = false;

        static PawnIOLib()
        {
            // Try to load DLL from PawnIO installation directory
            var pawnioPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PawnIO", "PawnIOLib.dll");
            if (File.Exists(pawnioPath))
            {
                NativeLibrary.Load(pawnioPath);
                _dllLoaded = true;
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int pawnio_open(out IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int pawnio_close(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int pawnio_load(IntPtr handle, byte[] blob, IntPtr size);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int pawnio_execute(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            ulong[] inBuf,
            IntPtr inSize,
            ulong[] outBuf,
            IntPtr outSize,
            out IntPtr returnSize);

        // Overload for no input (NULL pointer)
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "pawnio_execute")]
        public static extern int pawnio_execute_noinput(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            IntPtr inBuf,
            IntPtr inSize,
            ulong[] outBuf,
            IntPtr outSize,
            out IntPtr returnSize);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int pawnio_version(out uint version);

        public static bool IsDllLoaded => _dllLoaded;
    }
}
