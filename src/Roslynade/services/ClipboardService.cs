using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Roslynade.Services
{
    public static class ClipboardService
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, nuint dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;

        public static bool SetText(string text)
        {
            if (text == null)
            {
                text = string.Empty;
            }

            // Retry up to 10 times in case clipboard is temporarily locked by another app
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        if (!EmptyClipboard())
                        {
                            return false;
                        }

                        nuint byteCount = (nuint)((text.Length + 1) * sizeof(char));
                        IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, byteCount);
                        if (hGlobal == IntPtr.Zero)
                        {
                            return false;
                        }

                        IntPtr target = GlobalLock(hGlobal);
                        if (target == IntPtr.Zero)
                        {
                            return false;
                        }

                        try
                        {
                            Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                            Marshal.WriteInt16(target, text.Length * sizeof(char), 0);
                        }
                        finally
                        {
                            GlobalUnlock(hGlobal);
                        }

                        return SetClipboardData(CF_UNICODETEXT, hGlobal) != IntPtr.Zero;
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }

                Thread.Sleep(50);
            }

            return false;
        }
    }
}

