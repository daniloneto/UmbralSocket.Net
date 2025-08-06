# UmbralSocket.Net - Projetos de Exemplo

Este diretório contém projetos de exemplo que demonstram como usar a biblioteca UmbralSocket.Net em diferentes cenários.

## 📁 Estrutura

```
samples/
└── UmbralSocket.Net.Sample/
    ├── Program.cs              # Aplicação de demonstração completa
    ├── README.md               # Documentação específica do sample
    ├── UmbralSocket.Net.Sample.csproj
    └── .vscode/
        └── launch.json         # Configurações de debug para VS Code
```

## 🚀 Como executar

### Opção 1: Menu interativo
```bash
cd samples/UmbralSocket.Net.Sample
dotnet run
```


### Opção 2: Comando direto
```bash
cd samples/UmbralSocket.Net.Sample

# Demos específicos
dotnet run unix        # Unix Socket (Linux/macOS/Windows 10+)
dotnet run namedpipe   # Named Pipe (Windows)
dotnet run benchmark   # Teste de performance
dotnet run json        # Serialização JSON com AOT
dotnet run server      # Ping-pong: sobe apenas o servidor
dotnet run client      # Ping-pong: sobe apenas o cliente
```
| **Ping-Pong** | Demonstra uso real em Docker Compose |
### ✅ Ping-pong client/server
- Demonstra comunicação bidirecional e concatenação dinâmica de mensagens
- Pronto para uso em Docker Compose (veja README principal)

### Opção 3: Usando VS Code Tasks
Se estiver usando VS Code:
1. `Ctrl+Shift+P` → "Tasks: Run Task"
2. Selecione "Run Sample"

## 📋 Demos incluídos

| Demo | Descrição | Funcionalidades |
|------|-----------|----------------|
| **Unix Socket** | Comunicação via Unix Domain Sockets | Echo, transformação de texto, multiplexação |
| **Named Pipe** | Comunicação via Named Pipes (Windows) | Calculadora, operações matemáticas |
| **Benchmark** | Teste de performance e throughput | Medição de ops/s e MB/s |
| **JSON** | Serialização com AOT compatibility | Source generation, objetos complexos |

## 🎯 Cenários demonstrados

### ✅ Comunicação básica
- Registro de handlers por opcode
- Envio e recebimento de mensagens
- Tratamento de erros

### ✅ Cross-platform
- Unix Sockets (Linux/macOS/Windows 10+)
- Named Pipes (Windows)
- Detecção automática de plataforma

### ✅ Performance
- Benchmark com 1000 operações
- Payload de 1KB
- Medição de throughput

### ✅ Serialização JSON
- Source generation para AOT
- Objetos tipados
- Compatibilidade com trimming

### ✅ Arquitetura cliente-servidor
- Servidor assíncrono
- Múltiplos handlers
- Cancelamento graceful

## 📊 Resultados típicos de benchmark

**Windows (Named Pipe):**
- ~6,500 ops/s
- ~6.4 MB/s throughput
- Latência sub-milissegundo

**Linux (Unix Socket):**
- ~8,000+ ops/s
- ~8+ MB/s throughput
- Latência ultra-baixa

## 🔧 Desenvolvimento

Para adicionar novos demos:

1. Adicione um novo caso no `switch` do `Main()`
2. Implemente o método correspondente
3. Registre handlers usando `RegisterHandler(opcode, handler)`
4. Use `ValueTask.FromResult()` para handlers síncronos
5. Implemente AOT-compatible JSON usando source generation

## 📝 Exemplo rápido

```csharp
// Servidor
var server = new UnixUmbralSocketServer();
server.RegisterHandler(0x01, payload =>
{
    var text = Encoding.UTF8.GetString(payload.ToArray());
    return ValueTask.FromResult(Encoding.UTF8.GetBytes($"Echo: {text}"));
});

// Cliente
var client = new UnixUmbralSocketClient();
var response = await client.SendAsync(0x01, Encoding.UTF8.GetBytes("Hello"));
Console.WriteLine(Encoding.UTF8.GetString(response)); // Echo: Hello
```

## 🛠 Debugging

Use as configurações de launch do VS Code para debug:
- `Run Sample (Interactive)` - Menu interativo
- `Run Sample (Unix Socket)` - Demo Unix Socket
- `Run Sample (Named Pipe)` - Demo Named Pipe
- `Run Sample (Benchmark)` - Benchmark
- `Run Sample (JSON)` - Demo JSON

---

Para mais informações, consulte a [documentação principal](../README.md) do projeto.
