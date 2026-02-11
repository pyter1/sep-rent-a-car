using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

namespace Bank.Api.Controllers;

[ApiController]
[Route("api/bank/dev")]
public sealed class DevController : ControllerBase
{
    [HttpGet("host-ip")]
    public ActionResult<object> GetHostIp()
    {
        var ip = GetLanIPv4();
        return Ok(new { ipv4 = ip });
    }

   private static string GetLanIPv4()
    {
        var candidates = new List<(int Score, string Ip)>();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;

            if (ni.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                continue;
            var name = (ni.Name ?? "").ToLowerInvariant();
            var desc = (ni.Description ?? "").ToLowerInvariant();

            // ignore common virtual adapters
            if (name.Contains("docker") || desc.Contains("docker")) continue;
            if (name.Contains("wsl") || desc.Contains("wsl")) continue;
            if (name.Contains("hyper-v") || desc.Contains("hyper-v")) continue;
            if (name.Contains("virtual") || desc.Contains("virtual")) continue;
            if (name.Contains("vmware") || desc.Contains("vmware")) continue;
            if (name.Contains("virtualbox") || desc.Contains("virtualbox")) continue;
            if (name.Contains("vethernet") || desc.Contains("vethernet")) continue;

            var props = ni.GetIPProperties();
            foreach (var ua in props.UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                var ip = ua.Address;
                if (IPAddress.IsLoopback(ip)) continue;

                var s = ip.ToString();
                if (s.StartsWith("169.254.")) continue; // APIPA

                if (!IsPrivateIPv4(ip)) continue;

                var score = 0;

                // prefer real LAN adapters
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) score += 100;
                else if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) score += 90;

                // prefer ranges
                if (s.StartsWith("192.168.")) score += 50;
                else if (s.StartsWith("10.")) score += 40;
                else score += 30; // 172.16-31

                candidates.Add((score, s));
            }
        }

        return candidates
            .OrderByDescending(x => x.Score)
            .Select(x => x.Ip)
            .FirstOrDefault() ?? "127.0.0.1";
    }

    private static bool IsPrivateIPv4(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        if (b[0] == 10) return true;
        if (b[0] == 192 && b[1] == 168) return true;
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
        return false;
    }

}
