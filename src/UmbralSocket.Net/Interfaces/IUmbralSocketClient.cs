using System;
using System.Threading;
using System.Threading.Tasks;

namespace UmbralSocket.Net.Interfaces;

/// <summary>
/// Defines the contract for an Umbral socket client that can send messages to a server.
/// </summary>
public interface IUmbralSocketClient
{
    /// <summary>
    /// Sends a message asynchronously to the server.
    /// </summary>
    /// <param name="opcode">The operation code that identifies the message type.</param>
    /// <param name="payload">The message payload data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous send operation. The task result contains the server response.</returns>
    ValueTask<byte[]> SendAsync(byte opcode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);
}
