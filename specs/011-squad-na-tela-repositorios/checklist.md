# Checklist — Spec 011: Squad na tela Repositórios

Status: **implementada** em 2026-10-18. Confirmada pelo usuário na tela em 2026-10-18.

## Qualidade da spec (antes de codar)

- [x] User stories com cenários Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante
- [x] Cada FR é testável
- [x] Decisões estruturais: nenhuma nova (a squad segue derivada do app)
- [x] Dados novos: nenhum

## Implementação

- [x] 0. Spec e checklist
- [x] 1. Backend: squad em `CatalogApp` e `CatalogRepository`, DTOs, testes de integração
- [x] 2. Frontend: coluna e filtro Squad
- [x] 3. Documentação

## Verificação

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde (168 unitários + 42 de integração), sem rede
- [x] `curl` mostra `squadId`/`squadName` nos dois endpoints
- [x] Navegador: coluna, filtro por squad, "Sem squad" e zerar ao trocar status, sem erros de console
- [x] "Sem squad" com itens: conferido pelo usuário com o scan `--source fake` (2026-10-18)
- [x] Confirmação do usuário na tela (2026-10-18)

## Encerramento

- [x] `Status` atualizado para `Implementada`
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado
