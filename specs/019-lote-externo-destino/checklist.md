# Checklist — Spec 019: Decisão em lote, host externo e destino no app

Critério de "concluído". Cada item deve ser verificável por alguém que não escreveu o código.

Status: **019a (backend) implementada** em 2026-11-03 (plano aprovado em plan mode no mesmo dia). Falta a confirmação do contrato pelo usuário e a **019b (frontend)**.

## Qualidade da spec (antes de codar)

- [x] User stories têm cenários de aceite em Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante
- [x] Cada FR é testável
- [x] Success Criteria são mensuráveis e não citam tecnologia
- [x] Decisão estrutural (tabela `host_externals`, exclusão mútua) registrada como adendo no ADR-011
- [x] Plano aprovado pelo usuário (plan mode)

## 019a — Backend

- [x] 1. Domínio e persistência: `HostExternal`, `IHostExternalRepository`, `HostExternalRepository`, tabela em `schema.sql`, DI
- [x] 2. Resolução: `HostExternalMatch`, `HostLinkInfo.External`, `LinkStatuses.External`, `HostLinkResolver` com externos
- [x] 3. `HostLinkService`: `SetExternalAsync`, `RemoveExternalAsync`, `ApplyBatchAsync`; exclusão mútua com o confirmado
- [x] 4. `IntegrationService`: `externo` no filtro e `Externals` nas seções do app
- [x] 5. API: `PUT/DELETE hosts/{host}/external`, `POST links/batch`, DTOs e mapeamento
- [x] Modelo de dados, access patterns, glossário, `docs/leitor-github.md`, `AGENTS.md`, `backlog.md`, `STATE.md`

## 019a — Verificação (skill `verify-change`)

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde, sem rede (426 unitários + 92 de integração)
- [x] `curl` em banco isolado (e2e.db): lote confirmando hosts, lote com sucesso parcial (409 + chave inválida), externo em lote com nome (modelo cobre os concretos), filtro `externo` (e 400 inválido), `externals` nas integrações do app, 400 de lote vazio / sem app / estado inválido, `PUT`/`DELETE` do externo (200, 400, 409 nos dois sentidos, 204, 404)
- [x] Confirmação do usuário sobre o contrato dos endpoints (exemplos de resposta) — confirmado em 2026-11-03

## 019b — Frontend (pendente)

- [x] Seleção em lote, "Marcar como externo", filtro "Externo", painel do host com o externo (019b1, `ng build` limpo; confirmado pelo usuário na tela em 2026-11-04)
- [ ] Bloco "vínculos não vistos"
- [ ] Coluna "Destino" na seção Integrações do app
- [ ] `npx ng build` sem erros nem avisos; roteiro no navegador em banco isolado
- [ ] Confirmação do usuário na tela (AAAA-MM-DD)

## Encerramento (`close-spec`)

- [ ] `Status` da spec `Implementada` (só após a 019b)
- [x] `.agents/project/STATE.md` reescrito (próximo passo = 019b)
- [x] Mapa do `AGENTS.md` e contagem de testes
