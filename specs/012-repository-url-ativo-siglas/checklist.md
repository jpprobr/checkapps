# Checklist — Spec 012: URL, ativo único e siglas normalizadas

Status: **implementada** em 2026-10-19. Resta confirmação do usuário na tela.

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

## Verificação

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde (178 unitários + 45 de integração), sem rede
- [x] Navegador, em banco isolado: cadastro manual com a URL de um descoberto (sem duplicar, 7 novos), dois ativos do mesmo papel, "Marcar como ativo" com aviso, sigla-app em maiúsculas e conflito, filtro Squad; sem erros de console
- [ ] Confirmação do usuário na tela

## Encerramento

- [x] `Status` atualizado para `Implementada`
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado (SonarProject permanece)
