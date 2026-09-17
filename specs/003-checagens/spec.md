# Spec 003: Checagens — revisões e demandas acompanhadas por app

**Status**: Em análise (vira Aprovada quando as Assumptions forem confirmadas)
**Criada em**: 2026-09-17
**Origem**: dor recorrente do tech lead — com 50+ repositórios (ECS, API Gateway, Lambda, infra), qualquer revisão ou demanda transversal vira uma planilha montada à mão: copiar nomes de apps/repos, criar colunas de controle, marcar quem já está conforme. Rascunho original discutido em 2026-09-16/17; as decisões abaixo já refletem as respostas do usuário.

## Escopo e fronteira

Uma **checagem** é a lista de trabalho do time para responder a uma demanda que atinge vários apps — uma exigência de segurança, um direcionamento da empresa, uma atualização de dependência, uma revisão de arquivamento. Ela substitui a planilha: quais apps entram, em que estado cada um está, quanto falta.

**O que ela não é**: o sistema de registro da governança corporativa. O CheckApps não impõe checagens, não aprova, não escalona atraso, não pontua conformidade. A demanda vem de fora; a checagem é como o time se organiza para atendê-la. Essa fronteira mantém a feature coerente com `.agents/product/vision.md` ("não é mais uma ferramenta de governança").

**Rastrear ≠ detectar.** Esta spec cobre o acompanhamento manual. Descobrir automaticamente quais apps têm a dependência X (varrer repositórios, ler manifestos) é outra capacidade — está no backlog como possível "coletor" que preenche o status de um item no futuro.

## User Scenarios & Testing

### User Story 1 — Abrir uma checagem e escolher os apps (Prioridade: P1)

Um tech lead recebe uma demanda ("atualizar para .NET 10", "revisar exposição pública no API Gateway", "adequar ao padrão de logs") e, em vez de abrir uma planilha, cria uma checagem no CheckApps: dá um título, descreve, informa quem é o autor e, se souber, as datas de início e fim. Em seguida seleciona os apps que ela atinge.

**Por que esta prioridade**: é o ato que substitui a planilha. Sem ele, não há feature.

**Teste independente**: criar uma checagem "Atualizar .NET 10", adicionar `checkout-api` e `billing-service`, abrir a checagem e ver os dois com status "pendente".

**Cenários de aceite**:

1. **Dado** a lista de checagens, **Quando** crio uma com título "Atualizar .NET 10" e autor "Maria", **Então** ela aparece aberta, com id `atualizar-net-10`, 0 apps, 0% concluído.
2. **Dado** a checagem aberta, **Quando** seleciono apps (filtrando por squad ou por tag para achá-los mais rápido) e confirmo, **Então** cada app vira uma linha com status "pendente".
3. **Dado** uma checagem com apps, **Quando** adiciono mais um ou removo um, **Então** a lista e o percentual refletem a mudança imediatamente.
4. **Dado** o formulário, **Quando** tento salvar sem título, **Então** vejo validação e nada é gravado.

### User Story 2 — Atualizar o status de cada app e ver o quanto falta (Prioridade: P1)

Conforme o trabalho avança, alguém do time marca cada app: em andamento (alguém pegou), conforme (já estava ok), não se aplica, concluído. Detalhes que não cabem no status — "implantado, aguardando validação", "PR #412", "não usa a lib" — vão na observação do item. A checagem mostra o percentual concluído.

**Por que esta prioridade**: é a coluna de controle da planilha — o motivo de ela existir.

**Teste independente**: na checagem com 2 apps, marcar um como "conforme" → 50%; o outro como "em andamento" → continua 50%; marcar "concluído" → 100%.

**Cenários de aceite**:

1. **Dado** um app "pendente" numa checagem, **Quando** mudo o status para "conforme" direto na linha, **Então** a mudança é salva sem sair da tela e o percentual sobe.
2. **Dado** um app numa checagem, **Quando** escrevo uma observação na linha, **Então** ela fica visível para quem abrir a checagem depois.
3. **Dado** uma checagem com 4 apps: 1 conforme, 1 não se aplica, 1 em andamento, 1 pendente, **Quando** olho o resumo, **Então** vejo 50% concluído (conforme e não-se-aplica contam; em andamento e pendente não) e a contagem por status.

### User Story 3 — Ver, a partir de um app, o que está pendente para ele (Prioridade: P1)

Um desenvolvedor abre o detalhe de um app e vê em quais checagens ele está e com que status. É o que faz o time olhar o CheckApps em vez de perguntar "tem alguma planilha me esperando?".

