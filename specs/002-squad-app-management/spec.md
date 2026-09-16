# Spec 002: Cadastro e gerenciamento de Squads e Apps

**Status**: Implementada
**Criada em**: 2026-09-16
**Origem**: pedido do usuário após o MVP — poder gerenciar squads e apps manualmente (listar, criar, editar, inativar, remover) em vez de depender só do seed, mesmo que no futuro os dados venham de integração. Incluiu a necessidade de **links corporativos por app**.

## User Scenarios & Testing

### User Story 1 — Cadastrar uma squad e um app sem mexer em código ou seed (Prioridade: P1)

Um coordenador cria uma squad nova pela interface, depois cria um app dentro dela informando nome, criticidade e tags. O sistema gera os ids; a pessoa não digita nenhum identificador técnico.

**Por que esta prioridade**: sem isso, todo dado entra por seed. É o que torna o produto usável fora de uma demonstração.

**Teste independente**: em `/`, clicar "+ Nova Squad", preencher, salvar; na squad, "+ Novo App", preencher, salvar. Ver o app no dashboard.

**Cenários de aceite**:

1. **Dado** a landing de squads, **Quando** crio "Squad Pagamentos", **Então** ela aparece na lista com id `squad-pagamentos` e status ativo, e sou levado ao dashboard dela.
2. **Dado** que já existe `squad-pagamentos`, **Quando** crio outra squad com o mesmo nome, **Então** ela recebe id `squad-pagamentos-2` (sem erro, sem sobrescrever).
3. **Dado** o formulário de novo app numa squad, **Quando** salvo com nome e squad, **Então** o app é criado ativo, sem Quality Level (ainda não há snapshot), e sou levado ao detalhe dele.
4. **Dado** o formulário de app, **Quando** tento salvar sem nome ou sem squad, **Então** vejo a validação e nada é gravado.

### User Story 2 — Editar um app, inclusive movê-lo de squad (Prioridade: P1)

Um coordenador corrige nome, descrição, criticidade ou tags de um app — ou o transfere para outra squad numa reorganização de times — sem perder o histórico de qualidade dele.

**Por que esta prioridade**: reorganização de squads é rotina; recriar o app perderia o id estável e o histórico.

**Teste independente**: editar `billing-service`, trocar a squad no dropdown, salvar; conferir que ele aparece na nova squad e que o Quality Level continua `C`.

**Cenários de aceite**:

1. **Dado** um app com histórico e Quality Level `C`, **Quando** edito o nome e salvo, **Então** o nome muda e o Quality Level, as métricas atuais e o histórico continuam intactos.
2. **Dado** um app na squad A, **Quando** troco para a squad B e salvo, **Então** ele deixa de aparecer na lista de A e passa a aparecer na de B, com o mesmo id.
3. **Dado** o formulário de edição, **Quando** escolho uma squad que não existe (via API), **Então** recebo erro e nada muda.

### User Story 3 — Inativar em vez de remover (Prioridade: P1)

Um coordenador tira de circulação um app (ou squad) descontinuado, sem apagar nada, e pode reativar depois. Listas escondem inativos por padrão.

**Por que esta prioridade**: é o caminho seguro e reversível para "sumir com" algo — deve ser mais fácil que remover.

**Teste independente**: na lista de apps, clicar "Inativar" num app; ele some da lista; marcar "mostrar inativos"; ele reaparece com a tag; "Ativar" o traz de volta.

**Cenários de aceite**:

1. **Dado** um app ativo na lista, **Quando** clico "Inativar", **Então** ele passa a `inactive`, some da lista padrão e mantém todo o histórico.
2. **Dado** a lista com inativos ocultos, **Quando** marco "Mostrar inativos", **Então** eles aparecem marcados e com a ação "Ativar".
3. **Dado** um app inativo, **Quando** clico "Ativar", **Então** volta a `active`.
4. O mesmo vale para squads na landing.

### User Story 4 — Remover cadastros feitos por engano, sem destruir histórico (Prioridade: P2)

Um coordenador remove de vez um app criado por engano (sem snapshots) ou uma squad vazia. Se houver histórico ou vínculos, o sistema recusa e orienta a inativar.

**Por que esta prioridade**: limpeza de erro é necessária, mas rara — e nunca pode custar histórico.

**Teste independente**: criar um app, removê-lo → some. Registrar um snapshot em outro app, tentar remover → mensagem de bloqueio.

**Cenários de aceite**:

