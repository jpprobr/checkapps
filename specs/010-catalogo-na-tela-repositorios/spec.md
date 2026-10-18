# Spec 010: Repositórios cadastrados na tela Repositórios, com atalho

**Status**: Implementada (2026-10-18)
**Criada em**: 2026-10-18
**Origem**: pedido do usuário depois da spec 009. A tela Repositórios só mostrava o que passou pelo scan; os repositórios cadastrados à mão ou pelo seed não apareciam, e nem os "no catálogo" tinham um caminho direto para gerenciar os repositórios do app. Era o "limite aceito" da spec 007 e um item do backlog.

## Escopo e fronteira

A tela Repositórios passa a listar também os repositórios que já estão no catálogo mas **não têm descoberto correspondente** (cadastrados à mão e do seed), nos filtros **Todos** e **No catálogo**. Toda linha "no catálogo" — do scan ou cadastrada — ganha o atalho **Gerenciar**, que abre a edição do app (seção Repositórios, spec 009).

**Não faz parte**: editar o repositório direto na tela Repositórios (o atalho leva à edição do app); "repositório ativo" único por app; reconhecimento por URL no scan; qualquer mudança de schema.

## User Scenarios & Testing

### User Story 1 — Ver os repositórios já cadastrados (Prioridade: P1)

**Teste independente**: com o seed, abrir Repositórios em "Todos".

**Cenários de aceite**:

1. **Dado** repositórios cadastrados à mão ou pelo seed, **Quando** abro Repositórios em "Todos" ou "No catálogo", **Então** vejo-os com a etiqueta "no catálogo", o link do app, a origem (cadastrado à mão, seed ou scan), a sigla-app e o papel.
2. **Dado** um repositório que veio do scan e virou app, **Quando** abro "Todos", **Então** ele aparece **uma vez só**.
3. **Dado** o filtro "Novos", "Ignorados" ou "De outra squad", **Então** os cadastrados não aparecem.
4. **Dado** o filtro de sigla, **Então** vale para as duas origens.

### User Story 2 — Chegar à edição do repositório (Prioridade: P1)

**Cenários de aceite**:

1. **Dado** qualquer linha "no catálogo", **Quando** clico em **Gerenciar**, **Então** abro a edição do app, na seção Repositórios.

### Edge Cases

- Sem nenhum repositório em lugar nenhum, o estado vazio continua explicando o scan.
- Repositório do seed não tem sigla, papel nem sugestão: as colunas mostram "—" / "papel não informado".

## Requirements

### Functional Requirements

- **FR-001**: `GET /api/repositories/catalog?sigla=` devolve os repositórios do catálogo sem descoberto correspondente (mesmo provedor e id externo), com `appId` e `appName`.
- **FR-002**: Repositório sem `external_id` (manual) entra sempre; repositório com descoberto correspondente nunca entra (sem duplicata).
- **FR-003**: A tela carrega essa lista só nos filtros Todos e No catálogo e a mescla após os descobertos.
- **FR-004**: Toda linha "no catálogo" tem o atalho **Gerenciar** → `/apps/{appId}/edit`.
- **FR-005**: O texto de escopo da tela diz que ela mostra os do scan e os cadastrados.

### Key Entities

`Repository` (existente) e `DiscoveredRepository` (existente), sem schema novo. "No catálogo" continua derivado.

## Success Criteria

- **SC-001**: Qualquer repositório do catálogo é alcançável a partir da tela Repositórios em um clique.
- **SC-002**: Nenhum repositório aparece duas vezes.

## Assumptions

- A ordem dos cadastrados é por app e nome, depois dos descobertos.
