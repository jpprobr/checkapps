# Checklist — Spec 020: Mapa de aplicações

Critério de "concluído". Cada item deve ser verificável por alguém que não escreveu o código.

Status: **020a (backend) implementada e confirmada** (2026-11-05). **020b (tela) implementada, verificada e validada pelo usuário** (2026-11-05).

## Qualidade da spec (antes de codar)

- [x] User stories têm cenários de aceite em Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante
- [x] Cada FR é testável
- [x] Success Criteria são mensuráveis e não citam tecnologia

## 020a — Backend

- [x] `MapService` (`Build` puro + `GetAsync`), `IAnalysisRepository.ListNoScannerRepositoriesAsync`, `MapController`, DTOs e mapeamento
- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde, sem rede (434 unitários + 95 de integração)
- [x] `curl` em banco isolado: arestas por ambiente (hom/prod), externo nomeado, nó "Sem app", laço (`selfLoop`), cobertura com `noScannerRepositories`, squad inexistente → grafo vazio
- [x] Confirmação do usuário sobre o contrato do endpoint (exemplo de resposta) — confirmado em 2026-11-05

## 020b — Tela

- [x] Menu "Mapa", grafo em SVG com layout automático (`@dagrejs/dagre`, rota carregada sob demanda), filtros e legenda em texto
- [x] Tabela "Quem chama quem" e painel de cobertura ("scanner indisponível", "não analisado")
- [x] `npx ng build` sem erros nem avisos; roteiro no navegador em banco isolado (Playwright do assistente, 12 verificações, 2026-11-05)
- [x] Confirmação do usuário na tela (2026-11-05)

## Encerramento (`close-spec`)

- [x] `Status` da spec `Implementada`
- [x] `.agents/project/STATE.md` reescrito (próximo passo = confirmar o contrato e 020b)
- [x] Mapa do `AGENTS.md` e contagem de testes
