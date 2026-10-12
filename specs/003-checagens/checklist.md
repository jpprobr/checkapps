# Checklist — Spec 003: Checagens

Status: **em análise** — nenhum item marcado ainda. Vira "Aprovada" quando a seção "Qualidade da spec" estiver completa; "Implementada" quando tudo estiver.

## Qualidade da spec (antes de codar)

- [x] User stories com cenários Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante — as decisões abertas viraram Assumptions explícitas
- [ ] Assumptions confirmadas pelo usuário (status dos itens já confirmados em 2026-09-17: `pendente`/`em-andamento`/`conforme`/`nao-se-aplica`/`concluido`; restam: remoção de app cascateia itens; reabrir permitido; remover checagem só sem itens; sem notificação)
- [x] FRs testáveis
- [x] Success Criteria mensuráveis e sem tecnologia
- [ ] Decisão sobre `CheckItemStatus` como enum registrada em `.agents/rules/backend.md`
- [ ] Modelo DynamoDB confirmado no plano (reuso do GSI1 com `APP#…`; denormalizar `appName` ou `BatchGetItem`)

## Implementação (incrementos previstos — refinar no plano)

- [ ] 1. Domain: `Check`, `CheckItem`, `CheckItemStatus`; regra de percentual (`conforme + nao-se-aplica + concluido`) como função pura — testes unitários do cálculo, incluindo zero apps
- [ ] 2. Infrastructure: `ICheckRepository` (`TryCreateAsync`, get com itens, list via GSI1 `CHECK`, itens por app via GSI1 `APP#…`, upsert/delete de item, delete de check); `Keys.cs` + `data-model` atualizados — testes de integração: AP-2, AP-3, idempotência de adicionar, cascata ao remover app
- [ ] 3. Application: `CheckService` (criar com slug, editar, fechar/reabrir, remover só sem itens, adicionar/remover apps validando que existem, atualizar item); `AppService.DeleteAsync` passa a apagar itens de checagem — testes unitários com fakes, incluindo os caminhos de 409
- [ ] 4. Api: endpoints do esboço; `DomainConflictException` → 409 — `curl` cobrindo criar, adicionar apps, mudar status, percentual, fechar, remover bloqueada
- [ ] 5. Frontend: `checks` (lista), `check-form`, `check-detail` (planilha com edição inline e seletor de apps), seção "Checagens" em `app-detail`, rotas, **e a navegação global junto** (ver "Navegação" em `.agents/product/backlog.md` — sem ela `/checks` nasce inalcançável, porque nenhuma tela existente linka para lá) — Playwright: criar checagem → adicionar 3 apps → marcar status → ver percentual → ver no detalhe do app → fechar → ver em fechadas
- [ ] Regras conquistadas registradas em `.agents/rules/`
- [ ] `.agents/architecture/data-model/dynamodb-single-table.md` atualizado com `CHECK#`/`ITEM#`

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