1. **Dado** um app sem nenhum snapshot, **Quando** confirmo "Remover", **Então** o app e seus repositórios, projetos Sonar e links são apagados.
2. **Dado** um app com pelo menos um snapshot, **Quando** tento remover, **Então** recebo `409` com a mensagem "possui histórico de qualidade e não pode ser removido. Inative o app em vez de removê-lo." e nada é apagado.
3. **Dado** uma squad sem apps, **Quando** removo, **Então** ela é apagada.
4. **Dado** uma squad com apps (ativos ou inativos), **Quando** tento remover, **Então** recebo `409` orientando a mover ou remover os apps antes.

### User Story 5 — Anexar links corporativos a um app (Prioridade: P2)

Um QA adiciona ao app links úteis — Confluence, runbook, dashboard — cada um com uma descrição e um endereço, e os vê no detalhe do app.

**Por que esta prioridade**: é o primeiro passo do estágio "Context" do roadmap, mas o valor imediato é conveniência, não a pergunta central do produto.

**Teste independente**: em `/apps/{id}/edit`, adicionar link "Confluence" → `https://…`; abrir `/apps/{id}` e ver o link clicável.

**Cenários de aceite**:

1. **Dado** o formulário de edição de um app, **Quando** adiciono um link com descrição e URL, **Então** ele aparece na tabela de links imediatamente e no detalhe do app.
2. **Dado** um link existente, **Quando** o removo, **Então** some da tabela e do detalhe.
3. **Dado** o formulário de link, **Quando** tento adicionar sem descrição ou sem URL, **Então** vejo validação e nada é gravado.
4. Um app pode ter zero, um ou muitos links; dois links podem ter a mesma descrição (ex.: "Runbook" de produção e de staging).

### Edge Cases

- Nome que gera slug vazio (só símbolos): id vira `item`, com sufixo em colisão.
- Inativar a partir da lista de apps: a lista não carrega `description`/`externalId`; o PUT substitui o item inteiro, então o frontend busca o `AppDetail` completo antes de enviar.
- Mover para squad inexistente: `404`. Criar app com `squadId` inexistente: `400`.
- Remover app com filhos (repos, links) mas sem snapshot: filhos apagados em cascata, sem confirmação individual.
- Colisão de slug em criação concorrente: resolvida pelo `PutItem` condicional, não por "checar depois gravar".

## Requirements

### Functional Requirements

- **FR-001**: O sistema DEVE criar uma squad a partir de nome e descrição opcional, gerando o id como slug do nome e atribuindo status `active`.
- **FR-002**: O sistema DEVE criar um app a partir de nome, squad (obrigatória e existente), descrição, criticidade, tags e id externo opcionais, gerando o id como slug do nome e atribuindo status `active`.
- **FR-003**: Em colisão de id, o sistema DEVE tentar `slug-2`, `slug-3`… de forma atômica (sem janela entre verificar e gravar).
- **FR-004**: O sistema DEVE permitir editar nome, descrição, status de uma squad. O id não muda.
- **FR-005**: O sistema DEVE permitir editar nome, squad, descrição, criticidade, tags, status e id externo de um app, preservando o estado de qualidade atual e o histórico. O id não muda.
- **FR-006**: O sistema DEVE recusar (`409`) a remoção de um app que tenha pelo menos um `QualitySnapshot`.
- **FR-007**: O sistema DEVE recusar (`409`) a remoção de uma squad que tenha pelo menos um app vinculado, ativo ou inativo.
- **FR-008**: Ao remover um app permitido, o sistema DEVE apagar também seus repositórios, projetos Sonar e links.
- **FR-009**: O sistema DEVE permitir listar, adicionar, editar e remover links de um app; cada link tem id, descrição e endereço.
- **FR-010**: As listagens de squads e apps DEVEM ocultar inativos por padrão e oferecer um controle para exibi-los.
- **FR-011**: As telas de listagem existentes DEVEM ser a superfície de gerenciamento (ações inline), sem uma área administrativa separada.
- **FR-012**: Ações destrutivas na UI DEVEM pedir confirmação; erros `409` da API DEVEM ser mostrados ao usuário com a mensagem original.

### Key Entities

- **AppLink** (novo, 1:N a partir de App): `id` (GUID), `appId`, `description`, `uri`. Substitui o antigo campo `App.links: string[]`. **Complementa** `App.tags` (rótulos livres para categorizar o app) — não são conceitos concorrentes.
- **App**: `squadId` passa a ser editável. Demais campos inalterados.
- **Squad**: inalterada; `status` passa a ter uso na UI (`active`/`inactive`).

