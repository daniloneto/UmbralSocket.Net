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
