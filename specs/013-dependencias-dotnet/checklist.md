# Checklist — Spec 013: Dependências de biblioteca e versão do .NET (primeiro scanner)

Status: **implementada** em 2026-10-24 e **confirmada pelo usuário na tela em 2026-10-26**, com 4 repositórios de demonstração no GitHub real (spec e ADR-011 aceitas pelo usuário; plano aprovado em plan mode).

## Qualidade da spec (antes de codar)

- [x] User stories com cenários Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante (respondidas em 2026-10-24: ler formato legado; fixtures inventadas bastam por ora; prefixo = nome da empresa no início do namespace, só em configuração local)
- [x] Fixtures de partida criadas em `tests/CheckApps.UnitTests/Fixtures/Dotnet/` (6 cenários com `EXPECTED.md`)
- [x] Cada FR é testável
- [x] Success Criteria são mensuráveis e não citam tecnologia
- [x] Decisões estruturais têm ADR em `.agents/architecture/decisions/`: **ADR-011** aceita em 2026-10-24
- [x] Dados novos são AI-friendly: identidade, relações, proveniência (`origin`, confiança, scanner) e tempo (snapshot por análise) definidos
- [x] Plano aprovado pelo usuário (plan mode)

## Implementação

- [x] 0. Spec, checklist, ADR-011 (aceita) e glossário (Descoberta, Análise, Scanner)
- [x] 1. Domínio e contratos: `IRepositoryScanner`, registro, `IRepositoryContentSource`, tipos de fato e confiança (com testes puros)
- [x] 2. Scanner .NET: `.csproj` (SDK e legado, com namespace MSBuild), `packages.config`, `Directory.Packages.props`, `Directory.Build.props`, resolução de versão, casos de borda (testes com as fixtures, inclusive `05-legado-nao-sdk`)
- [x] 3. Scanner mínimo de `package.json` **só nos testes**, para provar o contrato (SC-005)
- [x] 4. Persistência: tabelas e índices no `schema.sql`, repositório SQL portável, teste de banco antigo e de integração
- [x] 5. `AnalysisService`: orquestração, idempotência por commit, situação por repositório, falha isolada
- [x] 6. Fontes de conteúdo: `fake` (casos difíceis) e `github` (árvore + leitura pontual, limites, erros saneados), testes com HTTP falso
- [x] 7. Ferramenta `tools/CheckApps.Analyze` (`--sigla`, `--source`, `--dry-run`) e composição compartilhada
- [x] 7b. Marcação de pacote interno (`Analysis:InternalPackagePrefixes`, calculada na leitura, desligada sem configuração; prefixo real só em configuração local)
- [x] 8. API de leitura: `GET /api/apps/{id}/dependencies`, `GET /api/dependencies/packages`, `GET /api/dependencies/frameworks`
- [x] 9. Frontend: seção no detalhe do app (US2), tela **Dependências** (US3) e panorama (US4, P3), entrada no menu
- [x] 10. Regras "conquistadas" em `.agents/rules/`; `sqlite-schema.md` e `access-patterns.md`; `docs/` do leitor; `README.md`

## Verificação (skill `verify-change`)

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde, sem rede (282 unitários + 64 de integração)
- [x] `npx ng build` sem erros
- [x] Banco isolado (`e2e.db`): `GitHubScan --source fake` (ab1, cd2) → 8 apps pela triagem → `Analyze --sigla ab1,cd2 --source fake --dry-run` (nada gravado) → real (8 analisados: 3 ok, 2 parciais, 3 sem scanner) → segunda execução: 8 "sem alteração"
- [x] `curl` nos três endpoints: 200; 404 (app inexistente); 400 (nome vazio); busca `serilog` com caixa diferente (2.10.0 e 3.1.1); `internalOnly` sem nome (2 pacotes internos com `Analysis__InternalPackagePrefixes__0`); panorama com "sem informação" por último
- [x] Navegador (Playwright, roteiro em banco isolado): seção no app (central, propriedade não resolvida, arquivo inválido, legado, sem scanner, nunca analisado), busca de pacote e filtro de squad, "apenas internos", panorama e detalhe da célula, menu com `aria-current` e recolhido, celular sem rolagem horizontal da tela nova e a seção nova sem alargar a página; zero erros de console
- [x] Análise real com `--source github` (2026-10-26): 4 repositórios de demonstração na organização de teste (gerenciamento central, multi-alvo, legado, sem .NET); `--dry-run` sem gravar, gravação e segunda execução com 4 "sem alteração"; fatos conferidos contra os arquivos
- [x] Confirmação do usuário na tela (2026-10-26), incluindo a etiqueta "interno" com `Analysis:InternalPackagePrefixes` nos user-secrets da API

## Encerramento (skill `close-spec`)

- [x] `Status` desta spec atualizado para `Implementada`
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado (fase 2 do leitor feita; integrações e mapa permanecem)
- [x] Mapa do `AGENTS.md` e contagem de testes
