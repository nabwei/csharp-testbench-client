using System.Diagnostics;

namespace TestbenchClient;

public static class Simulator
{
    private static readonly string[] Scenarios = ["clean", "jitter", "duplicate", "missing", "corrupt", "stuck-low"];

    public static async Task<string[]> Query(string executable, string scenario)
    {
        if (!Scenarios.Contains(scenario))
            throw new ArgumentException("Unknown scenario");
        using Process simulator = new() { StartInfo = StartConfiguration(executable, scenario) };
        simulator.Start();
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
        try { return await Exchange(simulator, deadline.Token); }
        catch (OperationCanceledException)
        {
            if (!simulator.HasExited) simulator.Kill(entireProcessTree: true);
            await simulator.WaitForExitAsync();
            throw new TimeoutException("Simulator exceeded 10 seconds");
        }
    }

    private static ProcessStartInfo StartConfiguration(string executable, string scenario)
    {
        ProcessStartInfo configuration = new(Path.GetFullPath(executable))
        {
            RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        };
        configuration.ArgumentList.Add(scenario);
        return configuration;
    }

    private static async Task<string[]> Exchange(Process simulator, CancellationToken cancellation)
    {
        Task<string> output = simulator.StandardOutput.ReadToEndAsync(cancellation);
        Task<string> errors = simulator.StandardError.ReadToEndAsync(cancellation);
        for (int sequence = 0; sequence < 16; sequence++)
            await simulator.StandardInput.WriteLineAsync(
                Convert.ToHexString(Frames.Request((byte)sequence)).AsMemory(), cancellation);
        simulator.StandardInput.Close();
        await simulator.WaitForExitAsync(cancellation);
        string stderr = await errors;
        if (simulator.ExitCode != 0)
            throw new IOException($"Simulator exit {simulator.ExitCode}: {stderr}");
        return (await output).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
