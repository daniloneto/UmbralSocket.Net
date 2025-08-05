using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace UmbralSocket.Net.Interfaces;

/// <summary>
/// Defines the contract for an Umbral socket server that can receive and handle messages from clients.
/// </summary>
public interface IUmbralSocketServer
{
    /// <summary>
    /// Registers a message handler for a specific opcode.
    /// </summary>
    /// <param name="opcode">The operation code to handle.</param>
    /// <param name="handler">The handler function that processes the message and returns a response.</param>
    void RegisterHandler(byte opcode, Func<ReadOnlySequence<byte>, ValueTask<byte[]>> handler);
    
    /// <summary>
    /// Starts the server to listen for incoming connections.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);
}
