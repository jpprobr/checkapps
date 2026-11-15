# Checklist — Spec 022: Jornadas

Critério de "concluído". A spec só muda para `Implementada` quando tudo abaixo estiver marcado. Cada item deve ser verificável por alguém que não escreveu o código.

**Status**: implementada em 2026-11-09 (022a backend e 022b tela) e confirmada pelo usuário na tela em 2026-11-15.

## Qualidade da spec (antes de codar)

- [x] Todas as user stories têm cenários de aceite em Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante — as dúvidas viraram premissas "a validar" em Assumptions
- [x] Cada FR é testável (dá para escrever um teste que falha se ele não for atendido)
- [x] Success Criteria são mensuráveis e não citam tecnologia
- [x] Decisões estruturais têm ADR em `.agents/architecture/decisions/` (ADR-012)
- [x] Dados novos são AI-friendly: identidade estável, relações explícitas, proveniência e data definidas

## Implementação

- [x] 022a-1: schema, domínio, `IJourneyRepository` e `JourneyRepository`, com teste de banco antigo
- [x] 022a-2: `JourneyService` (regras da árvore e dos passos), guardas de squad e app
- [x] 022a-3: resolução dos passos e `JourneyImpactCalculator` (funções puras) e `IContractReader`
- [x] 022a-4: `JourneysController`, DTOs e `curl` incluindo 400, 404 e 409
- [x] 022b-1: modelos, serviço de API e rótulos no frontend
- [x] 022b-2: lista em árvore, formulário e item no menu
- [x] 022b-3: detalhe com passos, apps derivados, impacto e editor de passos
- [x] Regras "conquistadas" durante a implementação registradas em `.agents/rules/`
- [x] Modelo de dados atualizado em `.agents/architecture/data-model/`
- [x] Access patterns novos registrados em `.agents/architecture/data-model/access-patterns.md`

## Verificação

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde (unitários + integração)
- [x] Endpoints novos exercitados via `curl`, incluindo 400, 404 e 409
- [x] Impacto de ponta a ponta com o contrato de demonstração (`--fake-revision 0` e depois `1`)
- [x] Fluxo completo navegado no browser em banco isolado, sem erros no console
- [x] `README.md` e `AGENTS.md` atualizados se algo mudou em como rodar/testar
- [x] Confirmação do usuário na tela (2026-11-15: árvore de três níveis, squad nova, passos, regras de erro e impacto; os pontos de melhoria do impacto foram para o backlog)

## Encerramento

- [x] `Status` desta spec atualizado para `Implementada`
- [x] `docs/leitor-github.md` com a seção das jornadas
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado
