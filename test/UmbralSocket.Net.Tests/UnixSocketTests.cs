using System.Buffers;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UmbralSocket.Net.Unix;
using Xunit;

namespace UmbralSocket.Net.Tests;

/// <summary>
/// Tests for Unix domain socket implementation of Umbral socket communication.
/// </summary>
public class UnixSocketTests
{
    /// <summary>
    /// Tests sending and receiving messages through Unix domain sockets with various opcodes.
    /// </summary>
    /// <param name="opcode">The operation code to test.</param>
    [Theory]
    [InlineData(0x01)]
    [InlineData(0x02)]
    [InlineData(0x03)]
    public async Task SendAndReceive(byte opcode)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"umbral_{Guid.NewGuid():N}.sock");
        var server = new UnixUmbralSocketServer(path);
        server.RegisterHandler(opcode, payload => ValueTask.FromResult(payload.ToArray()));
        using var cts = new CancellationTokenSource();
        var serverTask = server.StartAsync(cts.Token);
        await Task.Delay(100);

        var client = new UnixUmbralSocketClient(path);
        var data = Encoding.UTF8.GetBytes("hello");
        var response = await client.SendAsync(opcode, data);
        Assert.Equal(data, response);

        cts.Cancel();
        await serverTask;
    }
}
