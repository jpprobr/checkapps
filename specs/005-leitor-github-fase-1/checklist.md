# Checklist — Spec 005: Leitor de GitHub, fase 1

Status: **implementada** em 2026-10-12, com a primeira leitura real do GitHub validada no mesmo dia.

## Qualidade da spec (antes de codar)

- [x] User stories com cenários Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante — as decisões viraram Assumptions
- [x] Cada FR é testável
- [x] Success Criteria mensuráveis e sem tecnologia
- [x] Decisão estrutural (integração externa, credencial, Search API) em ADR-010
- [x] Dados novos são AI-friendly: identidade estável, relações explícitas, proveniência, tempo e confiança da sugestão

## Implementação

- [x] 0. Spec, checklist e ADR-010
- [x] 1. Domain: `DiscoveredRepository`, `ScanScope`, `RepositoryNameConvention`, campos novos de `Repository` — testes unitários
- [x] 2. Persistência: schema, passo aditivo do bootstrapper, `IDiscoveredRepositoryRepository`, colunas novas em `AppRepository` — testes de integração, incluindo bootstrap sobre banco antigo
- [x] 3. Application: porta, `ScanService`, `TriageService`, `FakeRepositorySource` — testes de serviço
- [x] 4. GitHub real: `GitHubRepositorySource` — testes com `HttpMessageHandler` falso, token fora das mensagens
- [x] 5. Ferramenta `tools/CheckApps.GitHubScan` — fonte fake com e sem `--dry-run`, erros de uso, limite de resultados e ausência de token verificados
- [x] 5b. **Primeira leitura real** contra a organização de teste, em `--dry-run`: repositório encontrado, sigla-app lida do YAML, confiança alta
- [x] 6. API: `DiscoveredRepositoriesController` — `curl` incluindo 4xx
- [x] 7. Frontend: tela `/discovered`, menu, detalhe do app — Playwright
- [x] 8. Documentação e guia da organização de teste
- [x] Regras conquistadas registradas em `.agents/rules/`
- [x] Modelo de dados em `.agents/architecture/data-model/` e access patterns atualizados

## Verificação

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde (unitários + integração), **sem rede**
- [x] Endpoints exercitados via `curl`, incluindo 4xx
- [x] Fluxo completo no browser sem erros de console; roteiros das specs 001–003 repetidos
- [x] Banco local existente recebe as colunas e a tabela nova sem perda de dados
- [x] Varredura de segredo antes de qualquer commit (token e nome da organização fora dos arquivos versionados)
- [x] `README.md` e `AGENTS.md` atualizados (como rodar o scan)

## Encerramento

- [x] `Status` desta spec atualizado para `Implementada`
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado
