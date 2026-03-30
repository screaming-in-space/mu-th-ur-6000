using System.Diagnostics;

// Verify Docker is running — Temporal dev server needs it for the container.
try
{
    var docker = Process.Start(new ProcessStartInfo("docker", "info")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    });
    docker?.WaitForExit(TimeSpan.FromSeconds(5));
    if (docker is null || docker.ExitCode != 0)
        throw new InvalidOperationException("Docker returned a non-zero exit code.");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"""

      Docker Desktop is not running.
      MU-TH-UR needs Docker to start the Temporal dev server container.
      Start Docker Desktop and try again.

      Detail: {ex.Message}

    """);
    Console.ResetColor();
    Environment.Exit(1);
}

var builder = DistributedApplication.CreateBuilder(args);

// Temporal dev server — runs as a container via temporalio/admin-tools.
// Health-checked via the UI endpoint before downstream resources start.
// In production, replace with Temporal Cloud or a self-hosted cluster.
var temporal = builder.AddTemporalDevServer("temporal");

var api = builder.AddProject<Projects.Muthur_Api>("Muthur-api");

var worker = builder.AddProject<Projects.Muthur_Bishop_Worker>("Muthur-bishop-worker")
    .WithReference(temporal)
    .WaitFor(temporal);

await builder.Build().RunAsync();
