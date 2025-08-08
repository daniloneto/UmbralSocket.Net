using System;
using System.Buffers;

namespace UmbralSocket.Net.Transport
{
    /// <summary>
    /// Internal implementation of IBufferOwner backed by ArrayPool.
    /// </summary>
    internal sealed class PooledBufferOwner : IBufferOwner
    {
        private byte[]? _buffer;
        private readonly int _length;
        private bool _disposed;

        public PooledBufferOwner(int length)
        {
            _buffer = ArrayPool<byte>.Shared.Rent(length);
            _length = length;
        }

        public Memory<byte> Memory
        {
            get
            {
                if (_disposed || _buffer == null) throw new ObjectDisposedException(nameof(PooledBufferOwner));
                return new Memory<byte>(_buffer, 0, _length);
            }
        }

        public void Dispose()
        {
            if (!_disposed && _buffer != null)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = null;
                _disposed = true;
            }
        }
    }
}
