using System.Buffers;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UmbralSocket.Net.Windows;
using Xunit;

namespace UmbralSocket.Net.Tests;

/// <summary>
/// Tests for Named Pipe implementation of Umbral socket communication.
/// </summary>
public class NamedPipeTests
{
    /// <summary>
    /// Tests sending and receiving messages through Named Pipes with various opcodes.
    /// </summary>
    /// <param name="opcode">The operation code to test.</param>
    [Theory]
    [InlineData(0x01)]
    [InlineData(0x02)]
    [InlineData(0x03)]
    public async Task SendAndReceive(byte opcode)
    {
        var name = $"umbral_{Guid.NewGuid():N}";
        var server = new NamedPipeUmbralSocketServer(name);
        server.RegisterHandler(opcode, payload => ValueTask.FromResult(payload.ToArray()));
        using var cts = new CancellationTokenSource();
        var serverTask = server.StartAsync(cts.Token);
        await Task.Delay(100);

        var client = new NamedPipeUmbralSocketClient(name);
        var data = Encoding.UTF8.GetBytes("hello");
        var response = await client.SendAsync(opcode, data);
        Assert.Equal(data, response);

        cts.Cancel();
        await serverTask;
    }
}
