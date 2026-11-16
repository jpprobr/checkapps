# Checklist — Spec 008: Menu lateral

Status: **implementada** em 2026-10-17. Confirmada pelo usuário na tela em 2026-10-18.

## Qualidade da spec (antes de codar)

- [x] User stories com cenários Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante
- [x] Cada FR é testável
- [x] Decisões estruturais: nenhuma nova
- [x] Dados novos: nenhum (só preferência de interface no navegador)

## Implementação

- [x] 0. Spec e checklist
- [x] 1. `SidebarService`, shell, ícones e CSS
- [x] 2. Gaveta no celular
- [x] 3. Documentação e regras (`frontend.md`)

## Verificação

- [x] `ng build` sem erros
- [x] Navegador: desktop, recolhido + recarregar, celular, sem erros de console
- [x] Roteiros anteriores (specs 001–007) repetidos no navegador (2026-11-15, banco isolado, fontes fake): 71 de 72 checagens passaram. A que falhou é o estouro horizontal de 216 px da tela Repositórios em 390 px de largura, que já existia (tabela sem contêiner com rolagem) e foi para o `backlog.md`

## Encerramento

- [x] `Status` atualizado para `Implementada`
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado
- [x] Confirmação do usuário na tela (2026-10-18: menu funcionando)
