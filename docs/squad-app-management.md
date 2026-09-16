# CheckApps — Spec: Cadastro/Gerenciamento de Squads e Apps

## Contexto

O MVP entregue (ver `docs/plan.md`) só tinha telas de leitura (squads, dashboard, lista de apps, detalhe do app). Squads/Apps/Repositories/SonarProjects só entravam no sistema via `tools/CheckApps.Seed`. Esta spec adiciona cadastro manual (listar, criar, editar, inativar, remover) de Squads e Apps, mesmo que no futuro esses dados venham de integração/importação — a ideia é não depender só do seed.

Também foi levantada a necessidade de **Links corporativos por App** (N links por app, ex.: Confluence, runbook, dashboard). Decisão: `Links` (lista estruturada `Id/Descricao/Endereco`, 1:N como `Repository`) e `Tags` (lista simples de rótulos, já existente) são conceitos **complementares**, não concorrentes — nenhuma mudança em `Tags`.

Decisões fechadas com o usuário:
- **Remover = hard delete, mas bloqueado se houver histórico**: App só pode ser removido se não tiver nenhum `QualitySnapshot`; Squad só pode ser removida se não tiver nenhum App vinculado. Caso contrário, a UI orienta a inativar.
- **Id gerado automaticamente** (slug do nome, com sufixo numérico em caso de colisão) — usuário não digita o Id.
- **`squadId` do App é editável** (permite mover o app de squad).
- **Sem autenticação** nas novas telas/endpoints, consistente com o resto do MVP.

## 1. Domain Model — mudanças

- `CheckApps.Domain.AppLink` — `Id`, `AppId`, `Description`, `Uri` (mesmo padrão de `Repository`/`SonarProject`).
- Campo `App.Links` (`IReadOnlyCollection<string>`) removido — substituído pela entidade `AppLink`. `App.Tags` não mudou.
- `CheckApps.Domain.Slug` — utilitário estático `Generate(string name) -> string` (lowercase, remove acentos, troca não-alfanumérico por `-`, colapsa/trima `-`). Puro, testável isoladamente.
- `App.SquadId` deixou de ser `init`-only e passou a ter `set` (necessário para permitir mover o app de squad na edição).
- `Squad.Status`/`App.Status` seguem `string?` livres no domínio (`"active" | "inactive"` por convenção da UI).

## 2. Regras de negócio (Application layer)

`SquadService` e `AppService` (`src/CheckApps.Application/Squads/SquadService.cs`, `src/CheckApps.Application/Apps/AppService.cs`):

- **Criar**: gera slug a partir do nome via `SlugIdGenerator.GenerateUniqueAsync` (`src/CheckApps.Application/SlugIdGenerator.cs`), tentando `TryCreateAsync` (Put condicional) e incrementando sufixo (`-2`, `-3`, ...) em colisão. Criar App exige `squadId` de uma squad existente.
- **Editar**: `GetAsync` → aplica os campos editáveis no objeto retornado → `SaveAsync` do mesmo objeto — nunca construir um `App`/`Squad` novo só com os campos do formulário (ver gotcha abaixo).
- **Remover App**: bloqueado se `IQualitySnapshotRepository.GetLatestAsync(appId)` retornar algo. Se livre, `AppRepository.DeleteAllAsync` apaga a partição inteira (`METADATA` + `REPO#`/`SONARPROJECT#`/`APPLINK#`).
- **Remover Squad**: bloqueado se `IAppRepository.ListBySquadAsync(squadId)` retornar algum app. Se livre, apaga só o item `METADATA`.
- Violação de regra de remoção lança `DomainConflictException` (`src/CheckApps.Application/DomainConflictException.cs`) → controller traduz para `409 Conflict`. `squadId`/`appId`/`linkId` inexistente lança `KeyNotFoundException` → `404`/`400`.

**Gotcha real do modelo (documentado e testado)**: `AppRepository.SaveAsync` faz um `PutItem` completo com TODOS os campos do `App`, inclusive os desnormalizados (`CurrentQualityLevel`, `CurrentMetrics`, `LastSnapshotAt`, `GSI2PK/SK`). A edição precisa carregar o App via `GetAsync`, mutar só os campos do formulário e salvar o mesmo objeto — senão apaga silenciosamente o estado de qualidade atual. Coberto por `AppServiceTests.UpdateAsync_CanMoveAppToAnotherSquad_AndPreservesCurrentQualityState`.

## 3. DynamoDB — novos itens e operações

