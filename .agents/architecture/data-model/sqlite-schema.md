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
| `repositories` | `id` | `app_id` → `apps` CASCADE. Desde a spec 005: `sigla`, `sigla_app` (**único quando presente**), `role` e `source` |
| `sonar_projects` | `id` | `app_id` → `apps` CASCADE |
| `app_links` | `id` | `app_id` → `apps` CASCADE |
| `quality_snapshots` | `id` | `app_id` → `apps` CASCADE; métricas em colunas, não JSON |
| `discovered_repositories` | `id` (`github:{externalId}`) | Caixa de entrada do leitor de GitHub (spec 005). `UNIQUE (provider, external_id)`; **sem FK** para `repositories` |
| `checks` | `id` (slug) | Checagens (spec 003). `status` = `open`/`closed`; datas de calendário em TEXT `yyyy-MM-dd` |
| `check_items` | `(check_id, app_id)` | `check_id` → `checks` e `app_id` → `apps`, **ambas sem cascata** |
| `check_events` | `id` | `check_id` → `checks` CASCADE; `app_id` **sem FK**. Só de acréscimo |
| `repository_analyses` | `id` | Uma execução de análise (spec 013, ADR-011). `repository_id` → `repositories` CASCADE. Histórico: a **corrente** é a mais recente com `status <> 'falha'`, derivada por consulta |
| `host_externals` | `id` | Host marcado como **externo ao catálogo** por decisão humana (spec 019): outra squad, terceiro ou legado que ninguém vai cadastrar como app. **Sem** FK (sobrevive às análises); `host_key` = `host[:porta]` em minúsculas, **único**, pode ser modelo com `{env}`; `name` livre opcional; `origin = HUMAN`. Exclusão mútua com um `host_links` confirmado do mesmo host é regra do serviço. |
| `host_links` | `id` | Vínculo host→app por **decisão humana** (spec 017): `app_id` → `apps` CASCADE; **sem** FK para análises ou integrações (sobrevive a elas). `host_key` = `host[:porta]` em minúsculas, pode ser modelo com `{env}`; `state` = `confirmado` \| `rejeitado`; `origin = HUMAN`. `UNIQUE (host_key, app_id)`; um `confirmado` por host é regra do serviço. |
| `repository_integrations` | `id` | Integrações declaradas de uma análise (spec 014): `analysis_id` → `repository_analyses` CASCADE. Host, porta, caminho base, seção, ambiente e estado já **normalizados** (nunca o valor bruto), com `origin`, `confidence` e scanner |
| `repository_dependencies` | `id` | Fatos de uma análise: `analysis_id` → `repository_analyses` CASCADE. Neutra à tecnologia (`ecosystem` + `kind` + `name`), com `origin`, `confidence` e scanner |

Índices (além dos das análises, abaixo): `apps(squad_id)`, `quality_snapshots(app_id, collected_at DESC)` — o segundo serve tanto o histórico por período quanto a busca do snapshot mais recente —, `check_items(app_id)` e `check_events(check_id, occurred_at DESC)`.

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

**FKs de `check_items` sem cascata, de propósito** (spec 003). Um app em checagem não pode ser removido (FR-014) e uma checagem com itens também não (FR-012). `AppService` e `CheckService` verificam antes e respondem 409 com mensagem; a FK é a segunda camada, que recusa mesmo se o serviço falhar. Consequência: `apps` deixou de ser apagável por cascata em qualquer situação — um teste de integração cobre a recusa.

**`discovered_repositories` guarda só o que não dá para derivar**: `status` é `new`, `ignored` ou `other-squad`. "Já está no catálogo" **não é um status** — é a existência de uma linha em `repositories` com o mesmo `(provider, external_id)`; assim, remover um app não deixa o descoberto num estado mentiroso. Sem FK porque o descoberto precede e sobrevive ao vínculo. O upsert atualiza os dados observados e `last_seen_at`, e **nunca** toca `status` nem `first_seen_at`.

**`repositories.sigla_app` é único só quando presente** (índice único parcial `ux_repositories_sigla_app`): a sigla-app identifica um repositório, e repositórios de infra não têm. O índice é criado pelo `SqliteSchemaBootstrapper`, não pelo `schema.sql`, porque depende de uma coluna que bancos antigos ainda não têm.

**`check_events` é histórico só de acréscimo**, gravado na mesma transação da mudança que registra (fechar, reabrir, entrada e saída de app, mudança de status). `app_id` sem FK pelo mesmo motivo de `quality_snapshots.sonar_project_id`: o evento é um fato passado e precisa sobreviver à remoção do app. Cascateia com a checagem, que só pode ser removida vazia. Status e tipos são gravados como os códigos do glossário (`em-andamento`, `item-added`), não como nomes de enum.

