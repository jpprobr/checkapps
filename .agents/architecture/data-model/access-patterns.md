# Registro de access patterns

> **Em transição (2026-10-04)**: o ADR-009 decidiu migrar para SQLite (spec 004). A coluna de fit abaixo descreve o DynamoDB e deve ser reescrita ao fim da migração — vários ❌ deixam de ser impossíveis. A análise que levou à troca está em `../retrospectives/2026-10-dynamodb-single-table.md`.

Índice único de todo padrão de acesso que o sistema suporta, como é resolvido no DynamoDB e quão bem cabe. **Regra de manutenção**: toda spec que cria ou muda um padrão adiciona ou edita a linha aqui, no mesmo commit. Antes de propor um GSI novo, um `Scan` ou uma desnormalização, consulte esta tabela e o ADR-002.

**Critério de fit em dez segundos**: uma entidade e seus filhos (partição, `begins_with`, série temporal) → ✅. Cruzar entidades (join, agregação transversal, filtro por atributo de outra entidade) → o DynamoDB empurra para memória ou para índice novo → ⚠️ ou ❌.

Chaves e índices: `dynamodb-single-table.md`. Fonte da verdade: `Keys.cs` / `TableSchema.cs`.

## Spec 001 — MVP core (implementada)

| # | Padrão | Resolução | Fit | Observação |
|---|---|---|---|---|
| 1 | Listar squads | Query GSI1 `PK="SQUAD"` | ⚠️ | Partição única; ok em dezenas, anti-padrão em milhares |
| 2 | Buscar uma squad | GetItem `SQUAD#{id}/METADATA` | ✅ | |
| 3 | Apps de uma squad, com estado atual | Query GSI1 `PK="SQUAD#{id}"`, `begins_with APP#` | ✅ | Uma query graças à desnormalização (#7) |
| 4 | Detalhe de um app | GetItem `APP#{id}/METADATA` | ✅ | |
| 5 | Repositórios de um app | Query `PK=APP#{id}`, `begins_with REPO#` | ✅ | |
| 6 | Projetos Sonar de um app | Query `PK=APP#{id}`, `begins_with SONARPROJECT#` | ✅ | |
| 7 | Quality assessment atual | Atributos desnormalizados no item App | ⚠️ | Rápido de ler; invariante mantida só por `QualitySnapshotService` (ADR-004) |
| 8 | Histórico completo de um app | Query `PK=APP#{id}`, `begins_with SNAPSHOT#` | ✅ | SK cronológico |
| 9 | Histórico num período | Query `SK BETWEEN SNAPSHOT#{from} AND SNAPSHOT#{to}#￿` | ✅ | |
| 10 | Situação atual de todos os apps da squad | = #3 | ✅ | |
| 11 | Evolução agregada da squad | #3 + #9 por app, agregado em memória | ⚠️ | N+1 na Application layer; em SQL seria um `GROUP BY`. Pré-agregar só se virar gargalo |
| 12 | Apps por Quality Level | Query GSI2 `PK="QUALITYLEVEL#{level}"` | ⚠️ | Precisou de um GSI inteiro; em SQL seria um `WHERE` |
| 13 | Registrar snapshot | `TransactWriteItems`: Put + Update condicional do App | ✅ | Atomicidade no banco; backfill fora de ordem seguro |
| 14 | Snapshot mais recente | #8 com `ScanIndexForward=false, Limit=1` | ✅ | |

## Spec 002 — Cadastro de Squads e Apps (implementada)

| # | Padrão | Resolução | Fit | Observação |
|---|---|---|---|---|
| 15 | Criar squad/app com id único | `PutItem` + `attribute_not_exists(PK)`, retry de slug | ✅ | Sem "checar-depois-gravar" (ADR-006) |
| 16 | Editar squad/app | GetItem → mutar → `PutItem` completo | ⚠️ | Obriga carregar-mutar-salvar (regra em `rules/backend.md`) |
| 17 | Mover app de squad | = #16; `ToItem` recalcula GSI1PK | ✅ | Reposiciona no índice sem passo extra |
| 18 | Remover app (sem histórico) | Query da partição + `BatchWriteItem` | ✅ | Repositório não filtra `SNAPSHOT#`; guarda é do serviço (ADR-005) |
| 19 | Remover squad (sem apps) | `DeleteItem` | ✅ | Guarda "sem apps" = #3 no serviço |
| 20 | Links de um app (CRUD) | `PK=APP#{id}`, `begins_with APPLINK#` | ✅ | |

## Spec 003 — Checagens (em análise)

| # | Padrão | Resolução prevista | Fit | Observação |
|---|---|---|---|---|
| 21 | Listar checagens (abertas/todas) | Query GSI1 `PK="CHECK"` | ⚠️ | Segunda partição única; mesma limitação de #1 |
| 22 | Checagem com todos os itens | Query `PK=CHECK#{id}` | ✅ | METADATA + `ITEM#…` numa partição |
| 23 | Checagens de um app | Query GSI1 `PK="APP#{id}"`, `begins_with CHECK#` | ✅ | Reaproveita GSI1 com espaço de valor novo; sem índice novo |
| 24 | Adicionar/remover app numa checagem | `PutItem`/`DeleteItem` em `ITEM#{appId}` | ✅ | Idempotente por construção |
| 25 | Status/observação de um item | `UpdateItem` | ✅ | |
| 26 | Percentual e contagem por status | Em memória a partir de #22 | ✅ | Itens já carregados |
| 27 | Nome/squad do app na linha da checagem | Desnormalizar no item **ou** `BatchGetItem` | ⚠️ | Segunda desnormalização candidata — gatilho 2 do ADR-002 se desatualizar na prática |
| 28 | Apps ainda não incluídos numa checagem | #3 (ou todos) menos #22, no cliente | ⚠️ | Ok com dezenas de apps |
| 29 | Remover app → apagar seus itens de checagem | #23 + `BatchWriteItem` | ✅ | Cascata no serviço (spec 003, FR-014) |

## Fora de escopo hoje — e por quê (registro para não redescobrir)

| Padrão | Fit | Por quê não |
|---|---|---|
| "O que está pendente para a minha squad em todas as checagens" | ❌ | Check → Item → App → Squad: dois joins; tudo em memória. **Gatilho 3 do ADR-002** |
| Painel com percentual de todas as checagens abertas | ❌ | Sem agregação transversal; carregaria todos os itens de todas |
| Checagem por regra ("todos os apps da squad X, sempre") | ❌ | Avaliar filtro no banco a cada leitura; escopada para fora da 003 por isso |
| Apps sem nenhum snapshot | ❌ | Negação/diferença de conjunto = `Scan` |
| Solução, topologia, dependência entre apps | ❌ | Grafo com join — pior caso de tabela única. **Gatilho 4** |
| Busca livre por nome/tag entre entidades | ❌ | Sem full-text; hoje é filtro client-side sobre a lista já carregada |
