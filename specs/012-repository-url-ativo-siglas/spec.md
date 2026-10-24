# Spec 012: Repository — reconhecimento por URL, ativo único e siglas normalizadas

**Status**: Implementada (2026-10-19); regra do ativo revisada e confirmada na tela em 2026-10-24
**Criada em**: 2026-10-19
**Origem**: o que sobrou do CRUD de Repository (spec 009 e backlog). Três lacunas: um cadastro manual achado depois pelo scan voltava como "novo" e podia gerar duplicata; nada impedia dois repositórios "ativos" no mesmo papel; e `AB1` e `ab1` contavam como siglas-app diferentes. O CRUD de SonarProject continua no backlog.

## Revisão (2026-10-24): bloquear em vez de rebaixar

Ao confirmar a spec na tela, o usuário viu que salvar um repositório como ativo rebaixava o outro sozinho, o que permite desativar um repositório por acidente; e que remover o novo deixava o antigo `legacy`. Decisão do usuário: **a API nunca rebaixa sozinha** — se o grupo (app + papel) já tem um ativo, devolve **409** nomeando-o, e a pessoa marca o outro como `legacy` antes. **Remover o ativo não promove ninguém**: o app fica sem ativo naquele papel até alguém usar "Marcar como ativo". Isso substitui a decisão de 2026-10-19 ("rebaixar automaticamente, não bloquear"); o texto abaixo já reflete a regra nova.

## Escopo e fronteira

1. **Reconhecimento por URL.** "No catálogo" deixa de ser só `provider` + `external_id` e passa a ser **isso ou a mesma URL** (sem espaços nas pontas, sem barra final, sem diferença de maiúsculas).
2. **Ativo único por papel.** Dentro de um app, no máximo um repositório `active` por papel (aplicação, infra, contrato, secrets; "sem papel" é um grupo). Salvar ou marcar um como ativo quando o grupo já tem outro ativo é **recusado com 409**, nomeando o ativo atual; nada é rebaixado sozinho. Decisão do usuário: por papel (não por app inteiro) (2026-10-19) e bloquear em vez de rebaixar (2026-10-24).
3. **Siglas normalizadas.** Sigla-app gravada em **MAIÚSCULAS**, sigla em minúsculas; a unicidade da sigla-app ignora a caixa.

**Não faz parte**: CRUD de SonarProject (backlog); preencher o `external_id` do cadastro manual quando o scan o reconhece; tratar `.git` no fim da URL ou `http` versus `https`; reescrever as siglas-app antigas em minúscula já gravadas (elas só passam a ser comparadas sem diferença de caixa); coluna nova ou mudança de schema.

## User Scenarios & Testing

### User Story 1 — O scan reconhece o que eu já cadastrei (Prioridade: P1)

**Teste independente**: com o scan fake, cadastrar à mão num app um repositório com a URL de um descoberto.

**Cenários de aceite**:

1. **Dado** um repositório cadastrado à mão com a mesma URL de um descoberto (mesmo com outra caixa ou barra final), **Quando** abro Repositórios, **Então** o descoberto aparece **no catálogo**, com o app, e não em "Novos".
2. **Dado** o mesmo caso, **Então** o repositório cadastrado não aparece uma segunda vez na lista.
3. **Dado** o mesmo caso, **Quando** tento criar app ou vincular a partir do descoberto, **Então** recebo 409 "já está no catálogo".
4. **Dado** URLs diferentes, ou repositório sem URL, **Então** nada é reconhecido.

### User Story 2 — Um ativo por papel (Prioridade: P1)

**Cenários de aceite**:

