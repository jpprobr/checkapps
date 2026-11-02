# Checklist — Spec 016: Navegação — Apps no menu, "Pacotes" e seções recolhíveis

Critério de "concluído". A spec só muda para `Implementada` quando tudo abaixo estiver marcado. Cada item deve ser verificável por alguém que não escreveu o código.

Status: **implementada** em 2026-10-27 (decisões do usuário no mesmo dia: menu em dois itens, tela Apps com lista geral, só Pacotes e Integrações recolhíveis e fechadas; plano aprovado em plan mode). Resta a confirmação do usuário na tela.

## Qualidade da spec (antes de codar)

- [x] User stories têm cenários de aceite em Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante
- [x] Cada FR é testável
- [x] Success Criteria são mensuráveis e não citam tecnologia
- [x] Decisões estruturais: nenhuma (sem ADR; mudança de interface e um parâmetro opcional de API)
- [x] Dados novos: nenhum; a preferência de seção é de navegador e a spec diz isso
- [x] Plano aprovado pelo usuário (plan mode)

## Implementação

- [x] 0. Espaçamento entre cartões corrigido (`:host` nos dois componentes de seção; intervalo de 20 px medido no navegador em 2026-10-27)
- [x] 1. API: `GET /api/apps` com `qualityLevel` opcional (sem ele, todos os apps); repositório e serviço; testes de integração (todos × por nível; ordenação)
- [x] 2. Frontend: tela **Apps** (`/apps`) reaproveitando `AppList` com `squadId` opcional, coluna Squad, filtro de squad e item de menu com ícone
- [x] 3. Frontend: "Dependências" → **Pacotes** (menu, título da tela, seção do app), rota `/packages` e redirecionamento de `/dependencies`
- [x] 4. Frontend: seções **Pacotes** e **Integrações** recolhíveis (`details`), fechadas por padrão, com resumo, estado em `localStorage` protegido
- [x] Regras "conquistadas" durante a implementação registradas em `.agents/rules/` (ex.: margem de componente filho; `localStorage` com `try/catch`)
- [x] Documentação atualizada: `README.md`, `AGENTS.md`, `docs/leitor-github.md` (nomes das telas), glossário (se um termo mudou)

## Verificação (skill `verify-change`)

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde, sem rede (350 unitários + 79 de integração)
- [x] `npx ng build` sem erros
- [x] `curl` em `GET /api/apps`: sem filtro (13 apps em 2 squads), `qualityLevel=A` (filtra), `qualityLevel=Z` (400), `qualityLevel=` vazio (todos)
- [x] Navegador (Playwright, banco isolado, 39 checagens): tela Apps (busca, filtros, inativos, vazio, link ao detalhe), menu (Apps, Pacotes, Integrações, `aria-current`, recolhido, celular), redirecionamento de `/dependencies`, seções fechadas por padrão, abrir/fechar, estado lembrado após recarregar, armazenamento bloqueado, intervalo igual entre cartões; zero erros de console
- [ ] Confirmação do usuário na tela

## Encerramento (skill `close-spec`)

- [x] `Status` desta spec atualizado para `Implementada`
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado (nada a mudar: a 016 não estava no backlog)
- [x] Mapa do `AGENTS.md` e contagem de testes
