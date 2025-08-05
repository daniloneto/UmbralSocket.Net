using System.Buffers;
using System.Buffers.Binary;

namespace UmbralSocket.Net.Common;

/// <summary>
/// Provides protocol operations for encoding and decoding Umbral messages.
/// </summary>
public static class UmbralProtocol
{
    /// <summary>
    /// Encodes a message into the Umbral protocol format [LEN|OPCODE|PAYLOAD].
    /// </summary>
    /// <param name="opcode">The operation code for the message.</param>
    /// <param name="payload">The message payload data.</param>
    /// <returns>The encoded message as a byte array.</returns>
    public static ReadOnlyMemory<byte> EncodeMessage(byte opcode, ReadOnlyMemory<byte> payload)
    {
        var len = 1 + payload.Length;
        var buffer = new byte[4 + len];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)len);
        buffer[4] = opcode;
        payload.CopyTo(buffer.AsMemory(5));
        return buffer;    }

    /// <summary>
    /// Attempts to decode a byte sequence into an Umbral message.
    /// </summary>
    /// <param name="input">The input byte sequence to decode.</param>
    /// <returns>A tuple containing success status, opcode, and payload. Returns false if decoding fails.</returns>
    public static (bool success, byte opcode, ReadOnlySequence<byte> payload) TryDecode(ReadOnlySequence<byte> input)
    {
        if (input.Length < 5)
            return (false, 0, default);

        var reader = new SequenceReader<byte>(input);
        if (!reader.TryReadBigEndian(out int len))
            return (false, 0, default);
        if (input.Length < 4L + len)
            return (false, 0, default);
        if (!reader.TryRead(out byte opcode))
            return (false, 0, default);

        var payload = input.Slice(reader.Consumed, len - 1);
        return (true, opcode, payload);
    }
}
