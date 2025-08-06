# Changelog

# [1.2.0] - 2025-08-06
### Added
- Exemplo ping-pong client/server para Docker Compose, demonstrando comunicação bidirecional e concatenação dinâmica de mensagens.
- Dockerfile multi-stage: build e publish NativeAOT dentro do container, com instalação automática do clang.
- docker-compose.yml para testes automatizados de client/server em containers separados.
- Suporte total a .NET 9 NativeAOT no build Docker.

### Changed
- Documentação dos exemplos revisada para destacar o uso em ambientes Docker/Linux.
- Exemplo de ping-pong agora mostra concatenação alternada de "-ping" e "-pong".

## [1.1.0] - 2025-08-06
### Added
- Adicionado arquivo `SequenceExtensions.cs` com método `ToArray` para `ReadOnlySequence<byte>`.
- Todos os exemplos e samples agora usam explicitamente `UmbralSocket.Net.SequenceExtensions.ToArray(payload)`.
- Documentação dos exemplos atualizada para refletir a API real.
- Exemplo de uso JSON e servidor básico corrigidos para uso seguro e compatível com AOT.
- Adicionado este arquivo de changelog.

### Changed
- Atualização do README principal, README do sample e SAMPLES.md para exemplos mais claros e compatíveis.
- Pequenas melhorias de documentação e comentários.

### Fixed
- Correção de possíveis confusões de API para novos usuários.

## [1.0.0] - 2025-07-XX
- Primeira versão pública do UmbralSocket.Net.
