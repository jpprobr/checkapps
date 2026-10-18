# STATE.md — estado atual do projeto

> Reescrito (não anexado) ao fim de cada sessão de trabalho. Se este arquivo estiver desatualizado em relação ao `git log`, o git está certo. Última atualização: **2026-10-18**, ao concluir a spec 010 (cadastrados na tela Repositórios), sobre as specs 008 (menu lateral) e 009 (CRUD manual de Repository). **A spec 010 está no working tree, ainda sem commit** (commits só quando o usuário pedir).

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
- **Leitor de GitHub, fase 1** (`specs/005-leitor-github-fase-1/`, ADR-010, guia `docs/leitor-github.md`):
  - ferramenta `tools/CheckApps.GitHubScan` — scan **por sigla** (validada por formato, limite de resultados), `--dry-run`, `--source fake|github`; o escopo é validado antes de pedir token;
  - tela **Repositórios** no menu: todos os repositórios que passaram pelo scan, cada um com o estado dele — novo, no catálogo (link do app, sem ações), de outra squad, ignorado; filtro padrão "Todos" ("Novos" é a caixa de entrada). Ações: criar app (painel pré-preenchido), vincular a app existente (aviso se a sigla difere), de outra squad, ignorar, restaurar;
  - `Repository` tem `sigla`, `siglaApp` (único quando presente), `role` e `source`; aparecem no detalhe do app;
  - fonte `fake` com os casos difíceis; fonte real do GitHub (Search API + YAML da sigla-app) testada com HTTP falso **e lida uma vez de verdade**.
- **Scan pela tela** (`specs/006-scan-pela-tela/`): bloco **Buscar no GitHub** em Repositórios — siglas, *Pré-visualizar* e *Escanear*; mesmo `ScanService` do CLI. **Desligado por padrão** (`Scan:Enabled`); erros 400/409/502; um scan por vez.
- **Menu "Repositórios"** (`specs/007-menu-repositorios/`) e "já no catálogo" no resultado do scan.
- **Testes**: 168 unitários + 42 de integração, verdes. **Nenhum exige Docker, serviço externo ou rede.**
- **Ambiente local**: skill `run-local`; `README.md` com o passo a passo. Verificado nesta sessão: seed, API em `:5232`, Angular em `:4200`, `curl` nos três endpoints novos e um roteiro Playwright (menu desktop/recolhido/recarregar/celular + CRUD completo, sem erros de console além do 409 esperado).

## O que está mockado / provisório

- **Dados de Sonar** vêm de `MockSonarProvider` + `DemoDataFactory`. Sem integração real com SonarQube.
- **O GitHub real foi lido uma vez** (2026-10-12, `--dry-run`). **Ainda não exercitados com o GitHub real**: arquivado, paginação, limite de resultados, limite de chamadas, acesso parcial e YAML inválido — só cobertos por JSON fixo nos testes.
- **O scan pela tela ainda não foi usado contra o GitHub real** (pendência do usuário): exige os segredos da **API**, separados dos da ferramenta de linha de comando.
- **Sem autenticação** (ADR-007): "minha squad" é a última visitada (navegador); autor de checagem é texto livre. **O endpoint de scan usa o token configurado e qualquer pessoa que abra o sistema pode dispará-lo** — por isso vem desligado; autenticação é pré-requisito de uso compartilhado.
- **Sem deploy, sem CI.** Litestream e ECS documentados como alvo (spec 004, ADR-009), não implementados.
- **O seed não cria checagens nem descobertos** — o scan `--source fake` cria os descobertos de demonstração.
- **Pendente de verificação nas specs 008/009**: os roteiros Playwright das specs 001–007 não foram repetidos contra o novo shell (seletores de menu mudaram de header para `aside`); confirmação do usuário na tela.

## Decisões pendentes / perguntas abertas