- **AppLink**: `PK=APP#{appId}`, `SK=APPLINK#{linkId}` (mesmo padrão de `Repository`).
- **Create condicional**: `ISquadRepository.TryCreateAsync`/`IAppRepository.TryCreateAsync` — `PutItemRequest` com `ConditionExpression = "attribute_not_exists(PK)"`; retorna `false` (não lança) em conflito.
- **Delete App** (`IAppRepository.DeleteAllAsync`): `Query` por `PK=APP#{appId}` (toda a partição, sem filtro de SK) + `BatchWriteItem` em lotes de 25. É um primitivo "burro": não protege contra apagar `SNAPSHOT#*` — a proteção "não remover com histórico" é só do `AppService`, não do repositório (documentado e testado em `ManagementTests.DeleteAllAsync_HasNoBuiltInGuard_AlsoWipesQualitySnapshots`).
- **Delete Squad** (`ISquadRepository.DeleteAsync`): `DeleteItem` simples.
- **AppLink CRUD** em `IAppRepository`: `GetLinksAsync`, `SaveLinkAsync` (Put — cria e edita), `DeleteLinkAsync`.

## 4. API — novos endpoints

```
POST   /api/squads                          {name, description?} -> SquadDto (201)
PUT    /api/squads/{squadId}                {name, description?, status?} -> SquadDto
DELETE /api/squads/{squadId}                 204, ou 409 se houver apps vinculados

POST   /api/apps                             {name, squadId, description?, criticality?, tags?, externalId?} -> AppDetailDto (201)
PUT    /api/apps/{appId}                     idem + status? -> AppDetailDto
DELETE /api/apps/{appId}                     204, ou 409 se houver quality snapshots

GET    /api/apps/{appId}/links               -> AppLinkDto[]
POST   /api/apps/{appId}/links               {description, uri} -> AppLinkDto (201)
PUT    /api/apps/{appId}/links/{linkId}      {description, uri} -> AppLinkDto
DELETE /api/apps/{appId}/links/{linkId}      204
```

Sem endpoint dedicado para inativar — "inativar/ativar" na UI é um `PUT` com `status` alternado (a lista de apps busca o `AppDetail` completo antes, para não sobrescrever campos que o `AppSummaryDto` não carrega).

## 5. Frontend

As telas de listagem existentes viraram a superfície de gerenciamento (sem tela administrativa paralela):

- `SquadPicker` — botão "+ Nova Squad", ações por linha (Editar/Ativar-Inativar/Remover), toggle "mostrar inativas".
- `AppList` — botão "+ Novo App", mesmas ações por linha, toggle "mostrar inativos".
- `SquadForm` (novo) — rotas `/squads/new` e `/squads/:squadId/edit`.
- `AppForm` (novo) — rotas `/squads/:squadId/apps/new` e `/apps/:appId/edit`; em modo edição inclui a seção de Links (CRUD direto contra os endpoints de link, fora do submit principal).
- `AppDetail` — seção "Links" (somente leitura) + botão "Editar".

## 6. Status da implementação

1. Domain (`AppLink`, `Slug`, `App.SquadId` mutável) — ✅ (6 testes de `Slug`)
2. Infrastructure (`TryCreateAsync`, `DeleteAllAsync`/`DeleteAsync`, CRUD de `AppLink`) — ✅ (6 testes de integração em `ManagementTests`)
3. Application (regras de criar/editar/remover, `DomainConflictException`, `SlugIdGenerator`) — ✅ (12 testes unitários em `SquadServiceTests`/`AppServiceTests`)
4. Api (novos endpoints, DTOs, mapeamento de exceções) — ✅ (verificado via `curl`: slug+colisão, mover app de squad, link CRUD, bloqueios 409/400)
5. Frontend (`SquadForm`, `AppForm`, ações nas listas, Links no `AppDetail`) — ✅ (verificado via Playwright: criar squad → criar app → adicionar link → editar → bloqueio de remoção com histórico → inativar → remover app sem histórico)

Total: 48 testes automatizados (37 unitários + 11 integração).

## Riscos / trade-offs

- Geração de slug com retry via `PutItem` condicional evita corrida em criações concorrentes, mas é best-effort (falha após 50 tentativas) — irrelevante na escala do MVP.
- Remover um App apaga `Repository`/`SonarProject`/`AppLink` em cascata sem confirmação individual — aceitável pois esses itens não têm valor histórico próprio (diferente de `QualitySnapshot`, que bloqueia a remoção).
- Sem autenticação: qualquer pessoa com a URL pode criar/editar/remover — igual ao restante do MVP; revisitar quando (se) o CheckApps sair do estágio de laboratório.
- Testes de integração com `IClassFixture<DynamoDbLocalFixture>` por classe rodam em paralelo entre classes por padrão no xUnit — como cada fixture apaga/recria a mesma tabela física, isso causa falhas intermitentes quando há mais de uma classe de teste. Corrigido com uma `[CollectionDefinition]`/`[Collection]` compartilhada (`DynamoDbCollection`), que serializa as classes que usam a mesma tabela.
