# STATE.md — estado atual do projeto

> Reescrito (não anexado) ao fim de cada sessão de trabalho. Se este arquivo estiver desatualizado em relação ao `git log`, o git está certo. Última atualização: **2026-10-26**, ao fechar a spec 013 (scanner .NET: dependências e versão do .NET), depois de revisar a regra do ativo único da 012 e criar as skills `skill-discovery`, `verify-change` e `close-spec`. **As specs 012 e 013 estão fechadas. A 013 foi confirmada pelo usuário na tela com 4 repositórios de demonstração no GitHub real, mas NÃO está commitada.**

## Onde estamos

Estágio do roadmap: **Inventário + Health + Evolution concluídos**, mais **Checagens** e o **leitor de GitHub, fase 1** (features operacionais fora da sequência de estágios). Fase de laboratório, rodando só em máquina local, uma pessoa + agentes.

## O que funciona

- **MVP core** (`specs/001-mvp-core/`): squads, apps, repositórios, projetos Sonar, histórico de `QualitySnapshot`, dashboard da squad, lista de apps, detalhe com gráfico de evolução e tabela de histórico.
- **Cadastro de Squads e Apps** (`specs/002-squad-app-management/`): criar/editar/inativar/remover pela UI e pela API, ids por slug, guardas de remoção (409), links corporativos por app.
- **Persistência em SQLite** (`specs/004-migracao-sqlite/`, ADR-009): Dapper com SQL explícito, `schema.sql` idempotente **mais um passo aditivo** no bootstrapper (`AdditiveColumns`) para colunas novas em tabelas existentes.
- **Checagens** (`specs/003-checagens/`) e **navegação global** (`/` → última squad visitada).
- **Menu lateral** (`specs/008-menu-lateral/`): `SidebarService` + `app.html`/`app.css`. Marca, seletor de squad no topo, Squads / Repositórios / Checagens com ícones SVG embutidos; recolhível (só ícones, `aria-label` + `title`, estado em `localStorage` `checkapps.sidebarCollapsed`; clicar no ícone da squad expande); gaveta em ≤ 768 px (fecha ao navegar, no fundo e com Esc). Ativo por fundo + barra + `aria-current`.
- **CRUD manual de Repository** (`specs/009-crud-repository/`): `POST/PUT/DELETE /api/apps/{appId}/repositories[/{id}]` (404 app/repo, 400 sem nome, 409 sigla-app duplicada; criação com `source = MANUAL`; edição preserva `source` e `externalId`); UI na **edição do app** (seção Repositórios: tabela com Editar/Remover + formulário). Lógica no `AppService`; persistência nova em `AppRepository` (`GetRepositoryAsync`, `DeleteRepositoryAsync`, `IsSiglaAppInUseAsync(siglaApp, exceptId)`). Sem mudança de schema.
- **Cadastrados na tela Repositórios** (`specs/010-catalogo-na-tela-repositorios/`): nos filtros Todos e No catálogo a tela também lista os repositórios do catálogo sem descoberto correspondente (manuais e seed) — `GET /api/repositories/catalog`, `AppRepository.ListCatalogOnlyRepositoriesAsync` — e toda linha "no catálogo" tem o atalho **Gerenciar** → edição do app. O filtro padrão da tela é **Todos**.
- **Squad na tela Repositórios** (`specs/011-squad-na-tela-repositorios/`): coluna e filtro **Squad**, só para exibição/consulta. A squad continua derivada (Squad → App → Repository, sem `squad_id` em `repositories`); `catalogApp` e `GET /api/repositories/catalog` trazem `squadId`/`squadName`; quem não virou app aparece como "sem squad". Guardar a squad de "de outra squad" ficou de fora (exigiria coluna nova na caixa de entrada).
- **Repository: URL, ativo único e siglas** (`specs/012-repository-url-ativo-siglas/`): "no catálogo" = mesmo `provider`+`external_id` **ou** mesma URL (`RepositoryUrl`, `CatalogMatchSql`; `IsInCatalogAsync(DiscoveredRepository)` e `GetCatalogAppsAsync(descobertos)` por `Id` do descoberto); **um ativo por papel dentro do app** — salvar, marcar como ativo (`POST /api/apps/{id}/repositories/{id}/activate`, devolve o repositório) ou vincular pela triagem com outro ativo no mesmo papel **devolve 409 nomeando o ativo atual** (`AppService.EnsureNoOtherActiveInRoleAsync`); nada é rebaixado nem promovido sozinho, inclusive ao remover o ativo (revisão de 2026-10-24, pedido do usuário após testar na tela); sigla-app gravada em MAIÚSCULAS e sigla em minúsculas, unicidade da sigla-app sem diferença de caixa. UI: situação e "Marcar como ativo" na edição do app (desabilitado, com explicação, enquanto outro ativo ocupa o papel; o 409 aparece como mensagem). Trocar o ativo são dois passos: marcar o antigo como `legacy` e depois ativar o novo. Limites: `.git` e `http`/`https` não tratados na URL; siglas-app antigas em minúscula não são reescritas.
- **Leitor de GitHub, fase 1** (`specs/005-leitor-github-fase-1/`, ADR-010, guia `docs/leitor-github.md`):
  - ferramenta `tools/CheckApps.GitHubScan` — scan **por sigla** (validada por formato, limite de resultados), `--dry-run`, `--source fake|github`; o escopo é validado antes de pedir token;
  - tela **Repositórios** no menu: todos os repositórios que passaram pelo scan, cada um com o estado dele — novo, no catálogo (link do app, sem ações), de outra squad, ignorado; filtro padrão "Todos" ("Novos" é a caixa de entrada). Ações: criar app (painel pré-preenchido), vincular a app existente (aviso se a sigla difere), de outra squad, ignorar, restaurar;
  - `Repository` tem `sigla`, `siglaApp` (único quando presente), `role` e `source`; aparecem no detalhe do app;
  - fonte `fake` com os casos difíceis; fonte real do GitHub (Search API + YAML da sigla-app) testada com HTTP falso **e lida uma vez de verdade**.
