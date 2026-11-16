# Checklist — Spec 006: Scan pela tela

Status: **implementada** em 2026-10-12. Scan pela tela contra o GitHub real confirmado pelo usuário em 2026-11-15.

## Qualidade da spec (antes de codar)

- [x] User stories com cenários Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante
- [x] Cada FR é testável
- [x] Success Criteria mensuráveis e sem tecnologia
- [x] Decisão estrutural (token na API, endpoint desligado por padrão, autenticação antes de uso compartilhado) em nota do ADR-010
- [x] Dados novos: nenhum — não se aplica (reaproveita o modelo da spec 005)

## Implementação

- [x] 0. Spec, checklist e nota no ADR-010
- [x] 1. Composição única e fonte sem exceção no construtor — testes unitários e de integração de composição; CLI idêntico
- [x] 2. API: `RepositoryScanController`, guarda de concorrência, erros 400/409/502 — `curl`
- [x] 3. Frontend: bloco "Buscar no GitHub" — Playwright
- [x] 4. Documentação
- [x] 5. Melhoria: "já no catálogo" no resultado do scan (FR-011) e estado vazio explicado — consulta em lote, testes unitários e de integração, roteiro de navegador do cenário vivido no primeiro uso real
- [x] Regras conquistadas registradas em `.agents/rules/`

## Verificação

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde (154 unitários + 35 de integração), **sem rede**
- [x] CLI com saída idêntica à da spec 005 nos casos: fake com e sem `--dry-run`, sigla inválida, limite, ausência de token (diferenças: a linha de ajuda do `--source`, e o erro da convenção vir antes do do token quando faltam os dois)
- [x] Endpoints exercitados via `curl`, incluindo 400, 409 e 502, e dois scans simultâneos (um 200 e um 409), com servidor local no lugar do GitHub; o token não aparece em resposta nem em log
- [x] Fluxo completo no browser sem erros de console; roteiros das specs 001–005 repetidos
- [x] Varredura de segredo antes de qualquer commit
- [x] **Scan pela tela contra o GitHub real** (usado e testado pelo usuário com o GitHub real; confirmado em 2026-11-15)
- [x] `README.md`, `AGENTS.md` e `docs/leitor-github.md` atualizados

## Encerramento

- [x] `Status` desta spec atualizado para `Implementada`
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado
