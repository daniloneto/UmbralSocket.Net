## Zero-copy & Buffer Pooling

UmbralSocket.Net usa zero-copy e buffer pooling para máxima performance:

- **APIs zero-copy**: `SendAsync(opcode, ReadOnlySequence<byte>, ct)` para envio sem cópias
- **Buffer pooling**: `ArrayPool<byte>.Shared` para buffers temporários, `IBufferOwner` para abstração
- **Pipelines**: `System.IO.Pipelines` (`PipeReader`/`PipeWriter`) para I/O end-to-end
- **Message handlers**: Receba payloads como `ReadOnlySequence<byte>` diretamente

```csharp
// Envio zero-copy com ReadOnlySequence<byte>
var largePayload = new byte[1024 * 1024]; // 1MB
var sequence = new ReadOnlySequence<byte>(largePayload);
await client.SendAsync(0x01, sequence, cancellationToken);

// Handler que recebe ReadOnlySequence<byte> (zero-copy)
server.RegisterHandler(0x01, async (opcode, payload, ct) =>
{
    // Trabalhe diretamente com ReadOnlySequence<byte> - sem cópias!
    Console.WriteLine($"Received {payload.Length} bytes");
    // ... process payload without copying ...
});
```

Veja exemplos completos em `samples/PingPongZeroCopy`.

## Observability

Monitoramento completo via EventCounters, DiagnosticSource e ILogger:

### EventCounters Disponíveis
- `connections-active`: Conexões ativas
- `bytes-sent` / `bytes-received`: Throughput de dados  
- `send-queue-length` / `receive-queue-length`: Tamanho das filas
- `latency-mean-ms`: Latência média end-to-end
- `latency-p50-ms`, `latency-p95-ms`, `latency-p99-ms`: Percentis de latência

### Monitoramento com dotnet-counters

```bash
# Monitorar todas as métricas do UmbralSocket
dotnet-counters monitor --process-id <pid> UmbralSocket

# Exemplo de saída:
[UmbralSocket]
    connections-active                                   3
    bytes-sent (B / 1 sec)                         1,247,832
    bytes-received (B / 1 sec)                     1,089,234  
    send-queue-length                                    0
    receive-queue-length                                 2
    latency-mean-ms                                   1.23
    latency-p50-ms                                    0.95
    latency-p95-ms                                    3.45
    latency-p99-ms                                    8.90
```

### DiagnosticSource & OpenTelemetry

```csharp
// Atividades emitidas automaticamente
var source = new ActivitySource("UmbralSocket");

// Use com OpenTelemetry para rastrear operações
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddSource("UmbralSocket")
        .AddJaegerExporter());

// Tags incluídas automaticamente:
// - opcode, payload_length, peer, transport (unix|namedpipe)
// - success/failure, response.length
```

### ILogger Integration

```csharp
// Habilite logging via IpcOptions
var options = new IpcOptions 
{ 
    Logger = serviceProvider.GetService<ILogger<MyService>>(),
    EnableCounters = true,
    EnableActivitySource = true
};

var server = new UnixUmbralSocketServer("/tmp/app.sock", options);
```

## Security & Permissions

### Unix Domain Sockets (Linux/macOS)

Gerenciamento seguro de permissões de socket:

```csharp
// Cria diretório com permissões 0700 e socket com 0660
var socketPath = UnixSocketPathHelper.CreateSocketDirectory("/var/run", "app.sock");
var server = new UnixUmbralSocketServer(socketPath);
```

**Hardening Checklist Unix:**
- ✅ Diretório do socket: 0700 (apenas owner)
- ✅ Arquivo do socket: 0660 (owner + group)  
- ✅ Use `systemd` `RuntimeDirectory` em produção
- ✅ Configure `chown`/`chgrp` para grupo adequado

```bash
# Exemplo systemd unit
[Unit]
Description=My UmbralSocket App

[Service]
ExecStart=/app/myapp
RuntimeDirectory=myapp
RuntimeDirectoryMode=0700
User=myapp
Group=myapp-group
```

### Named Pipes (Windows)

ACLs restritivas para máxima segurança:

```csharp
// Cria PipeSecurity com ACL restrita
var pipeSecurity = NamedPipeSecurityHelper.CreateRestrictedPipeSecurity("MyServiceAccount");

// Em produção, você passaria isso para o constructor do servidor
// var server = new NamedPipeUmbralSocketServer("MyApp", pipeSecurity);
```

