using System.ComponentModel;
using System.Text.Json;
using TestbenchClient;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: Client <sensor_sim executable> <clean|jitter|duplicate|missing|corrupt|stuck-low>");
    return 2;
}
try
{
    string[] lines = await Simulator.Query(args[0], args[1]);
    Report report = Acquisition.Analyze(lines, 16);
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    return report.Passed ? 0 : 1;
}
catch (Exception error) when (error is IOException or Win32Exception or ArgumentException or TimeoutException)
{
    Console.Error.WriteLine($"error: {error.Message}");
    return 2;
}
