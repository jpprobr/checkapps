# Checklist — Spec 004: Migração DynamoDB → SQLite

Status: **implementada** em 2026-10-04, incluindo o incremento 6 (remoção do N+1), feito em seguida e verificado separadamente.

## Qualidade da spec

- [x] User stories com cenários Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante
- [x] FRs testáveis
- [x] Success Criteria mensuráveis
- [x] Decisão registrada em ADR-009; ADR-002 marcada como substituída e ADR-004 com a cláusula 3 revogada
- [x] Retrospectiva do experimento DynamoDB preservada

## Implementação (incrementos — refinar no plano)

- [x] 1. **Schema e acesso**: `schema.sql` consolidado e idempotente, conexão SQLite (WAL, `busy_timeout`), Dapper, bootstrapper que aplica o schema. *Verificação: aplicar em banco vazio e inspecionar as tabelas.*
- [x] 2. **Repositórios**: `SquadRepository`, `AppRepository`, `QualitySnapshotRepository` em SQL — aproveitando as simplificações (join para estado atual, `GROUP BY` para evolução da squad), não traduzindo o DynamoDB literalmente. *Verificação: testes de integração reescritos passando.*
- [x] 3. **Fiação e seed**: DI apontando para a nova infra, `appsettings` com `ConnectionStrings`, seed funcionando sem alteração de lógica. *Verificação: seed roda e o resumo bate com o de hoje.*
- [x] 4. **Testes de integração**: banco temporário por classe, sem Docker, sem `[Collection]` compartilhada. *Verificação: `dotnet test` em máquina sem Docker; execução em paralelo sem interferência.*
- [x] 6. **Remoção do N+1** em `SquadService.GetQualityEvolutionAsync` (feito após os demais, com verificação própria): nova porta `GetHistoryBySquadAsync`, duas consultas em vez de 1+N, agregação mantida na Application. Teste de integração próprio; saída do endpoint idêntica à baseline.
- [x] 5. **Limpeza e documentação**: remover `docker-compose.yml`, pacote AWS SDK e arquivos de chave; atualizar skill `run-local`, `AGENTS.md` (gotchas que deixam de existir), `.agents/rules/backend.md`, `README.md` e o registro de access patterns para a era SQL. *Verificação: teste de sessão fria com a skill atualizada.*

## Verificação

- [x] `dotnet build` sem erros nem avisos
- [x] Os 48 testes passam **sem alteração de asserção**
- [x] Nenhuma mudança em `Domain`, `Application`, `Api` ou `frontend` além de DI e configuração
- [x] Respostas dos endpoints idênticas às de antes (comparar JSON de alguns endpoints antes/depois)
- [x] Roteiro Playwright das specs 001 e 002 passa sem alterar o script
- [ ] ~~Infraestrutura com menos da metade das 814 linhas~~ — **não atingido**: 637 linhas de C# (−22%). Estimativa errada; análise na retrospectiva da migração.
- [x] `dotnet test` funciona sem Docker instalado

## Encerramento

- [x] Status desta spec para `Implementada`
- [x] `.agents/architecture/data-model/` reescrito para o modelo relacional (o documento de single-table vira histórico ou é substituído)
- [x] `access-patterns.md` revisto para a era SQL; os ❌ que deixaram de ser impossíveis reclassificados
- [ ] Access patterns da spec 003 (#21–#29) revisados à luz do SQL — fazer ao implementar a 003
- [x] `.agents/project/STATE.md` reescrito
- [x] Nova retrospectiva curta em `.agents/architecture/retrospectives/` com o que a migração ensinou (custo real vs. estimado, o que quebrou)