**Por que esta prioridade**: fecha o ciclo — a planilha só o tech lead olhava; esta visão é para quem executa.

**Teste independente**: `checkout-api` está em duas checagens; abrir `/apps/checkout-api` mostra as duas com o status dele em cada.

**Cenários de aceite**:

1. **Dado** um app em três checagens (duas abertas, uma fechada), **Quando** abro o detalhe do app, **Então** vejo as abertas com o status dele em cada uma, e a fechada em uma seção separada ou recolhida.
2. **Dado** um app que não está em nenhuma checagem, **Quando** abro o detalhe, **Então** a seção informa isso sem erro.
3. **Dado** um app inativado, **Quando** ele está numa checagem, **Então** continua aparecendo nela (inativar não remove de checagens).

### User Story 4 — Fechar uma checagem, ou mantê-la aberta de propósito (Prioridade: P2)

Quando a demanda está atendida, o tech lead fecha a checagem manualmente. Se o time não consegue priorizar, ele a deixa aberta — mesmo com 30% por meses — e nada se perde. Checagens fechadas continuam consultáveis.

**Por que esta prioridade**: o fechamento manual é o que permite usar a checagem como memória de pendência, não só como tarefa.

**Teste independente**: fechar uma checagem a 100%; ela sai da lista padrão; com o filtro "mostrar fechadas" ela aparece e ainda mostra todos os itens.

**Cenários de aceite**:

1. **Dado** uma checagem aberta a 100%, **Quando** clico "Fechar", **Então** ela passa a fechada, sai da lista padrão e mantém todos os itens e observações.
2. **Dado** uma checagem aberta a 30%, **Quando** clico "Fechar", **Então** o sistema permite (com confirmação) — fechamento não exige 100%.
3. **Dado** uma checagem fechada, **Quando** clico "Reabrir", **Então** volta a aberta.
4. **Dado** uma checagem sem nenhum app (criada por engano), **Quando** clico "Remover", **Então** é apagada. Com apps, não há remoção — só fechamento.

### User Story 5 — Ter uma visão geral para levar a uma reunião (Prioridade: P2)

Antes de uma reunião com o coordenador, o tech lead abre a lista de checagens e vê, para cada uma aberta: título, autor, datas, quantos apps, percentual concluído. É o suficiente para reportar sem preparar nada.

**Por que esta prioridade**: útil, mas os indicadores de tela resolvem a primeira versão. Relatórios e export amadurecem depois (backlog).

**Teste independente**: lista de checagens mostra cada uma com percentual e contagem.

**Cenários de aceite**:

1. **Dado** três checagens abertas, **Quando** abro a lista, **Então** vejo as três com título, autor, período (se informado), nº de apps e percentual concluído, ordenadas da mais recente.
2. **Dado** a lista, **Quando** marco "mostrar fechadas", **Então** as fechadas aparecem marcadas, com seu percentual final.

### Edge Cases

- App removido do sistema (hard delete, permitido só sem histórico — ADR-005): seus itens em checagens são removidos junto. Não sobra linha órfã.
- App inativado: itens permanecem; a linha na checagem indica que o app está inativo.
- Dois títulos iguais: o segundo recebe id com sufixo (`atualizar-net-10-2`), como squads e apps (ADR-006).
- Checagem com centenas de apps: a tela precisa continuar usável (busca/filtro dentro da checagem por nome, squad e status).
- Mesmo app adicionado duas vezes à mesma checagem: operação idempotente — não cria linha duplicada.
- Percentual com zero apps: exibir "—", não 0% nem divisão por zero.

## Requirements

### Functional Requirements

