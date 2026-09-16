# Spec 001: MVP Core — Inventário, Health e Evolution

**Status**: Implementada
**Criada em**: 2026-09-13 (retrofit para este formato em 2026-09-16)
**Origem**: brief inicial do projeto — evolução de uma POC Python/Streamlit que consultava o SonarQube, para uma aplicação web com histórico. Visão completa em `.agents/product/vision.md`.

> Esta spec foi escrita originalmente como plano de análise (problem statement → domain model → access patterns → DynamoDB → API → frontend → incrementos → riscos) e reestruturada depois no formato padrão. O conteúdo técnico original está preservado em **Design notes**.

## User Scenarios & Testing

### User Story 1 — Ver como estão os apps da minha squad (Prioridade: P1)

Um coordenador ou QA abre a squad e, em uma tela, vê quantos apps ela tem, como eles se distribuem por Quality Level, quais melhoraram, quais pioraram e quais merecem atenção agora.

**Por que esta prioridade**: é a pergunta que motivou o produto ("como estão meus apps?"). Sem isso, não há MVP.

**Teste independente**: com a massa de demonstração carregada, abrir `/squads/sq-1` e conferir os números sem tocar em nenhuma outra tela.

**Cenários de aceite**:

1. **Dado** uma squad com apps que já receberam snapshots, **Quando** abro o dashboard dela, **Então** vejo a contagem de apps, a distribuição por Quality Level (A–E) e três listas: melhoraram, pioraram, merecem atenção (C/D/E).
2. **Dado** um app cujo Quality Level subiu entre os dois últimos snapshots, **Quando** abro o dashboard, **Então** ele aparece em "melhoraram" com o nível anterior e o atual.
3. **Dado** uma squad sem apps, **Quando** abro o dashboard, **Então** vejo contagem zero e nenhuma lista com erro.

### User Story 2 — Entender a evolução de um app e o que causou a mudança (Prioridade: P1)

Um QA abre o detalhe de um app e vê o Quality Level ao longo do tempo, com as métricas de cada coleta, para descobrir qual indicador explica uma piora ou melhora.

**Por que esta prioridade**: histórico era o maior gap da POC. "Estão melhorando ou piorando?" é a segunda pergunta central.

**Teste independente**: abrir `/apps/billing-service` e ver B → C → C com as métricas de cada mês.

**Cenários de aceite**:

1. **Dado** um app com três snapshots mensais, **Quando** abro o detalhe, **Então** vejo um gráfico com o Quality Level de cada coleta em ordem cronológica e uma tabela com coverage, bugs, vulnerabilities, code smells e duplications por coleta.
2. **Dado** o mesmo app, **Quando** comparo duas linhas da tabela, **Então** consigo identificar qual métrica mudou de forma consistente com a mudança de nível (ex.: coverage caiu de 70% para 55% quando o nível foi de B para C).
3. **Dado** um app sem nenhum snapshot, **Quando** abro o detalhe, **Então** vejo "sem histórico" e nenhum Quality Level, sem erro.

### User Story 3 — Encontrar um app específico (Prioridade: P2)

Um QA lista os apps da squad, busca por nome, filtra por Quality Level e ordena, para chegar rápido ao app que interessa.

**Por que esta prioridade**: útil com dezenas de apps; com cinco, o dashboard já basta. Por isso P2.

**Teste independente**: em `/squads/sq-1/apps`, filtrar por `C` e ver só `billing-service`.

**Cenários de aceite**:

1. **Dado** a lista de apps de uma squad, **Quando** digito parte do nome, **Então** a lista filtra em tempo real.
2. **Dado** a lista, **Quando** seleciono um Quality Level, **Então** só apps naquele nível aparecem.
3. **Dado** a lista, **Quando** escolho ordenar por Quality Level, **Então** os apps aparecem de A para E.

### User Story 4 — Ver a composição de um app (Prioridade: P2)

Um coordenador vê, no detalhe do app, quais repositórios e quais projetos Sonar o compõem — inclusive quando são vários.

**Por que esta prioridade**: necessário para o "inventário", mas não é o que o usuário abre a tela para ver.

**Teste independente**: `/apps/billing-service` mostra dois repositórios (legacy + v2); `/apps/notifications-service` mostra dois projetos Sonar.

**Cenários de aceite**:

1. **Dado** um app com dois repositórios, **Quando** abro o detalhe, **Então** vejo ambos com nome, status e link.
2. **Dado** um app com dois projetos Sonar, **Quando** abro o detalhe, **Então** vejo ambos com sua origem (`SONAR_MOCK`).

### User Story 5 — Registrar uma nova coleta de qualidade (Prioridade: P3)

Um processo (hoje o seed; amanhã a integração real com o Sonar) registra um snapshot de métricas para um app, e o sistema calcula o Quality Level pela regra vigente e atualiza o estado atual do app.

