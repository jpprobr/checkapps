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

## Spec 003 — Checagens (implementada)

Revistos para SQL em 2026-10-12. Os ⚠️ da versão DynamoDB (partição única, desnormalização de nome do app, diferença calculada no cliente) deixaram de existir. Detalhes na spec.

| # | Padrão | Resolução prevista | Fit | Observação |
|---|---|---|---|---|
| 21 | Listar checagens (abertas/todas) com contagens | `checks` + `LEFT JOIN check_items` + `GROUP BY` | ✅ | |
| 22 | Checagem com todos os itens | `check_items` + `JOIN apps` por `check_id` | ✅ | |
| 23 | Checagens de um app | `check_items` + `JOIN checks` por `app_id` | ✅ | Índice em `check_items(app_id)` |
| 24 | Adicionar/retirar app numa checagem | `INSERT … ON CONFLICT DO NOTHING` / `DELETE`, com evento | ✅ | Idempotente; portável para Postgres |
| 25 | Status/observação de um item | `UPDATE`, com evento se o status mudou | ✅ | |
| 26 | Percentual e contagem por status | Domínio, a partir de #21 ou #22 | ✅ | Regra de percentual é função pura |
| 27 | Nome/squad do app na linha da checagem | Join em #22 | ✅ | Sem desnormalização |
| 28 | Apps ainda não incluídos numa checagem | `apps` + `NOT EXISTS` em `check_items` | ✅ | No servidor |
| 29 | Guarda: app em checagem não é removido | `EXISTS` em `check_items` por `app_id` + FK sem cascata | ✅ | Substitui a cascata prevista antes (decisão de 2026-10-12) |
| 31 | Histórico de uma checagem | `check_events` por `check_id`, mais recente primeiro | ✅ | Índice em `check_events(check_id, occurred_at)` |

## Spec 005 — Leitor de GitHub, fase 1 (implementada)

| # | Padrão | Resolução | Fit | Observação |
|---|---|---|---|---|
| 32 | Upsert de descoberto por `(provider, external_id)` | `SELECT` + `INSERT`/`UPDATE` em transação | ✅ | Lê antes para classificar novo/atualizado/inalterado (o scan reporta); **nunca** toca `status` nem `first_seen_at`. `ON CONFLICT` não informaria o resultado |
| 33 | Caixa de entrada por status e sigla, sem os já catalogados | `discovered_repositories` com `NOT EXISTS` em `repositories` | ✅ | Índice em `(status, sigla)` |
| 34 | "Já está no catálogo?" | `EXISTS` em `repositories` por `(provider, external_id)` | ✅ | É o que define o estado; não há status duplicado |
| 35 | Sigla-app em uso? | `EXISTS` em `repositories` por `sigla_app` | ✅ | Índice único parcial é a rede de segurança (FR-013) |
| 36 | Repositórios de um app, com sigla, sigla-app e papel | `repositories` por `app_id` | ✅ | Pré-existente; ganhou as colunas |

## Spec 013 — Dependências e versão do .NET (implementada)

| # | Padrão | Resolução | Fit | Observação |
|---|---|---|---|---|
| 37 | Análise corrente de um repositório | `repository_analyses` por `repository_id`, `status <> 'falha'`, mais recente (`LIMIT 1`) | ✅ | Índice `(repository_id, analyzed_at DESC)`. A mesma subconsulta é a base das demais leituras |
| 38 | Dependências de um app (por repositório) | `repositories` do app + análise corrente + fatos | ✅ | Inclui repositórios nunca analisados e a falha mais nova que a corrente |
| 39 | Quem usa o pacote X (versão, app, squad) | `repository_dependencies` ⨝ análise corrente ⨝ `repositories` ⨝ `apps` ⨝ `squads`, `lower(name) = @name` | ✅ | Índice `(kind, ecosystem, lower(name))`. Uma linha por projeto; o resumo de versões conta apps distintos |
| 40 | Pacotes internos em uso (sem nome) | os mesmos fatos de pacote, filtrados por prefixo **no serviço** | ✅ | O prefixo é configuração local; SQL não conhece a empresa |
| 41 | Framework por app e squad (repositórios ativos) | `apps` ⨝ `squads` ⟕ `repositories` ativos ⟕ fatos `framework` da corrente | ✅ | App sem informação volta com `Framework = NULL`; app inativo fica de fora |
| 42 | Repositórios do catálogo de uma sigla | `repositories` por `lower(sigla)` ⨝ `apps` ⟕ `squads` | ✅ | O que a análise percorre |
| 43 | Cobertura: repositórios nunca analisados e data da análise mais recente | `NOT EXISTS` sobre `repository_analyses` e `MAX(analyzed_at)` | ✅ | Mostrada na busca de pacote: a resposta pode estar incompleta |

## Spec 014 — Integrações declaradas (implementada)

| # | Padrão | Resolução | Fit | Observação |
|---|---|---|---|---|
| 44 | Integrações de um app (por repositório) | `repositories` do app + análise corrente (mesma subconsulta do #37) + `repository_integrations` por `analysis_id` | ✅ | Inclui repositório nunca analisado e a falha mais nova que a corrente; o serviço agrupa por seção (`reference`), arquivo base primeiro |
| 45 | Hosts chamados, com apps e squads | `repository_integrations` ⨝ análise corrente ⨝ `repositories` ⨝ `apps` ⨝ `squads`, `host IS NOT NULL` | ✅ | Agrupa por `host[:porta]` literal **no serviço**; `{env}` não é casado com `dev`/`hom` por palpite |
| 46 | Filtro por trecho do host | `lower(host) LIKE @pattern ESCAPE '\'` | ✅ | Curingas digitados (`%`, `_`) são escapados; índice `(lower(host))` serve ao igual, o "contém" varre as correntes (volume pequeno) |
| 47 | Quem chama o host X (detalhe) | o mesmo SQL do #45 filtrado por trecho; o serviço compara a chave exata (`host[:porta]`) | ✅ | Host inexistente → 404 |

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