**Hardening Checklist Windows:**
- ✅ Apenas SYSTEM e Administrators por padrão
- ✅ Adicione contas específicas conforme necessário  
- ✅ `Everyone` explicitamente NEGADO
- ✅ Execute serviço com conta dedicada

**Quando usar cada transporte:**
- **Unix Domain Sockets**: Linux/macOS, máxima performance, controle granular de permissões
- **Named Pipes**: Windows, integração nativa com security model do Windows
# <img src="https://raw.githubusercontent.com/daniloneto/UmbralSocket.Net/refs/heads/main/logo.png" />
[![CI](https://github.com/daniloneto/UmbralSocket.Net/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/daniloneto/UmbralSocket.Net/actions)
[![NuGet](https://img.shields.io/nuget/v/UmbralSocket.Net.svg)](https://www.nuget.org/packages/UmbralSocket.Net)

Uma biblioteca binária de comunicação ultraleve e de altíssimo desempenho via Unix Sockets ou Named Pipes para backends .NET 9 AOT. Inspirada em [umbral-socket](https://github.com/alan-venv/umbral-socket) do Rust.

## Exemplo rápido

```csharp
using System.Buffers;
using System.Text;
using UmbralSocket.Net.Unix;

var server = new UnixUmbralSocketServer();
server.RegisterHandler(0x01, payload =>
{
    // Converter ReadOnlySequence<byte> para string
    var text = Encoding.UTF8.GetString(UmbralSocket.Net.SequenceExtensions.ToArray(payload));
    return ValueTask.FromResult(Encoding.UTF8.GetBytes($"SAVE:{text}"));
});

var cts = new CancellationTokenSource();
_ = server.StartAsync(cts.Token);

var client = new UnixUmbralSocketClient();
var response = await client.SendAsync(0x01, Encoding.UTF8.GetBytes("hello"));
Console.WriteLine(Encoding.UTF8.GetString(response)); // SAVE:hello
cts.Cancel();
```

## 🚀 Exemplos completos

Confira os **projetos de exemplo completos** em [`samples/`](samples/) que demonstram:

- ✅ **Unix Sockets** e **Named Pipes**
- ✅ **Múltiplos handlers** por opcode  
- ✅ **Serialização JSON** com AOT
- ✅ **Benchmarks de performance**
- ✅ **Diferentes cenários de uso**
- ✅ **Cross-platform compatibility**

### 🎮 Executar os exemplos

```bash
cd samples/UmbralSocket.Net.Sample

# Modo interativo (menu)
dotnet run


# Demos específicos
dotnet run unix        # Unix Socket demo (Linux/macOS/Windows 10+)
dotnet run namedpipe   # Named Pipe demo (Windows)
dotnet run benchmark   # Performance benchmark  
dotnet run json        # JSON serialization demo
dotnet run server      # Ping-pong: sobe apenas o servidor
dotnet run client      # Ping-pong: sobe apenas o cliente
```
## 🏓 Exemplo ping-pong (client/server)

O exemplo ping-pong demonstra comunicação bidirecional real entre dois processos (ou containers):

### 🐳 Rodando o exemplo ping-pong com Docker Compose (WSL/Linux)

Você pode testar o ping-pong client/server facilmente usando Docker Compose no WSL ou Linux:

```bash
# Na raiz do projeto
docker compose up --build
```

Isso irá:
- Fazer o build automático do projeto (NativeAOT) dentro do container
- Subir dois serviços: um como server e outro como client
- Exibir no terminal o ping-pong acontecendo entre os dois containers

Para reiniciar do zero (limpar imagens/volumes):
```bash
docker compose down -v
docker system prune -af --volumes
```

> O exemplo está pronto para ambientes Linux/WSL2, sem necessidade de dependências locais além do Docker.

📋 **Ver detalhes completos:** [SAMPLES.md](SAMPLES.md)

## Licença

Este projeto está licenciado sob a [Licença MIT](LICENSE).

## Atribuição

Este projeto foi inspirado pelo [umbral-socket](https://github.com/alan-venv/umbral-socket) de alan-venv, que está licenciado sob as licenças Apache-2.0 e MIT. Embora esta seja uma implementação independente para .NET, reconhecemos a inspiração e o design conceitual do projeto original em Rust.
