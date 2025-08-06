# UmbralSocket.Net

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
dotnet run namedpipe   # Named Pipe demo (Windows)
dotnet run benchmark   # Performance benchmark  
dotnet run json        # JSON serialization demo
```

📋 **Ver detalhes completos:** [SAMPLES.md](SAMPLES.md)

## Licença

Este projeto está licenciado sob a [Licença MIT](LICENSE).

## CI/CD e Publicação

Este projeto usa GitHub Actions para automação de CI/CD com as seguintes funcionalidades:

- **Build e Teste Automático**: Executado em todos os PRs e pushes
- **Publicação no NuGet**: Automática quando uma tag de versão é criada
- **Publicação no GitHub Packages**: Backup da publicação

### Configuração de Secrets

Para que a publicação funcione, configure os seguintes secrets no GitHub:

1. `NUGET_API_KEY`: Sua chave de API do NuGet.org
2. `GITHUB_TOKEN`: Automaticamente fornecido pelo GitHub

### Como fazer um release

1. Atualize a versão no arquivo `.csproj`
2. Crie uma tag: `git tag v1.0.0`
3. Faça push da tag: `git push origin v1.0.0`
4. O GitHub Actions automaticamente publicará no NuGet

## Atribuição

Este projeto foi inspirado pelo [umbral-socket](https://github.com/alan-venv/umbral-socket) de alan-venv, que está licenciado sob as licenças Apache-2.0 e MIT. Embora esta seja uma implementação independente para .NET, reconhecemos a inspiração e o design conceitual do projeto original em Rust.
