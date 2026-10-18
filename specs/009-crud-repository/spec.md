# Spec 009: CRUD manual de Repository

**Status**: Implementada (2026-10-17)
**Criada em**: 2026-10-17
**Origem**: candidato de baixo custo do STATE.md. Hoje um repositório só entra no catálogo pelo scan; repositórios do seed ou de fora do GitHub não podem ser cadastrados, corrigidos nem removidos.

## Escopo e fronteira

Na edição de um app, a pessoa **adiciona, edita e remove repositórios** à mão: nome, provedor, URL, sigla, sigla-app e papel. Repositório criado assim tem `source = MANUAL`.

**Não faz parte**: listar repositórios manuais na tela Repositórios (que continua sendo a triagem dos descobertos); mover repositório entre apps; qualquer coluna nova.

## User Scenarios & Testing

### User Story 1 — Cadastrar e corrigir repositórios de um app (Prioridade: P1)

**Teste independente**: editar um app, adicionar um repositório, editar, remover.

**Cenários de aceite**:

1. **Dado** a edição de um app, **Quando** preencho nome (e opcionais) e adiciono, **Então** o repositório aparece na lista do app e no detalhe, com origem manual.
2. **Dado** um repositório existente, **Quando** edito e salvo, **Então** os campos mudam; origem e id externo são preservados.
3. **Dado** um repositório, **Quando** removo (com confirmação), **Então** ele sai do app.
4. **Dado** uma sigla-app já usada por outro repositório, **Quando** salvo, **Então** vejo o erro de conflito (409) e nada muda.
5. **Dado** nome vazio, **Então** erro 400.

### Edge Cases

- Remover um repositório vindo do scan o devolve à caixa de entrada (o estado "no catálogo" é derivado de `provider + external_id`).
- Editar mantendo a própria sigla-app não conflita consigo mesmo.

## Requirements

### Functional Requirements

- **FR-001**: `POST /api/apps/{appId}/repositories` cria com id novo e `source = MANUAL`; 404 se o app não existe; 400 sem nome; 409 em sigla-app duplicada.
- **FR-002**: `PUT /api/apps/{appId}/repositories/{repoId}` atualiza nome, provedor, URL, status, sigla, sigla-app e papel; preserva `source` e `externalId`; 404 se não existir.
- **FR-003**: `DELETE /api/apps/{appId}/repositories/{repoId}` remove; 404 se não existir; 204.
- **FR-004**: A tela de edição do app tem a seção Repositórios com tabela (editar/remover) e formulário.
- **FR-005**: Erros 409 exibem a mensagem da API.

### Key Entities

`Repository` (já existente): identidade `id` (guid), pertence a um `App`, origem `source` (`MANUAL` | `GITHUB_SCAN`). Sem mudança de schema.

## Success Criteria

- **SC-001**: Criar, editar e remover um repositório sem sair da edição do app.
- **SC-002**: Duas sigla-app iguais nunca coexistem.

## Assumptions

- Papel é texto livre (lista aberta, sugestões `aplicacao`, `infra`, `contrato`, `secrets` no formulário); nulo é aceito.
- Fora desta spec (backlog): "repositório ativo" único por app; a tela Repositórios listar os manuais; reconhecimento por URL no scan.
