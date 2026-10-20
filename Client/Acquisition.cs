namespace TestbenchClient;

public sealed record Report(
    IReadOnlyList<Sample> Samples, IReadOnlyList<int> MissingSequences,
    IReadOnlyList<string> Anomalies)
{
    public bool Passed => Samples.Count > 1 && MissingSequences.Count == 0 && Anomalies.Count == 0;
}

public static class Acquisition
{
    public static Report Analyze(IEnumerable<string> replyLines, int requestedCount)
    {
        if (requestedCount is < 2 or > 256)
            throw new ArgumentOutOfRangeException(nameof(requestedCount), "Choose 2..256 requests");
        List<Sample> samples = [];
        List<string> anomalies = [];
        HashSet<int> seen = [];
        foreach (string line in replyLines)
        {
            Sample sample;
            try { sample = RequestedSample(line, requestedCount, seen); }
            catch (FormatException error) { anomalies.Add(error.Message); continue; }
            if (samples.Count > 0 && sample.Sequence < samples[^1].Sequence)
                anomalies.Add($"Out-of-order sequence {sample.Sequence}");
            seen.Add(sample.Sequence);
            samples.Add(sample);
        }
        CheckTiming(samples, anomalies);
        int[] missing = Enumerable.Range(0, requestedCount).Where(sequence => !seen.Contains(sequence)).ToArray();
        return new Report(samples, missing, anomalies);
    }

    private static Sample RequestedSample(string line, int count, HashSet<int> seen)
    {
        Sample sample = Frames.ParseReply(line);
        if (sample.Sequence >= count || seen.Contains(sample.Sequence))
            throw new FormatException($"Unexpected or duplicate sequence {sample.Sequence}");
        return sample;
    }

    private static void CheckTiming(List<Sample> samples, List<string> anomalies)
    {
        for (int index = 1; index < samples.Count; index++)
        {
            long interval = (long)samples[index].Timestamp - samples[index - 1].Timestamp;
            if (Math.Abs(interval - 1000) > 50)
                anomalies.Add($"Timing at sequence {samples[index].Sequence}: interval={interval} ticks");
        }
    }
}
