# Checklist — Spec 007: Menu "Repositórios"

Status: **implementada** em 2026-10-13. Resta um item, que só o usuário pode fazer: a confirmação na tela.

## Qualidade da spec (antes de codar)

- [x] User stories com cenários Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante
- [x] Cada FR é testável
- [x] Success Criteria mensuráveis e sem tecnologia
- [x] Decisões estruturais: nenhuma nova (sem schema, sem regra de triagem nova)
- [x] Dados novos: nenhum — não se aplica ("no catálogo" continua derivado)

## Implementação

- [x] 0. Spec e checklist
- [x] 1. Backend: escopo de catálogo na listagem, `catalogApp` por item, `catalog` e novo `all` na API — testes de integração e unitários, `curl`
- [x] 2. Frontend: pasta e componente renomeados, rota `/repositories` + redirecionamento, menu, filtro "No catálogo", linha com etiqueta e link, textos — Playwright
- [x] 3. Documentação
- [x] Regras conquistadas registradas em `.agents/rules/`

## Verificação

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde (162 unitários + 40 de integração), **sem rede**
- [x] Endpoints exercitados via `curl`, incluindo `catalog`, `all` e um valor inválido (400)
- [x] Fluxo completo no browser sem erros de console; roteiros das specs 001–006 repetidos (seletores de menu e URL ajustados)
- [x] Varredura de segredo antes de qualquer commit
- [x] `README.md`, `AGENTS.md` e `docs/leitor-github.md` atualizados
- [ ] Confirmação do usuário na tela com o repositório que já virou app

## Encerramento

- [x] `Status` desta spec atualizado para `Implementada`
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado
