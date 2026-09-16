# Regras — backend (`src/**`, `tests/**`, `tools/**`)

Leia antes de editar código .NET. Só regras não-óbvias e específicas deste projeto; o resto é convenção C# normal.

## Persistência

- **Editar = carregar → mutar → salvar o mesmo objeto.** `AppRepository.SaveAsync` e `SquadRepository.SaveAsync` são `PutItem` completos. Construir um `App` novo só com os campos do formulário apaga `currentQualityLevel`, `currentMetrics`, `lastSnapshotAt` e o GSI2. Há teste que falha se você fizer isso.
- **Todo `QualitySnapshot` passa por `QualitySnapshotService.RegisterAsync`.** É o único caminho que calcula o Quality Level e atualiza o estado desnormalizado do App na mesma transação. Nunca chame `IQualitySnapshotRepository.AddAsync` direto de código de produto (o seed é a exceção deliberada).
- **Repositórios são primitivos "burros".** Regra de negócio (guardas de remoção, validação de squad existente, geração de id único) fica nos serviços de aplicação. `DeleteAllAsync` apaga a partição inteira, inclusive snapshots, se chamado direto — a proteção é de `AppService.DeleteAsync`.
- **Chaves só via `Keys.cs`.** Nunca monte `"APP#" + id` inline. Novo tipo de item = método novo em `Keys` + atualizar `.agents/architecture/data-model/dynamodb-single-table.md`.
- **Criar entidade com id gerado = `TryCreateAsync` (Put condicional) + `SlugIdGenerator`.** Não faça "checar se existe, depois gravar".
- **Sem `DynamoDBContext`.** Só o client de baixo nível com `Dictionary<string, AttributeValue>`. `AttributeValue.NULL` é `bool?` — compare com `!= true`, não com `!`.

## Domínio e aplicação

- `Id` é `init`-only em todas as entidades. `App.SquadId` é a única exceção com `set` (mover de squad é um requisito).
- Enums no domínio, strings na API: `QualityLevel` serializa como `"A"`, não `0` (`JsonStringEnumConverter` global). Ao adicionar enum novo, não precisa fazer nada; ao mudar a serialização, verifique o frontend.
- `Status` de Squad/App é `string?` livre no domínio; `"active"`/`"inactive"` é convenção da UI, não enum. Não introduza enum sem ADR.
- Exceções de regra de negócio: `DomainConflictException` → controller devolve 409 com a mensagem. Entidade referenciada inexistente: `KeyNotFoundException` → 404 (ou 400 quando é input do cliente, como `squadId` no `POST /api/apps`).

## Testes

- **Unitários usam fakes escritos à mão** (`tests/CheckApps.UnitTests/TestDoubles/FakeRepositories.cs`), não Moq/NSubstitute. Se precisar de comportamento novo no fake, adicione lá — os fakes têm armazenamento real em dicionário para que regras de colisão/remoção sejam testáveis.
- **Toda classe de teste de integração DynamoDB usa `[Collection(DynamoDbCollection.Name)]`**, nunca `IClassFixture<DynamoDbLocalFixture>`. Com `IClassFixture`, cada classe recria a mesma tabela em paralelo e derruba as outras. Já aconteceu.
- Testes de integração usam ids com `Guid.NewGuid()` para não colidir entre testes — a tabela é recriada só uma vez por execução da suíte.
- Testes de integração exigem DynamoDB Local no ar (`docker compose up -d`). Se falharem com `ResourceNotFoundException` ou erro de conexão, é isso.

## Ferramentas

- Antes de `dotnet build`, garanta que nenhuma API está rodando — o processo trava os DLLs (`MSB3027`). `netstat -ano | grep :5232` e `taskkill //PID <pid> //F`.
- Seed é idempotente (`PutItem` sobrescreve). Rode quantas vezes quiser.
