using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Markban.IntegrationTests.Infrastructure;

public sealed class WebTestServer : IAsyncDisposable
{
    private readonly Process _process;
    private readonly Task<string> _stdoutTask;
    private readonly Task<string> _stderrTask;

    private WebTestServer(Process process, string baseUrl)
    {
        _process = process;
        _stdoutTask = process.StandardOutput.ReadToEndAsync();
        _stderrTask = process.StandardError.ReadToEndAsync();
        Client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        BaseUrl = baseUrl;
    }

    public HttpClient Client { get; }

    public string BaseUrl { get; }

    public static async Task<WebTestServer> StartAsync(string dllPath, string rootPath)
    {
        var port = GetFreePort();
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add(dllPath);
        psi.ArgumentList.Add("--root");
        psi.ArgumentList.Add(rootPath);
        psi.ArgumentList.Add("web");
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(port.ToString());
        psi.ArgumentList.Add("--no-open");

        var process = Process.Start(psi)!;
        var server = new WebTestServer(process, $"http://127.0.0.1:{port}");
        await server.WaitUntilReadyAsync();
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        await _stdoutTask;
        await _stderrTask;
    }

    private async Task WaitUntilReadyAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!timeout.IsCancellationRequested)
        {
            if (_process.HasExited)
            {
                var stdout = await _stdoutTask;
                var stderr = await _stderrTask;
                throw new InvalidOperationException($"Web server exited early.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
            }

            try
            {
                using var response = await Client.GetAsync("/api/boards", timeout.Token);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                break;
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(100, timeout.Token);
        }

        throw new TimeoutException("Timed out waiting for the web server to start.");
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
