using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Aspire.Hosting;

public static class DockerDesktopExtensions
{
    /// <summary>
    /// Ensures Docker Desktop is running before Aspire starts container resources.
    /// If Docker isn't responsive, attempts to launch Docker Desktop and polls
    /// for readiness up to <paramref name="timeout"/>.
    /// </summary>
    public static async Task<IDistributedApplicationBuilder> EnsureDockerAsync(
        this IDistributedApplicationBuilder builder,
        TimeSpan? timeout = null)
    {
        if (await IsDockerRunningAsync()) { return builder; }

        Console.WriteLine("Docker is not running — attempting to start Docker Desktop...");
        LaunchDockerDesktop();

        var limit = timeout ?? TimeSpan.FromSeconds(60);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < limit)
        {
            await Task.Delay(2000);
            if (await IsDockerRunningAsync())
            {
                Console.WriteLine("Docker Desktop is ready.");
                return builder;
            }
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("Docker Desktop did not start within 60 seconds. Start it manually and try again.");
        Console.ResetColor();
        Environment.Exit(1);
        return builder; // unreachable
    }

    private readonly static ProcessStartInfo Windows = new("cmd", "/c start \"\" \"C:\\Program Files\\Docker\\Docker\\Docker Desktop.exe\"")
    {
        UseShellExecute = false,
        CreateNoWindow = true
    };

    private readonly static ProcessStartInfo OSX = new("open", "-a Docker")
    {
        UseShellExecute = false,
        CreateNoWindow = true
    };

    private readonly static ProcessStartInfo Linux = new("systemctl", "--user start docker-desktop")
    {
        UseShellExecute = false,
        CreateNoWindow = true
    };

    private static void LaunchDockerDesktop()
    {
        var psi = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? OSX
            : Linux;

        try { Process.Start(psi); } catch { /* best effort */ }
    }

    private static async Task<bool> IsDockerRunningAsync()
    {
        try
        {
            var proc = Process.Start(new ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (proc is null) return false;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await proc.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException) { proc.Kill(); return false; }
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }
}
