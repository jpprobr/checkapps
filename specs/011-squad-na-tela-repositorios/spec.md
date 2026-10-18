# Spec 011: Squad na tela Repositórios (exibição e filtro)

**Status**: Implementada (2026-10-18)
**Criada em**: 2026-10-18
**Origem**: pergunta do usuário depois da spec 010 — faz sentido mostrar o time dos repositórios e os que estão sem time? Decisão: manter o modelo como está (Squad → App → Repository) e trazer a squad **só para exibição, consulta e filtro**.

## Escopo e fronteira

A tela Repositórios ganha a coluna **Squad** e o filtro **Squad**. A squad de um repositório é **sempre a do app dele**; não existe `squad_id` em `repositories` e nada novo é gravado. Repositórios que ainda não viraram app — *novos*, *ignorados*, *de outra squad* — não têm squad e aparecem como **"sem squad"**.

**Não faz parte**: guardar a squad de um repositório "de outra squad" (exigiria uma coluna nova na caixa de entrada); mudar a squad por esta tela (a squad do app se muda na edição do app); filtro de squad na API (o filtro é no cliente, a lista já vem completa).

## User Scenarios & Testing

### User Story 1 — Ver a squad de cada repositório (Prioridade: P1)

**Teste independente**: abrir Repositórios em "Todos".

**Cenários de aceite**:

1. **Dado** um repositório no catálogo (do scan, manual ou do seed), **Quando** abro a tela, **Então** a coluna Squad mostra a squad do app dele.
2. **Dado** um repositório que ainda não virou app, **Então** a coluna mostra "sem squad".

### User Story 2 — Filtrar por squad (Prioridade: P1)

**Cenários de aceite**:

1. **Dado** o filtro Squad, **Quando** escolho uma squad, **Então** vejo só os repositórios dela.
2. **Dado** o filtro "Sem squad", **Então** vejo só os que não estão em nenhum app.
3. **Dado** os filtros de status, sigla e squad, **Então** eles se combinam; trocar o status zera os de sigla e squad.

### Edge Cases

- Trocar o app de squad (edição do app) muda a squad dos repositórios dele sem nenhuma outra ação, porque o vínculo é derivado.
- Squad inativa continua aparecendo no filtro enquanto houver repositórios nela.

## Requirements

### Functional Requirements

- **FR-001**: `catalogApp` (descobertos e resultado do scan) e o item de `GET /api/repositories/catalog` trazem `squadId` e `squadName`.
- **FR-002**: A tela tem a coluna Squad e o filtro Squad ("Todas as squads", "Sem squad" e as squads presentes nas linhas carregadas).
- **FR-003**: "Sem squad" é exibido para toda linha sem app.
- **FR-004**: O filtro combina com status e sigla.

### Key Entities

Sem entidade nova nem coluna nova: a squad é derivada de `repositories.app_id → apps.squad_id → squads`.

## Success Criteria

- **SC-001**: Qualquer repositório mostra a squad dele (ou "sem squad") sem sair da tela.
- **SC-002**: Filtrar por squad leva um clique.

## Assumptions

- As opções do filtro vêm das linhas carregadas; uma squad sem repositórios no filtro de status atual não aparece na lista.
