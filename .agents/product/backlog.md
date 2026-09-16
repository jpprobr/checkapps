# Backlog — itens conhecidos, ainda não priorizados

Demandas identificadas e conscientemente adiadas. Nenhuma tem spec ainda; quando uma for puxada, ela vira `specs/NNN-…/` copiando `specs/_template/`. Ordem aproximada de valor, não compromisso.

## Estágio Health (completa o que o MVP mockou)

- **Integração real com SonarQube** — `SonarQubeProvider` implementando `ISonarProvider`. Decisões em aberto: autenticação (token), endpoint de métricas, mapeamento de campos (a POC original não está disponível para conferir nomes), cadência de coleta (job agendado? webhook? manual?), e como tratar projetos Sonar que não existem mais. É o item que tira o produto do modo demonstração. Pré-requisito: acesso a um SonarQube real (provavelmente só na máquina corporativa).
- **Regras de Quality Assessment v2** — as regras da v1 (pior rating + rebaixamento por coverage < 60% e duplicação > 20%) são placeholder. A regra corporativa real da POC precisa ser transcrita como `QualityAssessmentEngineV2`, mantendo a v1 para snapshots antigos. Hoje as regras só existem no código; a spec deveria documentá-las em linguagem de negócio.

## Estágio Inventário (completa o cadastro)

- **CRUD de Repository e SonarProject** — hoje só entram pelo seed. Mesma forma da spec 002: ações inline no detalhe do app, formulários simples, filhos de App na mesma partição. Sem guarda de remoção necessária (não têm histórico próprio). Provavelmente a spec mais barata da lista.
- **Import em lote** — CSV ou JSON de squads/apps para carga inicial em ambiente novo, em vez de cadastrar um a um. Útil na migração para a máquina corporativa.

## Operação (necessário antes de qualquer uso compartilhado)

- **Autenticação e autorização** — substitui ADR-007. Provavelmente integração com IdP corporativo. A modelagem de permissões (quem edita o quê, por squad? por papel?) é decisão de produto, não detalhe técnico.
- **Deploy em AWS real** — nunca validado além do DynamoDB Local. Envolve: criar a tabela na conta, hospedar API e frontend (a decisão de onde é um ADR), configuração por ambiente, e o custo de DynamoDB on-demand na escala esperada.
- **CI** — build + testes unitários em todo push; testes de integração exigem DynamoDB Local no pipeline (container de serviço).
- **`.github/copilot-instructions.md`** — ponteiro fino para `AGENTS.md`, criar quando o Copilot passar a ser usado no repo.

## Melhorias no que existe

- **Pré-agregação da evolução por squad** — só se o cálculo em memória (`SquadService.GetQualityEvolutionAsync`) virar gargalo com muitos apps × snapshots. Não construir preventivamente (ADR-002).
- **Gráfico de evolução com interação** — hover/tooltip, múltiplas métricas numéricas sobrepostas, seleção de período. O SVG atual é deliberadamente mínimo.
- **Filtro de período no dashboard da squad** — a API já aceita `from`/`to` em `quality-evolution`; o frontend não expõe.
- **Campos de auditoria** (`createdAt`, `updatedAt`, `createdBy`) — compatível com o modelo atual; faz sentido junto com autenticação.

## Estágio Context (visão, não plano)

Produto, Operação, Jornadas, Dependências, Integrações, Documentação, Arquitetura relacionados a um App. Nenhum item aqui tem forma ainda — depende de integrações reais existirem primeiro. Ver `roadmap.md`.
