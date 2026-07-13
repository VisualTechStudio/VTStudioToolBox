using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace VTStudioToolBox.Network;

public class StunServer
{
    public const ushort DefaultPort = 3478;

    public string Hostname { get; }
    public ushort Port { get; }

    private StunServer(string hostname, ushort port)
    {
        Hostname = hostname;
        Port = port;
    }

    public static bool TryParse(string s, [NotNullWhen(true)] out StunServer? result, ushort defaultPort = DefaultPort)
    {
        result = null;
        if (string.IsNullOrEmpty(s)) return false;

        int lastColon = s.LastIndexOf(':');
        string host;
        ushort port;

        if (lastColon > 0)
        {
            host = s[..lastColon];
            if (ushort.TryParse(s[(lastColon + 1)..], out port))
            {
                result = new StunServer(host, port);
                return true;
            }
        }

        result = new StunServer(s, defaultPort);
        return true;
    }

    public override string ToString()
    {
        if (Port == DefaultPort) return Hostname;
        if (IPAddress.TryParse(Hostname, out IPAddress? ip) && ip.AddressFamily == AddressFamily.InterNetworkV6)
            return $"[{ip}]:{Port}";
        return $"{Hostname}:{Port}";
    }
}
