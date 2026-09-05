using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using VTStudioToolBox.Helpers;

namespace VTStudioToolBox.Network;

/// <summary>
/// RFC 5780 NAT behavior discovery state machine.
/// Determines Mapping Behavior and Filtering Behavior using a dual-homed STUN server.
/// </summary>
public class Stun5780Client : IAsyncDisposable
{
    private const int DefaultTimeoutMs = 3000;

    private readonly Socket _socket;
    private readonly IPEndPoint _serverEndPoint;

    private IPEndPoint? _otherEndPoint;
    private IPEndPoint? _mappingTest2PublicEndPoint;

    public Stun5780Result Result { get; } = new();

    public Stun5780Client(IPEndPoint serverEndPoint)
    {
        _serverEndPoint = serverEndPoint;
        _socket = new Socket(serverEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

        IPAddress localIp = GetBestLocalIp(serverEndPoint.AddressFamily);
        _socket.Bind(new IPEndPoint(localIp, 0));
        Logger.Info("Stun5780Client", $"Bound to local endpoint {_socket.LocalEndPoint}");
    }

    private static IPAddress GetBestLocalIp(AddressFamily family)
    {
        try
        {
            var preferred = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                    && n.NetworkInterfaceType is System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211
                        or System.Net.NetworkInformation.NetworkInterfaceType.Ethernet)
                .ToList();

            foreach (var iface in preferred)
            {
                var ip = iface.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == family)?.Address;
                if (ip != null) return ip;
            }

            foreach (var iface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up))
            {
                var ip = iface.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == family
                        && !IPAddress.IsLoopback(a.Address))?.Address;
                if (ip != null) return ip;
            }
        }
        catch (Exception ex) { Logger.Warn("Stun5780Client", $"GetBestLocalIp failed: {ex.Message}"); }

        return family == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
    }

    public async Task<Stun5780Result> QueryAsync(CancellationToken ct = default)
    {
        Result.Reset();
        _otherEndPoint = null;
        _mappingTest2PublicEndPoint = null;

        try
        {
            // Step 1: Binding Test
            var bindingMsg = CreateBindingRequest();
            var bindingResp = await SendAndReceiveAsync(bindingMsg, _serverEndPoint, ct);

            if (bindingResp == null)
            {
                Result.BindingTestResult = Network.BindingTestResult.Fail;
                Result.MappingBehavior = MappingBehavior.Fail;
                Result.FilteringBehavior = FilteringBehavior.Unknown;
                return Result;
            }

            var mappedAddr = bindingResp.Message.GetXorMappedAddress();
            _otherEndPoint = bindingResp.Message.GetOtherAddress();

            Result.LocalEndPoint = bindingResp.Local;
            Result.PublicEndPoint = mappedAddr;

            if (mappedAddr == null)
            {
                Result.BindingTestResult = Network.BindingTestResult.UnsupportedServer;
                Result.MappingBehavior = MappingBehavior.UnsupportedServer;
                Result.FilteringBehavior = FilteringBehavior.Unknown;
                return Result;
            }

            Result.BindingTestResult = Network.BindingTestResult.Success;

            // Check OTHER-ADDRESS validity
            if (_otherEndPoint == null
                || _otherEndPoint.Address.Equals(_serverEndPoint.Address)
                || _otherEndPoint.Port == _serverEndPoint.Port)
            {
                Result.FilteringBehavior = FilteringBehavior.UnsupportedServer;
                Result.MappingBehavior = MappingBehavior.UnsupportedServer;
                return Result;
            }

            // Step 2: Filtering Test II - CHANGE-REQUEST change IP+port
            var filter2Msg = CreateBindingRequestWithChangeRequest(changeIp: true, changePort: true);
            var filter2Resp = await SendAndReceiveAsync(filter2Msg, _serverEndPoint, ct);

            if (filter2Resp != null)
            {
                Result.FilteringBehavior = filter2Resp.Remote.Equals(_otherEndPoint)
                    ? FilteringBehavior.EndpointIndependent
                    : FilteringBehavior.UnsupportedServer;

                // Proceed to mapping tests
                return await RunMappingTests(ct);
            }

            // Step 3: Filtering Test III - CHANGE-REQUEST change port only
            var filter3Msg = CreateBindingRequestWithChangeRequest(changeIp: false, changePort: true);
            var filter3Resp = await SendAndReceiveAsync(filter3Msg, _serverEndPoint, ct);

            if (filter3Resp == null)
            {
                Result.FilteringBehavior = FilteringBehavior.AddressAndPortDependent;
            }
            else if (filter3Resp.Remote.Address.Equals(_serverEndPoint.Address)
                     && !filter3Resp.Remote.Port.Equals(_serverEndPoint.Port))
            {
                Result.FilteringBehavior = FilteringBehavior.AddressDependent;
            }
            else
            {
                Result.FilteringBehavior = FilteringBehavior.UnsupportedServer;
            }

            // Proceed to mapping tests
            return await RunMappingTests(ct);
        }
        catch (Exception ex)
        {
            Logger.Error("Stun5780Client", "Query failed", ex);
            Result.MappingBehavior = MappingBehavior.Fail;
            Result.FilteringBehavior = FilteringBehavior.Unknown;
            return Result;
        }
    }

    private async Task<Stun5780Result> RunMappingTests(CancellationToken ct)
    {
        // If no NAT (local == public), mapping is Direct
        if (Result.PublicEndPoint != null && Result.PublicEndPoint.Equals(Result.LocalEndPoint))
        {
            Result.MappingBehavior = MappingBehavior.Direct;
            return Result;
        }

        // Mapping Test II: send binding request to (OTHER_ADDRESS, server_port)
        var map2Target = new IPEndPoint(_otherEndPoint!.Address, _serverEndPoint.Port);
        var map2Msg = CreateBindingRequest();
        var map2Resp = await SendAndReceiveAsync(map2Msg, map2Target, ct);

        if (map2Resp == null)
        {
            Result.MappingBehavior = MappingBehavior.Fail;
            return Result;
        }

        var mappedAddr2 = map2Resp.Message.GetXorMappedAddress();
        if (mappedAddr2 == null)
        {
            Result.MappingBehavior = MappingBehavior.Fail;
            return Result;
        }

        if (mappedAddr2.Equals(Result.PublicEndPoint))
        {
            Result.MappingBehavior = MappingBehavior.EndpointIndependent;
            return Result;
        }

        _mappingTest2PublicEndPoint = mappedAddr2;

        // Mapping Test III: send binding request to OTHER_ADDRESS (different IP and port)
        var map3Msg = CreateBindingRequest();
        var map3Resp = await SendAndReceiveAsync(map3Msg, _otherEndPoint!, ct);

        if (map3Resp == null)
        {
            Result.MappingBehavior = MappingBehavior.Fail;
            return Result;
        }

        var mappedAddr3 = map3Resp.Message.GetXorMappedAddress();
        if (mappedAddr3 == null)
        {
            Result.MappingBehavior = MappingBehavior.Fail;
            return Result;
        }

        Result.MappingBehavior = mappedAddr3.Equals(_mappingTest2PublicEndPoint)
            ? MappingBehavior.AddressDependent
            : MappingBehavior.AddressAndPortDependent;

        return Result;
    }

    private StunMessage CreateBindingRequest()
    {
        return new StunMessage
        {
            MessageType = 0x0001, // Binding Request
            MagicCookie = StunMessage.Rfc5389MagicCookie
        };
    }

    private StunMessage CreateBindingRequestWithChangeRequest(bool changeIp, bool changePort)
    {
        var msg = CreateBindingRequest();
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

public class Stun5780Result
{
    public BindingTestResult BindingTestResult { get; set; } = BindingTestResult.Unknown;
    public MappingBehavior MappingBehavior { get; set; } = MappingBehavior.Unknown;
    public FilteringBehavior FilteringBehavior { get; set; } = FilteringBehavior.Unknown;
    public IPEndPoint? LocalEndPoint { get; set; }
    public IPEndPoint? PublicEndPoint { get; set; }
    public IPEndPoint? OtherEndPoint { get; set; }

    public void Reset()
    {
        BindingTestResult = BindingTestResult.Unknown;
        MappingBehavior = MappingBehavior.Unknown;
        FilteringBehavior = FilteringBehavior.Unknown;
        LocalEndPoint = null;
        PublicEndPoint = null;
        OtherEndPoint = null;
    }
}
