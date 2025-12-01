using System;
using System.Runtime.InteropServices;

namespace AutoDaily.Core.Native
{
    public static class Kernel32
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
