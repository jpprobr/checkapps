# Checklist — Spec 001: MVP Core

Status: **concluído** (commit `a2fa02e`, 2026-09-16). Mantido como registro do critério de "done" que foi aplicado.

## Qualidade da spec

- [x] User stories com cenários Dado/Quando/Então (retrofit de 2026-09-16)
- [x] Nenhum `[NEEDS CLARIFICATION]` restante
- [x] FRs testáveis
- [x] Success Criteria mensuráveis e sem tecnologia
- [x] Decisões estruturais em ADR (001 a 004, 007)

## Implementação (incrementos originais do plano)

- [x] 1. Scaffold da solution (Domain, Application, Infrastructure, Api, UnitTests, IntegrationTests) — `dotnet build` limpo
- [x] 2. `IQualityAssessmentEngine` + `QualityAssessmentEngineV1` — testes unitários dos critérios
- [x] 3. `ISonarProvider` + `MockSonarProvider` + `DemoDataFactory` — teste unitário da forma dos dados (cenários B→B→A, B→C→C, A→A→A; app com 2 repos; app com 2 projetos Sonar)
- [x] 4. DynamoDB: bootstrap da tabela, repositórios, `docker-compose.yml` — testes de integração dos padrões #3, #8/#9, #12, #13 (incl. backfill fora de ordem)
- [x] 5. `SquadService`, `AppService`, `QualitySnapshotService` — teste unitário da agregação de tendência
- [x] 6. `tools/CheckApps.Seed` — seed rodado e resumo conferido
- [x] 7. Web API com os 11 endpoints — todos exercitados via `curl`, incluindo o `POST` transacional; enums serializando como string
- [x] 8. Angular: scaffold + Dashboard da Squad
- [x] 9. Lista de Apps + Detalhe do App com gráfico e tabela de histórico
- [x] 10. Loading/vazio/erro em todas as telas; `README.md`

## Verificação

- [x] `dotnet build` 0 erros, 0 avisos (vulnerabilidade `Microsoft.OpenApi` corrigida fixando 2.7.5)
- [x] `dotnet test`: 19 unitários + 5 integração na entrega original (hoje 37 + 11 após spec 002)
- [x] `curl` em todos os endpoints
- [x] Navegação end-to-end com Playwright headless: landing → dashboard → lista (filtro por nível) → detalhe; zero erros de console
- [x] Bug encontrado e corrigido na verificação visual: datas um mês atrás por `DatePipe` sem `'UTC'` — regra registrada em `.agents/rules/frontend.md`

## Encerramento

- [x] Status `Implementada`
- [x] Documentação: `README.md`; esta spec (originalmente `docs/plan.md`)
