# STATE.md — estado atual do projeto

> Reescrito (não anexado) ao fim de cada sessão de trabalho. Se este arquivo estiver desatualizado em relação ao `git log`, o git está certo. Última atualização: **2026-10-12**, ao concluir a spec 006 (scan pela tela), sobre a spec 005 (leitor de GitHub, fase 1).

## Onde estamos

Estágio do roadmap: **Inventário + Health + Evolution concluídos**, mais **Checagens** e o **leitor de GitHub, fase 1** (features operacionais fora da sequência de estágios). Fase de laboratório, rodando só em máquina local, uma pessoa + agentes.

## O que funciona

- **MVP core** (`specs/001-mvp-core/`): squads, apps, repositórios, projetos Sonar, histórico de `QualitySnapshot`, dashboard da squad, lista de apps, detalhe com gráfico de evolução e tabela de histórico.
- **Cadastro de Squads e Apps** (`specs/002-squad-app-management/`): criar/editar/inativar/remover pela UI e pela API, ids por slug, guardas de remoção (409), links corporativos por app.
- **Persistência em SQLite** (`specs/004-migracao-sqlite/`, ADR-009): Dapper com SQL explícito, `schema.sql` idempotente **mais um passo aditivo** no bootstrapper (`AdditiveColumns`) para colunas novas em tabelas existentes.
- **Checagens** (`specs/003-checagens/`) e **navegação global** (shell, seletor de squad, `/` → última squad visitada).
- **Leitor de GitHub, fase 1** (`specs/005-leitor-github-fase-1/`, ADR-010, guia `docs/leitor-github.md`):
  - ferramenta `tools/CheckApps.GitHubScan` — scan **por sigla** (validada por formato, limite de resultados), `--dry-run`, `--source fake|github`; o escopo é validado antes de pedir token;
  - caixa de entrada **Descobertos** no menu: criar app (painel pré-preenchido, nada grava antes de confirmar), vincular a app existente (aviso se a sigla difere), de outra squad, ignorar, restaurar;
  - `Repository` ganhou `sigla`, `siglaApp` (único quando presente), `role` e `source`; aparecem no detalhe do app;
  - fonte `fake` com os casos difíceis; fonte real do GitHub (Search API + YAML da sigla-app) testada com HTTP falso **e lida uma vez de verdade** (ver abaixo).
- **Scan pela tela** (`specs/006-scan-pela-tela/`): bloco **Buscar no GitHub** em Descobertos — siglas, *Pré-visualizar* (não grava) e *Escanear*; o CLI continua, e os dois executam o mesmo `ScanService` (composição única em `AddCheckAppsInfrastructure`, fonte por `Scan:Source`). **Desligado por padrão** (`Scan:Enabled`); a fonte expõe `ConfigurationProblem` em vez de lançar, então a API sobe sem credencial e a tela explica o que falta. Erros: 400 (escopo/limite), 409 (desligado, não configurado, já em andamento), 502 (falha do GitHub); um scan por vez.
- **"Já no catálogo" no resultado do scan**: o item que já virou app aparece com a marca e o link do app (e o resumo traz a contagem); a tabela de baixo explica por que está vazia. Veio do primeiro uso real, em que o repositório escaneado já era um app e a tela parecia "não mudar".
- **Testes**: 158 unitários + 38 de integração, verdes (inclui um teste de composição com a validação de DI do Development ligada). **Nenhum exige Docker, serviço externo ou rede.**
- **Ambiente local**: skill `run-local`; `README.md` com o passo a passo.

## O que está mockado / provisório

- **Dados de Sonar** vêm de `MockSonarProvider` + `DemoDataFactory`. Sem integração real com SonarQube.
- **O GitHub real foi lido uma vez** (2026-10-12, organização de teste, `--dry-run`): encontrou o repositório, leu a sigla-app do `config.yaml` e subiu a confiança de baixa para alta. **Ainda não exercitados com o GitHub real**: arquivado, paginação, limite de resultados, limite de chamadas, acesso parcial e YAML inválido — hoje só cobertos por JSON fixo nos testes.
- **O scan pela tela ainda não foi usado contra o GitHub real** (pendência do checklist da spec 006, a cargo do usuário): exige os segredos da **API**, separados dos da ferramenta de linha de comando.
- **Sem autenticação** (ADR-007): "minha squad" é a última visitada (navegador); autor de checagem é texto livre; histórico não registra quem fez. **O endpoint de scan da API usa o token configurado e qualquer pessoa que abra o sistema pode dispará-lo** — por isso vem desligado e só deve ficar ligado em uso individual; autenticação é pré-requisito de uso compartilhado.
- **Sem deploy, sem CI.** Litestream e deploy no ECS documentados como alvo (spec 004, ADR-009), não implementados.
- **O seed não cria checagens nem descobertos** — o scan `--source fake` cria os descobertos de demonstração.

