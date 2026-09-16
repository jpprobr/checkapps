# Checklist — Spec 002: Cadastro e gerenciamento de Squads e Apps

Status: **concluído** (commit `4420f55`, 2026-09-16). Registro do critério de "done" aplicado.

## Qualidade da spec

- [x] Decisões abertas resolvidas com o usuário antes de codar (semântica de remover, geração de id, squad editável, autenticação) — viraram Assumptions e ADRs 005/006/007
- [x] User stories com cenários Dado/Quando/Então (retrofit de 2026-09-16)
- [x] FRs testáveis
- [x] Success Criteria mensuráveis

## Implementação (incrementos do plano)

- [x] 1. Domain: `AppLink`, `Slug`, remoção de `App.Links`, `App.SquadId` mutável — 6 testes de `Slug`
- [x] 2. Infrastructure: `TryCreateAsync`, `DeleteAllAsync`/`DeleteAsync`, CRUD de `AppLink` — 6 testes de integração (`ManagementTests`): colisão de create, delete de squad, delete cascata de app, CRUD de link, ausência de guarda no repositório
- [x] 3. Application: `SlugIdGenerator`, `DomainConflictException`, create/update/delete em `SquadService` e `AppService` — 12 testes unitários com fakes de armazenamento real
- [x] 4. Api: endpoints, DTOs, mapeamento de exceções — `curl`: slug + colisão (`squad-teste-crud-2`), app com squad inexistente (400), mover de squad (GSI1 reposiciona), links, remover squad vazia (204) e com apps (409), remover app com histórico (409), inativar preservando Quality Level
- [x] 5. Frontend: `SquadForm`, `AppForm` com links, ações inline, `AppDetail` com links e Editar, rotas
- [x] Regras conquistadas registradas em `.agents/rules/` (carrega-muta-salva, `[Collection]`, `AppDetail` antes do PUT)
- [x] Modelo de dados atualizado (`APPLINK#`, `TryCreateAsync`, `DeleteAllAsync`)

## Verificação

- [x] `dotnet build` 0 erros, 0 avisos
- [x] `dotnet test`: 37 unitários + 11 integração, verdes — após corrigir a corrida entre classes de teste (`[Collection]`)
- [x] `curl` em todos os endpoints novos, incluindo os caminhos 400/404/409
- [x] Playwright headless, fluxo completo: criar squad → criar app → adicionar link → editar → registrar snapshot → remover bloqueado (409 com mensagem na tela) → inativar → criar app descartável → remover com sucesso. Único "erro" de console: o 409 esperado.
- [x] Dados de teste limpos (reset do DynamoDB Local + seed)

## Encerramento

- [x] Status `Implementada`
- [x] `README.md` atualizado
- [x] Esta spec (originalmente `docs/squad-app-management.md`)
