# CheckApps — MVP: Análise (Inventário + Health + Evolution)

## Contexto

O diretório `CheckApps` está vazio (sem código, sem git). Este é um projeto novo (greenfield) que evolui uma POC Python/Streamlit para uma aplicação web (.NET 10 + Angular + DynamoDB) capaz de mostrar a composição de apps de uma squad e a evolução histórica da qualidade dessas aplicações (dados Sonar, inicialmente mockados).

O brief do usuário pede explicitamente uma fase de análise antes de qualquer implementação, cobrindo: problem statement, domain model, access patterns, design do DynamoDB, design da API, IA do frontend, plano de implementação incremental e riscos/trade-offs. Este plano entrega essas 8 seções para validação antes de qualquer código ser escrito.

---

## 1. Problem Statement

QAs e coordenadores não têm hoje uma forma simples de visualizar, por squad, **quais aplicações existem** e **como sua qualidade evolui ao longo do tempo**. A POC atual mostra um snapshot pontual do Sonar, mas não preserva histórico. O CheckApps MVP resolve isso: uma tela por squad que responde "como estão meus apps?" e "estão melhorando ou piorando?", com histórico real de métricas e nível de qualidade.

Fora de escopo neste MVP: arquitetura, jornadas de negócio, operações, SDD, Harness, agentes/RAG — isso é visão futura, não este incremento.

## 2. Domain Model

```
Squad 1──N App 1──N Repository
             │
             └──0..N SonarProject 1──N QualitySnapshot
```

- **Squad**: id, name, description?, status?
- **App**: id interno estável (não é o nome do repo), name, squadId, description?, criticality?, tags[], links[], status. Pode ter identificadores externos (ex.: id corporativo) guardados como atributo, sem depender deles como chave.
- **Repository** (1:N a partir de App): id, appId, name, provider, url, externalId?, status. Suporta múltiplos repos por app (migração, legado+novo, coexistência).
- **SonarProject** (0:N a partir de App): id, appId, projectKey, name, url, externalId?, status, source. Não assume 1:1 com Repository.
- **QualitySnapshot**: id, appId, sonarProjectId, collectedAt (timestamp), source, métricas (coverage, bugs, vulnerabilities, codeSmells, duplications, reliability, maintainability, security), **qualityLevel** + **assessmentRulesVersion** (gravados no momento da coleta).

**Métrica vs Quality Assessment**: métricas são dados observados (ex. `coverage=72.4`). `qualityLevel` é derivado por um motor de regras (`IQualityAssessmentEngine`) separado do modelo de dados. O resultado da avaliação é gravado de forma imutável dentro de cada `QualitySnapshot` (junto com a versão da regra usada), então evoluir as regras no futuro não altera snapshots históricos já gravados.

## 3. Access Patterns

