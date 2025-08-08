using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UmbralSocket.Net.Unix;
using UmbralSocket.Net.Windows;

namespace UmbralSocket.Net.Sample;

// Helper method to convert ReadOnlySequence<byte> to byte[]
internal static class SequenceExtensions
{
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

// JSON source generation for AOT compatibility
[JsonSerializable(typeof(UserRequest))]
[JsonSerializable(typeof(UserResponse))]
[JsonSerializable(typeof(ProductRequest))]
[JsonSerializable(typeof(ProductResponse))]
[JsonSerializable(typeof(CalculatorRequest))]
internal partial class SampleJsonContext : JsonSerializerContext
{
}

// Data models
internal record UserRequest(int Id, string Action);
internal record UserResponse(int Id, string Name, string Email, string Status);
internal record ProductRequest(string Sku, string Action);
internal record ProductResponse(string Sku, string Name, double Price, int Stock);
internal record CalculatorRequest(double A, double B, string Operation);

/// <summary>
/// Sample application demonstrating UmbralSocket.Net usage
/// </summary>
public static class Program
{    /// <summary>
    /// Main entry point
    /// </summary>    /// <summary>
    /// Main entry point
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== UmbralSocket.Net Sample Application ===\n");

        if (args.Length == 0)
        {
            await RunInteractiveDemo();
            return;
        }

