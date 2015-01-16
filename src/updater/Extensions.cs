using System;

namespace CitizenMP.Server.Installer
{
    internal static class Extensions
    {
        public static bool IsWin32(this OperatingSystem os)
        {
            return os.Platform == PlatformID.Win32NT ||
                   os.Platform == PlatformID.Win32S ||
                   os.Platform == PlatformID.Win32Windows;
        }
    }
}