- **Scan pela tela** (`specs/006-scan-pela-tela/`): bloco **Buscar no GitHub** em Repositórios — siglas, *Pré-visualizar* e *Escanear*; mesmo `ScanService` do CLI. **Desligado por padrão** (`Scan:Enabled`); erros 400/409/502; um scan por vez.
- **Menu "Repositórios"** (`specs/007-menu-repositorios/`) e "já no catálogo" no resultado do scan.
- **Testes**: 282 unitários + 64 de integração, verdes. **Nenhum exige Docker, serviço externo ou rede.**
- **Ambiente local**: skill `run-local`; `README.md` com o passo a passo. Verificado nesta sessão: seed, API em `:5232`, Angular em `:4200`, `curl` nos três endpoints novos e um roteiro Playwright (menu desktop/recolhido/recarregar/celular + CRUD completo, sem erros de console além do 409 esperado).

- **Análise de repositórios: dependências e versão do .NET** (`specs/013-dependencias-dotnet/`, ADR-011, guia `docs/leitor-github.md` seção 6b): ferramenta `tools/CheckApps.Analyze` (`--sigla`, `--source github|fake`, `--dry-run`; mesmo `UserSecretsId` do `GitHubScan`) lê os repositórios **já catalogados** da sigla, escolhe os scanners pela lista de arquivos e grava uma **análise** por execução (`repository_analyses` + `repository_dependencies`: fatos neutros à tecnologia com origem `PARSER`, confiança alta/média/baixa em texto, scanner e arquivo). Idempotente por commit; falha de um repositório não para os outros e não esconde a última análise boa (a **corrente** = a mais recente que não falhou). O scanner .NET (`Infrastructure/Analysis/DotNet/DotNetScanner.cs`, só `System.Xml.Linq`) lê `*.csproj` (SDK e legado com `packages.config`), `Directory.Build.props` e `Directory.Packages.props`: gerenciamento central, `VersionOverride`, propriedades resolvidas ou cruas, flutuante/intervalo, `TargetFrameworks`, XML inválido vira aviso. Fontes de conteúdo: `GitHubRepositoryContentSource` (id do repositório → commit → árvore → arquivos pedidos; `GitHubApiClient` compartilhado com a descoberta) e `FakeRepositoryContentSource` (subconjunto em texto para a demonstração). API **só de leitura**: `GET /api/apps/{id}/dependencies`, `GET /api/dependencies/packages?name=&squadId=&internalOnly=`, `GET /api/dependencies/frameworks`. Tela: seção **Dependências** no app e item **Dependências** no menu (aba "Quem usa o pacote" com resumo de versões, filtro de squad, "apenas internos" e aviso de repositórios nunca analisados; aba ".NET por squad", framework × squad com detalhe da célula). "Pacote interno" = nome começa com `Analysis:InternalPackagePrefixes` (config local, **nunca em arquivo versionado**), calculado na leitura. Fixtures em `tests/CheckApps.UnitTests/Fixtures/Dotnet/` (6 cenários com `EXPECTED.md`, copiadas para a saída; as pastas `obj/` e `bin/` delas estão liberadas no `.gitignore`).
- **Skills do repositório** (`.agents/skills/`, wrappers em `.claude/skills/`): `run-local`, `skill-discovery` (descobre e cria skills), `verify-change` (build, testes, curl, navegador em banco isolado, confirmação) e `close-spec` (abrir/encerrar spec e sessão). As três últimas foram criadas em 2026-10-24 e **ainda não foram exercitadas de ponta a ponta**: usá-las na próxima spec e corrigir o que faltar.

