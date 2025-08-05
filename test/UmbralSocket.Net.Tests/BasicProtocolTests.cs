using System;
using System.Buffers;
using System.Text;
using UmbralSocket.Net.Common;
using Xunit;

namespace UmbralSocket.Net.Tests;

/// <summary>
/// Tests for the basic Umbral protocol encoding and decoding functionality.
/// </summary>
public class BasicProtocolTests
{
    /// <summary>
    /// Tests encoding and decoding of messages with various opcodes and payloads.
    /// </summary>
    /// <param name="opcode">The operation code to test.</param>
    /// <param name="text">The text payload to encode and decode.</param>
    [Theory]
    [InlineData(0x01, "SAVE")]
    [InlineData(0x02, "SUMMARY")]
    [InlineData(0x03, "PURGE")]
    public void EncodeDecode(byte opcode, string text)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        var encoded = UmbralProtocol.EncodeMessage(opcode, payload);
        var sequence = new ReadOnlySequence<byte>(encoded);
        var (success, decodedOpcode, decodedPayload) = UmbralProtocol.TryDecode(sequence);
        Assert.True(success);
        Assert.Equal(opcode, decodedOpcode);
        Assert.Equal(payload, decodedPayload.ToArray());
    }
}
