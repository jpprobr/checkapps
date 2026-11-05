# Spec 020: Mapa de aplicações (fase 3, fatia 3 — primeira versão, só leitura)

**Status**: Implementada. **020a (backend)** confirmada pelo usuário (2026-11-05); **020b (tela)** implementada, verificada e validada pelo usuário na tela (2026-11-05).
**Criada em**: 2026-11-05
**Origem**: com as decisões humanas host→app da spec 017 e da spec 019 já existem ligações app A → app B confiáveis; o mapa é a primeira visão que as usa (backlog, "Mapa de aplicações").

> Formato compatível com o template de spec do Spec-Kit (github/spec-kit).

## Escopo e fronteira

- **Dentro**: um endpoint que devolve o grafo "quem chama quem" (nós e arestas por ambiente) derivado das análises correntes e das decisões humanas, e a cobertura do mapa; na 020b, a tela com o grafo, filtros e a tabela equivalente.
- **Fora**: Solução (subgrafo nomeado), ligações inferidas por IA ou IaC, qualquer tabela de ligações própria (o grafo é derivado a cada leitura), interfaces Refit (spec 015), sugestão automática de vínculo.
- **Sem escrita**: o mapa só lê. Quem decide o destino de cada host continua sendo uma pessoa, na tela Integrações.

## User Scenarios & Testing *(obrigatório)*

### User Story 1 — Ver quem chama quem (P1)

Como pessoa da squad, quero ver as apps e as ligações entre elas, por ambiente, para entender a arquitetura sem montá-la à mão.

**Aceite**
- **Dado** hosts confirmados para apps, **quando** abro o mapa, **então** vejo uma aresta app origem → app destino por ambiente, com a quantidade de integrações e os hosts que a formam.
- **Dado** um host marcado como externo, **então** a aresta vai para um nó de serviço externo com o nome informado (ou o host).
- **Dado** hosts sem decisão (ou só rejeitados), **então** vão para um nó único "Sem app".

### User Story 2 — Saber o que o mapa não enxerga (P1)

**Aceite**
- **Dado** repositórios cuja análise é `sem-scanner` ou que nunca foram analisados, **então** a cobertura do mapa os lista, para que ninguém leia a ausência de aresta como ausência de integração.

### Edge Cases

- Integração do arquivo base ou de ambiente desconhecido (ex.: `qa`): `environmentKind` nulo, vale para todos os ambientes.
- App que chama o próprio host: aresta com `selfLoop = true` (a tela decide se mostra).
- Filtro por squad restringe os apps **de origem**; destinos de outras squads entram como nós.
- Squad sem integrações ou inexistente: grafo vazio, não é erro.
- App com vínculo confirmado removido do catálogo: o nó usa o nome guardado no vínculo.

## Requirements *(obrigatório)*

- **FR-001**: `GET /api/map?squadId=` devolve `nodes`, `edges` e `coverage`.
- **FR-002**: O ambiente da aresta é o `environmentKind` da integração (arquivo de origem), nunca inferido do nome do host.
- **FR-003**: O destino segue a precedência da spec 019: app confirmado, depois externo, depois "sem app".
- **FR-004**: Arestas agregam por (origem, destino, ambiente), com `count` e `hosts`.
- **FR-005**: `coverage` traz repositórios não analisados (contagem) e os repositórios `sem-scanner` (lista, com o app).
- **FR-006**: Só leitura; nenhuma tabela nova.

## Success Criteria *(obrigatório)*

- **SC-001**: Uma decisão tomada na tela Integrações aparece no mapa na leitura seguinte, sem passo extra.
- **SC-002**: O mapa nunca mostra uma ligação que nenhuma pessoa decidiu (o destino sem decisão é "Sem app").

## Assumptions

- A fonte hoje é a varredura **.NET** (`appsettings*.json`); a inferência de ambiente por nome de arquivo (`Dev/Development`, `Hom/Homolog/Staging`, `Prod/Production`, tabela de apelidos configurável) vale só para ela. Repositórios sem scanner compatível ficam em "scanner indisponível" na cobertura.
- O mapa tem de ser legível com poucas decisões: com pouco vínculo mostrará muito "Sem app" (esperado).

## Design notes

- `MapService.Build` é uma função pura (testada sem I/O); `MapService.GetAsync` só lê (`IAnalysisRepository.ListIntegrationRowsAsync`, `GetCoverageAsync`, `ListNoScannerRepositoriesAsync`, `IAppRepository.ListAllAsync`) e reutiliza o `HostLinkResolver` da 017/019.
- Nó: `app` (id do app), `externo` (`externo:<nome em minúsculas>`; o mesmo nome em caixas diferentes é um nó só), `sem-app` (um nó só).
- **020b (tela, plano aprovado 2026-11-05)**: item "Mapa" no menu; layout automático com `@dagrejs/dagre` e SVG próprio (pan/zoom); filtros de ambiente, squad, "colorir por" (squad | Quality Level), mostrar chamadas ao próprio app e "Sem app"; cabeçalho "Fonte: varredura .NET"; painel de cobertura ("scanner indisponível", "não analisado"); tabela "Quem chama quem" equivalente ao SVG (e única visão em tela estreita).