## O que está mockado / provisório

- **Dados de Sonar** vêm de `MockSonarProvider` + `DemoDataFactory`. Sem integração real com SonarQube.
- **O GitHub real foi lido** (2026-10-12, `--dry-run`; e em 2026-10-26 pela descoberta e pela análise, com 4 repositórios de demonstração `jplay-dm1-*`). **Ainda não exercitados com o GitHub real**: arquivado, paginação, limite de resultados, limite de chamadas, acesso parcial e YAML inválido — só cobertos por JSON fixo nos testes.
- **O scan pela tela ainda não foi usado contra o GitHub real** (pendência do usuário): exige os segredos da **API**, separados dos da ferramenta de linha de comando.
- **Sem autenticação** (ADR-007): "minha squad" é a última visitada (navegador); autor de checagem é texto livre. **O endpoint de scan usa o token configurado e qualquer pessoa que abra o sistema pode dispará-lo** — por isso vem desligado; autenticação é pré-requisito de uso compartilhado.
- **Sem deploy, sem CI.** Litestream e ECS documentados como alvo (spec 004, ADR-009), não implementados.
- **O seed não cria checagens nem descobertos** — o scan `--source fake` cria os descobertos de demonstração.
- **Roteiro de regressão no novo shell (2026-10-18, banco isolado `e2e.db`)**: 001–011 repetidas no navegador — squads, apps, links, checagens, triagem, filtros, CRUD de repositório, menu recolhido e gaveta — sem regressão e sem erros de console. **Bug antigo achado e corrigido (spec 002, não era regressão)**: `POST`/`PUT` de link com app inexistente devolvia 500 (violação de FK); agora `AppService` checa o app (`EnsureAppExistsAsync`) e a API devolve 404. O scan pela tela **não** foi disparado (a API desta máquina tem `Scan:Enabled` ligado com fonte `github`).

## Decisões pendentes / perguntas abertas

- **Solução e Tipo de app**: definições no glossário aguardam revisão do usuário.
- **A convenção de nome dos repositórios é uniforme na organização?** Hoje é configuração (`Scan:NameTemplate`).
- **Formato real do YAML da sigla-app** e caminho do arquivo: hoje `config.yaml` com a chave `siglaApp` na raiz, configuráveis.
- **Exceção futura**: repositório compartilhado entre squads — não tratado.
- Repositório que **some do GitHub** continua na caixa (só `last_seen_at` envelhece) — comportamento não decidido.
- **CRUD de Repository**: sem pendências além do **CRUD de SonarProject**, que segue em backlog por decisão do usuário. Dados antigos com dois ativos no mesmo papel: nenhum dos dois pode ser salvo como ativo até um virar `legacy`.
- `.github/copilot-instructions.md` ainda não existe; confirmar na máquina corporativa se Devin/Copilot varrem `.agents/skills/` (ADR-008).

