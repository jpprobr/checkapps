# Checklist — Spec 010: Cadastrados na tela Repositórios

Status: **implementada** em 2026-10-18. Confirmada pelo usuário na tela em 2026-10-18.

## Qualidade da spec (antes de codar)

- [x] User stories com cenários Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante
- [x] Cada FR é testável
- [x] Decisões estruturais: nenhuma nova
- [x] Dados novos: nenhum

## Implementação

- [x] 0. Spec e checklist
- [x] 1. Backend: consulta, serviço, endpoint e teste de integração
- [x] 2. Frontend: linhas dos cadastrados e atalho Gerenciar
- [x] 3. Documentação

## Verificação

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde (168 unitários + 42 de integração), sem rede
- [x] `curl /api/repositories/catalog` com a massa do seed
- [x] Navegador: Todos, Novos (vazio), No catálogo e Gerenciar, sem erros de console
- [x] Confirmação do usuário na tela (2026-10-18)

## Encerramento

- [x] `Status` atualizado para `Implementada`
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado
