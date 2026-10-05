# Registro de access patterns

> **Pós-migração para SQLite (2026-10-04)**: com banco relacional, *todo* padrão desta tabela é atendido por uma consulta comum — join, agregação e filtro por qualquer coluna deixaram de ser restrição de banco. A coluna "Fit" abaixo é mantida como **registro histórico** de como cada padrão era resolvido no DynamoDB; ela não descreve mais uma limitação do sistema.
>
> O que este registro continua servindo: listar os padrões que o produto precisa atender (a disciplina access-pattern-first segue valendo, com outro custo de erro) e lembrar quais consultas foram escopadas para fora por decisão de **produto** — não mais por limitação técnica.

Índice único de todo padrão de acesso que o sistema suporta, como é resolvido no DynamoDB e quão bem cabe. **Regra de manutenção**: toda spec que cria ou muda um padrão adiciona ou edita a linha aqui, no mesmo commit. Antes de propor um GSI novo, um `Scan` ou uma desnormalização, consulte esta tabela e o ADR-002.

**Critério de fit (era DynamoDB, histórico)**: uma entidade e seus filhos → ✅; cruzar entidades → ⚠️ ou ❌. Em SQL, essa distinção deixou de existir.

Schema vigente: `sqlite-schema.md`. Fonte da verdade: `schema.sql`.

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
| 11 | Evolução agregada da squad | #3 + #30, agregado na Application | ✅ | Era N+1 no DynamoDB; hoje são duas consultas. A agregação fica na Application de propósito — a classificação de tendência é regra de negócio e precisa das linhas |
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

## Spec 004 — Migração para SQLite (implementada)

| # | Padrão | Resolução | Fit | Observação |
|---|---|---|---|---|
| 30 | Histórico de todos os apps de uma squad | `quality_snapshots` com `INNER JOIN apps` e filtro de período | ✅ | Criado para eliminar o N+1 do padrão #11; não existia equivalente no DynamoDB |

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

## Fora de escopo hoje — agora por decisão de produto, não de banco

Com SQLite, todos os itens abaixo são tecnicamente triviais. Seguem fora de escopo porque ninguém pediu ainda — o que muda é que incluí-los passou a ser decisão de produto, e não mais "o banco não deixa". O ❌ registra como era no DynamoDB.

| Padrão | Fit | Por quê não |
|---|---|---|
| "O que está pendente para a minha squad em todas as checagens" | ❌ | Check → Item → App → Squad: dois joins; tudo em memória. **Gatilho 3 do ADR-002** |
| Painel com percentual de todas as checagens abertas | ❌ | Sem agregação transversal; carregaria todos os itens de todas |
| Checagem por regra ("todos os apps da squad X, sempre") | ❌ | Avaliar filtro no banco a cada leitura; escopada para fora da 003 por isso |
| Apps sem nenhum snapshot | ❌ | Negação/diferença de conjunto = `Scan` |
| Solução, topologia, dependência entre apps | ❌ | Grafo com join — pior caso de tabela única. **Gatilho 4** |
| Busca livre por nome/tag entre entidades | ❌ | Sem full-text; hoje é filtro client-side sobre a lista já carregada |
