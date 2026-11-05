# Checklist — Spec 020: Mapa de aplicações

Critério de "concluído". Cada item deve ser verificável por alguém que não escreveu o código.

Status: **020a (backend) implementada** em 2026-11-05 (plano aprovado em plan mode no mesmo dia). Falta a confirmação do contrato pelo usuário e a **020b (tela)**.

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

## 020b — Tela (pendente)

- [ ] Menu "Mapa", grafo em SVG com layout automático, filtros e legenda em texto
- [ ] Tabela "Quem chama quem" e painel de cobertura ("scanner indisponível", "não analisado")
- [ ] `npx ng build` sem erros nem avisos; roteiro no navegador em banco isolado
- [ ] Confirmação do usuário na tela (AAAA-MM-DD)

## Encerramento (`close-spec`)

- [ ] `Status` da spec `Implementada` (só após a 020b)
- [x] `.agents/project/STATE.md` reescrito (próximo passo = confirmar o contrato e 020b)
- [x] Mapa do `AGENTS.md` e contagem de testes