- **Solução e Tipo de app**: definições no glossário aguardam revisão do usuário.
- **A convenção de nome dos repositórios é uniforme na organização?** Hoje é configuração (`Scan:NameTemplate`).
- **Formato real do YAML da sigla-app** e caminho do arquivo: hoje `config.yaml` com a chave `siglaApp` na raiz, configuráveis.
- **Exceção futura**: repositório compartilhado entre squads — não tratado.
- Repositório que **some do GitHub** continua na caixa (só `last_seen_at` envelhece) — comportamento não decidido.
- **CRUD de Repository — o que ficou de fora** (backlog): "repositório ativo" único por app, reconhecimento por URL no scan (hoje um repositório manual sem `external_id` e depois achado pelo scan aparece como *novo*). Sigla-app é comparada com diferença de maiúsculas (`AB1` ≠ `ab1`) — a criação manual não normaliza.
- `.github/copilot-instructions.md` ainda não existe; confirmar na máquina corporativa se Devin/Copilot varrem `.agents/skills/` (ADR-008).

## Gotchas para a próxima sessão

Os críticos estão em `AGENTS.md` e nas regras. Os que mais mordem hoje:
- **Segredos**: `GitHub:Token`, `GitHub:Organization`, `Scan:NameTemplate` e `Scan:Enabled` ficam em `dotnet user-secrets` — **a ferramenta e a API têm `UserSecretsId` e armazenamentos separados** — ou em variáveis `GitHub__…`/`Scan__…`. Nunca em arquivo versionado nem em exemplo de documentação. `dotnet user-secrets list` imprime o token: filtre com `| grep "Scan:"`.
- **A API lê a configuração só na subida**; um processo antigo em `:5232` responde com a configuração velha e trava o build (`taskkill //F //IM CheckApps.Api.exe`).
- O caminho relativo do banco resolve contra `%LOCALAPPDATA%\CheckApps`. Para testar sem mexer na massa real: `Sqlite__DatabasePath=<nome>.db` no seed, no scan **e** na API. **Os testes manuais desta sessão (repositório criado e removido no `app-1`) rodaram contra o banco real da máquina** — ficou resíduo só se algo falhou no meio.
- Em Development a API valida a DI na subida (`DiscoveryCompositionTests`).
- `DatePipe` com `'UTC'` para períodos e datas de calendário; sem `'UTC'` para instantes reais.
- Playwright: escopar seletores de texto a `main`; a gaveta do celular fora da tela ainda é "visível" para `isVisible()` — meça `boundingBox().x`. O Playwright não está no repo: instale em pasta fora dele e use `executablePath` do Chromium já baixado em `%LOCALAPPDATA%\ms-playwright`.
- Status de checagem e de triagem são **códigos** (`em-andamento`, `other-squad`), não nomes de enum.
- **`heredoc` do shell quebra com apóstrofo/acento em português**: escreva arquivos e edições com as ferramentas de arquivo (Python via heredoc funcionou só sem apóstrofos no texto).

## Próximos passos (em ordem)

1. **Confirmar na tela** (usuário) o CRUD de repositório (spec 009) e os cadastrados na tela Repositórios (spec 010); **commitar** a spec 010.
2. **Usar o scan pela tela com o GitHub real** (usuário): segredos da API (`docs/leitor-github.md`, seção 5), reiniciar a API e escanear em **Repositórios → Buscar no GitHub**; depois criar os repositórios de teste que faltam (arquivado, infra, outra sigla, fora do acesso do token).
3. Repetir os roteiros Playwright das specs 001–007 contra o novo shell.
4. Revisão do usuário no glossário (Solução, Tipo de app).
5. Candidatos de baixo custo: o que sobrou do CRUD de Repository (ativo único, reconhecimento por URL), CRUD de SonarProject, checagens e repositórios na massa de demonstração do seed, decidir o que fazer com repositório que some do GitHub.
6. Depois: fase 2 do leitor (dependências de biblioteca); tags chave-valor; spec de Solução.
7. Na migração para a máquina corporativa: `.github/copilot-instructions.md`, verificação do `.agents/skills/`; GitHub App no lugar do token pessoal (ADR-010).
8. Antes de qualquer uso compartilhado: substituir ADR-007 por uma decisão de autenticação.
