using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace UmbralSocket.Net.Transport
{
    /// <summary>
    /// IPC options for UmbralSocket, including buffer sizes, logging, and diagnostics.
    /// </summary>
    public sealed class IpcOptions
    {
        /// <summary>
        /// Size of the send buffer.
        /// </summary>
        public int SendBufferSize { get; init; } = 64 * 1024;

        /// <summary>
        /// Size of the receive buffer.
        /// </summary>
        public int ReceiveBufferSize { get; init; } = 64 * 1024;

        /// <summary>
        /// Optional logger for state changes, warnings, and errors.
        /// </summary>
        public ILogger? Logger { get; init; }

        /// <summary>
        /// Enable EventCounters for observability.
        /// </summary>
        public bool EnableCounters { get; init; } = true;

        /// <summary>
        /// Enable ActivitySource for distributed tracing.
        /// </summary>
        public bool EnableActivitySource { get; init; } = true;

        /// <summary>
        /// Optional ActivitySource for custom tracing.
        /// </summary>
        public ActivitySource? ActivitySource { get; init; }
    }
}
