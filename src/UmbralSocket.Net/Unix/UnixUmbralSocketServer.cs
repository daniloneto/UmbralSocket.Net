using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UmbralSocket.Net.Common;
using UmbralSocket.Net.Interfaces;

namespace UmbralSocket.Net.Unix;

/// <summary>
/// Unix domain socket implementation of the Umbral socket server.
/// </summary>
public sealed class UnixUmbralSocketServer : IUmbralSocketServer
{
    private readonly string _path;
    private readonly Dictionary<byte, Func<ReadOnlySequence<byte>, ValueTask<byte[]>>> _handlers = new();

    /// <summary>
    /// Initializes a new instance of the UnixUmbralSocketServer class.
    /// </summary>
    /// <param name="path">The Unix socket path. Defaults to "/tmp/umbral.sock" if not specified.</param>
    public UnixUmbralSocketServer(string? path = null)
    {
        _path = path ?? "/tmp/umbral.sock";
        if (File.Exists(_path))
            File.Delete(_path);
    }

    /// <summary>
    /// Registers a message handler for a specific opcode.
    /// </summary>
    /// <param name="opcode">The operation code to handle.</param>
    /// <param name="handler">The handler function that processes the message and returns a response.</param>
    public void RegisterHandler(byte opcode, Func<ReadOnlySequence<byte>, ValueTask<byte[]>> handler)
        => _handlers[opcode] = handler;

    /// <summary>
    /// Starts the server to listen for incoming Unix domain socket connections.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var ep = new UnixDomainSocketEndPoint(_path);
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(ep);
        listener.Listen(5);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptAsync(cancellationToken);
                _ = ProcessClientAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ProcessClientAsync(Socket client, CancellationToken cancellationToken)
    {
        using (client)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var lenBuffer = new byte[4];
                if (!await ReceiveAllAsync(client, lenBuffer, cancellationToken))
                    break;
                var len = (int)BinaryPrimitives.ReadUInt32BigEndian(lenBuffer);
                var buffer = new byte[len];
                if (!await ReceiveAllAsync(client, buffer, cancellationToken))
                    break;
                byte opcode = buffer[0];
                var payload = new ReadOnlySequence<byte>(buffer).Slice(1);
                if (_handlers.TryGetValue(opcode, out var handler))
                {
                    var responsePayload = await handler(payload);
                    var response = UmbralProtocol.EncodeMessage(opcode, responsePayload);
                    await client.SendAsync(response, SocketFlags.None, cancellationToken);
                }
            }
        }
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
