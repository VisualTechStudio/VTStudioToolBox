using System;
using System.Buffers.Binary;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace VTStudioToolBox.Helpers;

internal static class DnsResolver
{
    private static readonly HttpClient Http = new();

    public static async Task<IPAddress?> ResolveAsync(string hostname)
    {
        // Check if it's already an IP
        if (IPAddress.TryParse(hostname, out IPAddress? ip))
            return ip;

        // Try DNS-over-HTTPS (bypasses WARP)
        try
        {
            var result = await ResolveViaDoHAsync(hostname);
            if (result != null)
            {
                Logger.Info("DnsResolver", $"Resolved {hostname} to {result} via DoH");
                return result;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("DnsResolver", $"DoH failed for {hostname}: {ex.Message}");
        }

        // Try system DNS
        try
        {
            IPAddress[] ips = await Dns.GetHostAddressesAsync(hostname);
            if (ips.Length > 0)
            {
                // Filter out 198.18.x.x (Cloudflare WARP)
                var realIp = ips.FirstOrDefault(ip2 =>
                {
                    byte[] bytes = ip2.GetAddressBytes();
                    return !(bytes[0] == 198 && bytes[1] == 18);
                });

                if (realIp != null)
                {
                    Logger.Info("DnsResolver", $"Resolved {hostname} to {realIp} via system DNS");
                    return realIp;
                }

                Logger.Warn("DnsResolver", $"System DNS only returned WARP IPs for {hostname}");
                return ips[0];
            }
        }
        catch (Exception ex) { Logger.Warn("DnsResolver", $"System DNS failed: {ex.Message}"); }

        return null;
    }

    private static async Task<IPAddress?> ResolveViaDoHAsync(string hostname)
    {
        // Use Cloudflare DoH API
        string url = $"https://cloudflare-dns.com/dns-query?name={hostname}&type=A";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "application/dns-json");

        using var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("Answer", out var answers))
        {
            foreach (var answer in answers.EnumerateArray())
            {
                if (answer.TryGetProperty("data", out var data))
                {
                    string ipStr = data.GetString()!;
                    if (IPAddress.TryParse(ipStr, out IPAddress? resolvedIp))
                    {
                        // Filter out 198.18.x.x
                        byte[] bytes = resolvedIp.GetAddressBytes();
                        if (bytes[0] == 198 && bytes[1] == 18)
                        {
                            Logger.Warn("DnsResolver", $"DoH returned WARP IP {resolvedIp} for {hostname}");
                            continue;
                        }
                        return resolvedIp;
                    }
                }
            }
        }

        return null;
    }
}
