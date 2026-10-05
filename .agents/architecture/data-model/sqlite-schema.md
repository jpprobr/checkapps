# Modelo de dados — SQLite (relacional)

> **Fonte da verdade**: `src/CheckApps.Infrastructure/Sqlite/schema.sql`. Este documento explica as decisões; o arquivo é o que cria o banco. Se divergirem, o `schema.sql` está certo.
>
> O desenho anterior (DynamoDB single-table) está em `dynamodb-single-table.md`, mantido como histórico — ver ADR-009 e a retrospectiva do experimento.

Banco em arquivo único. Caminho: `Sqlite:DatabasePath` — absoluto é usado como está (produção); relativo resolve contra `%LOCALAPPDATA%/CheckApps`, **não** contra o diretório de trabalho (o ASP.NET Core troca o CWD no startup e o console do seed não; depender disso faria API e seed abrirem bancos diferentes).

## Tabelas

| Tabela | Chave | Observações |
|---|---|---|
| `squads` | `id` | |
| `apps` | `id` | `squad_id` → `squads(id)`. **Sem colunas de estado de qualidade** |
| `app_tags` | `(app_id, tag)` | `app_id` → `apps` CASCADE |
| `repositories` | `id` | `app_id` → `apps` CASCADE |
| `sonar_projects` | `id` | `app_id` → `apps` CASCADE |
| `app_links` | `id` | `app_id` → `apps` CASCADE |
| `quality_snapshots` | `id` | `app_id` → `apps` CASCADE; métricas em colunas, não JSON |

Índices: `apps(squad_id)` e `quality_snapshots(app_id, collected_at DESC)` — o segundo serve tanto o histórico por período quanto a busca do snapshot mais recente.

## Decisões que não são óbvias no DDL

**Estado de qualidade atual é derivado, não armazenado** (ADR-009 revoga a cláusula 3 do ADR-004). "App com seu snapshot mais recente" é:

```sql
LEFT JOIN quality_snapshots s ON s.id = (
    SELECT id FROM quality_snapshots WHERE app_id = a.id
    ORDER BY collected_at DESC, sonar_project_id DESC LIMIT 1)
```

O desempate por `sonar_project_id` espelha a antiga ordenação da SK (`SNAPSHOT#{iso}#{sonarProjectId}`), para o caso de dois projetos Sonar coletarem no mesmo instante. A subconsulta correlacionada é portável para Postgres (lá, `LATERAL` seria uma alternativa).

Consequência boa: a armadilha do `SaveAsync` sumiu estruturalmente — não há como apagar o Quality Level ao salvar um App, porque `SaveAsync` não escreve esses campos.

**Tags em tabela normalizada**, não CSV/JSON: o backlog prevê filtrar por convenção de tag (`solucao:`, `topologia:`). A leitura é por consulta separada e costura em C# — **não** `GROUP_CONCAT`, cuja equivalente no Postgres é `STRING_AGG` (portabilidade, FR-006).

**`quality_snapshots.sonar_project_id` não tem FK**, de propósito: o DynamoDB nunca validou essa referência e existe massa (testes, backfills) apontando para projetos inexistentes. Adicionar a constraint seria mudança de comportamento, não migração. Candidato a spec futura de integridade.

**`PRAGMA foreign_keys` é por conexão e vem desligado.** `SqliteConnectionFactory` liga em toda conexão, junto com `journal_mode = WAL` e `busy_timeout`. Sem isso, as cascatas declaradas aqui simplesmente não acontecem.

**Datas em TEXT ISO-8601 UTC** (`IsoTimestamp`), exatamente como no DynamoDB — é o que mantém a serialização da API idêntica e o schema portável.

## Evolução do schema

Hoje: apenas `schema.sql`, idempotente, aplicado no bootstrap (`SqliteSchemaBootstrapper`). Scripts numerados de migration passam a existir **a partir do primeiro deploy com dados reais** — antes disso, recriar é mais barato que versionar.

`ALTER TABLE` do SQLite é limitado: `ADD COLUMN`, `RENAME`, e `DROP COLUMN` só a partir da 3.35. Mudança de tipo ou constraint exige criar tabela nova, copiar e trocar.

## Produção e migração futura

Em produção (ECS, instância única): arquivo em disco efêmero + Litestream replicando o WAL para S3, com restore no boot. Deploy stop-then-start para garantir escritor único. Detalhes na spec 004 e no ADR-009.

Quando o modelo estabilizar, este `schema.sql` é o que será submetido ao processo corporativo de modelagem para criar o Postgres — por isso ele é mantido legível e portável, e não um histórico de `ALTER`s.
