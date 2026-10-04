# Spec 004: Migração de persistência — DynamoDB → SQLite

**Status**: Aprovada (pronta para plano de implementação)
**Criada em**: 2026-10-04
**Origem**: ADR-009. Avaliação de fit do DynamoDB (`.agents/architecture/retrospectives/2026-10-dynamodb-single-table.md`) + destino de deploy conhecido (ECS instância única) + incompatibilidade do processo corporativo de RDS com o método incremental do projeto.

## Escopo e fronteira

Trocar a implementação de persistência **sem alterar comportamento observável**. É uma spec de infraestrutura: o sucesso se mede por nada mudar para quem usa, e por tudo ficar mais simples para quem desenvolve.

**Não faz parte**: migrar dados (não existe dado de produção — o DynamoDB Local é efêmero e a massa vem do seed); mudar contrato de API; mexer no frontend; implementar Litestream e deploy no ECS (só documentar o alvo); otimizar consultas além do que o SQL já simplifica naturalmente.

## User Scenarios & Testing

### User Story 1 — Nada muda para quem usa (Prioridade: P1)

Quem abre o CheckApps depois da migração não percebe diferença nenhuma: mesmas telas, mesmos dados, mesmos tempos de resposta, mesmos códigos de erro.

**Por que esta prioridade**: é o critério que define migração bem-sucedida. Qualquer diferença percebida é defeito.

**Teste independente**: rodar o roteiro Playwright das specs 001 e 002 sem alteração nenhuma no script e ver tudo passar.

**Cenários de aceite**:

1. **Dado** o ambiente migrado com o seed aplicado, **Quando** navego landing → dashboard → lista → detalhe, **Então** vejo exatamente os mesmos dados de antes (2 squads, 5 apps, cenários B→B→A, B→C→C, A→A→A) e nenhum erro de console.
2. **Dado** um app com histórico, **Quando** tento removê-lo, **Então** continuo recebendo `409` com a mesma mensagem.
3. **Dado** qualquer endpoint da API, **Quando** comparo a resposta com a de antes da migração, **Então** o JSON é idêntico em formato e conteúdo (enums como string, datas em ISO UTC).

### User Story 2 — O ciclo de desenvolvimento fica mais simples (Prioridade: P1)

Quem desenvolve roda a aplicação e os testes sem Docker, sem container de banco e sem reseed depois de reiniciar a máquina.

**Por que esta prioridade**: é metade do ganho da migração, sentido todos os dias.

**Teste independente**: em uma máquina limpa (sem Docker), `dotnet run` e `dotnet test` funcionam.

**Cenários de aceite**:

1. **Dado** um clone novo do repositório sem Docker instalado, **Quando** rodo `dotnet test`, **Então** unitários e integração passam.
2. **Dado** que reiniciei a máquina, **Quando** subo a API, **Então** os dados continuam lá — sem rodar o seed de novo.
3. **Dado** os testes de integração, **Quando** rodam, **Então** cada classe usa seu próprio banco temporário e podem rodar em paralelo sem interferência.

### User Story 3 — O schema é legível e entregável (Prioridade: P2)

Quando chegar o momento de submeter o modelo ao processo corporativo para criar o Postgres, existe um `schema.sql` único e limpo que descreve o banco inteiro — não um histórico de alterações para interpretar.

**Por que esta prioridade**: é o que torna a migração futura barata; não bloqueia o dia a dia.

**Teste independente**: abrir `schema.sql` e conseguir ler o modelo inteiro em uma tela e meia.

**Cenários de aceite**:

1. **Dado** o repositório, **Quando** procuro o schema, **Então** encontro um arquivo único, comentado, com todas as tabelas, chaves, índices e constraints.
2. **Dado** o `schema.sql`, **Quando** o aplico em um banco vazio, **Então** o sistema funciona — ele é a fonte de criação, não documentação paralela.

### Edge Cases

- Banco inexistente no primeiro start: `schema.sql` é aplicado de forma idempotente no bootstrap (equivalente ao `DynamoDbTableBootstrapper` de hoje).
- Dois processos escrevendo (ex.: API e seed simultâneos em dev): WAL mode e `busy_timeout` tornam isso seguro localmente; em produção, a garantia é a instância única.
- Datas: continuam gravadas e lidas como ISO-8601 UTC, como hoje — sem depender de tipo de data do SQLite.
- Métricas: `coverage` e `duplications` em `REAL`; `bugs`, `vulnerabilities` e `codeSmells` em `INTEGER`; ratings em `TEXT`.

## Requirements

### Functional Requirements

