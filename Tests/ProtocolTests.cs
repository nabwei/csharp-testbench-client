using System.Buffers.Binary;
using System.Text;
using TestbenchClient;
using Xunit;

namespace ClientTests;

public class ProtocolTests
{
    [Fact]
    public void StandardCheckVectorMatchesCcittFalse()
    {
        Assert.Equal((ushort)0x29b1, Frames.Crc(Encoding.ASCII.GetBytes("123456789")));
        Assert.Equal("D3010501FB6F", Convert.ToHexString(Frames.Request(5)));
    }

    [Fact]
    public void KnownReplyDecodesLittleEndianSample()
    {
        Assert.Equal(new Sample(5, 5000, 1015), Frames.ParseReply("d301050088130000f7030c78"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("d3")]
    [InlineData("xyz")]
    [InlineData("d301050088130000f7030c")]
    [InlineData("d301050088130000f7030c7800")]
    [InlineData("d301050088130000f6030c78")]
    public void MalformedOrCorruptReplyIsRejected(string hexLine)
    {
        Assert.Throws<FormatException>(() => Frames.ParseReply(hexLine));
    }

    [Theory]
    [InlineData(0, 0x00)]
    [InlineData(1, 0x02)]
    [InlineData(3, 0x04)]
    public void HeaderAndDeviceErrorsAreRejectedEvenWithValidCrc(int offset, byte replacement)
    {
        byte[] frame = Convert.FromHexString(Reply(0, 0));
        frame[offset] = replacement;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(10), Frames.Crc(frame.AsSpan(0, 10)));
        Assert.Throws<FormatException>(() => Frames.ParseReply(Convert.ToHexString(frame)));
    }

    [Fact]
    public void CleanRepliesPassAcquisition()
    {
        Report report = Acquisition.Analyze([Reply(0, 0), Reply(1, 1000)], 2);
        Assert.True(report.Passed);
        Assert.Equal(2, report.Samples.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void DuplicateOrUnrequestedSequenceDoesNotSatisfyMissingRequest(byte secondSequence)
    {
        Report report = Acquisition.Analyze([Reply(0, 0), Reply(secondSequence, 1000)], 2);
        Assert.False(report.Passed);
        Assert.Equal(new[] { 1 }, report.MissingSequences);
        Assert.Single(report.Anomalies);
    }

    [Fact]
    public void ReorderedRepliesAreReported()
    {
        Report report = Acquisition.Analyze([Reply(1, 1000), Reply(0, 0)], 2);
        Assert.Empty(report.MissingSequences);
        Assert.Contains(report.Anomalies, anomaly => anomaly.StartsWith("Out-of-order"));
        Assert.False(report.Passed);
    }

    [Fact]
    public void RejectedFrameRemainsMissing()
    {
        Report report = Acquisition.Analyze([Reply(0, 0), "bad-hex"], 2);
        Assert.Equal(new[] { 1 }, report.MissingSequences);
        Assert.Single(report.Anomalies);
        Assert.False(report.Passed);
    }

    [Theory]
    [InlineData(950, true)]
    [InlineData(1050, true)]
    [InlineData(1051, false)]
    [InlineData(0, false)]
    public void TimingToleranceIsInclusive(uint secondTimestamp, bool expectedPass)
    {
        Assert.Equal(expectedPass, Acquisition.Analyze([Reply(0, 0), Reply(1, secondTimestamp)], 2).Passed);
    }

    internal static string Reply(byte sequence, uint timestamp)
    {
        byte[] frame = [0xd3, 1, sequence, 0, 0, 0, 0, 0, 0xe8, 3, 0, 0];
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), timestamp);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(10), Frames.Crc(frame.AsSpan(0, 10)));
        return Convert.ToHexString(frame);
    }
}