1. **Dado** um app com um repositório `active` de papel *aplicacao*, **Quando** tento salvar outro *aplicacao* como ativo, **Então** recebo 409 com o nome do ativo atual, nada é gravado e ninguém é rebaixado; repositórios de outros papéis não são afetados.
2. **Dado** um repositório `legacy` e outro ativo no mesmo papel, **Quando** vejo a lista, **Então** "Marcar como ativo" aparece desabilitado com a explicação; depois que o outro vira `legacy` (edição), o botão volta e o repositório vira `active`.
3. **Dado** um repositório salvo como `legacy` ou `archived`, **Então** nada é verificado nem alterado.
4. **Dado** a triagem "vincular a app", **Quando** o descoberto é ativo e o app já tem um ativo do mesmo papel, **Então** recebo 409 nomeando o ativo atual e o descoberto continua pendente.
5. **Dado** que removo o repositório ativo, **Então** os `legacy` do mesmo papel continuam `legacy`.

### User Story 3 — Siglas sem diferença de caixa (Prioridade: P2)

**Cenários de aceite**:

1. **Dado** o cadastro de uma sigla-app `ab1-0001`, **Então** ela é gravada como `AB1-0001` e a sigla `AB1` como `ab1`.
2. **Dado** `AB1-0001` já em uso, **Quando** salvo `Ab1-0001` em outro repositório, **Então** recebo 409.
3. **Dado** repositórios cadastrados com sigla em caixas diferentes, **Então** o filtro de sigla da tela os agrupa.

### Edge Cases

- Repositório do seed, sem papel: forma um grupo "sem papel" próprio; o seed (um `legacy` e um `active` no mesmo app) já respeita a regra.
- Dados antigos com dois ativos no mesmo papel: nenhum dos dois pode ser salvo como ativo até um virar `legacy`; salvar um deles como `legacy` é sempre permitido.
- Dois apps com o mesmo `external_id` ou a mesma URL (não há unicidade): vale o primeiro por id do app.

## Requirements

### Functional Requirements

- **FR-001**: "No catálogo" = existe Repository com o mesmo `provider` + `external_id` **ou** a mesma URL normalizada (`lower(rtrim(trim(url), '/'))`; `RepositoryUrl.Normalize`).
- **FR-002**: A regra vale em todos os pontos: listagem de descobertos (pendentes e no catálogo), `IsInCatalog`, app de cada descoberto (`GetCatalogAppsAsync`, agora por `Id` do descoberto) e a lista de cadastrados sem descoberto.
- **FR-003**: `POST /api/apps/{appId}/repositories/{repoId}/activate` marca como ativo e devolve o repositório; 404 se não existir; 409 se o grupo já tem outro ativo.
- **FR-004**: Criar, atualizar ou vincular (triagem) um repositório como `active` quando outro já é ativo no mesmo papel do app (comparado sem diferença de caixa) devolve 409 com o nome do ativo atual; o próprio repositório nunca conflita consigo mesmo. Nenhum repositório é rebaixado ou promovido automaticamente, inclusive ao remover.
- **FR-005**: A tela de edição do app mostra a situação de cada repositório e o botão **Marcar como ativo**, desabilitado (com explicação) enquanto outro ativo ocupa o papel; o 409 da API aparece como mensagem.
- **FR-006**: Sigla-app em maiúsculas e sigla em minúsculas na gravação manual e na triagem; unicidade da sigla-app sem diferença de caixa.

### Key Entities

`Repository` (existente): `status` (`active` | `legacy` | `archived`, texto livre no banco), `role`, `url`, `sigla`, `sigla_app`. Nada novo.

## Success Criteria

- **SC-001**: Um repositório cadastrado à mão nunca aparece como "novo" nem duplicado depois de o scan encontrá-lo.
- **SC-002**: Nenhum app tem dois repositórios ativos do mesmo papel criados por esta aplicação, e nenhum repositório muda de situação sem ação explícita da pessoa sobre ele.
- **SC-003**: Nenhum par de siglas-app que difira só na caixa coexiste.

## Assumptions

- A URL do GitHub é estável e canônica (`html_url`); formas alternativas (`.git`, `http`) ficam para quando aparecerem.
- O papel é texto livre; a comparação ignora a caixa.
