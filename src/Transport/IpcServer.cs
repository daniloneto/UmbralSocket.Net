using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace UmbralSocket.Net.Transport
{    /// <summary>
    /// IPC server using PipeReader/PipeWriter for zero-copy and buffer pooling.
    /// </summary>
    public abstract class IpcServer : IDisposable
    {
        /// <summary>
        /// The IPC options configuration.
        /// </summary>
        protected readonly IpcOptions _options;

        /// <summary>
        /// Initializes a new instance of the IpcServer class.
        /// </summary>
        /// <param name="options">The IPC options configuration.</param>
        protected IpcServer(IpcOptions options)
        {
            _options = options;
        }        /// <summary>
        /// Accepts a client and returns a connected IpcClient.
        /// </summary>
        public abstract Task<IpcClient> AcceptAsync(CancellationToken ct = default);

        /// <summary>
        /// Disposes the IPC server.
        /// </summary>
        public virtual void Dispose() { }
    }
}