## Gotchas para a próxima sessão

Os críticos estão em `AGENTS.md` e nas regras. Os que mais mordem hoje:
- **Segredos**: `GitHub:Token`, `GitHub:Organization`, `Scan:NameTemplate` e `Scan:Enabled` ficam em `dotnet user-secrets` — **a ferramenta e a API têm `UserSecretsId` e armazenamentos separados** — ou em variáveis `GitHub__…`/`Scan__…`. Nunca em arquivo versionado nem em exemplo de documentação. `dotnet user-secrets list` imprime o token: filtre com `| grep "Scan:"`.
- **A API lê a configuração só na subida**; um processo antigo em `:5232` responde com a configuração velha e trava o build (`taskkill //F //IM CheckApps.Api.exe`).
- O caminho relativo do banco resolve contra `%LOCALAPPDATA%\CheckApps`. Para testar sem mexer na massa real: `Sqlite__DatabasePath=<nome>.db` no seed, no scan **e** na API. Os testes manuais de 2026-10-24 rodaram em `e2e.db` (isolado), apagado ao fim; a massa real não foi tocada.
- Em Development a API valida a DI na subida (`DiscoveryCompositionTests`).
- `DatePipe` com `'UTC'` para períodos e datas de calendário; sem `'UTC'` para instantes reais.
- Playwright: escopar seletores de texto a `main`; a gaveta do celular fora da tela ainda é "visível" para `isVisible()` — meça `boundingBox().x`. O Playwright não está no repo: instale em pasta fora dele e use `executablePath` do Chromium já baixado em `%LOCALAPPDATA%\ms-playwright`.
- Status de checagem e de triagem são **códigos** (`em-andamento`, `other-squad`), não nomes de enum.
- **`heredoc` do shell quebra com apóstrofo/acento em português**: escreva arquivos e edições com as ferramentas de arquivo (Python via heredoc funcionou só sem apóstrofos no texto).

## Próximos passos (em ordem)

1. **Commitar a spec 013** quando o usuário pedir (nada dela está commitado; `rascunho.md` fica de fora). Os repositórios de demonstração ficam em `checkapps-demo/` fora deste repositório, com o passo a passo.
2. **Exercitar as skills novas** (`verify-change`, `close-spec`, `skill-discovery`): a `verify-change` foi seguida à mão nesta spec (banco isolado, `curl`, Playwright); registrar o que faltar nelas.
3. **Usar o scan pela tela com o GitHub real** (usuário): segredos da API (`docs/leitor-github.md`, seção 5), reiniciar a API e escanear em **Repositórios → Buscar no GitHub**; depois criar os repositórios de teste que faltam (arquivado, infra, outra sigla, fora do acesso do token).
4. Repetir os roteiros Playwright das specs 001–007 contra o novo shell.
5. Revisão do usuário no glossário (Solução, Tipo de app).
6. Candidatos de baixo custo: checagens e repositórios na massa de demonstração do seed; decidir o que fazer com repositório que some do GitHub. (CRUD de SonarProject: em backlog, não fazer agora.)
7. Depois: fase 3 do leitor (integrações entre apps por `appsettings` e Refit, apelidos host→app, mapa de aplicações), detecção automática de status nas Checagens a partir dos fatos da 013, dependências transitivas, tags chave-valor e a spec de Solução.
8. Na migração para a máquina corporativa: `.github/copilot-instructions.md`, verificação do `.agents/skills/`; GitHub App no lugar do token pessoal (ADR-010).
9. Antes de qualquer uso compartilhado: substituir ADR-007 por uma decisão de autenticação.
