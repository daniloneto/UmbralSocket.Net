using System;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace UmbralSocket.Net.Security
{    /// <summary>
    /// Helper for Named Pipe ACL management (Windows-only).
    /// </summary>
    public static class NamedPipeSecurityHelper
    {        /// <summary>
        /// Creates a restricted pipe security configuration with access control rules.
        /// </summary>
        /// <param name="allowedAccount">Optional account name to grant read/write access to.</param>
        /// <returns>A PipeSecurity object with restricted access rules.</returns>
        public static PipeSecurity CreateRestrictedPipeSecurity(string? allowedAccount = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("NamedPipeSecurityHelper is Windows-only.");

            var ps = new PipeSecurity();
            
            // Use well-known SIDs instead of string names to avoid translation issues
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            
            ps.AddAccessRule(new PipeAccessRule(systemSid, PipeAccessRights.FullControl, AccessControlType.Allow));
            ps.AddAccessRule(new PipeAccessRule(adminSid, PipeAccessRights.FullControl, AccessControlType.Allow));
            
            if (!string.IsNullOrEmpty(allowedAccount))
            {
                ps.AddAccessRule(new PipeAccessRule(allowedAccount, PipeAccessRights.ReadWrite, AccessControlType.Allow));
            }
            
            ps.AddAccessRule(new PipeAccessRule(everyoneSid, PipeAccessRights.ReadWrite, AccessControlType.Deny));
            return ps;
        }
    }
}
