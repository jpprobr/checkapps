# Checklist — Spec 012: URL, ativo único e siglas normalizadas

Status: **implementada** em 2026-10-19; regra do ativo **revisada em 2026-10-24** (bloquear com 409 em vez de rebaixar). Confirmada pelo usuário na tela em 2026-10-24 (troca de ativo, validação por papel inclusive sem papel, sigla-app em maiúsculas e conflito sem diferença de caixa; URL, filtro Squad e remoção sem promoção ficaram só com a verificação automatizada).

## Qualidade da spec (antes de codar)

- [x] User stories com cenários Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante (escopo do ativo e conflito decididos com o usuário)
- [x] Cada FR é testável
- [x] Decisões estruturais: nenhuma nova (sem schema, sem ADR)
- [x] Dados novos: nenhum

## Implementação

- [x] 0. Spec e checklist
- [x] 1. Reconhecimento por URL: `RepositoryUrl`, `CatalogMatchSql`, novas assinaturas, fakes e testes
- [x] 2. Ativo único por papel: `AppService`, `TriageService`, endpoint `activate`, testes
- [x] 3. Siglas normalizadas: gravação, unicidade sem caixa, filtro da tela
- [x] 4. Frontend: situação, "Marcar como ativo" e aviso na edição do app
- [x] 5. Documentação
- [x] 6. Revisão 2026-10-24: `EnsureNoOtherActiveInRoleAsync` (409) no lugar de `DemoteOtherActiveAsync`, `activate` devolve o repositório, botão desabilitado e dica na tela, testes reescritos

## Verificação

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde (182 unitários + 45 de integração), sem rede
- [x] Navegador, em banco isolado: cadastro manual com a URL de um descoberto (sem duplicar, 7 novos), dois ativos do mesmo papel, "Marcar como ativo" com aviso, sigla-app em maiúsculas e conflito, filtro Squad; sem erros de console
- [x] Revisão 2026-10-24, `curl` em banco isolado: 409 ao criar/ativar com outro ativo no papel, 201 em papel diferente e como `legacy`, `activate` 200 após o outro virar `legacy`, 404 inexistente, remover o ativo mantém o `legacy`, triagem `link` 409/200
- [x] Revisão 2026-10-24, navegador (Playwright), em banco isolado: botão desabilitado com explicação, 409 na tela com o nome, fluxo em dois passos; único erro de console é o 409 esperado
- [x] Confirmação do usuário na tela (2026-10-24)

## Encerramento

- [x] `Status` atualizado para `Implementada`
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado (SonarProject permanece)
