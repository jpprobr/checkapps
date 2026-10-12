# Estrutura do projeto

Monólito modular em .NET 10 com frontend Angular separado. A dependência entre camadas é **estritamente para dentro**: `Api` e `Infrastructure` dependem de `Application`, que depende de `Domain`. `Domain` não depende de nada.

```
CheckApps.slnx
├── src/
│   ├── CheckApps.Domain            entidades e regras de negócio puras
│   ├── CheckApps.Application       casos de uso e portas (interfaces)
│   ├── CheckApps.Infrastructure    SQLite (Dapper), mock do Sonar, seed
│   └── CheckApps.Api               ASP.NET Core Web API (controllers, DTOs)
├── tests/
│   ├── CheckApps.UnitTests         Domain + Application, com fakes à mão
│   └── CheckApps.IntegrationTests  repositórios contra SQLite temporário
├── tools/
│   ├── CheckApps.Seed              popula o banco com dados de demonstração
│   └── CheckApps.GitHubScan        scan por sigla: descobre repositórios e grava a caixa de entrada
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

Orquestra o domínio e define as **portas** que a infraestrutura implementa. Não sabe qual é o banco — foi isso que tornou a migração do DynamoDB para SQLite barata.

- **Portas de persistência** (`Persistence/`): `ISquadRepository`, `IAppRepository`, `IQualitySnapshotRepository`.
- **Porta do Sonar** (`Sonar/ISonarProvider`): hoje só o mock implementa; um `SonarQubeProvider` real implementaria a mesma interface.
- **Serviços de aplicação**: `SquadService`, `AppService`, `QualitySnapshotService`. É aqui que vivem as regras de negócio que cruzam entidades — guardas de remoção, validação de squad existente, geração de id único (`SlugIdGenerator`), agregação de evolução por squad.
- **`DomainConflictException`**: violação de regra de negócio; a API traduz para 409.

## `src/CheckApps.Infrastructure`

- **`Sqlite/`**: implementação das portas de persistência com Dapper e SQL explícito. `schema.sql` (recurso embutido) é a **fonte da verdade** do modelo; ver `data-model/sqlite-schema.md`. `SqliteConnectionFactory` aplica os PRAGMAs por conexão e `SqliteSchemaBootstrapper` cria o schema se não existir.
- **`Sonar/MockSonarProvider`**: métricas determinísticas por `projectKey`.
- **`Seeding/`**: `DemoDataFactory` monta a massa de demonstração (2 squads, 5 apps, 3 meses de histórico com cenários de melhora / piora / estabilidade).
- **`DependencyInjection/`**: `AddCheckAppsInfrastructure` registra tudo, inclusive a fábrica de conexão SQLite (caminho do arquivo via `Sqlite:DatabasePath`).

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

**Registrar um snapshot de qualidade** — `POST /api/apps/{id}/quality-snapshots` → `AppsController` → `QualitySnapshotService.RegisterAsync` (calcula o Quality Level pela regra vigente e congela a versão dela) → `QualitySnapshotRepository.AddAsync` (insert). É o **único** caminho de escrita de snapshot; nunca gravar por fora dele, senão o nível não passa pela engine.

**Editar um app** — `PUT /api/apps/{id}` → `AppService.UpdateAsync` → `GetAsync` → muta os campos → `SaveAsync`. `SaveAsync` grava só os dados próprios do App; o estado de qualidade é derivado na leitura e não corre risco (no DynamoDB corria — ver ADR-009).

**Remover um app** — `DELETE /api/apps/{id}` → `AppService.DeleteAsync` → verifica `GetLatestAsync` (se houver snapshot, lança `DomainConflictException`) → `DeleteAllAsync` (apaga o App; repositórios, projetos Sonar, links e snapshots saem por cascata). O repositório não protege nada; a guarda é do serviço.