**Por que esta prioridade**: sem isso nada existe, mas não é uma ação do usuário final — é infraestrutura do produto.

**Teste independente**: `POST /api/apps/{id}/quality-snapshots` e conferir que `GET /api/apps/{id}` reflete o novo nível.

**Cenários de aceite**:

1. **Dado** um app, **Quando** registro um snapshot com métricas, **Então** o snapshot é gravado com `qualityLevel` e `assessmentRulesVersion`, e o app passa a mostrar esse nível como atual.
2. **Dado** um app cujo snapshot mais recente é de agosto, **Quando** registro um snapshot de junho (backfill), **Então** o snapshot de junho é gravado no histórico mas o estado atual continua sendo o de agosto.

### Edge Cases

- App sem snapshot: aparece na lista sem Quality Level e não entra na distribuição do dashboard nem no índice por nível.
- Snapshot fora de ordem (backfill): histórico preservado, estado atual não regride.
- Regra de assessment mudar no futuro: snapshots antigos mantêm o nível e a versão com que foram classificados; nada é recalculado.
- Squad com muitos apps: a evolução agregada é calculada em memória a cada request — aceitável na escala do MVP.

## Requirements

### Functional Requirements

- **FR-001**: O sistema DEVE listar todas as squads.
- **FR-002**: O sistema DEVE retornar os dados de uma squad pelo seu id.
- **FR-003**: O sistema DEVE listar os apps de uma squad, cada um já com seu Quality Level e métricas atuais, sem consulta adicional por app.
- **FR-004**: O sistema DEVE retornar os dados de um app pelo seu id, incluindo estado de qualidade atual.
- **FR-005**: O sistema DEVE listar os repositórios de um app (um app pode ter N repositórios).
- **FR-006**: O sistema DEVE listar os projetos Sonar de um app (um app pode ter 0..N projetos; não se assume 1:1 com repositório).
- **FR-007**: O sistema DEVE retornar o quality assessment atual de um app (nível, métricas, data do último snapshot, versão da regra).
- **FR-008**: O sistema DEVE retornar o histórico completo de snapshots de um app em ordem cronológica.
- **FR-009**: O sistema DEVE retornar o histórico de um app restrito a um período (`from`/`to`).
- **FR-010**: O sistema DEVE calcular a evolução agregada de uma squad: distribuição de Quality Level por data de coleta e, para cada app, a tendência entre os dois snapshots mais recentes (melhorou / piorou / estável / novo).
- **FR-011**: O sistema DEVE listar apps por Quality Level atual, independentemente de squad.
- **FR-012**: O sistema DEVE registrar um novo snapshot calculando o Quality Level pela regra vigente e gravando no snapshot a versão da regra usada.
- **FR-013**: Um snapshot registrado NUNCA é alterado ou recalculado.
- **FR-014**: O Quality Level DEVE ser derivado por um motor de regras versionado e separado das métricas, de forma que a regra possa evoluir sem alterar a estrutura dos snapshots.
- **FR-015**: O sistema DEVE fornecer uma massa de dados de demonstração com pelo menos duas squads, apps com evolução positiva, negativa e estável, um app com múltiplos repositórios e um com múltiplos projetos Sonar.
- **FR-016**: Todo objeto DEVE ter identidade estável (o id de um app não é o nome do repositório), relações explícitas, origem dos dados (`source`) e timestamp claro nos dados históricos.

### Key Entities

- **Squad**: time responsável por apps. `id`, `name`, `description?`, `status?`.
- **App**: unidade central. `id` interno estável, `squadId`, `name`, `description?`, `criticality?`, `tags[]`, `status?`, `externalId?`, mais o estado atual desnormalizado (`currentQualityLevel`, `currentMetrics`, `lastSnapshotAt`, `assessmentRulesVersion`).
- **Repository** (1:N a partir de App): `id`, `appId`, `name`, `provider?`, `url?`, `externalId?`, `status?`.
- **SonarProject** (0:N a partir de App): `id`, `appId`, `projectKey`, `name?`, `url?`, `externalId?`, `status?`, `source`.
- **QualitySnapshot** (1:N a partir de SonarProject, imutável): `id`, `appId`, `sonarProjectId`, `collectedAt`, `source`, `metrics`, `qualityLevel`, `assessmentRulesVersion`.
- **QualityMetrics** (value object): `coverage`, `bugs`, `vulnerabilities`, `codeSmells`, `duplications`, `reliability`, `maintainability`, `security`.
- **QualityLevel**: enum `A` (melhor) … `E` (pior).

Persistência e chaves: `.agents/architecture/data-model/dynamodb-single-table.md`.

## Success Criteria

