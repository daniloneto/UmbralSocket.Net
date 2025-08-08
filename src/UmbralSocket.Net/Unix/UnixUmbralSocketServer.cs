using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UmbralSocket.Net.Common;
using UmbralSocket.Net.Interfaces;
using UmbralSocket.Net.Transport;

namespace UmbralSocket.Net.Unix;

/// <summary>
/// Unix domain socket implementation of the Umbral socket server.
/// </summary>
public sealed class UnixUmbralSocketServer : IUmbralSocketServer
{
    private readonly string _path;
    private readonly Dictionary<byte, Func<ReadOnlySequence<byte>, ValueTask<byte[]>>> _handlers = new();
    private readonly Transport.IpcOptions _options;
    private int _activeConnections = 0;

    /// <summary>
    /// Initializes a new instance of the UnixUmbralSocketServer class.
    /// </summary>
    /// <param name="path">The Unix socket path. Defaults to "/tmp/umbral.sock" if not specified.</param>
    public UnixUmbralSocketServer(string? path = null)
    {
        _path = path ?? "/tmp/umbral.sock";
        if (File.Exists(_path))
            File.Delete(_path);
        _options = new Transport.IpcOptions(); // TODO: allow passing options
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
                
                // NÃO use 'using' aqui.
                // Inicie o processamento em uma tarefa separada e não a aguarde.
                // A nova tarefa será dona do socket do cliente.
                _ = Task.Run(() => ProcessAndCleanupClientAsync(client, cancellationToken), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Wraps client processing with proper socket cleanup and connection counting.
    /// </summary>
    private async Task ProcessAndCleanupClientAsync(Socket client, CancellationToken cancellationToken)
    {
        try
        {
            // O using vai aqui! Ele garante que o socket seja fechado
            // não importa o que aconteça dentro de ProcessClientAsync.
            using (client) 
            {
                var activeConnections = Interlocked.Increment(ref _activeConnections);
                if (_options.EnableCounters)
                    Diagnostics.UmbralSocketEventSource.Log.ConnectionChanged(activeConnections);

                await ProcessClientAsync(client, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Log de erros que podem acontecer durante o processamento
            _options.Logger?.LogError($"Unhandled error processing client: {ex.Message}");
        }
        finally
        {
            var activeConnections = Interlocked.Decrement(ref _activeConnections);
            if (_options.EnableCounters)
                Diagnostics.UmbralSocketEventSource.Log.ConnectionChanged(activeConnections);
        }
    }

    /// <summary>
    /// Processes client requests in a loop, maintaining the connection for multiple messages.
    /// </summary>
    private async Task ProcessClientAsync(Socket client, CancellationToken cancellationToken)
    {
        try
        {
            // Process multiple messages in a loop until client disconnects
            while (!cancellationToken.IsCancellationRequested)
            {
                // Read message length header
                var lenBufferOwner = new PooledBufferOwner(4);
                try
                {
                    var lenBuffer = lenBufferOwner.Memory;
                    if (!await ReceiveAllAsync(client, lenBuffer, cancellationToken))
                    {
                        _options.Logger?.LogDebug("Client disconnected gracefully");
                        return;
                    }
                    
                    var len = (int)BinaryPrimitives.ReadUInt32BigEndian(lenBuffer.Span);
                    
                    // Read message payload
                    var bufferOwner = new PooledBufferOwner(len);
                    try
                    {
                        var buffer = bufferOwner.Memory;
                        if (!await ReceiveAllAsync(client, buffer, cancellationToken))
                        {
                            _options.Logger?.LogDebug("Client disconnected while reading payload");
                            return;
                        }
                        
                        byte opcode = buffer.Span[0];
                        var payload = new ReadOnlySequence<byte>(buffer.Slice(1, len - 1));
                        
                        // Handle activity tracking
                        if (_options.EnableActivitySource && _options.ActivitySource != null)
                        {
                            using var activity = _options.ActivitySource.StartActivity(Diagnostics.ActivityNames.Receive, System.Diagnostics.ActivityKind.Server);
                            if (activity != null)
                            {
                                activity.SetTag("opcode", opcode);
                                activity.SetTag("payload_length", payload.Length);
                                activity.SetTag("peer", client.RemoteEndPoint?.ToString());
                                activity.SetTag("transport", "unix");
                            }
                        }
                        if (_options.EnableCounters)
                            Diagnostics.UmbralSocketEventSource.Log.BytesReceived(len);
                            
                        // Process the message
                        if (_handlers.TryGetValue(opcode, out var handler))
                        {
                            try
                            {
                                var responsePayload = await handler(payload);
                                var response = UmbralProtocol.EncodeMessage(opcode, responsePayload);
                                
                                if (_options.EnableActivitySource && _options.ActivitySource != null)
                                {
                                    using var activity = _options.ActivitySource.StartActivity(Diagnostics.ActivityNames.Send, System.Diagnostics.ActivityKind.Server);
                                    if (activity != null)
                                    {
                                        activity.SetTag("opcode", opcode);
                                        activity.SetTag("payload_length", responsePayload.Length);
                                        activity.SetTag("peer", client.RemoteEndPoint?.ToString());
                                        activity.SetTag("transport", "unix");
                                    }
                                }
                                if (_options.EnableCounters)
                                    Diagnostics.UmbralSocketEventSource.Log.BytesSent(response.Length);

                                // Send response - ensure all bytes are sent
                                var totalSent = 0;
                                while (totalSent < response.Length)
                                {
                                    var bytesSent = await client.SendAsync(response.Slice(totalSent), SocketFlags.None, cancellationToken);
                                    totalSent += bytesSent;
                                }
                            }
                            catch (Exception handlerEx)
                            {
                                // Handler failed - log error and send error response
                                _options.Logger?.LogError($"Handler for opcode {opcode:X2} failed: {handlerEx.Message}");
                                
                                // Send error response (empty payload indicates error)
                                var errorResponse = UmbralProtocol.EncodeMessage(opcode, Array.Empty<byte>());
                                try
                                {
                                    var totalSent = 0;
                                    while (totalSent < errorResponse.Length)
                                    {
                                        var bytesSent = await client.SendAsync(errorResponse.Slice(totalSent), SocketFlags.None, cancellationToken);
                                        totalSent += bytesSent;
                                    }
                                }
                                catch (Exception sendEx)
                                {
                                    _options.Logger?.LogError($"Failed to send error response: {sendEx.Message}");
                                    // If we can't send error response, exit the loop
                                    return;
                                }
                            }
                        }
                        else
                        {
                            // Handler not found - send error response
                            _options.Logger?.LogWarning($"No handler registered for opcode {opcode:X2}");
                            var errorResponse = UmbralProtocol.EncodeMessage(opcode, Array.Empty<byte>());
                            try
                            {
                                var totalSent = 0;
                                while (totalSent < errorResponse.Length)
                                {
                                    var bytesSent = await client.SendAsync(errorResponse.Slice(totalSent), SocketFlags.None, cancellationToken);
                                    totalSent += bytesSent;
                                }
                            }
                            catch (Exception sendEx)
                            {
                                _options.Logger?.LogError($"Failed to send 'handler not found' error response: {sendEx.Message}");
                                // If we can't send error response, exit the loop
                                return;
                            }
                        }
                    }
                    finally
                    {
                        bufferOwner.Dispose();
                    }
                }
                finally
                {
                    lenBufferOwner.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _options.Logger?.LogError($"Error processing client: {ex.Message}");
        }
    }

    private static async ValueTask<bool> ReceiveAllAsync(Socket socket, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            var n = await socket.ReceiveAsync(buffer.Slice(read), SocketFlags.None, cancellationToken);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }
}
