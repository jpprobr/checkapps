# Modelo de dados — DynamoDB single-table

> **Fonte da verdade**: `src/CheckApps.Infrastructure/DynamoDb/Keys.cs` e `TableSchema.cs`. Este documento espelha o código para leitura rápida; se divergir, o código está certo e este arquivo precisa ser corrigido.

Tabela única `CheckApps` (nome configurável via `DynamoDb:TableName`). Chave composta `PK` (hash) + `SK` (range), ambos string. Dois GSIs, cada um justificado por um access pattern explícito — não há índice "por precaução". O índice de todos os padrões, com o fit de cada um, está em `access-patterns.md`; os gatilhos para revisitar o DynamoDB estão no ADR-002.

## Tabela base

| Entidade | `PK` | `SK` | Observação |
|---|---|---|---|
| Squad | `SQUAD#{squadId}` | `METADATA` | |
| App | `APP#{appId}` | `METADATA` | carrega estado atual desnormalizado (abaixo) |
| Repository | `APP#{appId}` | `REPO#{repoId}` | filho do App |
| SonarProject | `APP#{appId}` | `SONARPROJECT#{sonarProjectId}` | filho do App |
| AppLink | `APP#{appId}` | `APPLINK#{linkId}` | filho do App |
| QualitySnapshot | `APP#{appId}` | `SNAPSHOT#{collectedAtIso}#{sonarProjectId}` | imutável; `collectedAtIso` é ISO-8601 UTC (`"O"`), ordena lexicograficamente = cronologicamente |

Tudo que pertence a um App vive na **mesma partição** (`APP#{appId}`), diferenciado pelo prefixo do `SK`. Uma `Query` por `PK` + `begins_with(SK, prefixo)` lista cada tipo de filho; uma `Query` só por `PK` traz a partição inteira (é o que `DeleteAllAsync` usa).

## GSI1 — relação Squad → App e listagem de squads

| Entidade | `GSI1PK` | `GSI1SK` |
|---|---|---|
| Squad | `SQUAD` (constante) | `SQUAD#{squadId}` |
| App | `SQUAD#{squadId}` | `APP#{appId}` |

- Listar squads: `Query GSI1 WHERE GSI1PK = "SQUAD"` — partição única, adequada a poucas squads (limitação conhecida, ver ADR-002).
- Listar apps de uma squad: `Query GSI1 WHERE GSI1PK = "SQUAD#{squadId}" AND begins_with(GSI1SK, "APP#")`.
- Mover um app de squad = trocar `App.SquadId` e salvar; `ToItem` recalcula `GSI1PK` e o `PutItem` completo reposiciona o item no índice sem passo extra.

## GSI2 — apps por Quality Level (esparso)

| Entidade | `GSI2PK` | `GSI2SK` |
|---|---|---|
| App (só quando tem `currentQualityLevel`) | `QUALITYLEVEL#{level}` | `APP#{appId}` |

- Só existe no item App, e só depois do primeiro snapshot. Apps sem histórico não aparecem no índice.
- Atualizado na mesma transação que grava um snapshot novo (abaixo).

## Estado desnormalizado no item App

Para responder "situação atual de todos os apps da squad" com uma única `Query` no GSI1, o item `APP#{id} / METADATA` carrega:

`currentQualityLevel`, `currentMetrics` (map), `lastSnapshotAt`, `lastSnapshotId`, `assessmentRulesVersion`, e as chaves `GSI2PK`/`GSI2SK`.

**Invariante**: esses campos só são escritos por `QualitySnapshotRepository.AddAsync`, dentro de um `TransactWriteItems`:

1. `Put` do item snapshot;
2. `Update` do item App com `ConditionExpression: attribute_not_exists(lastSnapshotAt) OR lastSnapshotAt < :collectedAt`.

Se a condição falha (snapshot mais antigo que o atual — backfill fora de ordem), a transação é cancelada e o código grava **só o snapshot** com um `PutItem` avulso, sem tocar o estado atual. O snapshot histórico é preservado; o "atual" não regride.

