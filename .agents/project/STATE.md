# STATE.md — estado atual do projeto

> Reescrito (não anexado) ao fim de cada sessão de trabalho. Se este arquivo estiver desatualizado em relação ao `git log`, o git está certo. Última atualização: **2026-10-04**, ao concluir a migração para SQLite (spec 004).

## Onde estamos

Estágio do roadmap: **Inventário + Health + Evolution concluídos** (ver `.agents/product/roadmap.md`). Fase de laboratório, rodando só em máquina local, uma pessoa + agentes.

## O que funciona

- **MVP core** (`specs/001-mvp-core/`): squads, apps, repositórios, projetos Sonar, histórico de `QualitySnapshot`, dashboard da squad, lista de apps com busca/filtro/ordenação, detalhe com gráfico de evolução e tabela de histórico.
- **Cadastro de Squads e Apps** (`specs/002-squad-app-management/`): criar/editar/inativar/remover pela UI e pela API, ids por slug, guardas de remoção (409), links corporativos por app.
- **Persistência em SQLite** (`specs/004-migracao-sqlite/`, ADR-009): Dapper com SQL explícito, schema em `schema.sql`. Nenhuma mudança de comportamento observável — verificado endpoint a endpoint contra baseline e com os roteiros Playwright originais, sem alterar os scripts.
- **Testes**: 37 unitários + 12 de integração, verdes. **Nenhum exige Docker ou serviço externo.**
- **Ambiente local**: skill `run-local` (sem Docker); `README.md` com o passo a passo humano.

## O que está mockado / provisório

- **Dados de Sonar** vêm de `MockSonarProvider` + `DemoDataFactory`. Sem integração real com SonarQube.
- **Sem autenticação** (ADR-007). Sem deploy. Sem CI.
- **Litestream e deploy no ECS** estão documentados como alvo (spec 004, ADR-009), não implementados. O restore precisa ser testado antes de confiar dado real a ele.

## Decisões pendentes / perguntas abertas

- **Spec 003 (Checagens)** segue em análise: faltam confirmar as assumptions sobre remoção de app cascatear itens, reabrir sem trilha, remover checagem só sem itens e ausência de notificação. Os access patterns #21–#29 dela precisam ser revistos à luz do SQL (vários deixaram de ter restrição).
- **Navegação global** (menu/shell): analisada em 2026-10-12 e **deliberadamente adiada** — deve ser feita junto com a spec 003, não antes, senão `/checks` nasce inalcançável. Análise completa (personas, dependência de autenticação, faseamento) no backlog. Pendência independente e barata: os breadcrumbs atuais são inconsistentes e não mostram o nome da squad.
- **Classificação de apps** (tipo/arquétipo, solução): conversa em aberto no backlog — recomendação é começar por convenção de tag antes de criar entidade. O termo para "modal/carrinho" ainda não foi decidido; a leitura mais recente é que se parece mais com **agrupamento (Solução)** do que com atributo do app.
- `.github/copilot-instructions.md` ainda não existe — criar quando o Copilot entrar neste repo.
- Confirmar, na máquina corporativa, se Devin/Copilot varrem `.agents/skills/` (premissa do ADR-008).

## Gotchas para a próxima sessão

Os críticos estão em `AGENTS.md`. Os que mais mordem hoje: o caminho relativo do banco resolve contra `%LOCALAPPDATA%\CheckApps` e **não** contra o diretório atual (de propósito); API antiga presa em `:5232` trava o build; `DatePipe` sempre com `'UTC'`.

## Próximos passos (em ordem)

1. **Fechar as assumptions da spec 003** e implementá-la — já em SQL, que foi o motivo de a migração vir antes.
2. Classificação de apps (tipo, solução) — decidir por tag ou campo; ver backlog.
3. Na migração para a máquina corporativa: `.github/copilot-instructions.md` e verificação do `.agents/skills/`.
4. Antes de qualquer uso compartilhado: substituir ADR-007 por uma decisão de autenticação.

Verificado em 2026-09-16: uma sessão nova do Claude Code, sem histórico, subiu o ambiente local só com `CLAUDE.md` → `AGENTS.md` → este arquivo → skill `run-local`. A skill mudou com a migração (não há mais Docker) — vale repetir o teste.
