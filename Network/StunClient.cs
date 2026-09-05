using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using VTStudioToolBox.Helpers;

namespace VTStudioToolBox.Network;

public class StunClient : IAsyncDisposable
{
    private const int DefaultTimeoutMs = 3000;

    private readonly Socket _socket;
    private readonly IPEndPoint _serverEndPoint;

    public StunClient(IPEndPoint serverEndPoint)
    {
        _serverEndPoint = serverEndPoint;
        _socket = new Socket(serverEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

        IPAddress localIp = GetBestLocalIp(serverEndPoint.AddressFamily);
        _socket.Bind(new IPEndPoint(localIp, 0));
        Logger.Info("StunClient", $"Bound to local endpoint {_socket.LocalEndPoint}");
    }

    private static IPAddress GetBestLocalIp(AddressFamily family)
    {
        try
        {
            var preferred = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                    && n.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet)
                .ToList();

            foreach (var iface in preferred)
            {
                var ip = iface.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == family)?.Address;
                if (ip != null) return ip;
            }

            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up))
            {
                var ip = iface.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == family
                        && !IPAddress.IsLoopback(a.Address))?.Address;
                if (ip != null) return ip;
            }
        }
        catch (Exception ex) { Logger.Warn("StunClient", $"GetBestLocalIp failed: {ex.Message}"); }

        return family == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
    }

    public async Task<ClassicStunResult> QueryAsync(CancellationToken ct = default)
    {
        var result = new ClassicStunResult();

        try
        {
            // Test I
            var test1Msg = CreateBindingRequest();
            var test1Resp = await SendAndReceiveAsync(test1Msg, _serverEndPoint, ct);

            if (test1Resp == null)
            {
                result.NatType = NatType.UdpBlocked;
                return result;
            }

            var mappedAddr1 = test1Resp.Message.GetMappedAddress();
            var changedAddr = test1Resp.Message.GetChangedAddress();

            result.LocalEndPoint = test1Resp.Local;
            result.PublicEndPoint = mappedAddr1;

            if (mappedAddr1 == null || changedAddr == null)
            {
                result.NatType = NatType.UnsupportedServer;
                return result;
            }

            if (changedAddr.Address.Equals(test1Resp.Remote.Address)
                || changedAddr.Port == test1Resp.Remote.Port)
            {
                result.NatType = NatType.UnsupportedServer;
                return result;
            }

            // Test II: CHANGE-REQUEST change IP+port, sent to original server
            var test2Msg = CreateBindingRequest(changeIp: true, changePort: true);
            var test2Resp = await SendAndReceiveAsync(test2Msg, _serverEndPoint, ct);

            if (test2Resp != null)
            {
                var localEp = _socket.LocalEndPoint as IPEndPoint;
                result.NatType = mappedAddr1.Equals(localEp) ? NatType.OpenInternet : NatType.FullCone;
                return result;
            }

            // Test I #2: send binding request to CHANGED-ADDRESS
            var test12Msg = CreateBindingRequest();
            var test12Resp = await SendAndReceiveAsync(test12Msg, changedAddr, ct);

            if (test12Resp == null)
            {
                result.NatType = NatType.UnsupportedServer;
                return result;
            }

            var mappedAddr2 = test12Resp.Message.GetMappedAddress();
            if (mappedAddr2 != null && !mappedAddr2.Equals(mappedAddr1))
            {
                result.NatType = NatType.Symmetric;
                return result;
            }

            // Test III: CHANGE-REQUEST change port only, sent to original server
            var test3Msg = CreateBindingRequest(changeIp: false, changePort: true);
            var test3Resp = await SendAndReceiveAsync(test3Msg, _serverEndPoint, ct);

            result.NatType = test3Resp != null ? NatType.RestrictedCone : NatType.PortRestrictedCone;
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error("StunClient", "Query failed", ex);
            result.NatType = NatType.Unknown;
            return result;
        }
    }

    private StunMessage CreateBindingRequest(bool changeIp = false, bool changePort = false)
    {
        var msg = new StunMessage { MessageType = 0x0001, MagicCookie = 0 };
        if (changeIp || changePort)
            msg.Attributes.Add(StunAttribute.BuildChangeRequest(changeIp, changePort));
        return msg;
    }

    private async Task<StunResponse?> SendAndReceiveAsync(StunMessage message, IPEndPoint target, CancellationToken ct)
    {
        try
        {
            byte[] sendBuf = new byte[0x1000];
            int length = message.WriteTo(sendBuf);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(DefaultTimeoutMs);

            await _socket.SendToAsync(sendBuf.AsMemory(0, length), SocketFlags.None, target, cts.Token);

            byte[] recvBuf = new byte[0x10000];
            EndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
            var result = await _socket.ReceiveFromAsync(recvBuf, SocketFlags.None, remoteEp, cts.Token);

            var responseMsg = new StunMessage();
            if (responseMsg.TryParse(recvBuf.AsSpan(0, result.ReceivedBytes))
                && responseMsg.IsSameTransaction(message))
            {
                return new StunResponse(responseMsg, (IPEndPoint)result.RemoteEndPoint,
                    (IPEndPoint)_socket.LocalEndPoint!);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) { }

        return null;
    }

    public ValueTask DisposeAsync()
    {
        _socket.Dispose();
        return ValueTask.CompletedTask;
    }
}

public record StunResponse(StunMessage Message, IPEndPoint Remote, IPEndPoint Local);

public class ClassicStunResult
{
    public NatType NatType { get; set; } = NatType.Unknown;
    public IPEndPoint? LocalEndPoint { get; set; }
    public IPEndPoint? PublicEndPoint { get; set; }
}
