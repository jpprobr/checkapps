# STATE.md — estado atual do projeto

> Reescrito (não anexado) ao fim de cada sessão de trabalho. Se este arquivo estiver desatualizado em relação ao `git log`, o git está certo. Última atualização: **2026-10-12**, ao concluir a spec 003 (Checagens) e a navegação global.

## Onde estamos

Estágio do roadmap: **Inventário + Health + Evolution concluídos**, mais **Checagens** (feature operacional fora da sequência de estágios). Fase de laboratório, rodando só em máquina local, uma pessoa + agentes.

## O que funciona

- **MVP core** (`specs/001-mvp-core/`): squads, apps, repositórios, projetos Sonar, histórico de `QualitySnapshot`, dashboard da squad, lista de apps com busca/filtro/ordenação, detalhe com gráfico de evolução e tabela de histórico.
- **Cadastro de Squads e Apps** (`specs/002-squad-app-management/`): criar/editar/inativar/remover pela UI e pela API, ids por slug, guardas de remoção (409), links corporativos por app.
- **Persistência em SQLite** (`specs/004-migracao-sqlite/`, ADR-009): Dapper com SQL explícito, schema em `schema.sql`, bootstrap idempotente.
- **Checagens** (`specs/003-checagens/`): criar checagem, adicionar apps em lote (seletor com filtro por squad, tag e nome), status e observação inline por app, percentual, fechar/reabrir, remover só sem itens, histórico só de acréscimo (`check_events`), seção "Checagens" no detalhe do app. App em checagem não pode ser removido (409 + FK sem cascata; nota no ADR-005).
- **Navegação global**: shell com `Squads` | `Checagens`, seletor de squad no header ciente de contexto, `/` → última squad visitada (lembrada no navegador), lista de squads em `/squads`, breadcrumbs com o nome da squad.
- **Testes**: 64 unitários + 20 de integração, verdes. **Nenhum exige Docker ou serviço externo.**
- **Ambiente local**: skill `run-local` (sem Docker); `README.md` com o passo a passo humano.

## O que está mockado / provisório

- **Dados de Sonar** vêm de `MockSonarProvider` + `DemoDataFactory`. Sem integração real com SonarQube.
- **Sem autenticação** (ADR-007): "minha squad" é a última visitada, lembrada no navegador; autor de checagem é texto livre; o histórico não registra quem fez.
- **Sem deploy, sem CI.** Litestream e deploy no ECS documentados como alvo (spec 004, ADR-009), não implementados.
- **O seed não cria checagens** — numa instalação nova a tela de Checagens começa vazia (item no backlog).

## Decisões pendentes / perguntas abertas

- **Solução e Tipo de app**: definições no glossário aguardam revisão do usuário. Leitura atual: "modal"/"carrinho" são soluções (subgrafo nomeado); "topologia" e "arquitetura" são termos a evitar.
- **Convenção de nome dos repositórios** é uniforme na organização ou varia por sigla? Define se a configuração do scan é um padrão único ou um por sigla.
- **Exceção futura**: repositório compartilhado entre squads — não tratado por enquanto.
- `.github/copilot-instructions.md` ainda não existe — criar quando o Copilot entrar neste repo.
- Confirmar, na máquina corporativa, se Devin/Copilot varrem `.agents/skills/` (premissa do ADR-008).

## Gotchas para a próxima sessão

Os críticos estão em `AGENTS.md` e nas regras. Os que mais mordem hoje:
- O caminho relativo do banco resolve contra `%LOCALAPPDATA%\CheckApps` e **não** contra o diretório atual (de propósito). Para testar sem mexer na massa de demonstração: `Sqlite__DatabasePath=checkapps-curl.db` no seed e na API.
- API antiga presa em `:5232` trava o build (`taskkill //F //IM CheckApps.Api.exe`).
- `DatePipe` com `'UTC'` para períodos e datas de calendário; **sem** `'UTC'` para instantes reais (histórico de checagem).
- Playwright: escopar seletores de texto a `main` — o seletor de squad do header repete os nomes das squads.
- Status de checagem são códigos (`em-andamento`), não nomes de enum — `CheckCodes` no Domain, `check-labels.ts` no frontend.
- Em documentação: exemplos de sigla, sigla-app e convenção de nome usam valores fictícios (`AB1`, `AB1-1234`, `{org}-{sigla}-`) — manter assim.

## Próximos passos (em ordem)

1. Revisão do usuário no glossário (Solução, Tipo de app).
2. **Leitor de GitHub, fase 1** (spec 005): inventário por sigla, caixa de entrada com triagem e pré-cadastro confirmado. Recomendação registrada: implementar com duas fontes — um provider fake com os casos difíceis (testes e demo) e o GitHub real apontando para uma organização de teste pessoal — já que o GitHub corporativo não está acessível desta máquina. Junto vêm os campos de repositório (sigla, sigla-app, papel) e o CRUD de Repository.
3. Barato e a qualquer momento: checagens na massa de demonstração do seed.
4. Na migração para a máquina corporativa: `.github/copilot-instructions.md` e verificação do `.agents/skills/`.
5. Antes de qualquer uso compartilhado: substituir ADR-007 por uma decisão de autenticação.

Verificado em 2026-09-16: uma sessão nova do Claude Code, sem histórico, subiu o ambiente local só com `CLAUDE.md` → `AGENTS.md` → este arquivo → skill `run-local`. A skill mudou com a migração (não há mais Docker) e com a navegação (`/` redireciona) — vale repetir o teste.
