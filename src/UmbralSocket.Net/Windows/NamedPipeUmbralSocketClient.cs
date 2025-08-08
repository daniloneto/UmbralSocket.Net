using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Extensions.Logging;
using UmbralSocket.Net.Common;
using UmbralSocket.Net.Interfaces;
using UmbralSocket.Net.Transport;

namespace UmbralSocket.Net.Windows;

/// <summary>
/// Named pipe implementation of the Umbral socket client for Windows.
/// Provides persistent connection management and memory-efficient operations.
/// </summary>
public sealed class NamedPipeUmbralSocketClient : IUmbralSocketClient, IAsyncDisposable
{
    private static readonly ActivitySource ActivitySource = new("UmbralSocket.NamedPipeClient");
    private readonly string _name;
    private readonly ILogger? _logger;
    private NamedPipeClientStream? _client;
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the NamedPipeUmbralSocketClient class.
    /// </summary>
    /// <param name="name">The named pipe name. Defaults to "umbral" if not specified.</param>
    /// /// <param name="logger">Optional logger for diagnostics.</param>
    public NamedPipeUmbralSocketClient(string? name = null, ILogger? logger = null)
    {
        _name = name ?? "umbral";
        _logger = logger;
    }    /// <summary>
    /// Sends a message asynchronously to the server via named pipe.
    /// Maintains a persistent connection for improved performance.
    /// </summary>
    /// <param name="opcode">The operation code that identifies the message type.</param>
    /// <param name="payload">The message payload data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous send operation. The task result contains the server response.</returns>
    public async ValueTask<byte[]> SendAsync(byte opcode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        var startTime = System.Diagnostics.Stopwatch.GetTimestamp();
        
        using var activity = ActivitySource.StartActivity("NamedPipe.SendAsync");
        activity?.SetTag("opcode", opcode);
        activity?.SetTag("payload.length", payload.Length);

        await EnsureConnectedAsync(cancellationToken);

        PooledBufferOwner? lenBufferOwner = null;
        PooledBufferOwner? bufferOwner = null;

        try
        {
            // Send request
            var request = UmbralProtocol.EncodeMessage(opcode, payload);
            await _client!.WriteAsync(request, cancellationToken);
            await _client.FlushAsync(cancellationToken);

            // Read response length
            lenBufferOwner = new PooledBufferOwner(4);
            var lenBuffer = lenBufferOwner.Memory;
            if (!await ReadExactAsync(_client, lenBuffer, cancellationToken))
            {
                throw new UmbralSocketException("Connection closed while reading response length");
            }

            int len = (int)BinaryPrimitives.ReadUInt32BigEndian(lenBuffer.Span);
            
            // Handle empty response (server error)
            if (len == 0)
            {
                throw new UmbralSocketException("Server returned an error (empty response)");
            }

            // Read response payload
            bufferOwner = new PooledBufferOwner(len);
            var buffer = bufferOwner.Memory;
            if (!await ReadExactAsync(_client, buffer, cancellationToken))
            {
                throw new UmbralSocketException("Connection closed while reading response payload");
            }

            byte responseOpcode = buffer.Span[0];
            if (responseOpcode != opcode)
            {
                throw new UmbralSocketException($"Opcode mismatch: expected {opcode}, got {responseOpcode}");
            }
            
            var result = buffer.Slice(1, len - 1).ToArray();
            
            activity?.SetTag("response.length", result.Length);
            return result;
        }
        finally
        {
            lenBufferOwner?.Dispose();
            bufferOwner?.Dispose();
            
            // Record latency
            var endTime = System.Diagnostics.Stopwatch.GetTimestamp();
            var latencyMs = (endTime - startTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Diagnostics.UmbralSocketEventSource.Log.RecordLatency(latencyMs);
        }
    }    /// <summary>
    /// Advanced zero-copy send using <c>ReadOnlySequence&lt;byte&gt;</c>.
    /// </summary>
    public async ValueTask SendAsync(byte opcode, ReadOnlySequence<byte> payload, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("NamedPipe.SendAsync.ZeroCopy");
        activity?.SetTag("opcode", opcode);
        activity?.SetTag("payload.length", payload.Length);

        await EnsureConnectedAsync(cancellationToken);

        PooledBufferOwner? headerOwner = null;

        try
        {
            // Frame header
            var len = 1 + (int)payload.Length;
            headerOwner = new PooledBufferOwner(5);
            var header = headerOwner.Memory;
            BinaryPrimitives.WriteUInt32BigEndian(header.Span, (uint)len);
            header.Span[4] = opcode;
            
            await _client!.WriteAsync(header, cancellationToken);
            foreach (var segment in payload)
            {
                await _client.WriteAsync(segment, cancellationToken);            }
            await _client.FlushAsync(cancellationToken);
        }
        finally
        {
            headerOwner?.Dispose();
        }
    }

    /// <summary>
    /// Ensures the client is connected to the named pipe server.
    /// </summary>
    private async ValueTask EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client?.IsConnected == true)
            return;

        await _connectionSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_client?.IsConnected == true)
                return;            _client?.Dispose();
            _client = new NamedPipeClientStream(".", _name, PipeDirection.InOut, PipeOptions.Asynchronous);
            await _client.ConnectAsync(cancellationToken);
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Reads exactly the specified number of bytes from the stream.
    /// </summary>
    /// <param name="stream">The pipe stream to read from.</param>
    /// <param name="buffer">The buffer to read data into.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if all bytes were read successfully, false if the stream was closed.</returns>
    private static async ValueTask<bool> ReadExactAsync(PipeStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.Slice(read), cancellationToken);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }

    /// <summary>
    /// Disposes the client and closes the connection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _connectionSemaphore.WaitAsync();
        try
        {
            _client?.Dispose();
            _client = null;
        }
        finally
        {
            _connectionSemaphore.Release();
            _connectionSemaphore.Dispose();
        }
        
        ActivitySource.Dispose();
    }
}
