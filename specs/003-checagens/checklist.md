# Checklist — Spec 003: Checagens

Status: **implementada** em 2026-10-12.

## Qualidade da spec (antes de codar)

- [x] User stories com cenários Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante — as decisões abertas viraram Assumptions explícitas
- [x] Assumptions confirmadas pelo usuário (status dos itens em 2026-09-17; o resto em 2026-10-12: app em checagem não é removido, reabrir com histórico, remover checagem só sem itens, sem notificação nesta versão)
- [x] FRs testáveis
- [x] Success Criteria mensuráveis e sem tecnologia
- [x] Modelo de dados definido nas Design notes, já em SQL (`checks`, `check_items`, `check_events`); detalhes finos confirmados no plano
- [x] Dados novos são AI-friendly: ids estáveis (slug para checagem; `(checkId, appId)` para item), relações por FK, proveniência manual (autor em texto livre), tempo em `created_at`, `closed_at`, `updated_at` e no histórico de eventos

## Implementação (incrementos previstos — refinar no plano)

- [x] 1. Domain: `Check`, `CheckItem`, `CheckEvent`, enums, `CheckCodes`, `CheckProgress` — 13 testes unitários (percentual com zero apps, arredondamento, ida e volta dos códigos)
- [x] 2. Infrastructure: `checks`, `check_items`, `check_events` em `schema.sql`; `CheckRepository` — 8 testes de integração (AP-1/2/3/9, idempotência em linhas e histórico, ordem do histórico, FK recusando remover app, remoção de checagem levando o histórico)
- [x] 3. Application: `CheckService` e guarda nova em `AppService.DeleteAsync` — 13 testes de serviço + 1 em `AppServiceTests`; os testes existentes só ganharam o fake novo no construtor, nenhuma asserção mudou
- [x] 4. Api: `ChecksController` + `GET /api/apps/{appId}/checks` — `curl` cobrindo criar, lote (inclusive repetido), status, observação, percentual, fechar/reabrir (e 409 ao repetir), histórico, candidatos, remover checagem com itens (409), remover app em checagem (409, e 204 depois de retirá-lo), app inexistente (400), status desconhecido (400), checagem inexistente (404)
- [x] 5. Frontend: `check-list`, `check-form`, `check-detail` (planilha, seletor de apps, histórico), seção "Checagens" em `app-detail`, rotas
- [x] 6. Navegação global: shell, `CurrentSquadService`, seletor de squad, `/` → última squad, `/squads`, breadcrumbs corrigidos — Playwright: criar checagem → adicionar 3 apps → marcar status → ver percentual → ver no detalhe do app → fechar → ver em fechadas → reabrir → ver histórico → trocar de squad pelo header (lista e dashboard) → `/` cai na última squad
- [x] Decisão sobre os enums de checagem registrada em `.agents/rules/backend.md`
- [x] Regras conquistadas registradas em `.agents/rules/` (evento na mesma transação, `TimeProvider`, `ArgumentException` → 400, shell e `CurrentSquadService`, `ParamAwareReuseStrategy`, horário local no histórico, Playwright com escopo em `main`)
- [x] ADR-005 com nota sobre a nova condição de guarda (app em checagem)
- [x] `.agents/architecture/data-model/sqlite-schema.md` e `access-patterns.md` atualizados

## Verificação

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde: 64 unitários + 20 de integração (eram 37 + 12)
- [x] Endpoints exercitados via `curl`, incluindo 4xx
- [x] Fluxo completo no browser sem erros de console; roteiros Playwright das specs 001 e 002 continuam passando — só os seletores de texto ganharam escopo `main`, porque o seletor de squad do header repete os nomes das squads
- [x] `README.md` / `AGENTS.md`: nada mudou em como rodar; a skill `run-local` foi ajustada (`/` agora redireciona)
- [x] Banco local existente recebeu as tabelas novas pelo bootstrap idempotente, sem perda (5 apps e 15 snapshots antes e depois)

## Encerramento

- [x] Status desta spec atualizado
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado (navegação e breadcrumbs entregues; itens novos descobertos adicionados)
