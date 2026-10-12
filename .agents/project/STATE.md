# STATE.md — estado atual do projeto

> Reescrito (não anexado) ao fim de cada sessão de trabalho. Se este arquivo estiver desatualizado em relação ao `git log`, o git está certo. Última atualização: **2026-10-12**, ao fim de uma sessão só de produto (backlog, visão, glossário) — nenhum código mudou desde a spec 004.

## Onde estamos

Estágio do roadmap: **Inventário + Health + Evolution concluídos** (ver `.agents/product/roadmap.md`). Fase de laboratório, rodando só em máquina local, uma pessoa + agentes.

## O que funciona

- **MVP core** (`specs/001-mvp-core/`): squads, apps, repositórios, projetos Sonar, histórico de `QualitySnapshot`, dashboard da squad, lista de apps com busca/filtro/ordenação, detalhe com gráfico de evolução e tabela de histórico.
- **Cadastro de Squads e Apps** (`specs/002-squad-app-management/`): criar/editar/inativar/remover pela UI e pela API, ids por slug, guardas de remoção (409), links corporativos por app.
- **Persistência em SQLite** (`specs/004-migracao-sqlite/`, ADR-009): Dapper com SQL explícito, schema em `schema.sql`. Nenhuma mudança de comportamento observável em relação ao DynamoDB.
- **Testes**: 37 unitários + 12 de integração, verdes. **Nenhum exige Docker ou serviço externo.**
- **Ambiente local**: skill `run-local` (sem Docker); `README.md` com o passo a passo humano.

## O que está mockado / provisório

- **Dados de Sonar** vêm de `MockSonarProvider` + `DemoDataFactory`. Sem integração real com SonarQube.
- **Sem autenticação** (ADR-007). Sem deploy. Sem CI.
- **Litestream e deploy no ECS** estão documentados como alvo (spec 004, ADR-009), não implementados. O restore precisa ser testado antes de confiar dado real a ele.

## O que mudou nesta sessão (só documentação de produto)

- **`.agents/product/glossary.md`** (novo): vocabulário do domínio, cada termo marcado como Definido, Em aberto ou Evitar. Consultar antes de nomear campo, entidade ou tela. `AGENTS.md` aponta para ele.
- **Leitor de repositórios GitHub** (backlog): três fases (inventário → dependências de biblioteca → dependências entre apps). Princípios: **o scan propõe, a pessoa decide**; escopo **por sigla**, nunca a organização inteira, com limite de resultados; contexto do scan = squad + siglas; pré-cadastro confirmado por uma pessoa, vivendo na caixa de entrada e não na tabela de apps.
- **Modelo de repositório** (backlog + glossário): sigla e sigla-app são geridas pela organização e só lidas pelo CheckApps; sigla-app vem de um YAML do repo e identifica o **repositório**, não o app; sigla é campo do repositório e o app a herda; conta AWS é da sigla por ambiente; repositório tem **papel** (aplicação, infra, contrato, secrets); infra com repo próprio (SQS, EventBridge) é **app**.
- **Tags chave-valor** (backlog): proposta no estilo Datadog, em app e repositório, com a **regra campo × tag** — dado com que o sistema toma decisões é campo, não tag.
- **Mapa de aplicações** (backlog): grafo de apps, Solução como subgrafo nomeado, ligações com origem e confiança.
- **Visão AI-friendly** (`vision.md`): AI-friendly ≠ IA dentro; primeiro passo provável é um servidor MCP expondo o CheckApps para as ferramentas que o time já usa. O template de spec agora pergunta identidade, relações, proveniência e tempo de cada dado novo.
- **Nada disso está implementado.** `sqlite-schema.md` e `access-patterns.md` continuam descrevendo o que existe; mudam quando a spec correspondente for implementada.

## Decisões pendentes / perguntas abertas

- **Spec 003 (Checagens)** segue em análise: faltam confirmar as assumptions sobre remoção de app cascatear itens, reabrir sem trilha, remover checagem só sem itens e ausência de notificação. Os access patterns #21–#29 dela precisam ser revistos à luz do SQL.
- **Navegação global** (menu/shell): deliberadamente adiada para junto da spec 003. Pendência independente e barata: os breadcrumbs atuais são inconsistentes e não mostram o nome da squad.
- **Solução e Tipo de app**: definições no glossário aguardam revisão do usuário. Leitura atual: "modal"/"carrinho" são soluções (subgrafo nomeado); "topologia" e "arquitetura" são termos a evitar.
- **Convenção de nome dos repositórios** é uniforme na organização ou varia por sigla? Define se a configuração do scan é um padrão único ou um por sigla.
- **Exceção futura**: repositório compartilhado entre squads — não tratado por enquanto.
- `.github/copilot-instructions.md` ainda não existe — criar quando o Copilot entrar neste repo.
- Confirmar, na máquina corporativa, se Devin/Copilot varrem `.agents/skills/` (premissa do ADR-008).

## Gotchas para a próxima sessão

Os críticos estão em `AGENTS.md`. Os que mais mordem hoje: o caminho relativo do banco resolve contra `%LOCALAPPDATA%\CheckApps` e **não** contra o diretório atual (de propósito); API antiga presa em `:5232` trava o build; `DatePipe` sempre com `'UTC'`.

Em documentação: exemplos de sigla, sigla-app e convenção de nome usam valores fictícios (`AB1`, `AB1-1234`, `{org}-{sigla}-`) — manter assim.

## Próximos passos (em ordem)

1. Revisão do usuário no glossário (Solução, Tipo de app).
2. **Fechar as assumptions da spec 003** e implementá-la, junto com a navegação global.
3. Leitor de GitHub, fase 1 (ferramenta de linha de comando, inventário por sigla) — depende de acesso ao GitHub corporativo; junto vêm os campos de repositório (sigla, sigla-app, papel) e o CRUD de Repository.
4. Na migração para a máquina corporativa: `.github/copilot-instructions.md` e verificação do `.agents/skills/`.
5. Antes de qualquer uso compartilhado: substituir ADR-007 por uma decisão de autenticação.

Verificado em 2026-09-16: uma sessão nova do Claude Code, sem histórico, subiu o ambiente local só com `CLAUDE.md` → `AGENTS.md` → este arquivo → skill `run-local`. A skill mudou com a migração (não há mais Docker) — vale repetir o teste.
