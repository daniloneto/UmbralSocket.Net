using System.Buffers;

namespace UmbralSocket.Net;

/// <summary>
/// Extension methods for working with ReadOnlySequence&lt;byte&gt; in the Umbral socket library.
/// </summary>
public static class SequenceExtensions
{
    /// <summary>
    /// Converts a ReadOnlySequence&lt;byte&gt; to a byte array.
    /// </summary>
    /// <param name="sequence">The sequence to convert.</param>
    /// <returns>A byte array containing the sequence data.</returns>
    public static byte[] ToArray(this ReadOnlySequence<byte> sequence)
    {
        if (sequence.IsSingleSegment)
        {
            return sequence.FirstSpan.ToArray();
        }

        var buffer = new byte[sequence.Length];
        sequence.CopyTo(buffer);
        return buffer;
    }
}