- **SC-001**: Um coordenador ou QA, sem treinamento, responde em uma única sessão: quais apps a squad tem, como está a qualidade atual, quais melhoraram/pioraram, qual a evolução histórica, qual indicador causou a mudança e quais apps merecem atenção.
- **SC-002**: Com a massa de demonstração, é possível identificar visualmente os três cenários planejados: um app B → B → A (melhora), um B → C → C (piora), um A → A → A (estável).
- **SC-003**: Nenhum snapshot histórico é alterado por coletas posteriores, inclusive backfill fora de ordem.
- **SC-004**: O dashboard da squad obtém o estado atual de todos os apps com uma única consulta ao banco (sem uma consulta por app).
- **SC-005**: A regra de Quality Assessment pode ser substituída por uma versão nova sem migração de dados e sem alterar o nível de snapshots já gravados.

## Assumptions

- Dados de Sonar são mockados (`MockSonarProvider`); a estrutura das métricas foi inferida do schema típico do SonarQube, já que a POC original não estava disponível para leitura.
- DynamoDB Local (Docker, `-inMemory`) no dia a dia; validação contra conta AWS pessoal fica para depois.
- Sem autenticação (ADR-007).
- A regra v1 de assessment é deliberadamente simples: nível base = pior entre reliability/maintainability/security; coverage < 60% rebaixa um nível; duplications > 20% rebaixa um nível; piso E.
- A evolução agregada da squad é calculada em memória; pré-agregação só se virar gargalo.
- Listagem de squads usa uma partição única — adequado para dezenas, não milhares.

## Design notes

### Access patterns → como cada um é resolvido

| # | Padrão | Resolução |
|---|---|---|
| 1 | Listar squads | Query GSI1 `PK=SQUAD` |
| 2 | Buscar uma squad | GetItem `PK=SQUAD#{id}, SK=METADATA` |
| 3 | Listar apps de uma squad | Query GSI1 `PK=SQUAD#{squadId}, SK begins_with APP#` |
| 4 | Detalhes de um app | GetItem `PK=APP#{appId}, SK=METADATA` |
| 5 | Repositórios de um app | Query `PK=APP#{appId}, SK begins_with REPO#` |
| 6 | Projetos Sonar de um app | Query `PK=APP#{appId}, SK begins_with SONARPROJECT#` |
| 7 | Assessment atual | Atributos desnormalizados no item App — sem query extra |
| 8 | Histórico de um app | Query `PK=APP#{appId}, SK begins_with SNAPSHOT#` |
| 9 | Histórico num período | Query `SK BETWEEN SNAPSHOT#{from} AND SNAPSHOT#{to}#￿` |
| 10 | Situação atual de todos os apps da squad | Mesma query do #3 |
| 11 | Evolução agregada da squad | Application layer: #3 + #9 por app, agregado em memória |
| 12 | Apps por Quality Level | Query GSI2 `PK=QUALITYLEVEL#{level}` |
| 13 | Registrar snapshot | `TransactWriteItems`: Put snapshot + Update condicional do App |
| 14 | Snapshot mais recente | Query #8 com `ScanIndexForward=false, Limit=1` |

Detalhes de chaves, GSIs e exemplos de item: `.agents/architecture/data-model/dynamodb-single-table.md`. Racional: ADR-002, ADR-004.

### API

```
GET  /api/squads
GET  /api/squads/{squadId}
GET  /api/squads/{squadId}/apps
GET  /api/squads/{squadId}/quality-evolution?from=&to=
GET  /api/apps?qualityLevel={level}
GET  /api/apps/{appId}
GET  /api/apps/{appId}/repositories
GET  /api/apps/{appId}/sonar-projects
GET  /api/apps/{appId}/quality
GET  /api/apps/{appId}/quality/history?from=&to=
POST /api/apps/{appId}/quality-snapshots
```

### Frontend

- Dashboard da Squad (`/squads/:squadId`), Lista de Apps (`/squads/:squadId/apps`), Detalhe do App (`/apps/:appId`), landing com seleção de squad (`/`).
- Componentes standalone; gráfico de evolução em SVG próprio; paleta de Quality Level validada para acessibilidade (ver `.agents/rules/frontend.md`).

### Arquitetura

Monólito modular em quatro projetos (ADR-003). Abstração `ISonarProvider` com `MockSonarProvider` hoje e `SonarQubeProvider` no futuro.

### Riscos e trade-offs assumidos

- Single-table exige modelagem upfront e é menos flexível para queries ad-hoc; se o estágio Context exigir buscas livres, avaliar banco complementar.
- Estado desnormalizado no App cria uma invariante: todo snapshot passa por `QualitySnapshotService`.
- GSI1 com partição única para squads; evolução agregada em memória; split em quatro projetos — todos aceitos conscientemente na escala do MVP e revisáveis.
- Gráfico de evolução é SVG mínimo, sem hover/tooltip — próximo passo de polimento se o produto avançar.
