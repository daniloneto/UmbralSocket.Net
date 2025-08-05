using System;
using System.Buffers.Binary;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using UmbralSocket.Net.Common;
using UmbralSocket.Net.Interfaces;

namespace UmbralSocket.Net.Windows;

/// <summary>
/// Named pipe implementation of the Umbral socket client for Windows.
/// </summary>
public sealed class NamedPipeUmbralSocketClient : IUmbralSocketClient
{
    private readonly string _name;

    /// <summary>
    /// Initializes a new instance of the NamedPipeUmbralSocketClient class.
    /// </summary>
    /// <param name="name">The named pipe name. Defaults to "umbral" if not specified.</param>
    public NamedPipeUmbralSocketClient(string? name = null)
    {
        _name = name ?? "umbral";
    }

    /// <summary>
    /// Sends a message asynchronously to the server via named pipe.
    /// </summary>
    /// <param name="opcode">The operation code that identifies the message type.</param>
    /// <param name="payload">The message payload data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous send operation. The task result contains the server response.</returns>
    public async ValueTask<byte[]> SendAsync(byte opcode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        using var client = new NamedPipeClientStream(".", _name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(cancellationToken);
        var request = UmbralProtocol.EncodeMessage(opcode, payload);
        await client.WriteAsync(request, cancellationToken);
        await client.FlushAsync(cancellationToken);

        var lenBuffer = new byte[4];
        if (!await ReadExactAsync(client, lenBuffer, cancellationToken))
            throw new UmbralSocketException("Connection closed");
        int len = (int)BinaryPrimitives.ReadUInt32BigEndian(lenBuffer);
        var buffer = new byte[len];
        if (!await ReadExactAsync(client, buffer, cancellationToken))
            throw new UmbralSocketException("Connection closed");
        byte responseOpcode = buffer[0];
        if (responseOpcode != opcode)
            throw new UmbralSocketException("Opcode mismatch");
        var result = new byte[len - 1];
        Array.Copy(buffer, 1, result, 0, len - 1);
        return result;
    }

    private static async Task<bool> ReadExactAsync(PipeStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }
}