## Decisões pendentes / perguntas abertas

- **Solução e Tipo de app**: definições no glossário aguardam revisão do usuário ("modal"/"carrinho" = soluções; "topologia" e "arquitetura" são termos a evitar).
- **A convenção de nome dos repositórios é uniforme na organização?** Hoje é configuração (`Scan:NameTemplate`), um padrão único. Se variar por sigla, precisa de um padrão por sigla.
- **Formato real do YAML da sigla-app** e caminho do arquivo: hoje `config.yaml` com a chave `siglaApp` na raiz, configuráveis (`GitHub:ConfigFilePath`, `GitHub:SiglaAppKey`). Só chaves da raiz do mapa são lidas.
- **Exceção futura**: repositório compartilhado entre squads — não tratado.
- Repositório que **some do GitHub** continua na caixa (só `last_seen_at` envelhece) — comportamento não decidido.
- `.github/copilot-instructions.md` ainda não existe; confirmar na máquina corporativa se Devin/Copilot varrem `.agents/skills/` (ADR-008).

## Gotchas para a próxima sessão

Os críticos estão em `AGENTS.md` e nas regras. Os que mais mordem hoje:
- **Segredos**: `GitHub:Token`, `GitHub:Organization`, `Scan:NameTemplate` e `Scan:Enabled` ficam em `dotnet user-secrets` — **a ferramenta e a API têm `UserSecretsId` e armazenamentos separados; quem usa os dois configura os dois** — ou em variáveis `GitHub__…`/`Scan__…` (que vencem os user-secrets). Nunca em arquivo versionado, nem em exemplo de documentação (que usa `AB1`, `AB1-0001`, `{prefixo}-{sigla}-`). `dotnet user-secrets list` imprime o token: filtre com `| grep "Scan:"`.
- **A API lê a configuração só na subida**: depois de mudar um segredo, reinicie-a. Um processo antigo na porta `:5232` responde com a configuração velha (e trava o build: `taskkill //F //IM CheckApps.Api.exe`).
- O caminho relativo do banco resolve contra `%LOCALAPPDATA%\CheckApps`. Para testar sem mexer na massa real: `Sqlite__DatabasePath=<nome>.db` no seed, no scan **e** na API. Os segredos reais da máquina interferem nos roteiros de teste (a convenção de nome, por exemplo): sobrescreva com variável de ambiente (`Scan__NameTemplate`).
- Em Development a API valida a DI na subida: o teste de composição (`DiscoveryCompositionTests`) cobre isso — dependência de algo que só um host registra volta a quebrar a API.
- `DatePipe` com `'UTC'` para períodos e datas de calendário; **sem** `'UTC'` para instantes reais.
- Playwright: escopar seletores de texto a `main` (o seletor de squad do header repete os nomes); opções de `<select>` fechado não são "visíveis" — use `state: 'attached'`.
- Status de checagem e de triagem são **códigos** (`em-andamento`, `other-squad`), não nomes de enum.
- `heredoc` do shell quebra com texto em português: escreva scripts de edição com a ferramenta de arquivos; texto com acento passado pelo shell chega corrompido (inclusive em JSON de `curl`).

## Próximos passos (em ordem)

1. **Usar o scan pela tela com o GitHub real** (usuário): configurar os segredos da API (`docs/leitor-github.md`, seção 5), reiniciar a API e escanear em **Descobertos → Buscar no GitHub**; depois, criar os demais repositórios de teste (arquivado, infra, outra sigla, um fora do acesso do token) para ver os casos que ainda só os testes cobrem.
2. Revisão do usuário no glossário (Solução, Tipo de app).
3. Candidatos de baixo custo: checagens e descobertos na massa de demonstração do seed; CRUD manual de Repository (campos de sigla, sigla-app, papel); decidir o que fazer com repositório que some do GitHub.
4. Depois: fase 2 do leitor (dependências de biblioteca) — destrava "quais apps usam a biblioteca X" e a detecção automática de status das checagens; tags chave-valor; spec de Solução.
5. Na migração para a máquina corporativa: `.github/copilot-instructions.md` e verificação do `.agents/skills/`; GitHub App no lugar do token pessoal (ADR-010).
6. Antes de qualquer uso compartilhado: substituir ADR-007 por uma decisão de autenticação.

Verificado em 2026-09-16: uma sessão nova do Claude Code, sem histórico, subiu o ambiente local só com `CLAUDE.md` → `AGENTS.md` → este arquivo → skill `run-local`. A skill e o fluxo mudaram desde então — vale repetir o teste.
