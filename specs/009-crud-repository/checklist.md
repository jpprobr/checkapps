# Checklist — Spec 009: CRUD manual de Repository

Status: **implementada** em 2026-10-17. Confirmada pelo usuário na tela em 2026-10-18.

## Qualidade da spec (antes de codar)

- [x] User stories com cenários Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante
- [x] Cada FR é testável
- [x] Decisões estruturais: nenhuma nova
- [x] Dados novos: nenhum (schema já tem as colunas)

## Implementação

- [x] 0. Spec e checklist
- [x] 1. Persistência e serviço, com testes unitários e de integração
- [x] 2. Endpoints, com `curl`
- [x] 3. Frontend (seção na edição do app)
- [x] 4. Documentação

## Verificação

- [x] `dotnet build` sem erros
- [x] `dotnet test` verde, sem rede
- [x] `curl` nos três endpoints (201/200/204, 400, 404, 409)
- [x] Fluxo no navegador sem erros de console

## Encerramento

- [x] `Status` atualizado para `Implementada`
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado
- [x] Confirmação do usuário na tela (2026-10-18)
