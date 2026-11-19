using System;
using System.Net;
using System.Runtime.Versioning;

// csc does not emit this. Without it the 4.8 runtime treats the exe as a
// .NET Framework 4.0 app and leaves ServicePointManager on SSL 3.0 / TLS 1.0.
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace KavrosAfkWarden
{
    internal static class NetSecurity
    {
        internal static void EnableModernTls()
        {
            try { AppContext.SetSwitch("Switch.System.Net.DontEnableSystemDefaultTlsVersions", false); }
            catch (InvalidOperationException) { }
            catch (NotSupportedException) { }
            try
            {
                SecurityProtocolType protocols = ServicePointManager.SecurityProtocol | SecurityProtocolType.Tls12;
                protocols &= ~SecurityProtocolType.Ssl3;
                ServicePointManager.SecurityProtocol = protocols;
            }
            catch (NotSupportedException) { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; }
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)12288; }
            catch (NotSupportedException) { }
        }
    }
}
