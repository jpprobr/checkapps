# Estrutura do projeto

Monólito modular em .NET 10 com frontend Angular separado. A dependência entre camadas é **estritamente para dentro**: `Api` e `Infrastructure` dependem de `Application`, que depende de `Domain`. `Domain` não depende de nada.

```
CheckApps.slnx
├── src/
│   ├── CheckApps.Domain            entidades e regras de negócio puras
│   ├── CheckApps.Application       casos de uso e portas (interfaces)
│   ├── CheckApps.Infrastructure    DynamoDB, mock do Sonar, seed
│   └── CheckApps.Api               ASP.NET Core Web API (controllers, DTOs)
├── tests/
│   ├── CheckApps.UnitTests         Domain + Application, com fakes à mão
│   └── CheckApps.IntegrationTests  repositórios contra DynamoDB Local
├── tools/
│   └── CheckApps.Seed              popula o DynamoDB com dados de demonstração
├── frontend/                       Angular 20, standalone components
├── specs/                          especificações por feature (ver AGENTS.md)
└── .agents/                        contexto para agentes (este diretório)
```

## `src/CheckApps.Domain`

Sem dependências de framework. Contém:

- **Entidades**: `Squad`, `App`, `Repository`, `SonarProject`, `AppLink`, `QualitySnapshot`. Classes com `required` + `init` para identidade; `set` só onde a edição é permitida (ex.: `App.SquadId` é mutável porque um app pode mudar de squad).
- **Value objects**: `QualityMetrics` (record), `QualityLevel` (enum A–E, onde A é melhor).
- **Serviços de domínio**: `QualityAssessment/IQualityAssessmentEngine` + `QualityAssessmentEngineV1` — transforma métricas em Quality Level. Versionado de propósito: cada snapshot grava qual versão o classificou.
- **Utilitários puros**: `Slug.Generate` (gera ids a partir de nomes).

## `src/CheckApps.Application`

Orquestra o domínio e define as **portas** que a infraestrutura implementa. Não sabe que o banco é DynamoDB.

- **Portas de persistência** (`Persistence/`): `ISquadRepository`, `IAppRepository`, `IQualitySnapshotRepository`.
- **Porta do Sonar** (`Sonar/ISonarProvider`): hoje só o mock implementa; um `SonarQubeProvider` real implementaria a mesma interface.
- **Serviços de aplicação**: `SquadService`, `AppService`, `QualitySnapshotService`. É aqui que vivem as regras de negócio que cruzam entidades — guardas de remoção, validação de squad existente, geração de id único (`SlugIdGenerator`), agregação de evolução por squad.
- **`DomainConflictException`**: violação de regra de negócio; a API traduz para 409.

## `src/CheckApps.Infrastructure`

- **`DynamoDb/`**: implementação das portas de persistência sobre o AWS SDK de baixo nível (dicionários de `AttributeValue`, sem `DynamoDBContext`). `Keys.cs` e `TableSchema.cs` são a **fonte da verdade** das chaves; ver `data-model/dynamodb-single-table.md`. `DynamoDbTableBootstrapper` cria a tabela se não existir.
- **`Sonar/MockSonarProvider`**: métricas determinísticas por `projectKey`.
- **`Seeding/`**: `DemoDataFactory` monta a massa de demonstração (2 squads, 5 apps, 3 meses de histórico com cenários de melhora / piora / estabilidade).
- **`DependencyInjection/`**: `AddCheckAppsInfrastructure` registra tudo, inclusive o client DynamoDB (local via `ServiceUrl` ou AWS real via cadeia padrão de credenciais).

## `src/CheckApps.Api`

- **`Controllers/`**: `SquadsController`, `AppsController`. Controllers são finos: chamam um serviço de aplicação, mapeiam para DTO, traduzem exceções (`KeyNotFoundException` → 404/400, `DomainConflictException` → 409).
- **`Contracts/`**: DTOs (records) e o mapeamento `DtoMapping`. Enums serializam como string (`"A"`, não `0`).
- `Program.cs`: DI, CORS para `localhost:4200`, bootstrap da tabela em Development.

## `frontend/src/app`

- **`core/`**: modelos TypeScript (`models.ts`), serviços HTTP (`squads-api.service.ts`, `apps-api.service.ts`), `api-config.ts` (URL base).
- **`features/`**: um diretório por tela — `squad-picker` (landing + gerenciamento de squads), `squad-dashboard`, `app-list` (lista + gerenciamento de apps), `app-detail`, `squad-form`, `app-form`.
- **`shared/`**: `quality-badge`, `quality-history-chart` (SVG próprio, sem lib).
- Sem NgModules, sem state management externo — signals + serviços injetados.

## Fluxos que atravessam camadas (os que mais confundem)

**Registrar um snapshot de qualidade** — `POST /api/apps/{id}/quality-snapshots` → `AppsController` → `QualitySnapshotService.RegisterAsync` (calcula o Quality Level via engine) → `QualitySnapshotRepository.AddAsync` (transação: grava o snapshot **e** atualiza o estado desnormalizado no item do App, condicionado a ser mais recente). É o **único** caminho de escrita de snapshot; nunca gravar snapshot por fora dele.

**Editar um app** — `PUT /api/apps/{id}` → `AppService.UpdateAsync` → `GetAsync` → muta os campos → `SaveAsync` do **mesmo objeto**. `SaveAsync` é um `PutItem` completo; construir um `App` novo aqui apaga o estado de qualidade.

**Remover um app** — `DELETE /api/apps/{id}` → `AppService.DeleteAsync` → verifica `GetLatestAsync` (se houver snapshot, lança `DomainConflictException`) → `DeleteAllAsync` (apaga a partição inteira). O repositório não protege nada; a guarda é do serviço.