- **FR-001**: O sistema DEVE criar uma checagem com título (obrigatório), descrição, categoria/origem (texto livre: "segurança", "demanda do banco"…), autor (texto livre) e datas de início e fim (opcionais, informadas manualmente). O id é gerado como slug do título; a checagem nasce aberta.
- **FR-002**: O sistema DEVE permitir editar título, descrição, categoria, autor e datas de uma checagem. O id não muda.
- **FR-003**: O sistema DEVE permitir adicionar apps a uma checagem por seleção explícita, com apoio de filtros (squad, tag, nome) para localizá-los. Adicionar um app já presente é idempotente.
- **FR-004**: O sistema DEVE permitir remover um app de uma checagem.
- **FR-005**: Cada app numa checagem DEVE ter um status entre: `pendente` (padrão, não iniciado), `em-andamento` (iniciado, não terminado), `conforme` (já estava adequado), `nao-se-aplica`, `concluido`.
- **FR-006**: Cada app numa checagem PODE ter uma observação em texto livre, para detalhar o status (ex.: "implantado, aguardando validação", "bloqueado pela lib X") ou registrar qualquer outra informação (PR, responsável, decisão).
- **FR-007**: O sistema DEVE calcular o percentual de conclusão de uma checagem como `(conforme + nao-se-aplica + concluido) / total de apps`, e a contagem por status.
- **FR-008**: O sistema DEVE listar checagens, abertas por padrão, com opção de exibir fechadas; cada linha mostra título, autor, período, nº de apps e percentual.
- **FR-009**: O sistema DEVE exibir uma checagem como uma tabela com uma linha por app (nome, squad, status editável inline, observação editável inline, indicador de inativo), com busca/filtro por nome, squad e status.
- **FR-010**: O sistema DEVE, no detalhe de um app, listar as checagens em que ele está e seu status em cada, separando abertas de fechadas.
- **FR-011**: O sistema DEVE permitir fechar uma checagem manualmente, sem exigir 100%, e reabri-la.
- **FR-012**: O sistema DEVE permitir remover uma checagem apenas se ela não tiver nenhum app; caso contrário, só fechar.
- **FR-013**: Um app PODE estar em qualquer número de checagens simultaneamente.
- **FR-014**: Ao remover um app do sistema, o sistema DEVE remover seus itens de todas as checagens. Inativar um app NÃO remove seus itens.

### Key Entities

- **Check** (Checagem): `id` (slug), `title`, `description?`, `category?`, `author?`, `startDate?`, `endDate?`, `status` (`open` | `closed`), `createdAt`, `closedAt?`.
- **CheckItem** (uma linha da checagem): `checkId`, `appId`, `status` (`CheckItemStatus`), `notes?`, `updatedAt`. Identidade composta `(checkId, appId)`.
- **CheckItemStatus**: `pendente` | `em-andamento` | `conforme` | `nao-se-aplica` | `concluido`. Enum no domínio (ver Design notes — é uma exceção deliberada à convenção de status como string livre). Ciclo típico: `pendente → em-andamento → concluido`; `conforme` e `nao-se-aplica` são saídas diretas de `pendente`.
- **App**: inalterado. Ganha uma visão derivada ("minhas checagens"), não um campo.

## Success Criteria

- **SC-001**: Um tech lead substitui a planilha de uma demanda real na primeira vez que usa a feature, sem copiar nome de app à mão.
- **SC-002**: Qualquer pessoa do time descobre, em um clique a partir de um app, tudo que está pendente para ele em qualquer checagem.
- **SC-003**: Toda checagem aberta mostra seu percentual e a contagem por status sem cálculo manual, e isso basta para reportar numa reunião sem preparar material.
- **SC-004**: Uma checagem deixada aberta por meses não perde nenhuma informação (itens, status, observações), e pode ser fechada quando o time decidir.

## Assumptions

Cada uma pode ser contestada; confirmar todas transforma o Status em Aprovada.

- **Seleção de apps é manual e explícita.** Filtros por squad/tag/nome ajudam a encontrar; não existe checagem "automática por regra" (ex.: "todos os apps da squad X, para sempre").
- **Cinco status, sem `bloqueado` nem `implantado`.** "Bloqueado por X" e "implantado, aguardando validação" são nuances que vão na observação do item, não em status próprio — mantém o conjunto genérico e o percentual simples. Adicionar um status depois é barato. (Decidido em 2026-09-17: `em-andamento` entrou, `implantado` saiu.)
- **Autor é texto livre.** Não há autenticação (ADR-007); ninguém é "dono" em sentido de permissão.
- **Datas são informativas.** Não há alerta de atraso, escalonamento nem cor de "vencido" na v1.
- **Reporte é só na tela.** Sem export (Excel/CSV) nem histórico de percentual ao longo do tempo na v1. Ambos estão no backlog.
- **Reabrir é permitido.** Fechar/reabrir é só um flag, sem trilha de auditoria.
- **Remover checagem só sem itens.** Espelha ADR-005: o que tem dependentes não é apagado, é fechado.
- **Sem notificação.** Ninguém é avisado ao ser incluído numa checagem.
- **Uma checagem não pertence a uma squad.** Ela é transversal por natureza (a demanda atinge apps de várias squads); a squad de cada app aparece na linha.

## Design notes

### Access patterns

