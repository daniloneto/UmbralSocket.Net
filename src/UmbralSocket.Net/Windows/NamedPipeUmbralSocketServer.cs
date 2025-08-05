using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using UmbralSocket.Net.Common;
using UmbralSocket.Net.Interfaces;

namespace UmbralSocket.Net.Windows;

/// <summary>
/// Named pipe implementation of the Umbral socket server for Windows.
/// </summary>
public sealed class NamedPipeUmbralSocketServer : IUmbralSocketServer
{
    private readonly string _name;
    private readonly Dictionary<byte, Func<ReadOnlySequence<byte>, ValueTask<byte[]>>> _handlers = new();

    /// <summary>
    /// Initializes a new instance of the NamedPipeUmbralSocketServer class.
    /// </summary>
    /// <param name="name">The named pipe name. Defaults to "umbral" if not specified.</param>
    public NamedPipeUmbralSocketServer(string? name = null)
    {
        _name = name ?? "umbral";
    }

    /// <summary>
    /// Registers a message handler for a specific opcode.
    /// </summary>
    /// <param name="opcode">The operation code to handle.</param>
    /// <param name="handler">The handler function that processes the message and returns a response.</param>
    public void RegisterHandler(byte opcode, Func<ReadOnlySequence<byte>, ValueTask<byte[]>> handler)
        => _handlers[opcode] = handler;

    /// <summary>
    /// Starts the server to listen for incoming named pipe connections.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var server = new NamedPipeServerStream(_name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken);
                await ProcessClientAsync(server, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ProcessClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        var lenBuffer = new byte[4];
        while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
        {
            if (!await ReadExactAsync(pipe, lenBuffer, cancellationToken))
                break;
            int len = (int)BinaryPrimitives.ReadUInt32BigEndian(lenBuffer);
            var buffer = new byte[len];
            if (!await ReadExactAsync(pipe, buffer, cancellationToken))
                break;
            byte opcode = buffer[0];
            var payload = new ReadOnlySequence<byte>(buffer).Slice(1);
            if (_handlers.TryGetValue(opcode, out var handler))
            {
                var responsePayload = await handler(payload);
                var response = UmbralProtocol.EncodeMessage(opcode, responsePayload);
                await pipe.WriteAsync(response, cancellationToken);
                await pipe.FlushAsync(cancellationToken);
            }
        }
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
