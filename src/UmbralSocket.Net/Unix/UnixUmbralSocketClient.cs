using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UmbralSocket.Net.Transport;
// using UmbralSocket.Net.Transport; // Already imported
using UmbralSocket.Net.Common;
using UmbralSocket.Net.Interfaces;

namespace UmbralSocket.Net.Unix
{    /// <summary>
    /// Unix domain socket implementation of the Umbral socket client.
    /// </summary>
    public sealed class UnixUmbralSocketClient : IUmbralSocketClient, IAsyncDisposable
    {
        private readonly string _path;
        private Socket? _socket;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the UnixUmbralSocketClient class.
        /// </summary>
        /// <param name="path">The Unix socket path. Defaults to "/tmp/umbral.sock" if not specified.</param>
        public UnixUmbralSocketClient(string? path = null)
        {
            _path = path ?? "/tmp/umbral.sock";
        }

        /// <summary>
        /// Ensures the client is connected to the server.
        /// </summary>
        private async ValueTask EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UnixUmbralSocketClient));

            if (_socket?.Connected == true)
                return;

            if (_socket != null)
            {
                _socket.Dispose();
            }

            _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endpoint = new UnixDomainSocketEndPoint(_path);
            await _socket.ConnectAsync(endpoint, cancellationToken);
        }        /// <summary>
        /// Sends a message asynchronously to the server via Unix domain socket (zero-copy).
        /// </summary>
        public async ValueTask<byte[]> SendAsync(byte opcode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
        {
            var startTime = System.Diagnostics.Stopwatch.GetTimestamp();
            
            await EnsureConnectedAsync(cancellationToken);
            
            var request = UmbralProtocol.EncodeMessage(opcode, payload);
            await _socket!.SendAsync(request, SocketFlags.None, cancellationToken);

            // Use try...finally para garantir limpeza dos buffers
            var lenBufferOwner = new Transport.PooledBufferOwner(4);
            var bufferOwner = default(Transport.PooledBufferOwner);
            try
            {
                var lenBuffer = lenBufferOwner.Memory;
                if (!await ReceiveAllAsync(_socket, lenBuffer, cancellationToken))
                {
                    throw new UmbralSocketException("Connection closed while reading length header");
                }
                
                var len = (int)BinaryPrimitives.ReadUInt32BigEndian(lenBuffer.Span);
                bufferOwner = new Transport.PooledBufferOwner(len);
                var buffer = bufferOwner.Memory;
                
                if (!await ReceiveAllAsync(_socket, buffer, cancellationToken))
                {
                    throw new UmbralSocketException("Connection closed while reading payload");
                }
                  byte responseOpcode = buffer.Span[0];
                if (responseOpcode != opcode)
                {
                    throw new UmbralSocketException("Opcode mismatch");
                }
                
                // Verificar se é uma resposta de erro (payload vazio)
                var payloadLength = len - 1;
                if (payloadLength == 0)
                {
                    throw new UmbralSocketException("Server returned error response (empty payload)");
                }                
                return buffer.Slice(1, payloadLength).ToArray();
            }
            finally
            {
                // Garantido que ambos serão descartados
                lenBufferOwner.Dispose();
                bufferOwner?.Dispose(); // O '?' é importante pois ele pode ser nulo se a primeira leitura falhar
                
                // Record latency
                var endTime = System.Diagnostics.Stopwatch.GetTimestamp();
                var latencyMs = (endTime - startTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                Diagnostics.UmbralSocketEventSource.Log.RecordLatency(latencyMs);
            }
        }        /// <summary>
        /// Advanced zero-copy send using <c>ReadOnlySequence&lt;byte&gt;</c>.
        /// </summary>
        public async ValueTask SendAsync(byte opcode, ReadOnlySequence<byte> payload, CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);
            
            // Frame header
            var len = 1 + (int)payload.Length;
            var headerOwner = new Transport.PooledBufferOwner(5);
            try
            {
                var header = headerOwner.Memory;
                BinaryPrimitives.WriteUInt32BigEndian(header.Span, (uint)len);
                header.Span[4] = opcode;
                await _socket!.SendAsync(header, SocketFlags.None, cancellationToken);
                foreach (var segment in payload)
                {
                    await _socket.SendAsync(segment, SocketFlags.None, cancellationToken);
                }
            }
            finally
            {
                headerOwner.Dispose();
            }
        }

        /// <summary>
        /// Disposes the client and closes the connection.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                _socket?.Dispose();
                _socket = null;
            }
            return ValueTask.CompletedTask;
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
}