| # | Padrão | Resolução prevista |
|---|---|---|
| AP-1 | Listar checagens (abertas / todas) | Query GSI1 `GSI1PK = "CHECK"` (mesmo padrão de partição única usado para squads; mesma limitação de escala, aceitável) |
| AP-2 | Abrir uma checagem com todos os itens | Query `PK = CHECK#{id}` — traz `METADATA` + todos os `ITEM#…` |
| AP-3 | Checagens de um app com status | Query GSI1 `GSI1PK = "APP#{appId}" AND begins_with(GSI1SK, "CHECK#")` |
| AP-4 | Adicionar / remover app | `PutItem` / `DeleteItem` em `PK = CHECK#{id}, SK = ITEM#{appId}` (idempotente por construção) |
| AP-5 | Atualizar status / observação | `UpdateItem` no item |
| AP-6 | Percentual e contagem por status | Calculado em memória a partir de AP-2 (coerente com ADR-002: sem pré-agregação) |
| AP-7 | Criar / editar / fechar / reabrir / remover checagem | `TryCreateAsync` condicional (ADR-006) / `UpdateItem` / `DeleteItem` guardado no serviço |
| AP-8 | Ao remover um App, apagar seus itens | AP-3 para descobrir os itens + `BatchWriteItem` de delete |

### Modelo DynamoDB (proposta, a confirmar no plano)

| Item | `PK` | `SK` | `GSI1PK` | `GSI1SK` |
|---|---|---|---|---|
| Check | `CHECK#{checkId}` | `METADATA` | `CHECK` | `CHECK#{checkId}` |
| CheckItem | `CHECK#{checkId}` | `ITEM#{appId}` | `APP#{appId}` | `CHECK#{checkId}` |

`APP#{appId}` como valor de `GSI1PK` é um espaço novo — os itens App usam `SQUAD#…` e os itens Squad usam `SQUAD`. Não colide, e **não exige GSI novo**. `CheckItem` denormaliza `appName` e `squadId` para a tabela da checagem não precisar de um `BatchGetItem` por abertura; nome desatualizado após renomear o app é aceito (é só exibição) — alternativa é `BatchGetItem` dos apps, decidir no plano.

### Decisões que merecem registro

- **`CheckItemStatus` como enum**, não string livre: o cálculo do percentual (FR-007) depende de saber quais valores contam. É exceção à convenção de `.agents/rules/backend.md` ("status é string, convenção da UI") e deve ser registrada — provavelmente uma linha nova na própria regra, não um ADR.
- **Remoção de App cascateia para itens de checagem** (FR-014). Hoje `DeleteAllAsync` apaga só a partição do app; os itens vivem na partição da checagem. `AppService.DeleteAsync` passa a fazer AP-8 antes. Alternativa considerada e descartada: bloquear a remoção do app se estiver em checagem — desproporcional, porque remoção já só é permitida para apps sem histórico (recém-criados por engano).
- **Sem entidade "Demanda" separada de "Checagem".** O prompt original falava em relacionar apps a demandas; na prática a checagem *é* a demanda vista pelo time. Uma entidade a mais não paga o custo.

### Telas

- `/checks` — lista (abertas por padrão, toggle para fechadas), botão "+ Nova checagem".
- `/checks/new`, `/checks/:id/edit` — formulário de metadados.
- `/checks/:id` — a "planilha": cabeçalho com resumo (percentual, contagem por status, autor, período), botão "Adicionar apps" (abre seletor com filtro por squad/tag/nome e multi-seleção), tabela com status como dropdown inline e observação editável inline, filtro dentro da tabela, ações Fechar/Reabrir.
- `AppDetail` — nova seção "Checagens": abertas com status, fechadas recolhidas.

### API (esboço)

```
GET    /api/checks?includeClosed=
POST   /api/checks
GET    /api/checks/{checkId}                      # metadados + itens + resumo
PUT    /api/checks/{checkId}
POST   /api/checks/{checkId}/close
POST   /api/checks/{checkId}/reopen
DELETE /api/checks/{checkId}                      # 409 se houver itens
PUT    /api/checks/{checkId}/items/{appId}        # cria se não existe (status/notes)
DELETE /api/checks/{checkId}/items/{appId}
GET    /api/apps/{appId}/checks
```

`close`/`reopen` como ações explícitas (não `PUT status`) porque são transições com semântica (`closedAt`), diferente do `status` de Squad/App que é só um flag.

### Fora desta spec (backlog)

Detecção automática de status (coletor via GitHub), export Excel/CSV, histórico de percentual no tempo, alertas de prazo, seleção por solução/topologia quando essas entidades existirem.