        switch (args[0].ToLower())
        {
            case "unix":
                await RunUnixSocketDemo();
                break;
            case "namedpipe":
                await RunNamedPipeDemo();
                break;
            case "benchmark":
                await RunBenchmark();
                break;
            case "json":
                await RunJsonDemo();
                break;
            case "server":
                await RunPingPongServer();
                break;            case "client":
                await RunPingPongClient();
                break;
            default:
                ShowUsage();
                break;
        }
    }    /// <summary>
    /// Gets the socket path from environment variable or uses default
    /// </summary>
    /// <returns>Socket path to use</returns>
    private static string GetSocketPath()
    {
        var envPath = Environment.GetEnvironmentVariable("UMBRAL_SOCKET_PATH");
        var defaultPath = "/tmp/umbral_sample.sock";
        
        return envPath ?? defaultPath;
    }// PingPong Server: responde com o texto recebido acrescido de "+pong"
    private static async Task RunPingPongServer()
    {
        Console.WriteLine("=== PingPong Unix Socket Server ===");
        if (OperatingSystem.IsWindows())
        {
            Console.WriteLine("Unix Sockets requerem Linux/macOS ou Windows 10 build 17063+");
            return;
        }
        
        var socketPath = GetSocketPath();
        Console.WriteLine($"Criando servidor no socket: {socketPath}");
          // Verificar e criar diretório se necessário
        var socketDir = Path.GetDirectoryName(socketPath);
        if (!string.IsNullOrEmpty(socketDir) && !Directory.Exists(socketDir))
        {
            Console.WriteLine($"Criando diretório: {socketDir}");
            Directory.CreateDirectory(socketDir);
        }
        
        // Remover socket existente se houver
        if (File.Exists(socketPath))
        {
            Console.WriteLine($"Removendo socket existente: {socketPath}");
            File.Delete(socketPath);
        }
          var server = new UnixUmbralSocketServer(socketPath);
        server.RegisterHandler(0x42, payload =>
        {
            var text = Encoding.UTF8.GetString(SequenceExtensions.ToArray(payload));
            var response = text + "-pong";
            Console.WriteLine($"[SERVER] Recebido: '{text}' -> Resposta: '{response}'");
            Console.WriteLine($"[SERVER] Payload size: {payload.Length} bytes");
            return ValueTask.FromResult(Encoding.UTF8.GetBytes(response));
        });
          Console.WriteLine("Iniciando servidor...");
        var serverTask = server.StartAsync(CancellationToken.None);
        
        // Aguardar um pouco para o socket ser criado
        await Task.Delay(1000);
        
        // Forçar permissões do socket (Linux/Unix)
        if (!OperatingSystem.IsWindows() && File.Exists(socketPath))
        {
            try
            {
                // Definir permissões 0777 (rwxrwxrwx) para o socket
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"777 \"{socketPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                
                process.Start();
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"✅ Permissões definidas para o socket: {socketPath}");
                }
                else
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    Console.WriteLine($"⚠️ Falha ao definir permissões: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Erro ao definir permissões: {ex.Message}");
            }
        }
          // Verificar se o socket foi criado
        if (File.Exists(socketPath))
        {
            Console.WriteLine($"✅ Socket criado com sucesso: {socketPath}");
        }
        else
        {
            Console.WriteLine($"❌ Falha ao criar socket: {socketPath}");
        }
          // Listar conteúdo do diretório
        if (!string.IsNullOrEmpty(socketDir) && Directory.Exists(socketDir))
        {
            var files = Directory.GetFileSystemEntries(socketDir);
            Console.WriteLine($"[SERVER] Arquivos em {socketDir}: [{string.Join(", ", files)}]");
        }
        
        Console.WriteLine("Servidor PingPong iniciado. Aguardando conexões...");
        await serverTask;
    }    // PingPong Client: envia "ping", recebe resposta, acrescenta e repete 10 vezes
    private static async Task RunPingPongClient()
    {
        Console.WriteLine("=== PingPong Unix Socket Client ===");
        if (OperatingSystem.IsWindows())
        {
            Console.WriteLine("Unix Sockets requerem Linux/macOS ou Windows 10 build 17063+");
            return;
        }
        
        var socketPath = GetSocketPath();
        Console.WriteLine($"Conectando ao socket: {socketPath}");        // Aguardar servidor estar pronto e verificar socket várias vezes
        for (int attempt = 1; attempt <= 10; attempt++)
        {
            Console.WriteLine($"Tentativa {attempt}/10: Verificando socket...");
            var socketDir = Path.GetDirectoryName(socketPath);
            if (!string.IsNullOrEmpty(socketDir) && Directory.Exists(socketDir))
            {
                var files = Directory.GetFileSystemEntries(socketDir);
                Console.WriteLine($"Arquivos em {socketDir}: [{string.Join(", ", files)}]");
                
                // Se socket existe, verificar permissões
                if (files.Contains(socketPath))
                {
                    Console.WriteLine($"✅ Socket encontrado: {socketPath}");
                    
                    // Verificar permissões do socket (Linux/Unix)
                    if (!OperatingSystem.IsWindows())
                    {
                        try
                        {
                            var process = new System.Diagnostics.Process
                            {
                                StartInfo = new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = "ls",
                                    Arguments = $"-la \"{socketPath}\"",
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true
                                }
                            };
                            
                            process.Start();
                            var output = await process.StandardOutput.ReadToEndAsync();
                            await process.WaitForExitAsync();
                            
                            Console.WriteLine($"Permissões do socket: {output.Trim()}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Erro ao verificar permissões: {ex.Message}");
                        }
                    }
                    break;
                }
                
                // Verificar se o socket existe (Unix sockets são detectáveis de outras formas)
                if (File.Exists(socketPath))
                {
                    Console.WriteLine($"✅ Socket encontrado: {socketPath}");
                    break;
                }
            }
            else
            {
                Console.WriteLine($"Diretório não existe: {socketDir}");
            }
            
            if (attempt == 10)
            {
                Console.WriteLine($"❌ Socket não encontrado após 10 tentativas: {socketPath}");
                return;
            }
              await Task.Delay(1000);
        }
        
        // Manter conexão aberta para todas as iterações (mais eficiente)
        await using var client = new UnixUmbralSocketClient(socketPath);
        
        string msg = "ping";
        for (int i = 1; i <= 10; i++)
        {
            try
            {
                Console.WriteLine($"[CLIENT] Enviando iteração {i}: '{msg}'");                var response = await client.SendAsync(0x42, Encoding.UTF8.GetBytes(msg));
                var respStr = Encoding.UTF8.GetString(response);
                Console.WriteLine($"[CLIENT] {i}: {msg} -> {respStr}");
                msg = respStr + "-ping";
                
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLIENT] Erro na iteração {i}: {ex.Message}");
                Console.WriteLine($"[CLIENT] Stack trace: {ex.StackTrace}");
                
                // Tentar continuar com as próximas iterações para investigar se é um problema pontual
                await Task.Delay(1000);
            }
        }
        Console.WriteLine("\nPingPong finalizado!");
    }

    private static async Task RunInteractiveDemo()
    {
        Console.WriteLine("Escolha um demo para executar:");
        Console.WriteLine("1. Unix Socket Demo");
        Console.WriteLine("2. Named Pipe Demo (Windows)");
        Console.WriteLine("3. Benchmark");
        Console.WriteLine("4. JSON Demo");
        Console.WriteLine("5. Sair");
        Console.Write("\nEscolha uma opção (1-5): ");

        var choice = Console.ReadLine();
        
        switch (choice)
        {
            case "1":
                await RunUnixSocketDemo();
                break;
            case "2":
                await RunNamedPipeDemo();
                break;
            case "3":
                await RunBenchmark();
                break;
            case "4":
                await RunJsonDemo();
                break;
            case "5":
                Console.WriteLine("Saindo...");
                return;
            default:
                Console.WriteLine("Opção inválida!");
                await RunInteractiveDemo();
                break;
        }
    }

    private static async Task RunUnixSocketDemo()
    {
        Console.WriteLine("=== Unix Socket Demo ===");
        
        if (OperatingSystem.IsWindows())
        {
            Console.WriteLine("Unix Sockets requerem Linux/macOS ou Windows 10 build 17063+");
            return;
        }

        var server = new UnixUmbralSocketServer("/tmp/umbral_sample.sock");
        
        // Registrar handlers
        server.RegisterHandler(0x01, payload =>
        {
            var text = Encoding.UTF8.GetString(UmbralSocket.Net.SequenceExtensions.ToArray(payload));
            Console.WriteLine($"[SERVER] Recebido: {text}");
            return ValueTask.FromResult(Encoding.UTF8.GetBytes($"ECHO: {text}"));
        });

        server.RegisterHandler(0x02, payload =>
        {
            var text = Encoding.UTF8.GetString(UmbralSocket.Net.SequenceExtensions.ToArray(payload));
            Console.WriteLine($"[SERVER] Processando: {text}");
            return ValueTask.FromResult(Encoding.UTF8.GetBytes(text.ToUpper()));
        });

        var cts = new CancellationTokenSource();
        var serverTask = server.StartAsync(cts.Token);

        Console.WriteLine("Servidor Unix Socket iniciado...");
        await Task.Delay(1000); // Aguardar servidor inicializar        // Reutilizar conexão para melhor performance
        await using var client = new UnixUmbralSocketClient("/tmp/umbral_sample.sock");

        // Teste 1: Echo
        Console.WriteLine("\n--- Teste 1: Echo ---");
        var response1 = await client.SendAsync(0x01, Encoding.UTF8.GetBytes("Hello World!"));
        Console.WriteLine($"[CLIENT] Resposta: {Encoding.UTF8.GetString(response1)}");

        // Teste 2: Uppercase
        Console.WriteLine("\n--- Teste 2: Uppercase ---");
        var response2 = await client.SendAsync(0x02, Encoding.UTF8.GetBytes("convert to uppercase"));
        Console.WriteLine($"[CLIENT] Resposta: {Encoding.UTF8.GetString(response2)}");

        cts.Cancel();
        Console.WriteLine("\nDemo Unix Socket finalizado!");
    }

    private static async Task RunNamedPipeDemo()
    {
        Console.WriteLine("=== Named Pipe Demo ===");
        
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("Named Pipes são específicos do Windows");
            return;
        }

        var server = new NamedPipeUmbralSocketServer("UmbralSample");
        
        // Registrar handler de calculadora
        server.RegisterHandler(0x10, payload =>
        {
            var data = Encoding.UTF8.GetString(UmbralSocket.Net.SequenceExtensions.ToArray(payload)).Split(',');
            if (data.Length == 3 && 
                double.TryParse(data[0], out var a) && 
                double.TryParse(data[1], out var b))
            {
                var operation = data[2];
                double result = operation switch
                {
                    "+" => a + b,
                    "-" => a - b,
                    "*" => a * b,
                    "/" => b != 0 ? a / b : double.NaN,
                    _ => double.NaN
                };
                
                Console.WriteLine($"[SERVER] Calculando: {a} {operation} {b} = {result}");
                return ValueTask.FromResult(Encoding.UTF8.GetBytes(result.ToString()));
            }
            
            return ValueTask.FromResult(Encoding.UTF8.GetBytes("ERROR"));
        });

        var cts = new CancellationTokenSource();
        var serverTask = server.StartAsync(cts.Token);

        Console.WriteLine("Servidor Named Pipe iniciado...");
        await Task.Delay(1000);        // Reutilizar conexão para melhor performance
        await using var client = new NamedPipeUmbralSocketClient("UmbralSample");

        // Testes de calculadora
        var operations = new[]
        {
            ("10", "5", "+"),
            ("20", "3", "-"),
            ("7", "8", "*"),
            ("15", "3", "/")
        };

        Console.WriteLine("\n--- Teste: Calculadora via Named Pipe ---");
        foreach (var (a, b, op) in operations)
        {
            var request = $"{a},{b},{op}";
            var response = await client.SendAsync(0x10, Encoding.UTF8.GetBytes(request));
            var result = Encoding.UTF8.GetString(response);
            Console.WriteLine($"[CLIENT] {a} {op} {b} = {result}");
        }

        cts.Cancel();
        Console.WriteLine("\nDemo Named Pipe finalizado!");
    }

    private static async Task RunBenchmark()
    {
        Console.WriteLine("=== Benchmark Demo ===");
        
        // Escolher implementação baseada no OS
        if (OperatingSystem.IsWindows())
        {
            await RunNamedPipeBenchmark();
        }
        else
        {
            await RunUnixSocketBenchmark();
        }
    }

    private static async Task RunUnixSocketBenchmark()
    {
        var server = new UnixUmbralSocketServer("/tmp/umbral_benchmark.sock");
        server.RegisterHandler(0xFF, payload => ValueTask.FromResult(payload.ToArray())); // Echo simples

        var cts = new CancellationTokenSource();
        var serverTask = server.StartAsync(cts.Token);
        await Task.Delay(1000);        // Reutilizar conexão para melhor performance no benchmark
        await using var client = new UnixUmbralSocketClient("/tmp/umbral_benchmark.sock");
        var payload = new byte[1024]; // 1KB payload
        Random.Shared.NextBytes(payload);

        const int iterations = 1000;
        Console.WriteLine($"Executando {iterations} operações com payload de {payload.Length} bytes...");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            await client.SendAsync(0xFF, payload);
            if (i % 100 == 0)
                Console.Write(".");
        }
        
        stopwatch.Stop();
        
        var opsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
        var throughputMBps = (iterations * payload.Length) / (1024.0 * 1024.0) / stopwatch.Elapsed.TotalSeconds;
        
        Console.WriteLine($"\n\nResultados do Benchmark (Unix Socket):");
        Console.WriteLine($"Tempo total: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Operações por segundo: {opsPerSecond:F0} ops/s");
        Console.WriteLine($"Throughput: {throughputMBps:F2} MB/s");

        cts.Cancel();
    }

    private static async Task RunNamedPipeBenchmark()
    {
        var server = new NamedPipeUmbralSocketServer("UmbralBenchmark");
        server.RegisterHandler(0xFF, payload => ValueTask.FromResult(payload.ToArray())); // Echo simples

        var cts = new CancellationTokenSource();
        var serverTask = server.StartAsync(cts.Token);
        await Task.Delay(1000);        // Reutilizar conexão para melhor performance no benchmark
        await using var client = new NamedPipeUmbralSocketClient("UmbralBenchmark");
        var payload = new byte[1024]; // 1KB payload
        Random.Shared.NextBytes(payload);

        const int iterations = 1000;
        Console.WriteLine($"Executando {iterations} operações com payload de {payload.Length} bytes...");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            await client.SendAsync(0xFF, payload);
            if (i % 100 == 0)
                Console.Write(".");
        }
        
        stopwatch.Stop();
        
        var opsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
        var throughputMBps = (iterations * payload.Length) / (1024.0 * 1024.0) / stopwatch.Elapsed.TotalSeconds;
        
        Console.WriteLine($"\n\nResultados do Benchmark (Named Pipe):");
        Console.WriteLine($"Tempo total: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Operações por segundo: {opsPerSecond:F0} ops/s");
        Console.WriteLine($"Throughput: {throughputMBps:F2} MB/s");

        cts.Cancel();
    }

    private static async Task RunJsonDemo()
    {
        Console.WriteLine("=== JSON Demo ===");

        // Usar Named Pipe no Windows, Unix Socket nos outros
        var useNamedPipe = OperatingSystem.IsWindows();
        
        if (useNamedPipe)
        {
            var server = new NamedPipeUmbralSocketServer("UmbralJson");
            await RunJsonDemoWithNamedPipe(server);
        }
        else
        {
            var server = new UnixUmbralSocketServer("/tmp/umbral_json.sock");
            await RunJsonDemoWithUnixSocket(server);
        }
    }

    private static async Task RunJsonDemoWithUnixSocket(UnixUmbralSocketServer server)
    {
        server.RegisterHandler(0x20, HandleUserRequest);
        server.RegisterHandler(0x21, HandleProductRequest);

        var cts = new CancellationTokenSource();
        var serverTask = server.StartAsync(cts.Token);
        await Task.Delay(1000);        // Reutilizar conexão para melhor performance
        await using var client = new UnixUmbralSocketClient("/tmp/umbral_json.sock");
        
        await RunJsonTests(async (opcode, data) =>
        {
            var response = await client.SendAsync(opcode, Encoding.UTF8.GetBytes(data));
            return Encoding.UTF8.GetString(response);
        });

        cts.Cancel();
        Console.WriteLine("\nDemo JSON finalizado!");
    }

    private static async Task RunJsonDemoWithNamedPipe(NamedPipeUmbralSocketServer server)
    {
        server.RegisterHandler(0x20, HandleUserRequest);
        server.RegisterHandler(0x21, HandleProductRequest);

        var cts = new CancellationTokenSource();
        var serverTask = server.StartAsync(cts.Token);
        await Task.Delay(1000);        // Reutilizar conexão para melhor performance
        await using var client = new NamedPipeUmbralSocketClient("UmbralJson");
        
        await RunJsonTests(async (opcode, data) =>
        {
            var response = await client.SendAsync(opcode, Encoding.UTF8.GetBytes(data));
            return Encoding.UTF8.GetString(response);
        });

        cts.Cancel();
        Console.WriteLine("\nDemo JSON finalizado!");
    }

    private static async Task RunJsonTests(Func<byte, string, Task<string>> sendRequest)
    {
        Console.WriteLine("\n--- Teste: Serviço JSON ---");
        
        // Teste 1: Buscar usuário
        var userRequest = new UserRequest(123, "get");
        var userRequestJson = JsonSerializer.Serialize(userRequest, SampleJsonContext.Default.UserRequest);
        var userResponse = await sendRequest(0x20, userRequestJson);
        Console.WriteLine($"[CLIENT] User Response: {userResponse}");

        // Teste 2: Buscar produto
        var productRequest = new ProductRequest("ABC123", "get");
        var productRequestJson = JsonSerializer.Serialize(productRequest, SampleJsonContext.Default.ProductRequest);
        var productResponse = await sendRequest(0x21, productRequestJson);
        Console.WriteLine($"[CLIENT] Product Response: {productResponse}");
    }

    private static ValueTask<byte[]> HandleUserRequest(ReadOnlySequence<byte> payload)
    {
        var json = Encoding.UTF8.GetString(UmbralSocket.Net.SequenceExtensions.ToArray(payload));
        Console.WriteLine($"[SERVER] User Request: {json}");
        
        var response = new UserResponse(123, "João Silva", "joao@example.com", "active");
        var responseJson = JsonSerializer.Serialize(response, SampleJsonContext.Default.UserResponse);
        
        return ValueTask.FromResult(Encoding.UTF8.GetBytes(responseJson));
    }

    private static ValueTask<byte[]> HandleProductRequest(ReadOnlySequence<byte> payload)
    {
        var json = Encoding.UTF8.GetString(UmbralSocket.Net.SequenceExtensions.ToArray(payload));
        Console.WriteLine($"[SERVER] Product Request: {json}");
        
        var response = new ProductResponse("ABC123", "Produto de Exemplo", 99.99, 42);
        var responseJson = JsonSerializer.Serialize(response, SampleJsonContext.Default.ProductResponse);
        
        return ValueTask.FromResult(Encoding.UTF8.GetBytes(responseJson));
    }

    private static void ShowUsage()
    {        Console.WriteLine("Uso: dotnet run [opção]");
        Console.WriteLine();
        Console.WriteLine("Opções:");
        Console.WriteLine("  unix        - Executar demo Unix Socket");
        Console.WriteLine("  namedpipe   - Executar demo Named Pipe");
        Console.WriteLine("  benchmark   - Executar benchmark de performance");
        Console.WriteLine("  json        - Executar demo com JSON");
        Console.WriteLine("  (sem args)  - Modo interativo");
    }
}