**Análises de repositório são snapshots, como `quality_snapshots`** (spec 013). Cada execução da ferramenta `CheckApps.Analyze` sobre um repositório cujo commit mudou grava uma linha em `repository_analyses` e as suas em `repository_dependencies`; a mesma execução sobre o mesmo commit não grava nada. A análise **corrente** nunca é armazenada — é a subconsulta `SELECT id FROM repository_analyses WHERE repository_id = r.id AND status <> 'falha' ORDER BY analyzed_at DESC, id DESC LIMIT 1` —, e uma falha (`status = 'falha'`) fica no histórico sem esconder a última análise boa. `status` ∈ `ok | parcial | sem-scanner | falha`; `warnings` é texto, um aviso por linha.

**`repository_integrations` guarda só o endereço normalizado** (spec 014, FR-004/FR-013). Usuário, senha, query e fragmento de URL são descartados no **núcleo** antes de gravar (`IntegrationNormalizer`); connection strings nunca são lidas. `host` pode ser um **modelo** com `{env}` (`state = parametrizada`) ou nulo (`state = nao-resolvida`: valor vazio ou com `${VAR}`); `port` só quando não é a padrão do esquema; `reference` é a seção de configuração (`IPagamentosServiceCliente`) e `config_key` a chave completa. `environment` é o nome do ambiente **como está no arquivo** (`Homolog`) e `environment_kind` a categoria normalizada (`dev` | `hom` | `prod`), nula se o nome é desconhecido. Pertence à mesma análise dos pacotes (um retrato por commit); a identidade do host como entidade (apelidos, vínculo com app) é da fatia seguinte. Índices `(analysis_id)`, `(lower(host))` e `(lower(reference))`.

**`host_links` guarda decisão, não fato lido** (spec 017). Fica fora das análises: reanalisar não altera nem apaga vínculos; só a remoção do app os leva (cascata). A chave do host é texto (`host[:porta]`, pode ter `{env}`), sem FK para `repository_integrations`: o vínculo pode ser feito antes de o host aparecer numa análise e continua quando ele some ("não visto na última análise" é derivado). Ver o adendo da spec 017 no ADR-011.

**`host_externals` é a outra decisão humana sobre um host** (spec 019): tabela própria porque `host_links.app_id` é obrigatório com FK. Mesmo ciclo de vida do vínculo (reanalisar não apaga). Ver o adendo da spec 019 no ADR-011.

**`repository_dependencies` guarda só fatos, nunca conteúdo de arquivo** (FR-011). `version` é a versão **declarada** (pode ser `$(Propriedade)` não resolvida, `13.*` ou um intervalo, sempre com `confidence` e `confidence_note` em texto); `origin` é `PARSER` hoje e `RULE`/`AI` só entrarão como sugestão. "Pacote interno" **não é coluna**: é calculado na leitura a partir de `Analysis:InternalPackagePrefixes`, para que trocar o prefixo valha também para o histórico. Índice `(kind, ecosystem, lower(name))` serve a busca de pacote sem diferença de caixa.

## Evolução do schema

Hoje: `schema.sql`, idempotente, aplicado no bootstrap (`SqliteSchemaBootstrapper`), **mais um passo aditivo**: `CREATE TABLE IF NOT EXISTS` não acrescenta colunas a uma tabela existente, então o bootstrapper confere `pragma_table_info` e roda `ALTER TABLE … ADD COLUMN` para as colunas listadas em `AdditiveColumns` (hoje as 4 de `repositories`). É um paliativo — só `ADD COLUMN`, nunca recria nem converte — e há teste sobre um banco antigo. **Toda coluna nova entra no `CREATE TABLE` do `schema.sql` E em `AdditiveColumns`.** Scripts numerados de migration passam a existir **a partir do primeiro deploy com dados reais** — antes disso, recriar é mais barato que versionar.

`ALTER TABLE` do SQLite é limitado: `ADD COLUMN`, `RENAME`, e `DROP COLUMN` só a partir da 3.35. Mudança de tipo ou constraint exige criar tabela nova, copiar e trocar.

## Produção e migração futura

Em produção (ECS, instância única): arquivo em disco efêmero + Litestream replicando o WAL para S3, com restore no boot. Deploy stop-then-start para garantir escritor único. Detalhes na spec 004 e no ADR-009.

Quando o modelo estabilizar, este `schema.sql` é o que será submetido ao processo corporativo de modelagem para criar o Postgres — por isso ele é mantido legível e portável, e não um histórico de `ALTER`s.
