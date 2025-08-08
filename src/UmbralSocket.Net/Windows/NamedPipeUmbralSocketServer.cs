using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UmbralSocket.Net.Common;
using UmbralSocket.Net.Interfaces;
using UmbralSocket.Net.Transport;

namespace UmbralSocket.Net.Windows;

/// <summary>
/// Named pipe implementation of the Umbral socket server for Windows.
/// </summary>
public sealed class NamedPipeUmbralSocketServer : IUmbralSocketServer
{
    private readonly string _name;
    private readonly Dictionary<byte, Func<ReadOnlySequence<byte>, ValueTask<byte[]>>> _handlers = new();
    private readonly Transport.IpcOptions _options;
    private int _activeConnections = 0;

    /// <summary>
    /// Initializes a new instance of the NamedPipeUmbralSocketServer class.
    /// </summary>
    /// <param name="name">The named pipe name. Defaults to "umbral" if not specified.</param>
    public NamedPipeUmbralSocketServer(string? name = null)
    {
        _name = name ?? "umbral";
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
                var server = new NamedPipeServerStream(_name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken);
                
                // NÃO use 'using' aqui.
                // Inicie o processamento em uma tarefa separada e não a aguarde.
                // A nova tarefa será dona do pipe do cliente.
                _ = Task.Run(() => ProcessAndCleanupClientAsync(server, cancellationToken), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }    }

    /// <summary>
    /// Wraps client processing with proper pipe cleanup and connection counting.
    /// </summary>
    private async Task ProcessAndCleanupClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        try
        {
            // O using vai aqui! Ele garante que o pipe seja fechado
            // não importa o que aconteça dentro de ProcessClientAsync.
            using (pipe) 
            {
                var activeConnections = Interlocked.Increment(ref _activeConnections);
                if (_options.EnableCounters)
                    Diagnostics.UmbralSocketEventSource.Log.ConnectionChanged(activeConnections);

                await ProcessClientAsync(pipe, cancellationToken);
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
    }    /// <summary>
    /// Processes a single client request and sends response.
    /// </summary>
    private async Task ProcessClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        try
        {
            // Process multiple messages on the same connection
            while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                var lenBufferOwner = new PooledBufferOwner(4);
                var lenBuffer = lenBufferOwner.Memory;
                if (!await ReadExactAsync(pipe, lenBuffer, cancellationToken))
                {
                    lenBufferOwner.Dispose();
                    _options.Logger?.LogWarning("Connection closed before length received");
                    return;
                }
                int len = (int)BinaryPrimitives.ReadUInt32BigEndian(lenBuffer.Span);
                var bufferOwner = new PooledBufferOwner(len);
                var buffer = bufferOwner.Memory;
                if (!await ReadExactAsync(pipe, buffer, cancellationToken))
                {
                    lenBufferOwner.Dispose();
                    bufferOwner.Dispose();
                    _options.Logger?.LogWarning("Connection closed before payload received");
                    return;
                }
                byte opcode = buffer.Span[0];
                var payload = new ReadOnlySequence<byte>(buffer.Slice(1, len - 1));

                if (_options.EnableActivitySource && _options.ActivitySource != null)
                {
                    using var activity = _options.ActivitySource.StartActivity(Diagnostics.ActivityNames.Receive, System.Diagnostics.ActivityKind.Server);
                    if (activity != null)
                    {
                        activity.SetTag("opcode", opcode);
                        activity.SetTag("payload_length", payload.Length);
                        activity.SetTag("peer", "namedpipe");
                        activity.SetTag("transport", "namedpipe");
                    }
                }
                if (_options.EnableCounters)
                    Diagnostics.UmbralSocketEventSource.Log.BytesReceived(len);

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
                                activity.SetTag("peer", "namedpipe");
                                activity.SetTag("transport", "namedpipe");
                            }
                        }
                        if (_options.EnableCounters)
                            Diagnostics.UmbralSocketEventSource.Log.BytesSent(response.Length);

                        await pipe.WriteAsync(response, cancellationToken);
                        await pipe.FlushAsync(cancellationToken);
                    }
                    catch (Exception handlerEx)
                    {
                        // Handler falhou - logar o erro E enviar uma resposta de erro para o cliente
                        _options.Logger?.LogError($"Handler for opcode {opcode:X2} failed: {handlerEx.Message}");
                        
                        // Enviar resposta de erro (payload vazio indica erro)
                        var errorResponse = UmbralProtocol.EncodeMessage(opcode, Array.Empty<byte>());
                        try
                        {
                            await pipe.WriteAsync(errorResponse, cancellationToken);
                            await pipe.FlushAsync(cancellationToken);
                        }
                        catch (Exception sendEx)
                        {
                            _options.Logger?.LogError($"Failed to send error response: {sendEx.Message}");
                            // Se não conseguimos nem enviar o erro, a conexão será fechada
                            break;
                        }
                    }
                }
                else
                {
                    // Handler não encontrado - enviar resposta de erro
                    _options.Logger?.LogWarning($"No handler registered for opcode {opcode:X2}");
                    var errorResponse = UmbralProtocol.EncodeMessage(opcode, Array.Empty<byte>());
                    try
                    {
                        await pipe.WriteAsync(errorResponse, cancellationToken);
                        await pipe.FlushAsync(cancellationToken);
                    }
                    catch (Exception sendEx)
                    {
                        _options.Logger?.LogError($"Failed to send 'handler not found' error response: {sendEx.Message}");
                        break;
                    }
                }
                
                lenBufferOwner.Dispose();
                bufferOwner.Dispose();
            }
        }
        catch (Exception ex)
        {
            _options.Logger?.LogError($"Error processing client: {ex.Message}");
        }
        // NÃO feche o pipe aqui. O 'using' no método wrapper fará isso.
    }

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
}
