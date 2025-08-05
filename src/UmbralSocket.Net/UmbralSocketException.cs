using System;

namespace UmbralSocket.Net;

/// <summary>
/// Exception thrown when errors occur in Umbral socket operations.
/// </summary>
public class UmbralSocketException : Exception
{
    /// <summary>
    /// Initializes a new instance of the UmbralSocketException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public UmbralSocketException(string message) : base(message) { }
    
    /// <summary>
    /// Initializes a new instance of the UmbralSocketException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    public UmbralSocketException(string message, Exception inner) : base(message, inner) { }
}
