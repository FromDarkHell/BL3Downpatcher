using System;
using System.Runtime.InteropServices;

namespace BL3Downpatcher
{
    /// <summary>
    /// Code courtesy of Rick (Gibbed)
    /// A quick static class to help obtain the internal product version of an exe.
    /// </summary>
    public static class VersionHelper
    {
        private const string _DllName = "version";

        [DllImport(_DllName, EntryPoint = "GetFileVersionInfoSizeExW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint GetFileVersionInfoSizeEx(uint flags, string filename, out uint handle);

        [DllImport(_DllName, EntryPoint = "GetFileVersionInfoExW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileVersionInfoEx(uint flags, string filename, uint handle, uint size, IntPtr data);

        [DllImport(_DllName, EntryPoint = "VerQueryValueW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VerQueryValue(IntPtr block, string subblock, out IntPtr buffer, out uint size);

        public static string GetProductVersion(string path)
        {
            return GetProductVersionInternal(path);
        }

        private static unsafe string GetProductVersionInternal(string path)
        {
            var size = GetFileVersionInfoSizeEx(0, path, out var handle);
            if (size != 0)
            {
                var dataBytes = new byte[size];
                fixed (byte* dataBuffer = &dataBytes[0])
                {
                    var dataPointer = new IntPtr(dataBuffer);
                    if (GetFileVersionInfoEx(0, path, 0u, size, dataPointer) == true &&
                        GetTranslationValue(dataPointer, out var translation) == true &&
                        GetProductVersion(dataPointer, translation, out var productVersion) == true)
                    {
                        return productVersion;
                    }
                }
            }
            return "Unknown";
        }

        private static bool GetTranslationValue(IntPtr data, out uint value)
        {
            if (VerQueryValue(data, @"\VarFileInfo\Translation", out var pointer, out var size) == false)
            {
                value = 0;
                return false;
            }

            if (size != 4)
            {
                value = 0;
                return false;
            }

            value = ((uint)Marshal.ReadInt16(pointer) << 16) | (ushort)Marshal.ReadInt16((IntPtr)((long)pointer + 2));
            return true;
        }

        private static bool GetProductVersion(IntPtr data, uint codepage, out string value)
        {
            if (VerQueryValue(data, $@"\StringFileInfo\{codepage:X8}\ProductVersion", out var pointer, out var size) == false)
            {
                value = null;
                return false;
            }

            if (size == 0)
            {
                value = "";
                return true;
            }

            value = Marshal.PtrToStringUni(pointer);
            return true;
        }
    }
}
