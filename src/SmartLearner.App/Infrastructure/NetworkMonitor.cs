using System.Net.Http;

namespace SmartLearner.App.Infrastructure;

/// <summary>
/// 网络状态探测（X-01）。用 HTTP 探测而非 ICMP，3 秒超时，状态变化时广播。
/// </summary>
public sealed class NetworkMonitor : IDisposable
{
    private static readonly Uri ProbeUri = new("https://developer.zhihu.com/");
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly CancellationTokenSource _cts = new();
    private bool _started;

    public bool IsOnline { get; private set; }

    public event EventHandler<bool>? OnlineChanged;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _ = LoopAsync(_cts.Token);
    }

    private async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var online = await ProbeAsync(token);
            if (online != IsOnline)
            {
                IsOnline = online;
                Logger.Info(online ? "网络已连接" : "网络已断开");
                OnlineChanged?.Invoke(this, online);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task<bool> ProbeAsync(CancellationToken token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, ProbeUri);
            using var response = await _http.SendAsync(request, token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _http.Dispose();
        _cts.Dispose();
    }
}
