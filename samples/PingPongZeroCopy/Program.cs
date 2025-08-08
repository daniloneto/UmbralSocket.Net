using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UmbralSocket.Net.Unix;
using UmbralSocket.Net.Windows;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== Zero-Copy Ping Pong Demo ===");
        
        if (OperatingSystem.IsWindows())
        {
            await RunNamedPipeZeroCopyDemo();
        }
        else
        {
            await RunUnixSocketZeroCopyDemo();
        }
    }    static async Task RunUnixSocketZeroCopyDemo()
    {
        Console.WriteLine("Using Unix Domain Sockets");
        
        var server = new UnixUmbralSocketServer("/tmp/umbral_zerocopy.sock");
        server.RegisterHandler(0x01, payload =>
        {
            // Zero-copy: work directly with ReadOnlySequence<byte>
            Console.WriteLine($"[SERVER] Received {payload.Length} bytes (zero-copy)");
            
            // For demo: convert to string without copying (if it's text)
            var text = Encoding.UTF8.GetString(payload.ToArray());
            var response = $"ECHO: {text}";
            
            return ValueTask.FromResult(Encoding.UTF8.GetBytes(response));
        });

        var cts = new CancellationTokenSource();
        var serverTask = server.StartAsync(cts.Token);
        
        await Task.Delay(1000); // Wait for server to start

        var client = new UnixUmbralSocketClient("/tmp/umbral_zerocopy.sock");
        
        try
        {
            // Test with large payload (1MB) - using ReadOnlyMemory for response
            var largePayload = new byte[1024 * 1024]; // 1MB
            Random.Shared.NextBytes(largePayload);
            
            Console.WriteLine($"Sending large payload: {largePayload.Length} bytes using ReadOnlyMemory<byte>");
            
            // Use ReadOnlyMemory version that returns a response
            var largeResponse = await client.SendAsync(0x01, new ReadOnlyMemory<byte>(largePayload), CancellationToken.None);
            
            Console.WriteLine($"Large payload response received: {largeResponse.Length} bytes");
            
            // Test with smaller payload 
            var smallMessage = "Hello Zero-Copy World!";
            Console.WriteLine($"Sending small message: {smallMessage}");
            var response = await client.SendAsync(0x01, Encoding.UTF8.GetBytes(smallMessage));
            Console.WriteLine($"Small message response: {Encoding.UTF8.GetString(response)}");
            
            Console.WriteLine("Demo completed successfully!");
        }
        finally
        {
            // Give server time to finish processing
            await Task.Delay(100);
            cts.Cancel();
            await client.DisposeAsync();
        }
    }    static async Task RunNamedPipeZeroCopyDemo()
    {
        Console.WriteLine("Using Named Pipes");
        
        var server = new NamedPipeUmbralSocketServer("UmbralZeroCopy");
        server.RegisterHandler(0x01, payload =>
        {
            // Zero-copy: work directly with ReadOnlySequence<byte>
            Console.WriteLine($"[SERVER] Received {payload.Length} bytes (zero-copy)");
            
            // For demo: convert to string without copying (if it's text)
            var text = Encoding.UTF8.GetString(payload.ToArray());
            var response = $"ECHO: {text}";
            
            return ValueTask.FromResult(Encoding.UTF8.GetBytes(response));
        });

        var cts = new CancellationTokenSource();
        var serverTask = server.StartAsync(cts.Token);
        
        await Task.Delay(1000); // Wait for server to start

        var client = new NamedPipeUmbralSocketClient("UmbralZeroCopy");
        
        try
        {
            // Test with large payload (1MB) - using ReadOnlyMemory for response
            var largePayload = new byte[1024 * 1024]; // 1MB
            Random.Shared.NextBytes(largePayload);
            
            Console.WriteLine($"Sending large payload: {largePayload.Length} bytes using ReadOnlyMemory<byte>");
            
            // Use ReadOnlyMemory version that returns a response
            var largeResponse = await client.SendAsync(0x01, new ReadOnlyMemory<byte>(largePayload), CancellationToken.None);
            
            Console.WriteLine($"Large payload response received: {largeResponse.Length} bytes");
            
            // Test with smaller payload 
            var smallMessage = "Hello Zero-Copy World!";
            Console.WriteLine($"Sending small message: {smallMessage}");
            var response = await client.SendAsync(0x01, Encoding.UTF8.GetBytes(smallMessage));
            Console.WriteLine($"Small message response: {Encoding.UTF8.GetString(response)}");
            
            Console.WriteLine("Demo completed successfully!");
        }
        finally
        {
            // Give server time to finish processing
            await Task.Delay(100);
            cts.Cancel();
            await client.DisposeAsync();
        }
    }
}
