# Checklist — Spec 014: Integrações declaradas (fatia 1)

Critério de "concluído". A spec só muda para `Implementada` quando tudo abaixo estiver marcado. Cada item deve ser verificável por alguém que não escreveu o código.

Status: **implementada** em 2026-10-27 (spec e plano aprovados pelo usuário no mesmo dia; layout da tela escolhido: item de menu próprio "Integrações"). **Confirmada pelo usuário na tela em 2026-10-27**, com a análise real de dois repositórios de demonstração no GitHub.

## Qualidade da spec (antes de codar)

- [x] User stories têm cenários de aceite em Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante que bloqueie: tabela própria decidida no plano (adendo no ADR-011); o que o `{env}` vira na URL segue como pergunta aberta para a fatia 2 (não bloqueia esta)
- [x] Cada FR é testável
- [x] Success Criteria são mensuráveis e não citam tecnologia
- [x] Decisões estruturais têm ADR em `.agents/architecture/decisions/` (tabela própria de integrações: adendo no ADR-011; o ADR sobre analisador de sintaxe fica para a spec 015)
- [x] Dados novos são AI-friendly: identidade, relações, proveniência (`PARSER`, confiança, scanner) e tempo (snapshot por análise) definidos
- [x] Fixture de partida criada: `Fixtures/Dotnet/07-integracoes-appsettings` (base e por ambiente, `{env}`, placeholder, vazio, `localhost`, connection string, segredos plantados, JSON com comentário) com `EXPECTED.md`; inventada, a ajustar quando houver um modelo real
- [x] Plano aprovado pelo usuário (plan mode)

## Implementação

- [x] 1. Domínio e contratos: fato de integração, mecanismo, estado, normalização de host (com testes puros)
- [x] 2. Redação no núcleo (descarta userinfo, query, connection string, chaves de segredo) e teste com segredo plantado
- [x] 3. Scanner .NET: leitura tolerante de `appsettings*.json` e extração das integrações (testes com as fixtures)
- [x] 4. Persistência: tabela(s) e índices no `schema.sql`, repositório SQL portável, teste de banco antigo e de integração
- [x] 5. `AnalysisService` grava integrações na mesma análise; idempotência por commit; `--dry-run`; falha isolada
- [x] 6. Fonte `fake` serve `appsettings` de demonstração; a `github` já lê o que o scanner pede (limites e avisos da 013)
- [x] 7. API de leitura: `GET /api/apps/{id}/integrations`, `GET /api/integrations/hosts`, `GET /api/integrations/hosts/{host}`
- [x] 8. Frontend: seção **Integrações** no app, lista de hosts e detalhe, item de menu **Integrações** (opções de tela apresentadas ao usuário, que escolheu item próprio e o nome "Integrações")
- [x] Regras "conquistadas" durante a implementação registradas em `.agents/rules/`
- [x] Modelo de dados atualizado em `.agents/architecture/data-model/sqlite-schema.md` e `access-patterns.md`
- [x] `docs/leitor-github.md` e `README.md` atualizados (nova leitura, chaves de configuração)

## Verificação (skill `verify-change`)

- [x] `dotnet build` sem erros nem avisos
- [x] `dotnet test` verde, sem rede (350 unitários + 78 de integração)
- [x] `npx ng build` sem erros
- [x] Banco isolado: `GitHubScan --source fake` → apps pela triagem → `Analyze --source fake --dry-run` (nada gravado) → real → segunda execução sem alteração
- [x] `curl` nos endpoints novos, incluindo 404 e filtros
- [x] Navegador (Playwright, 31 checagens em banco isolado): seção no app (com integrações, parametrizada, não resolvida, sem scanner, sem appsettings, nunca analisado), lista e detalhe de hosts, busca e filtro, menu com `aria-current` e recolhido, recarregar e celular; zero erros de console
- [x] Segredo plantado na fixture ausente do banco, da saída e dos logs
- [x] Análise real contra o GitHub (2026-10-27): `appsettings*.json` empurrados para `jplay-dm1-app-pagamentos` e `jplay-dm1-app-bff`; `--dry-run` e gravação deram 4 integrações em cada (modelo `{env}`, Development/Staging/Production, uma não resolvida, `localhost` ignorado); JSON com comentário e vírgula sobrando tolerado; senha, chave e usuário da URL ausentes da API e do banco
- [x] Confirmação do usuário na tela (2026-10-27)

## Encerramento (skill `close-spec`)

- [x] `Status` desta spec atualizado para `Implementada`
- [x] `.agents/project/STATE.md` reescrito
- [x] `.agents/product/backlog.md` atualizado (fatia 1 entregue; fatias 2 e 3 permanecem)
- [x] Mapa do `AGENTS.md` e contagem de testes
