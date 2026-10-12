# Checklist — Spec 003: Checagens

Status: **aprovada** em 2026-10-12. Vira "Implementada" quando tudo estiver marcado.

## Qualidade da spec (antes de codar)

- [x] User stories com cenários Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante — as decisões abertas viraram Assumptions explícitas
- [x] Assumptions confirmadas pelo usuário (status dos itens em 2026-09-17; o resto em 2026-10-12: app em checagem não é removido, reabrir com histórico, remover checagem só sem itens, sem notificação nesta versão)
- [x] FRs testáveis
- [x] Success Criteria mensuráveis e sem tecnologia
- [x] Modelo de dados definido nas Design notes, já em SQL (`checks`, `check_items`, `check_events`); detalhes finos confirmados no plano
- [x] Dados novos são AI-friendly: ids estáveis (slug para checagem; `(checkId, appId)` para item), relações por FK, proveniência manual (autor em texto livre), tempo em `created_at`, `closed_at`, `updated_at` e no histórico de eventos

## Implementação (incrementos previstos — refinar no plano)

- [ ] 1. Domain: `Check`, `CheckItem`, `CheckItemStatus`; regra de percentual (`conforme + nao-se-aplica + concluido`) como função pura — testes unitários do cálculo, incluindo zero apps
- [ ] 2. Infrastructure: tabelas `checks`, `check_items`, `check_events` em `schema.sql`; `ICheckRepository` (criar capturando PK, get com itens via join, list com contagens, checagens por app, adicionar idempotente, retirar, atualizar item, fechar/reabrir, remover, histórico) — cada mudança com seu evento na mesma transação — testes de integração: AP-1, AP-2, AP-3, idempotência de adicionar, evento gravado junto, FK recusando remover app em checagem
- [ ] 3. Application: `CheckService` (criar com slug, editar, fechar/reabrir, remover só sem itens, adicionar/retirar apps validando que existem, atualizar item); `AppService.DeleteAsync` passa a recusar (409) app em checagem — testes unitários com fakes, incluindo os caminhos de 409
- [ ] 4. Api: endpoints do esboço, incluindo o histórico; `DomainConflictException` → 409 — `curl` cobrindo criar, adicionar apps, mudar status, percentual, fechar, reabrir, histórico, remover checagem bloqueada, remover app em checagem bloqueada
- [ ] 5. Frontend: `checks` (lista), `check-form`, `check-detail` (planilha com edição inline, seletor de apps e histórico), seção "Checagens" em `app-detail`, rotas, **e a navegação global junto** (ver "Navegação" em `.agents/product/backlog.md` — sem ela `/checks` nasce inalcançável, porque nenhuma tela existente linka para lá) — Playwright: criar checagem → adicionar 3 apps → marcar status → ver percentual → ver no detalhe do app → fechar → ver em fechadas → reabrir → ver histórico
- [ ] Decisão sobre `CheckItemStatus` como enum registrada em `.agents/rules/backend.md`
- [ ] Regras conquistadas registradas em `.agents/rules/`
- [ ] ADR-005 com nota sobre a nova condição de guarda (app em checagem)
- [ ] `.agents/architecture/data-model/sqlite-schema.md` e `access-patterns.md` atualizados

## Verificação

- [ ] `dotnet build` sem erros nem avisos
- [ ] `dotnet test` verde (unitários + integração)
- [ ] Endpoints exercitados via `curl`, incluindo 4xx
- [ ] Fluxo completo no browser sem erros de console
- [ ] `README.md` / `AGENTS.md` atualizados se algo mudou em como rodar

## Encerramento

- [ ] Status desta spec atualizado
- [ ] `.agents/project/STATE.md` reescrito
- [ ] `.agents/product/backlog.md` atualizado (itens desta spec removidos; novos descobertos adicionados)
