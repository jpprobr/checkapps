# Regras — backend (`src/**`, `tests/**`, `tools/**`)

Leia antes de editar código .NET. Só regras não-óbvias e específicas deste projeto; o resto é convenção C# normal.

## Persistência

- **SQL explícito com Dapper, sem ORM.** Mudança de schema é no `schema.sql` (idempotente, aplicado no bootstrap) + atualizar `.agents/architecture/data-model/sqlite-schema.md`.
- **SQL portável para Postgres** (FR-006 da spec 004): tipos declarados, datas em TEXT ISO-8601 UTC via `IsoTimestamp`, nada específico do SQLite. Em especial, **não use `GROUP_CONCAT`** — a função de agregação de strings difere no Postgres; prefira consulta separada e costura em C#.
- **O estado de qualidade atual do App é derivado do snapshot mais recente por consulta**, nunca armazenado (ADR-009 revogou a desnormalização). `SaveAsync` grava só os dados próprios do App.
- **Todo `QualitySnapshot` passa por `QualitySnapshotService.RegisterAsync`**, que é quem calcula o Quality Level pela regra vigente. Nunca chame `IQualitySnapshotRepository.AddAsync` direto de código de produto (o seed é a exceção deliberada).
- **Repositórios são primitivos "burros".** Regra de negócio (guardas de remoção, validação de squad existente, geração de id único) fica nos serviços de aplicação. `DeleteAllAsync` apaga o App e tudo que cascateia, inclusive snapshots — a proteção é de `AppService.DeleteAsync`. Exceção: app em checagem, que a FK de `check_items` recusa (`SqliteException`).
- **Criar entidade com id gerado = `TryCreateAsync` + `SlugIdGenerator`.** O `TryCreateAsync` só devolve `false` para violação de PK/UNIQUE (códigos estendidos do SQLite); violação de FK propaga como exceção, porque é erro de verdade e não "id já existe".
- **Mudança com histórico grava o evento na mesma transação** (checagens: `check_events`). O serviço monta o evento; o repositório grava mudança e evento juntos. Operação idempotente só grava evento quando de fato mudou algo (`INSERT … ON CONFLICT DO NOTHING` + linhas afetadas).
- **Coluna nova em tabela existente = `CREATE TABLE` + `AdditiveColumns` do `SqliteSchemaBootstrapper` + teste sobre banco antigo** (`SchemaEvolutionTests`). `CREATE TABLE IF NOT EXISTS` não altera tabela que já existe, e o banco local de quem já usava o CheckApps é uma delas. Índice que depende da coluna nova fica no bootstrapper, depois do `ADD COLUMN`.
- **`PRAGMA foreign_keys` é por conexão e vem desligado no SQLite.** Toda conexão sai de `SqliteConnectionFactory`, que já o liga — sem isso as cascatas do schema não acontecem.

## Domínio e aplicação

- `Id` é `init`-only em todas as entidades. `App.SquadId` é a única exceção com `set` (mover de squad é um requisito).
- Enums no domínio, strings na API: `QualityLevel` serializa como `"A"`, não `0` (`JsonStringEnumConverter` global). Ao adicionar enum novo, não precisa fazer nada; ao mudar a serialização, verifique o frontend.
- `Status` de Squad/App é `string?` livre no domínio; `"active"`/`"inactive"` é convenção da UI, não enum. Não introduza enum sem ADR.
- **Exceção deliberada: checagens usam enums** (`CheckItemStatus`, `CheckStatus`, `CheckEventType`), porque o percentual depende de saber quais status contam como concluídos. Eles **não** passam pelo `JsonStringEnumConverter`: circulam na API e no banco como os códigos do glossário (`em-andamento`, `nao-se-aplica`, `item-added`) via `CheckCodes` no Domain. DTOs de checagem carregam `string`.
- **Relógio injetado**: código novo que precisa de "agora" recebe `TimeProvider` (registrado como `TimeProvider.System`); testes usam `FixedTimeProvider` dos test doubles.
- Exceções de regra de negócio: `DomainConflictException` → controller devolve 409 com a mensagem. Entidade referenciada inexistente: `KeyNotFoundException` → 404 (ou 400 quando é input do cliente, como `squadId` no `POST /api/apps`).
- Em `CheckService`, como a mesma chamada pode ter os dois casos (checagem inexistente e app inexistente na lista), input inválido do cliente é `ArgumentException` → 400 e `KeyNotFoundException` fica só para o recurso endereçado na URL → 404. Mensagens de `ArgumentException` sem `paramName`, para não vazar "(Parameter '…')" na resposta.
- **Integração externa = porta em `Application` + implementação em `Infrastructure`**, devolvendo tipos nossos (`IRepositoryDiscoverySource` → `DiscoveredRepositoryInfo`), nunca o JSON do provedor. Há sempre uma versão fake com os casos difíceis.
- **Credencial só em configuração local** (`dotnet user-secrets` / variável de ambiente), nunca em arquivo versionado. Mensagens de erro de integração são **montadas pela nossa classe** — sem corpo de resposta nem cabeçalhos — e há teste que prova que o token não aparece. A ferramenta recusa rodar sem credencial.
- **Nenhum teste automatizado usa rede**: o cliente HTTP é testado com `HttpMessageHandler` falso e JSON no formato real.
- **O dado do scan nasce com `source = GITHUB_SCAN` e sugestões nunca são gravadas**: papel, nome e confiança são calculados na leitura (`RepositoryNameConvention`); só o que a pessoa confirma vira dado do catálogo. O scan nunca cria `App`.
- **`ScanService` não entra em `AddCheckAppsApplication`**: depende de uma `IRepositoryDiscoverySource`, que só a ferramenta de scan registra, e a validação de DI do ambiente Development quebraria a API na subida. `TriageService` (que a API usa) entra normalmente.
- **Guarda de remoção de App tem duas condições**: sem snapshots e fora de qualquer checagem (ADR-005 + spec 003).

## Testes

- **Unitários usam fakes escritos à mão** (`tests/CheckApps.UnitTests/TestDoubles/FakeRepositories.cs`), não Moq/NSubstitute. Se precisar de comportamento novo no fake, adicione lá — os fakes têm armazenamento real em dicionário para que regras de colisão/remoção sejam testáveis.
- **Testes de integração usam `IClassFixture<SqliteFixture>`**: cada classe ganha um arquivo de banco temporário próprio. Sem Docker, sem estado compartilhado, sem collection fixture.
- Testes de integração continuam usando ids com `Guid.NewGuid()`, por hábito e isolamento — embora com banco por classe não seja mais estritamente necessário.

## Ferramentas

- Antes de `dotnet build`, garanta que nenhuma API está rodando — o processo trava os DLLs (`MSB3027`). `netstat -ano | grep :5232` e `taskkill //PID <pid> //F`.
- Seed é idempotente (`PutItem` sobrescreve). Rode quantas vezes quiser.
