using System.Runtime.InteropServices;

namespace UmbralSocket.Net.Security
{
    /// <summary>
    /// Helper for Unix Domain Socket path management and permissions.
    /// </summary>
    public static class UnixSocketPathHelper
    {
        /// <summary>
        /// Creates a directory for the Unix domain socket and returns the full socket path.
        /// </summary>
        /// <param name="basePath">The base directory path.</param>
        /// <param name="socketName">The name of the socket file.</param>
        /// <returns>The full path to the socket file.</returns>
        public static string CreateSocketDirectory(string basePath, string socketName)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                throw new PlatformNotSupportedException("UnixSocketPathHelper is Linux/OSX-only.");

            var dir = Path.Combine(basePath, "umbral");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                // Set permissions to 0700 if running on Linux
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    TryChmod(dir, 0x1C0); // 0700
                }
            }
            var socketPath = Path.Combine(dir, socketName);
            return socketPath;
        }

        private static void UnixSetPermissions(string path, int mode)
        {
            // Use chmod via native syscall if available
            TryChmod(path, mode);
        }
        private static void TryChmod(string path, int mode)
        {
            try
            {
                // Only attempt if Mono.Unix is available
                var monoUnixType = Type.GetType("Mono.Unix.Native.Syscall, Mono.Posix.NETStandard");
                if (monoUnixType != null)
                {
                    var filePermissionsType = Type.GetType("Mono.Unix.Native.FilePermissions, Mono.Posix.NETStandard");
                    if (filePermissionsType != null)
                    {
                        var chmodMethod = monoUnixType.GetMethod("chmod", new[] { typeof(string), filePermissionsType });
                        if (chmodMethod != null)
                        {
                            var perm = Enum.ToObject(filePermissionsType, mode);
                            chmodMethod.Invoke(null, new[] { path, perm });
                        }
                    }
                }
            }
            catch
            {
                // Ignore if Mono.Unix is not available
            }
        }
    }
}