| # | Padrão | Como é resolvido |
|---|--------|-------------------|
| 1 | Listar squads | Query GSI1 `PK=SQUAD` |
| 2 | Buscar uma squad | GetItem tabela base `PK=SQUAD#{id}, SK=METADATA` |
| 3 | Listar apps de uma squad | Query GSI1 `PK=SQUAD#{squadId}, SK begins_with APP#` |
| 4 | Detalhes de um app | GetItem tabela base `PK=APP#{appId}, SK=METADATA` |
| 5 | Repositórios de um app | Query tabela base `PK=APP#{appId}, SK begins_with REPO#` |
| 6 | Projetos Sonar de um app | Query tabela base `PK=APP#{appId}, SK begins_with SONARPROJECT#` |
| 7 | Quality assessment atual de um app | Atributos desnormalizados no item App (METADATA) — sem query extra |
| 8 | Histórico de qualidade de um app | Query tabela base `PK=APP#{appId}, SK begins_with SNAPSHOT#` |
| 9 | Evolução de métricas num período | Query tabela base `PK=APP#{appId}, SK BETWEEN SNAPSHOT#{from} AND SNAPSHOT#{to}` |
| 10 | Situação atual de todos os apps de uma squad | Mesma query do #3 (App já carrega estado atual desnormalizado) |
| 11 | Evolução agregada da squad | Calculada na Application layer: busca apps da squad (#3) + histórico de cada um (#9) e agrega em memória — sem estrutura dedicada no MVP |
| 12 | Apps por Quality Level | Query GSI2 `PK=QUALITYLEVEL#{level}` |
| 13 | Registrar novo QualitySnapshot | `TransactWriteItems`: Put do snapshot + Update condicional do item App (estado atual + GSI2PK) |
| 14 | Snapshot mais recente de um app | Query tabela base `PK=APP#{appId}, SK begins_with SNAPSHOT#`, `ScanIndexForward=false, Limit=1` |

## 4. DynamoDB Design

Tabela única `CheckApps`. PK/SK na tabela base + 2 GSIs, cada um justificado por access patterns explícitos (sem índice "por precaução").

**Tabela base** — `PK` / `SK`:

| Entidade | PK | SK |
|---|---|---|
| Squad | `SQUAD#{squadId}` | `METADATA` |
| App | `APP#{appId}` | `METADATA` |
| Repository | `APP#{appId}` | `REPO#{repoId}` |
| SonarProject | `APP#{appId}` | `SONARPROJECT#{sonarProjectId}` |
| QualitySnapshot | `APP#{appId}` | `SNAPSHOT#{collectedAtIso}#{sonarProjectId}` |

**GSI1** (relação squad→app e listagem de squads) — `GSI1PK` / `GSI1SK`:
- Squad: `GSI1PK=SQUAD`, `GSI1SK=SQUAD#{squadId}`
- App: `GSI1PK=SQUAD#{squadId}`, `GSI1SK=APP#{appId}`

**GSI2** (apps por quality level, esparso — só existe no item App) — `GSI2PK` / `GSI2SK`:
- App: `GSI2PK=QUALITYLEVEL#{currentLevel}`, `GSI2SK=APP#{appId}`

**Item App carrega estado atual desnormalizado** (para resolver #7 e #10 sem query extra):
`currentQualityLevel`, `currentMetrics` (map), `lastSnapshotAt`, `lastSnapshotId`, `assessmentRulesVersion`.

**Exemplos de itens:**
```json
{"PK":"SQUAD#sq-1","SK":"METADATA","GSI1PK":"SQUAD","GSI1SK":"SQUAD#sq-1","name":"Squad Pagamentos"}

{"PK":"APP#app-1","SK":"METADATA","GSI1PK":"SQUAD#sq-1","GSI1SK":"APP#app-1",
 "GSI2PK":"QUALITYLEVEL#B","GSI2SK":"APP#app-1",
 "name":"checkout-api","currentQualityLevel":"B","lastSnapshotAt":"2026-08-01T00:00:00Z",
 "currentMetrics":{"coverage":78.0,"bugs":2}}

{"PK":"APP#app-1","SK":"SNAPSHOT#2026-08-01T00:00:00Z#sp-1",
 "collectedAt":"2026-08-01T00:00:00Z","sonarProjectId":"sp-1","source":"SONAR_MOCK",
 "metrics":{"coverage":78.0,"bugs":2,"vulnerabilities":0,"codeSmells":40},
 "qualityLevel":"A","assessmentRulesVersion":"v1"}
```

**Escrita de um novo snapshot (#13)**: `TransactWriteItems` com (a) `Put` do item snapshot e (b) `Update` do item App atualizando estado atual + `GSI2PK`, com `ConditionExpression` para só sobrescrever o estado atual se `collectedAt` novo > `lastSnapshotAt` existente (evita regressão ao inserir dados fora de ordem/backfill).

**Justificativas / limitações conhecidas** (ver seção 8 para detalhes): GSI1 usa uma partição única (`SQUAD`) para listar squads — ok na escala do MVP, não escalaria para milhares de squads. Padrão #11 (evolução agregada da squad) é resolvido em memória na Application layer, não no banco — mais simples agora, pode precisar de pré-agregação futura se o volume crescer.

> Nota de implementação: `GSI2SK` foi simplificado para `APP#{appId}` (sem o `SQUAD#{squadId}` prefixado do desenho inicial), já que o access pattern #12 não exige agrupar por squad e a chave menor evita precisar reler o squadId ao atualizar o GSI2 na escrita de um snapshot.

## 5. API Design (ASP.NET Core Web API)

```
GET  /api/squads
GET  /api/squads/{squadId}
GET  /api/squads/{squadId}/apps                     # inclui estado de qualidade atual (#3+#10)
GET  /api/squads/{squadId}/quality-evolution?from=&to=   # agregado (#11)

GET  /api/apps?qualityLevel={level}                 # (#12)
GET  /api/apps/{appId}
GET  /api/apps/{appId}/repositories
GET  /api/apps/{appId}/sonar-projects
GET  /api/apps/{appId}/quality                      # assessment atual (#7)
GET  /api/apps/{appId}/quality/history?from=&to=    # série histórica (#8/#9)

POST /api/apps/{appId}/quality-snapshots            # registra snapshot (#13) — usado pelo seed e por uma futura sync real do Sonar
```

## 6. Frontend — Information Architecture (Angular)

- **Dashboard da Squad** (`/squads/:squadId`) — tela inicial: contagem de apps, distribuição por Quality Level, tendência geral, listas "melhoraram" / "pioraram" / "atenção".
- **Lista de Apps** (`/squads/:squadId/apps`) — busca, filtro por squad e por Quality Level, ordenação, link para detalhe.
- **Detalhe do App** (`/apps/:appId`) — dados básicos, squad, repositórios, projetos Sonar, quality level atual, métricas atuais, gráfico de evolução histórica por métrica.

Componentes standalone, `SquadsApiService`/`AppsApiService` encapsulando chamadas HTTP, um gráfico SVG próprio (sem dependência externa) para a série temporal de Quality Level, mais uma tabela para o histórico detalhado de métricas.

## 7. Implementation Plan (incrementos verificáveis)

1. **Scaffold da solution**: `CheckApps.sln` com `CheckApps.Domain`, `CheckApps.Application`, `CheckApps.Infrastructure`, `CheckApps.Api`, `CheckApps.UnitTests`, `CheckApps.IntegrationTests`. Entidades de domínio como POCOs (Squad, App, Repository, SonarProject, QualitySnapshot). *Verificação: `dotnet build` limpo.* ✅
2. **Motor de Quality Assessment**: `IQualityAssessmentEngine` + implementação com regras simples versionadas, desacoplado de persistência. *Verificação: testes unitários cobrindo os critérios de classificação.* ✅
3. **Abstração Sonar**: `ISonarProvider` + `MockSonarProvider` gerando métricas determinísticas, mais um gerador de massa de dados demonstrando evolução positiva/negativa/estável (cenários Jun→Jul→Ago do brief). *Verificação: teste unitário validando a forma dos dados gerados.* ✅
4. **Infraestrutura DynamoDB**: script de criação da tabela (dev/local), repositórios (`ISquadRepository`, `IAppRepository`, `IQualitySnapshotRepository`) implementando os access patterns da seção 4 via AWS SDK for .NET (API de baixo nível, não `DynamoDBContext`, para controle explícito de PK/SK/GSI). *Verificação: testes de integração contra DynamoDB Local cobrindo os padrões #3, #8/#9, #12, #13.* ✅
5. **Application layer**: serviços orquestrando Domain + Infrastructure (SquadService, AppService, QualitySnapshotService), chamando o assessment engine na escrita do snapshot. ✅
6. **Seed**: ferramenta/comando que usa `MockSonarProvider`/`DemoDataFactory` para popular o DynamoDB com a massa demonstrativa (squads, apps com 1 e múltiplos repos, histórico multi-mês). *Verificação: rodar o seed e inspecionar os itens na tabela.* ✅
7. **Web API**: controllers mapeando os endpoints da seção 5, DTOs, logging estruturado básico (default do ASP.NET Core). *Verificação: chamadas via `curl` cobrindo todos os endpoints, incluindo o POST transacional.* ✅
8. **Angular scaffold + Dashboard da Squad** conectado à API real. *Verificação: rodar `ng serve` e navegar na tela.* ✅
9. **Lista de Apps + Detalhe do App** (com gráfico de evolução do Quality Level e tabela de histórico de métricas). *Verificação: navegação end-to-end no browser (Playwright) cobrindo os fluxos do critério de sucesso — sem erros de console.* ✅
10. **Polimento**: estados de loading/vazio/erro em todas as telas, README com instruções de execução (API, Angular, DynamoDB local/AWS pessoal, comando de seed). ✅

## 8. Riscos / Trade-offs

- **Single-table DynamoDB** exige mais modelagem upfront e é menos flexível para queries ad-hoc do que SQL — aceitável dado o objetivo explícito de aprendizado access-pattern-first; se a "Context Layer" futura exigir buscas mais livres (full-text, joins complexos), pode valer revisitar com um banco relacional complementar.
- **Estado atual desnormalizado no item App** (para resolver #7/#10 sem query extra) cria uma responsabilidade de consistência: todo `QualitySnapshot` precisa passar pelo `TransactWriteItems` do passo #13 (via `QualitySnapshotService`/`QualitySnapshotRepository.AddAsync`). Se algum processo futuro escrever snapshots por fora desse caminho, o estado atual do App fica desatualizado.
- **GSI1 com partição única `SQUAD`** para listar squads é um padrão de "partição quente de um item só" — ok para poucos squads (escala do MVP), não escalaria para milhares.
- **Evolução agregada da squad (#11)** é calculada em memória na Application layer (`SquadService.GetQualityEvolutionAsync`), não pré-agregada no banco. Mais simples agora; se squads crescerem muito em número de apps/snapshots, pode precisar de um job de pré-agregação — não construir isso preventivamente.
- **Split em 4 projetos** (Domain/Application/Infrastructure/Api) é mais estrutura do que um MVP mínimo estritamente precisa; justificado aqui porque modelagem de domínio e testabilidade são objetivos explícitos do laboratório. Se o escopo encolher, colapsar em menos projetos é uma simplificação legítima, não um retrocesso.
- **Dados mockados do Sonar** foram inferidos de um schema típico do SonarQube (a POC Python não estava disponível neste repositório para leitura direta); ao integrar o Sonar real futuramente, vale conferir se os nomes/formatos batem com a POC original.
- **Custo/operação do DynamoDB em conta AWS pessoal**: recomenda-se usar DynamoDB Local (Docker, `docker-compose.yml`) no dia a dia de desenvolvimento e validar pontualmente contra a conta AWS pessoal, para manter custo próximo de zero e reduzir atrito de credenciais.
- **Gráfico de evolução histórica**: implementado como SVG próprio focado no Quality Level (a métrica mais acionável para o critério de sucesso), com uma tabela para o histórico detalhado por métrica — não foi construído um sistema de hover/tooltip interativo completo (crosshair, múltiplas séries numéricas sobrepostas), o que seria o próximo passo de polimento visual caso o produto avance.
