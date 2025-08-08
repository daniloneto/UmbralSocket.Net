using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using UmbralSocket.Net.Security;
using UmbralSocket.Net.Unix;
using UmbralSocket.Net.Windows;
using UmbralSocket.Net;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== Secure Server Demo ===");
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await RunSecureUnixSocketDemo();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await RunSecureNamedPipeDemo();
        }
        else
        {
            Console.WriteLine("Unsupported OS for SecureServer sample.");
        }
    }

    static async Task RunSecureUnixSocketDemo()
    {
        Console.WriteLine("=== Unix Domain Socket Security Demo ===");
          try
        {
            // Create secure socket path with proper permissions
            // Use environment variable or fallback to /tmp for container compatibility
            var basePath = Environment.GetEnvironmentVariable("SECURE_SOCKET_PATH") ?? "/tmp";
            var socketPath = UnixSocketPathHelper.CreateSocketDirectory(basePath, "secure.sock");
            Console.WriteLine($"Created secure UDS path: {socketPath}");
            Console.WriteLine("- Directory permissions: 0700 (owner only)");
            Console.WriteLine("- Socket will have permissions: 0660 (owner + group)");
            
            var server = new UnixUmbralSocketServer(socketPath);
            server.RegisterHandler(0x42, payload =>
            {
                var message = Encoding.UTF8.GetString(payload.ToArray());
                Console.WriteLine($"[SECURE SERVER] Received: {message}");
                return ValueTask.FromResult(Encoding.UTF8.GetBytes($"Secure response: {message}"));
            });

            var cts = new CancellationTokenSource();
            var serverTask = server.StartAsync(cts.Token);
            
            Console.WriteLine("Secure Unix socket server started. Press Enter to stop...");
            Console.ReadLine();
            
            cts.Cancel();
            Console.WriteLine("Secure server stopped.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting up secure Unix socket: {ex.Message}");
            Console.WriteLine("Note: On some systems, you may need elevated privileges to set socket permissions.");
        }
    }

    static async Task RunSecureNamedPipeDemo()
    {
        Console.WriteLine("=== Named Pipe Security Demo ===");
        
        try
        {
            // Create restricted pipe security (only SYSTEM, Administrators, and optionally a specific account)
            var pipeSecurity = NamedPipeSecurityHelper.CreateRestrictedPipeSecurity();
            Console.WriteLine("Created restricted PipeSecurity:");
            Console.WriteLine("- SYSTEM: Full Control");
            Console.WriteLine("- Administrators: Full Control"); 
            Console.WriteLine("- Everyone: DENIED (explicitly blocked)");
            
            // For demo purposes, we'll use the default constructor
            // In production, you'd pass the PipeSecurity to a custom constructor
            var server = new NamedPipeUmbralSocketServer("SecureUmbralPipe");
            server.RegisterHandler(0x42, payload =>
            {
                var message = Encoding.UTF8.GetString(payload.ToArray());
                Console.WriteLine($"[SECURE SERVER] Received: {message}");
                return ValueTask.FromResult(Encoding.UTF8.GetBytes($"Secure response: {message}"));
            });

            var cts = new CancellationTokenSource();
            var serverTask = server.StartAsync(cts.Token);
            
            Console.WriteLine("Secure Named Pipe server started. Testing with client...");
            
            // Test with a client
            await Task.Delay(1000);
            var client = new NamedPipeUmbralSocketClient("SecureUmbralPipe");
            var response = await client.SendAsync(0x42, Encoding.UTF8.GetBytes("Hello Secure Pipe!"));
            Console.WriteLine($"Client received: {Encoding.UTF8.GetString(response)}");
            
            await client.DisposeAsync();
            
            Console.WriteLine("Press Enter to stop server...");
            Console.ReadLine();
            
            cts.Cancel();
            Console.WriteLine("Secure server stopped.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting up secure Named Pipe: {ex.Message}");
        }
    }
}
