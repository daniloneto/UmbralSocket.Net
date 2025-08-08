using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace UmbralSocket.Net.Transport
{    /// <summary>
    /// IPC client using PipeReader/PipeWriter for zero-copy and buffer pooling.
    /// </summary>
    public abstract class IpcClient : IDisposable
    {
        /// <summary>
        /// The pipe reader for receiving data.
        /// </summary>
        protected readonly PipeReader _reader;
        
        /// <summary>
        /// The pipe writer for sending data.
        /// </summary>
        protected readonly PipeWriter _writer;
        
        /// <summary>
        /// The IPC options configuration.
        /// </summary>
        protected readonly IpcOptions _options;

        /// <summary>
        /// Initializes a new instance of the IpcClient class.
        /// </summary>
        /// <param name="reader">The pipe reader for receiving data.</param>
        /// <param name="writer">The pipe writer for sending data.</param>
        /// <param name="options">The IPC options configuration.</param>
        protected IpcClient(PipeReader reader, PipeWriter writer, IpcOptions options)
        {
            _reader = reader;
            _writer = writer;
            _options = options;
        }

        /// <summary>
        /// Sends a message with the given opcode and payload (zero-copy).
        /// </summary>
        public abstract ValueTask SendAsync(byte opcode, ReadOnlyMemory<byte> payload, CancellationToken ct = default);

        /// <summary>
        /// Sends a message with the given opcode and payload (advanced zero-copy).
        /// </summary>
        public abstract ValueTask SendAsync(byte opcode, ReadOnlySequence<byte> payload, CancellationToken ct = default);        /// <summary>
        /// Receives messages and invokes the handler delegate for each message.
        /// </summary>
        public abstract Task ReceiveAsync(MessageHandler handler, CancellationToken ct = default);

        /// <summary>
        /// Disposes the IPC client and completes the underlying pipes.
        /// </summary>
        public virtual void Dispose()
        {
            _reader.Complete();
            _writer.Complete();
        }
    }

    /// <summary>
    /// Delegate for handling received messages.
    /// </summary>
    public delegate ValueTask MessageHandler(byte opcode, ReadOnlySequence<byte> payload, CancellationToken ct);
}
