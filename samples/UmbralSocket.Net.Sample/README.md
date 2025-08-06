# UmbralSocket.Net Sample

Este projeto demonstra o uso da biblioteca UmbralSocket.Net com diferentes cenários práticos.

## Como executar

```bash
# Modo interativo (menu)
dotnet run

# Executar demo específico
dotnet run unix        # Demo Unix Socket (Linux/macOS)
dotnet run namedpipe   # Demo Named Pipe (Windows)
dotnet run benchmark   # Teste de performance
dotnet run json        # Demo com serialização JSON
```

## Demos incluídos

### 1. Unix Socket Demo
Demonstra comunicação via Unix Domain Sockets com:
- Handler de echo
- Handler de conversão para maiúsculas
- Múltiplas operações sequenciais

### 2. Named Pipe Demo (Windows)
Demonstra comunicação via Named Pipes com:
- Calculadora simples
- Operações matemáticas básicas (+, -, *, /)
- Tratamento de erros

### 3. Benchmark
Teste de performance que mede:
- Operações por segundo
- Throughput em MB/s
- Latência de comunicação
- Automatically escolhe Unix Socket ou Named Pipe baseado no OS

### 4. JSON Demo
Demonstra serialização/deserialização JSON com:
- Serviço de usuários
- Serviço de produtos
- Objetos complexos
- Integração com System.Text.Json

## Características demonstradas

- ✅ Comunicação bidirecional
- ✅ Múltiplos handlers por opcode
- ✅ Serialização JSON
- ✅ Tratamento de erros
- ✅ Performance benchmarking
- ✅ Cross-platform (Unix Socket + Named Pipe)
- ✅ Uso com .NET 9 AOT

## Exemplos de uso

### Servidor básico
```csharp

var server = new UnixUmbralSocketServer();
server.RegisterHandler(0x01, payload =>
{
    var text = Encoding.UTF8.GetString(UmbralSocket.Net.SequenceExtensions.ToArray(payload));
    return ValueTask.FromResult(Encoding.UTF8.GetBytes($"Processed: {text}"));
});

var cts = new CancellationTokenSource();
await server.StartAsync(cts.Token);
```

### Cliente básico
```csharp
var client = new UnixUmbralSocketClient();
var response = await client.SendAsync(0x01, Encoding.UTF8.GetBytes("Hello"));
Console.WriteLine(Encoding.UTF8.GetString(response));
```

### Com JSON
```csharp
// Servidor
server.RegisterHandler(0x20, payload =>
{
    var request = JsonSerializer.Deserialize<UserRequest>(UmbralSocket.Net.SequenceExtensions.ToArray(payload));
    var response = new UserResponse { Name = "João", Email = "joao@example.com" };
    return ValueTask.FromResult(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response)));
});

// Cliente
var request = new UserRequest { Id = 123 };
var requestJson = JsonSerializer.Serialize(request);
var responseBytes = await client.SendAsync(0x20, Encoding.UTF8.GetBytes(requestJson));
var response = JsonSerializer.Deserialize<UserResponse>(responseBytes);
```
