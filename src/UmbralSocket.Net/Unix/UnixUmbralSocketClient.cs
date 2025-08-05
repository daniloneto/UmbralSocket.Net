using System;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UmbralSocket.Net.Common;
using UmbralSocket.Net.Interfaces;

namespace UmbralSocket.Net.Unix;

/// <summary>
/// Unix domain socket implementation of the Umbral socket client.
/// </summary>
public sealed class UnixUmbralSocketClient : IUmbralSocketClient
{
    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the UnixUmbralSocketClient class.
    /// </summary>
    /// <param name="path">The Unix socket path. Defaults to "/tmp/umbral.sock" if not specified.</param>
    public UnixUmbralSocketClient(string? path = null)
    {
        _path = path ?? "/tmp/umbral.sock";
    }

    /// <summary>
    /// Sends a message asynchronously to the server via Unix domain socket.
    /// </summary>
    /// <param name="opcode">The operation code that identifies the message type.</param>
    /// <param name="payload">The message payload data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous send operation. The task result contains the server response.</returns>
    public async ValueTask<byte[]> SendAsync(byte opcode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        var endpoint = new UnixDomainSocketEndPoint(_path);
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(endpoint, cancellationToken);
        var request = UmbralProtocol.EncodeMessage(opcode, payload);
        await socket.SendAsync(request, SocketFlags.None, cancellationToken);

        var lenBuffer = new byte[4];
        if (!await ReceiveAllAsync(socket, lenBuffer, cancellationToken))
            throw new UmbralSocketException("Connection closed");
        var len = (int)BinaryPrimitives.ReadUInt32BigEndian(lenBuffer);
        var buffer = new byte[len];
        if (!await ReceiveAllAsync(socket, buffer, cancellationToken))
            throw new UmbralSocketException("Connection closed");
        byte responseOpcode = buffer[0];
        if (responseOpcode != opcode)
            throw new UmbralSocketException("Opcode mismatch");
        var result = new byte[len - 1];
        Array.Copy(buffer, 1, result, 0, len - 1);
        return result;
    }

    private static async Task<bool> ReceiveAllAsync(Socket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            var n = await socket.ReceiveAsync(buffer.AsMemory(read), SocketFlags.None, cancellationToken);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }
}
