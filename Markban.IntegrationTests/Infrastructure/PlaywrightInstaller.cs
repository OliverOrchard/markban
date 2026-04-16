using System.Diagnostics;

namespace Markban.IntegrationTests.Infrastructure;

public static class PlaywrightInstaller
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _installed;

    public static async Task EnsureChromiumInstalledAsync()
    {
        if (_installed)
        {
            return;
        }

        await Gate.WaitAsync();
        try
        {
            if (_installed)
            {
                return;
            }

            var scriptPath = GetInstallerScriptPath();
            var psi = CreateStartInfo(scriptPath);
            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Playwright browser install failed.\nSTDOUT:\n{await stdout}\nSTDERR:\n{await stderr}");
            }

            _installed = true;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string GetInstallerScriptPath()
    {
        var fileName = OperatingSystem.IsWindows() ? "playwright.ps1" : "playwright.sh";
        var scriptPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Could not find Playwright install script at '{scriptPath}'.");
        }

        return scriptPath;
    }

    private static ProcessStartInfo CreateStartInfo(string scriptPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo("powershell")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" install chromium"
            };
        }

        return new ProcessStartInfo("bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = $"\"{scriptPath}\" install chromium"
        };
    }
}
