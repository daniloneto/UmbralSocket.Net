using System;

namespace UmbralSocket.Net.Transport
{
    /// <summary>
    /// Represents a pooled buffer owner for zero-copy operations.
    /// </summary>
    public interface IBufferOwner : IDisposable
    {
        /// <summary>
        /// Gets the owned memory buffer.
        /// </summary>
        Memory<byte> Memory { get; }
    }
}
