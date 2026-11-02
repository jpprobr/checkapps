# Checklist — Spec 017: Vínculo host→app (fatia 2a, backend)

Critério de "concluído". A spec só muda para `Implementada` quando tudo abaixo estiver marcado. Cada item deve ser verificável por alguém que não escreveu o código.

Status: **implementada** em 2026-11-02 (plano aprovado em plan mode no mesmo dia). Contrato dos endpoints confirmado pelo usuário em 2026-11-02.

## Qualidade da spec (antes de codar)

- [x] User stories têm cenários de aceite em Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante
- [x] Cada FR é testável
- [x] Success Criteria são mensuráveis e não citam tecnologia
- [x] Decisão estrutural (tabela `host_links`, vínculo humano, limite do gateway) registrada como adendo no ADR-011
- [x] Dados novos: identidade, relações, proveniência e tempo definidos na spec
- [x] Plano aprovado pelo usuário (plan mode)

## Implementação

- [x] 1. Domínio e persistência: `HostLink`, `IHostLinkRepository`, `HostLinkRepository`, tabela em `schema.sql`, registro de DI
- [x] 2. `HostKeyMatcher` (função pura) e `AnalysisOptions.EnvPlaceholderValues`
- [x] 3. `HostLinkService` (criar/trocar, remover, listar com `seen`; validações e conflito)
- [x] 4. `IntegrationService` traz o vínculo efetivo (hosts, detalhe, `linkStatus`) e anota as integrações do app
- [x] 5. API: `PUT`/`DELETE` do vínculo, `GET /api/integrations/links`, DTOs e mapeamento
- [x] Regras "conquistadas" registradas em `.agents/rules/`
- [x] Modelo de dados (`sqlite-schema.md`) e access patterns atualizados
- [x] Documentação: glossário, `docs/leitor-github.md`, `AGENTS.md` (mapa e contagem de testes), `backlog.md`

## Verificação (skill `verify-change`)

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde, sem rede (409 unitários + 89 de integração)
- [x] `curl` do ciclo completo em banco isolado (e2e.db): listar, `PUT` no modelo (host `hom` aparece `via template`), 409 no segundo confirmado, rejeitado de outro app, `linkStatus` (e 400 inválido), 400 de estado e de chave, 404 de app, destinos nas integrações do app, `GET links` com `seen`, `DELETE` 204/404/400
- [x] Confirmação do usuário sobre o contrato dos endpoints (exemplos de resposta), em 2026-11-02

## Encerramento (skill `close-spec`)

- [x] `Status` desta spec atualizado para `Implementada`
- [x] `.agents/project/STATE.md` reescrito (próximo passo = 2b, tela de confirmação)
- [x] `.agents/product/backlog.md` atualizado
- [x] Mapa do `AGENTS.md` e contagem de testes
