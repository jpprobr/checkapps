# AGENTS.md — CheckApps

Instruções para agentes de IA (Claude Code, GitHub Copilot, Devin, etc.) trabalhando neste repositório. Leia este arquivo antes de qualquer coisa; ele aponta para o resto.

## O que é

CheckApps mostra a composição de apps de uma squad e acompanha a saúde e a evolução histórica da qualidade dessas aplicações (dados de Sonar — mockados neste estágio), com cadastro manual de squads e apps. Visão completa em `.agents/product/vision.md`; estágio atual em `.agents/product/roadmap.md`.

Stack: .NET 10 (ASP.NET Core Web API, monólito modular) + Angular 20 (standalone) + SQLite (Dapper, SQL explícito; ver ADR-009). Detalhes de execução no `README.md`.

## Como rodar e testar

```bash
dotnet run --project tools/CheckApps.Seed             # cria o schema e popula dados de demo
dotnet run --project tools/CheckApps.GitHubScan -- --sigla ab1,cd2 --source fake   # caixa de entrada de demonstração
dotnet run --project tools/CheckApps.Analyze -- --sigla ab1,cd2 --source fake    # dependências e integrações dos repositórios já catalogados (--force reanalisa o mesmo commit)
dotnet run --project src/CheckApps.Api --urls http://localhost:5232
cd frontend && npm install && npm start               # Angular em :4200

dotnet test                                           # 529 testes; nenhum exige Docker, serviço externo ou rede
```

Persistência é SQLite em arquivo (`%LOCALAPPDATA%\CheckApps\checkapps.db`). Não há banco para subir.

A skill `run-local` (`.agents/skills/run-local/`) encapsula essa sequência com as checagens necessárias.

## Mapa: onde ler o quê

| Antes de… | Leia |
|---|---|
| Começar qualquer sessão | `.agents/project/STATE.md` — estado atual, pendências, próximos passos |
| Propor ou escopar algo novo | `.agents/product/vision.md`, `roadmap.md`, `backlog.md` |
| Usar ou introduzir um termo do domínio (squad, solução, checagem…) | `.agents/product/glossary.md` |
| Tocar código em `src/**` | `.agents/rules/backend.md`, depois `.agents/architecture/project-structure.md` |
| Tocar código em `frontend/**` | `.agents/rules/frontend.md` |
| Tocar persistência (`Infrastructure/Sqlite/**`) | `.agents/architecture/data-model/sqlite-schema.md` e o próprio `schema.sql` |
| Propor um access pattern, GSI ou desnormalização nova | `.agents/architecture/data-model/access-patterns.md` (registro vivo com o fit de cada padrão) e os gatilhos do ADR-002 |
| Mudar algo estrutural (stack, modelo, padrões) | `.agents/architecture/decisions/` — ADRs numerados; se a decisão já existe, respeite-a ou proponha um novo ADR |
| Entender por que uma decisão passada foi revista | `.agents/architecture/retrospectives/` — avaliações de experimentos concluídos (histórico, não vivo) |
| Mexer nos vínculos host→app (decisão humana, `{env}` ↔ `dev`/`hom`/`prod`, `host_links`) | `specs/017-vinculo-host-app/`, `specs/018-tela-vinculos-hosts/` (tela), `specs/019-lote-externo-destino/` (lote, host externo, destino no app), `specs/020-mapa-aplicacoes/` (mapa de aplicações), ADR-011 (adendo da spec 017), `docs/leitor-github.md` seção 6c |
| Mexer na análise de repositórios (scanners, dependências, versão do .NET, integrações declaradas) | `specs/013-dependencias-dotnet/`, `specs/014-integracoes-declaradas/`, ADR-011 (com o adendo da spec 014), `.agents/rules/backend.md` (seção Análise de repositórios), `docs/leitor-github.md` (seções 6b e 6c) e as fixtures em `tests/CheckApps.UnitTests/Fixtures/Dotnet/` (um repositório fictício por pasta, com o resultado esperado em `EXPECTED.md`) |
| Mexer na navegação (menu lateral, lista geral de Apps, seções recolhíveis do app, nome "Pacotes") | `specs/016-navegacao-apps-e-secoes/`, `.agents/rules/frontend.md` |
| Mexer no leitor de GitHub (scan, caixa de entrada, triagem, scan pela tela) | `specs/005-leitor-github-fase-1/`, `specs/006-scan-pela-tela/`, `specs/007-menu-repositorios/`, `specs/009-crud-repository/`, `specs/010-catalogo-na-tela-repositorios/`, `specs/012-repository-url-ativo-siglas/`, ADR-010 e `docs/leitor-github.md` — **token e nome da organização nunca entram no Git** |
| Implementar uma feature | A spec correspondente em `specs/NNN-*/spec.md` e seu `checklist.md` |
| Criar uma feature nova | Copie `specs/_template/` para `specs/NNN-nome/` e preencha antes de codar |
| Subir o ambiente local | Skill `run-local` em `.agents/skills/run-local/SKILL.md` |
| Dar uma mudança por pronta (build, testes, curl, navegador em banco isolado) | Skill `verify-change` em `.agents/skills/verify-change/SKILL.md` |
| Abrir uma spec nova ou encerrar spec/sessão (Status, checklist, STATE, backlog) | Skill `close-spec` em `.agents/skills/close-spec/SKILL.md` |
| Avaliar se algo recorrente deve virar skill, ou criar uma skill nova | Skill `skill-discovery` em `.agents/skills/skill-discovery/SKILL.md` |
| Encerrar a sessão | Reescreva `.agents/project/STATE.md` (não anexe — substitua) |

