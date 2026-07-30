using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using VTStudioToolBox.Auth;
using VTStudioToolBox.Helpers;
using VTStudioToolBox.Models;

namespace VTStudioToolBox.Services;

public sealed class AnalyticsService : IAnalyticsService, IAsyncDisposable
{
    private const string Endpoint = "https://api.example.com/analytics/collect"; // Replace
    private const int BatchSize = 10;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(3);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly Channel<AnalyticsEvent> _channel;
    private readonly Task _consumerTask;
    private readonly CancellationTokenSource _cts = new();
    private readonly string _deviceId;

    private static readonly HttpClient Http = new() { Timeout = HttpTimeout };

    public AnalyticsService(HardwareCollector hardwareCollector)
    {
        _deviceId = hardwareCollector.GetOrCreateDeviceGuid();

        _channel = Channel.CreateUnbounded<AnalyticsEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _consumerTask = ConsumeLoopAsync(_cts.Token);
    }

    // ── Producer Methods (non-blocking, fire-and-forget into Channel) ──

    public void TrackAppLaunch(HardwareInfo hardware)
    {
        _channel.Writer.TryWrite(new AnalyticsEvent
        {
            Type = AnalyticsEventType.AppLaunch,
            DeviceId = _deviceId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Hardware = hardware
        });
    }

    public void TrackToolUsage(string toolName)
    {
        _channel.Writer.TryWrite(new AnalyticsEvent
        {
            Type = AnalyticsEventType.ToolUsage,
            DeviceId = _deviceId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ToolName = toolName
        });
    }

    public async Task FlushAsync()
    {
        // Signal the consumer to drain remaining items
        _channel.Writer.TryComplete();
        await _consumerTask;
    }

    // ── Consumer Loop ──

    private async Task ConsumeLoopAsync(CancellationToken ct)
    {
        var buffer = new List<AnalyticsEvent>(BatchSize);
        using var periodicFlush = new PeriodicTimer(FlushInterval);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Wait for either a new item or the flush timer
                var readTask = _channel.Reader.ReadAsync(ct).AsTask();
                var timerTask = periodicFlush.WaitForNextTickAsync(ct).AsTask();

                await Task.WhenAny(readTask, timerTask);

                // Drain all immediately available items
                while (_channel.Reader.TryRead(out var evt))
                {
                    buffer.Add(evt);
                    if (buffer.Count >= BatchSize)
                    {
                        await SendBatchAsync(buffer, ct);
                        buffer.Clear();
                    }
                }

                // Timer-triggered flush for partial batches
                if (buffer.Count > 0)
                {
                    await SendBatchAsync(buffer, ct);
                    buffer.Clear();
                }
            }
        }
        catch (OperationCanceledException) { /* Expected on shutdown */ }
        catch (ChannelClosedException) { /* Expected on complete */ }

        // Final drain on shutdown
        while (_channel.Reader.TryRead(out var remaining))
            buffer.Add(remaining);

        if (buffer.Count > 0)
            await SendBatchAsync(buffer, CancellationToken.None);
    }

    // ── HTTP Sender ──

    private static async Task SendBatchAsync(List<AnalyticsEvent> batch, CancellationToken ct)
    {
        try
        {
            string json = JsonSerializer.Serialize(batch, JsonOpts);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await Http.PostAsync(Endpoint, content, ct);

            if (!resp.IsSuccessStatusCode)
                Logger.Warn("Analytics", $"Batch POST returned {resp.StatusCode}");
        }
        catch (OperationCanceledException) { /* Timeout or shutdown */ }
        catch (Exception ex)
        {
            // Silent fail — write lightweight local log, no UI error
            Logger.Warn("Analytics", $"Batch POST failed: {ex.Message}");
        }
    }

    // ── Cleanup ──

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
        try { await _consumerTask; } catch { }
        _cts.Dispose();
    }
}