**Consequência para edição** (`AppRepository.SaveAsync`): é um `PutItem` completo construído a partir de **todos** os campos do objeto `App`, inclusive os desnormalizados. Editar = `GetAsync` → mutar → `SaveAsync` do mesmo objeto. Um `App` construído do zero com só os campos do formulário sobrescreve o item sem esses campos e o app "perde" seu Quality Level. Coberto por teste (`AppServiceTests.UpdateAsync_CanMoveAppToAnotherSquad_AndPreservesCurrentQualityState`).

## Escritas condicionais e remoções

- **Criar Squad/App**: `PutItem` com `ConditionExpression: attribute_not_exists(PK)`. Retorna `false` em colisão; `SlugIdGenerator` tenta `slug`, `slug-2`, `slug-3`… A atomicidade fica no DynamoDB, não em "checar depois gravar".
- **Remover App** (`DeleteAllAsync`): `Query` da partição inteira + `BatchWriteItem` em lotes de 25. **Não filtra `SNAPSHOT#`** — apagaria o histórico. A guarda "só remove se não houver snapshot" é responsabilidade de `AppService.DeleteAsync`, não do repositório.
- **Remover Squad** (`DeleteAsync`): `DeleteItem` simples. A guarda "só se não houver apps" é de `SquadService.DeleteAsync`.
- **Links** (`SaveLinkAsync`): `PutItem` pelo `SK` — serve para criar e editar.

## Consultas por intervalo de tempo

Histórico de um app num período: `Query PK = "APP#{id}" AND SK BETWEEN "SNAPSHOT#{fromIso}" AND "SNAPSHOT#{toIso}#￿"`. O `￿` no limite superior inclui todos os `sonarProjectId` daquele instante. Snapshot mais recente: mesma query com `begins_with(SK, "SNAPSHOT#")`, `ScanIndexForward = false`, `Limit = 1`.

## Exemplos de item

```json
{ "PK": "SQUAD#sq-1", "SK": "METADATA",
  "GSI1PK": "SQUAD", "GSI1SK": "SQUAD#sq-1",
  "id": "sq-1", "name": "Squad Pagamentos", "status": "active" }

{ "PK": "APP#app-1", "SK": "METADATA",
  "GSI1PK": "SQUAD#sq-1", "GSI1SK": "APP#app-1",
  "GSI2PK": "QUALITYLEVEL#A", "GSI2SK": "APP#app-1",
  "id": "app-1", "squadId": "sq-1", "name": "checkout-api", "tags": ["demo"],
  "currentQualityLevel": "A", "lastSnapshotAt": "2026-08-01T00:00:00.0000000+00:00",
  "currentMetrics": { "coverage": 82, "bugs": 1, "reliability": "A", "..." : "..." } }

{ "PK": "APP#app-1", "SK": "APPLINK#d552c455…",
  "id": "d552c455…", "appId": "app-1", "description": "Confluence", "uri": "https://…" }

{ "PK": "APP#app-1", "SK": "SNAPSHOT#2026-08-01T00:00:00.0000000+00:00#app-1-sonar-1",
  "collectedAt": "2026-08-01T00:00:00.0000000+00:00", "sonarProjectId": "app-1-sonar-1",
  "source": "SONAR_MOCK", "qualityLevel": "A", "assessmentRulesVersion": "v1",
  "metrics": { "coverage": 82, "bugs": 1, "vulnerabilities": 0, "codeSmells": 30,
               "duplications": 5, "reliability": "A", "maintainability": "A", "security": "A" } }
```

## Ambientes

- **Local**: DynamoDB Local em Docker (`docker-compose.yml`), `-inMemory` — dados somem a cada reinício do container. Client usa credenciais fictícias `local/local` e `ServiceUrl = http://localhost:8000`.
- **AWS real**: deixar `DynamoDb:ServiceUrl` vazio; o SDK usa a cadeia padrão de credenciais (`aws configure`, variáveis de ambiente, etc.). Nenhuma credencial no código ou no git.
- Testes de integração usam a tabela `CheckAppsIntegrationTests`, recriada do zero pela `DynamoDbLocalFixture` (uma única instância via `[Collection]`).