`.agents/skills/` segue o padrão aberto **Agent Skills** (`SKILL.md` com frontmatter de seis campos). Copilot e Devin leem essa pasta diretamente; o Claude Code lê os wrappers em `.claude/skills/` que apontam para ela.

## Convenção de trabalho

1. Analisar antes de implementar: para qualquer mudança não trivial, produzir um plano (ou uma spec) e obter aprovação antes de escrever código.
2. Implementar em incrementos pequenos e verificáveis, cada um com sua própria verificação (build, testes, `curl`, navegação no browser).
3. Registrar decisões estruturais como ADR; registrar regras "conquistadas" (aprendidas com bugs reais) em `.agents/rules/`.
4. Commits pequenos, um por incremento. Só commitar quando pedido.
5. Ao final da sessão, atualizar `STATE.md`.

## Gotchas críticos (leia mesmo com pressa)

- **O banco é um arquivo e os dados persistem** entre reinícios. Para resetar: apague `%LOCALAPPDATA%\CheckApps\checkapps.db*` e rode o seed.
- **Caminho relativo do banco resolve contra `%LOCALAPPDATA%\CheckApps`, não contra o diretório atual.** É deliberado: o ASP.NET Core troca o diretório de trabalho no startup e o console do seed não — depender do CWD faria API e seed abrirem bancos diferentes.
- **O estado de qualidade atual do App é derivado por consulta**, não armazenado. `SaveAsync` grava só os dados próprios do App; não existe mais o risco de apagar `currentQualityLevel` ao salvar.
- **App com histórico de qualidade não pode ser removido** (409). É por design: inative em vez de remover. Squad com apps vinculados idem.
- **Cada classe de teste de integração usa seu próprio banco temporário** (`SqliteFixture` via `IClassFixture`). Não há estado compartilhado nem necessidade de collection fixture.
- **Datas no Angular sempre com `'UTC'`** no `DatePipe` (`| date:'MMM/yyyy':'UTC'`). Sem isso, `2026-08-01T00:00:00Z` vira julho em UTC-3.
- **Angular CLI 22 exige Node ≥ 22.22.** Este projeto está em Angular 20 por causa do Node 22.17 da máquina. Não faça `ng update` para 22 sem antes atualizar o Node.
- **`.claude/` está no `.gitignore`, exceto `.claude/skills/`.** Configurações locais do Claude Code não vão para o repo; os wrappers de skill vão.