- **FR-001**: O sistema DEVE implementar `ISquadRepository`, `IAppRepository` e `IQualitySnapshotRepository` sobre SQLite, mantendo as assinaturas atuais sem alteração.
- **FR-002**: O contrato HTTP DEVE permanecer idêntico — mesmas rotas, payloads, códigos de status e formatos (enums como string, datas ISO UTC).
- **FR-003**: O estado de qualidade atual do App DEVE deixar de ser desnormalizado e passar a ser derivado do snapshot mais recente por consulta (ADR-009 revoga a cláusula 3 do ADR-004). `AppSummaryDto` e `AppDetailDto` continuam expondo os mesmos campos.
- **FR-004**: O acesso a dados DEVE usar Dapper com SQL explícito; nenhum ORM com migrations geradas.
- **FR-005**: O schema DEVE viver em um `schema.sql` único e idempotente, aplicado no bootstrap. Scripts numerados de migration passam a existir a partir do primeiro deploy com dados reais.
- **FR-006**: O SQL DEVE ser portável para Postgres: tipos declarados, sem função específica do SQLite, sem depender de tipagem dinâmica.
- **FR-007**: As guardas de remoção (app com histórico, squad com apps) DEVEM permanecer na camada de Application, com o mesmo comportamento e as mesmas mensagens.
- **FR-008**: Os testes de integração DEVEM rodar sem Docker, cada classe com seu próprio banco temporário, sem necessidade de collection fixture compartilhada.
- **FR-009**: O seed DEVE continuar funcionando sem alteração na sua lógica, escrevendo pelos mesmos repositórios.
- **FR-010**: O `docker-compose.yml` e toda a configuração de DynamoDB Local DEVEM ser removidos, e a skill `run-local`, o `AGENTS.md` e `.agents/rules/backend.md` atualizados.

### Key Entities

Sem entidade nova. O mapeamento domínio → tabelas:

| Domínio | Tabela | Notas |
|---|---|---|
| `Squad` | `squads` | `id` PK |
| `App` | `apps` | `squad_id` FK; **sem** colunas de estado de qualidade (FR-003) |
| `Repository` | `repositories` | `app_id` FK |
| `SonarProject` | `sonar_projects` | `app_id` FK |
| `AppLink` | `app_links` | `app_id` FK |
| `QualitySnapshot` | `quality_snapshots` | `app_id` e `sonar_project_id`; métricas em colunas, não JSON |

Índices previstos: `apps(squad_id)`, `quality_snapshots(app_id, collected_at)`, mais os que os testes mostrarem necessários. Chaves estrangeiras com `ON DELETE CASCADE` onde a remoção já é cascata hoje (filhos de App); a guarda de "não remover app com histórico" continua na Application, não no banco.

## Success Criteria

- **SC-001**: Os 48 testes existentes passam sem alteração de asserção — só a infraestrutura de teste muda.
- **SC-002**: Nenhum arquivo em `Domain`, `Application`, `Api` ou `frontend` é modificado por causa da troca de banco (exceto remoção de referências a DynamoDB em DI e configuração).
- **SC-003**: `dotnet test` roda em máquina sem Docker.
- **SC-004**: A navegação end-to-end produz exatamente o mesmo resultado de antes da migração.
- **SC-005**: O código de infraestrutura encolhe: espera-se menos da metade das 814 linhas atuais.

## Assumptions

- Não há dados a migrar: o DynamoDB Local é efêmero e a massa é reproduzível pelo seed.
- Litestream e a configuração de deploy do ECS ficam **documentados** nesta spec como alvo, mas são implementados quando houver deploy de verdade — não agora.
- O caminho do arquivo do banco é configurável (`ConnectionStrings` em `appsettings`), com default local em um diretório ignorado pelo git.
- SQLite em WAL mode com `busy_timeout` configurado; instância única é premissa de produção (ADR-009).
- Postgres entra quando o modelo estabilizar; esta spec só garante que o SQL não atrapalhe esse dia.

## Design notes

### O que some

`Keys.cs`, `TableSchema.cs`, `AttributeValueExtensions.cs`, `QualityMetricsMapper.cs`, `DynamoDbTableBootstrapper.cs`, `DynamoDbOptions.cs`, o pacote `AWSSDK.DynamoDBv2`, o `docker-compose.yml`, a `DynamoDbLocalFixture` e a `DynamoDbCollection`.

### O que fica mais simples (e precisa ser aproveitado, não traduzido literalmente)

A tentação na migração é reescrever query por query mantendo a forma do DynamoDB. Isso jogaria fora metade do ganho. Especificamente:

- `SquadService.GetQualityEvolutionAsync` hoje faz N+1 (apps da squad, depois histórico por app). Deve virar **uma** consulta com `GROUP BY`.
- "Apps por Quality Level" era o GSI2; vira um `WHERE` sobre o nível mais recente.
- "Apps de uma squad com estado atual" era GSI1 mais desnormalização; vira um join com o snapshot mais recente (subconsulta correlacionada ou função de janela no SQLite; `LATERAL` no Postgres).
- As partições únicas (`"SQUAD"`, `"CHECK"`) simplesmente deixam de existir.

### Alvo de produção (documentar agora, implementar depois)

- Litestream replicando para S3, com `restore -if-db-not-exists -if-replica-exists` no boot; sem volume persistente — o disco efêmero do container basta.
- ECS: `desiredCount=1`, `minimumHealthyPercent=0`, `maximumPercent=100`, sem autoscaling, com deployment circuit breaker e rollback.
- **Testar o restore faz parte da entrega de produção**, não é opcional: matar a task, subir de novo, conferir os dados.
- A replicação falha em silêncio; prever uma verificação mínima antes de confiar dado real a ela.

### Impacto na spec 003

Checagens ainda não foi implementada, e é por isso que esta migração vem antes: as duas entidades novas (`Check`, `CheckItem`) nascem direto em SQL. Os access patterns #21 a #29 da 003 devem ser revisados depois da migração — vários deixam de ter restrição, e os ❌ listados como "fora de escopo" passam a ser possíveis, virando decisão de produto em vez de limitação de banco.