Persistência: `.agents/architecture/data-model/dynamodb-single-table.md`. Decisões: ADR-005 (remoção guardada), ADR-006 (ids por slug), ADR-007 (sem autenticação).

## Success Criteria

- **SC-001**: Um coordenador cria uma squad e um app nela, pela interface, em menos de dois minutos, sem tocar em código, seed ou banco.
- **SC-002**: Nenhuma operação de cadastro consegue apagar um `QualitySnapshot` ou fazer um app "perder" seu Quality Level atual.
- **SC-003**: Mover um app de squad preserva id, histórico e estado atual — verificável abrindo o detalhe antes e depois.
- **SC-004**: Duas criações com o mesmo nome resultam em dois registros distintos, nunca em sobrescrita.

## Assumptions

- Sem autenticação: qualquer pessoa com a URL pode criar/editar/remover (ADR-007).
- "Remover" é hard delete condicional, não soft delete; "inativar" é a operação normal (ADR-005).
- Cadastro de **Repository** e **SonarProject** fica fora desta spec — continuam entrando só pelo seed. Adiado de propósito; está no backlog.
- Não há endpoint dedicado para inativar: é um `PUT` com `status` alternado.
- `Status` continua `string?` livre no domínio; `active`/`inactive` é convenção da UI.

## Design notes

### API

```
POST   /api/squads                          {name, description?}            → 201 SquadDto
PUT    /api/squads/{squadId}                {name, description?, status?}   → 200
DELETE /api/squads/{squadId}                                                 → 204 | 409

POST   /api/apps                            {name, squadId, description?, criticality?, tags?, externalId?} → 201 AppDetailDto | 400 squad inexistente
PUT    /api/apps/{appId}                    idem + status?                    → 200 | 404
DELETE /api/apps/{appId}                                                     → 204 | 409

GET    /api/apps/{appId}/links                                               → AppLinkDto[]
POST   /api/apps/{appId}/links              {description, uri}              → 201
PUT    /api/apps/{appId}/links/{linkId}     {description, uri}              → 200
DELETE /api/apps/{appId}/links/{linkId}                                      → 204
```

### Camadas

- **Domain**: `AppLink`; `Slug.Generate`; `App.SquadId` com `set`.
- **Application**: `SlugIdGenerator` (retry sobre `TryCreateAsync`), `DomainConflictException`, `SquadService.Create/Update/Delete`, `AppService.Create/Update/Delete` + métodos de link. Guardas de remoção vivem aqui.
- **Infrastructure**: `TryCreateAsync` (Put condicional `attribute_not_exists(PK)`), `DeleteAllAsync` (Query da partição + `BatchWriteItem`), `DeleteAsync`, CRUD de `APPLINK#`.
- **Api**: novos endpoints; `KeyNotFoundException` → 404/400, `DomainConflictException` → 409.
- **Frontend**: `SquadForm`, `AppForm` (com seção de links no modo edição), ações inline em `SquadPicker` e `AppList`, seção de links e botão Editar em `AppDetail`.

### Gotchas descobertos na implementação (e onde foram parar)

- `AppRepository.SaveAsync` é `PutItem` completo → edição obrigatoriamente carrega-muta-salva. Teste `AppServiceTests.UpdateAsync_CanMoveAppToAnotherSquad_AndPreservesCurrentQualityState`. Regra em `.agents/rules/backend.md`.
- `DeleteAllAsync` não filtra `SNAPSHOT#` — apagaria o histórico se chamado direto. Teste `ManagementTests.DeleteAllAsync_HasNoBuiltInGuard_AlsoWipesQualitySnapshots` documenta isso. A guarda é do serviço.
- Duas classes de teste de integração com `IClassFixture<DynamoDbLocalFixture>` rodavam em paralelo e recriavam a mesma tabela → falhas intermitentes. Corrigido com `[Collection(DynamoDbCollection.Name)]`. Regra em `.agents/rules/backend.md`.
- `AppSummary` não carrega `description`/`externalId`; inativar pela lista precisa buscar o `AppDetail` antes do PUT. Regra em `.agents/rules/frontend.md`.

### Trade-offs

- Slug com retry é best-effort (50 tentativas) — irrelevante nesta escala.
- Cascata de filhos sem confirmação individual — aceitável porque não têm valor histórico próprio.
- Sem autenticação — dívida declarada (ADR-007).
