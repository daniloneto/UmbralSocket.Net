using System;
namespace UmbralSocket.Net;

/// <summary>
/// Configuration options for Umbral socket connections.
/// </summary>
public class UmbralSocketOptions
{
    /// <summary>
    /// Gets or sets the path (Unix socket) or name (Windows named pipe) for the socket connection.
    /// Defaults to "umbral" on Windows and "/tmp/umbral.sock" on Unix systems.
    /// </summary>
    public string PathOrName { get; init; } = OperatingSystem.IsWindows() ? "umbral" : "/tmp/umbral.sock";
    
    /// <summary>
    /// Gets or sets the buffer size used for socket operations. Default is 8192 bytes.
    /// </summary>
    public int BufferSize { get; init; } = 8192;
}
