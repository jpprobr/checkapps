# Backlog — itens conhecidos, ainda não priorizados

Demandas identificadas e conscientemente adiadas. Nenhuma tem spec ainda; quando uma for puxada, ela vira `specs/NNN-…/` copiando `specs/_template/`. Ordem aproximada de valor, não compromisso.

Com spec, ainda não implementadas: **Migração para SQLite** — `specs/004-migracao-sqlite/` (vem primeiro) e **Checagens** — `specs/003-checagens/`.

## Estágio Health (completa o que o MVP mockou)

- **Integração real com SonarQube** — `SonarQubeProvider` implementando `ISonarProvider`. Decisões em aberto: autenticação (token), endpoint de métricas, mapeamento de campos (a POC original não está disponível para conferir nomes), cadência de coleta (job agendado? webhook? manual?), e como tratar projetos Sonar que não existem mais. É o item que tira o produto do modo demonstração. Pré-requisito: acesso a um SonarQube real (provavelmente só na máquina corporativa).
- **Regras de Quality Assessment v2** — as regras da v1 (pior rating + rebaixamento por coverage < 60% e duplicação > 20%) são placeholder. A regra corporativa real da POC precisa ser transcrita como `QualityAssessmentEngineV2`, mantendo a v1 para snapshots antigos. Hoje as regras só existem no código; a spec deveria documentá-las em linguagem de negócio.

## Estágio Inventário (completa o cadastro)

- **CRUD de Repository e SonarProject** — hoje só entram pelo seed. Mesma forma da spec 002: ações inline no detalhe do app, formulários simples, filhos de App na mesma partição. Sem guarda de remoção necessária (não têm histórico próprio). Provavelmente a spec mais barata da lista. **Inclui "repositório ativo"**: `Repository.status` já existe (`active`/`legacy` no seed) mas não há invariante nem UI — a spec deve garantir no máximo um repositório ativo por app e uma ação "marcar como ativo". Com isso, "quais repositórios já podem ser arquivados" vira um filtro (`status != active`) ou uma checagem (spec 003) — não precisa de feature própria.
- **Import em lote** — CSV ou JSON de squads/apps para carga inicial em ambiente novo, em vez de cadastrar um a um. Útil na migração para a máquina corporativa.

## Operação (necessário antes de qualquer uso compartilhado)

- **Autenticação e autorização** — substitui ADR-007. Provavelmente integração com IdP corporativo. A modelagem de permissões (quem edita o quê, por squad? por papel?) é decisão de produto, não detalhe técnico.
- **Migração SQLite → Postgres** — quando o modelo estabilizar (poucas mudanças por trimestre), submeter de uma vez ao processo corporativo de modelagem um schema já validado por uso real. Gatilhos e racional no ADR-009.
- **Litestream e deploy no ECS** — replicação do WAL para S3, restore no boot, deploy stop-then-start com instância única. Documentado na spec 004; implementar quando houver deploy de verdade. **Testar o restore faz parte da entrega.**
- **Deploy em AWS real** — nunca validado fora da máquina local. Envolve: hospedar API e frontend no ECS (instância única), Litestream para durabilidade do SQLite, deploy stop-then-start e configuração por ambiente. Detalhes do alvo na spec 004 e no ADR-009.
- **CI** — build e testes em todo push. Ficou mais simples depois da migração: nenhum teste exige serviço externo ou container.
- **`.github/copilot-instructions.md`** — ponteiro fino para `AGENTS.md`, criar quando o Copilot passar a ser usado no repo.

## Melhorias no que existe

- **Pré-agregação da evolução por squad** — só se o cálculo em memória (`SquadService.GetQualityEvolutionAsync`) virar gargalo com muitos apps × snapshots. Não construir preventivamente (ADR-002).
- **Gráfico de evolução com interação** — hover/tooltip, múltiplas métricas numéricas sobrepostas, seleção de período. O SVG atual é deliberadamente mínimo.
- **Filtro de período no dashboard da squad** — a API já aceita `from`/`to` em `quality-evolution`; o frontend não expõe.
- **Campos de auditoria** (`createdAt`, `updatedAt`, `createdBy`) — compatível com o modelo atual; faz sentido junto com autenticação.

## Evoluções de Checagens (após a spec 003 estar implementada)

- **Detecção automática de status** — um "coletor" que varre repositórios (GitHub) para responder "quais apps têm a dependência X / estão no .NET Y" e pré-preenche o status dos itens de uma checagem. É a metade "detectar" que a spec 003 deliberadamente deixou de fora ("rastrear ≠ detectar"). Exige integração com GitHub e leitura de manifestos (`.csproj`, `package.json`…). Provavelmente a evolução de maior valor da feature.
- **Export de checagens** (Excel/CSV) — a POC exportava; reporte em reunião pode pedir. Só depois de validar que os indicadores de tela não bastam.
- **Histórico de percentual no tempo** — "como estava semana passada". Exige snapshot periódico da contagem por status; mesmo padrão de `QualitySnapshot`.
- **Alertas de prazo** — cor de "vencido", lembrete. Cuidado: é o primeiro passo em direção a governança; decidir explicitamente.

## Navegação (fazer JUNTO com a spec 003, não antes)

Análise feita em 2026-10-12 e já concluída — quando o assunto voltar, é executar, não reanalisar.

**Por que não agora**: o que justifica uma barra de seções é dar casa às Checagens, que são transversais por natureza (a spec 003 diz explicitamente que uma checagem não pertence a uma squad). Mas Checagens não tem uma linha de código e nenhuma rota. Construir o menu antes daria uma barra com **uma seção só**, e obrigaria a decidir às cegas sobre a persona de dev — que a própria implementação da 003 vai esclarecer. **Se a 003 for implementada sem navegação, `/checks` nasce inalcançável**: não há tela de onde linkar.

**Personas e quantas squads cada uma vê** (respostas do usuário, 2026-10-12):

| Persona | Squads | Observação |
|---|---|---|
| Tech Lead | sempre 1 | usuário mais frequente; a home como lista de squads é puro atrito para ele |
| Coordenador de grupo | 2 a 5 | trocar de squad **é** a navegação primária |
| Coordenação ampla (futuro) | ~10 a 15 | a partir daí um dropdown plano incomoda; prever filtro |
| Dev | 1–2 apps | é quem **menos** precisa de menu: favorita `/apps/{id}` e resolve |

Todos os apps pertencem a alguma squad — a árvore squad→app é válida, não há órfãos.

**Dependência de autenticação**: "minha squad" / "minhas squads" exige saber quem está olhando, e não há autenticação (ADR-007). Enquanto não houver, todos veem tudo. Desenhar de forma que vire persona-aware depois, sem redesenho. Paliativo barato e sem identidade: **lembrar a última squad visitada no navegador** e fazer `/` ir direto para ela (volta a ser lista se não houver). Dá ao Tech Lead o "já abre na minha squad", e quando a autenticação chegar isso vira o default real.

**Escopo quando for feito**:
- Shell fino em `app.html` (hoje é só `<router-outlet />`) com nome do produto → home e seções de topo `Squads` | `Checagens`.
- Seletor de squad no header, **ciente de contexto**: trocar de squad na lista de apps leva à lista de apps da outra, não à home. Serve também de indicador permanente de "qual squad estou vendo".
- `/` redireciona para a última squad visitada.
- Desenhar o seletor aceitando um filtro depois, sem refazer (pensando nas 10–15 squads).

**Fora do escopo, com gatilho explícito**:
- **Busca global de apps** — parecia a dor central até sabermos que os usuários frequentes são Tech Lead e Coordenador, que pensam por squad, e que o dev favorita o app dele. Reavaliar quando existir a visão ampla (10–15 squads) ou se o uso real mostrar gente caçando app.
- **Sidebar com árvore** de squads expandindo apps — duplica as telas de lista e vira parede de links na escala real.
- **Escopo por usuário e restrição de dev** (ex.: dev só escreve em item de checagem) — é decisão de autenticação, entra com o ADR que substituir o 007.

**Independente disso e fazível a qualquer momento**: os breadcrumbs são **defeito, não arquitetura**. Hoje a lista de apps diz "Dashboard" em vez do nome da squad; o detalhe do app tem trilha mais rasa (`Squads`) que a lista (`Squads / Dashboard`), apesar de estar mais fundo; e nenhum mostra o nome da squad. Não depende da 003 nem de autenticação.

## Estágio Context (visão, não plano)

Produto, Operação, Jornadas, Dependências, Integrações, Documentação, Arquitetura relacionados a um App. Depende de integrações reais existirem primeiro. Ver `roadmap.md`. Ideias já levantadas (2026-09-17), em ordem de custo:

- **Topologia / arquitetura do app** ("modal" legado vs "carrinho" recente) — com dois valores conhecidos, é classificação, não entidade. **Começar por convenção de tag** (`topologia:carrinho`), que já filtra na UI e pode ser usada como filtro no seletor de apps das checagens, sem mudar modelo. Só vira entidade se precisar de atributos próprios (descrição, diagrama, dono). O termo "topologia" ainda está em aberto.
- **Solução (`id-solution`)** — agrupar apps e repositórios que compõem uma mesma solução. É um segundo eixo de agrupamento ao lado de Squad (Squad = quem cuida; Solução = o que compõe) e por isso merece ADR. **Começar também por tag** (`solucao:pagamentos`); promover a entidade quando uma checagem precisar selecionar "por solução" com dados que a solução carrega, ou quando diagramas precisarem dela.
- **Dependências entre apps** (A depende de B) para diagrama de dependência — o item de maior risco: grafo mantido à mão envelhece em semanas (o mesmo problema da planilha). Só vale com uma fonte de detecção (chamadas observadas, manifestos, API Gateway) ou uma necessidade de diagrama forte o bastante para pagar a manutenção manual.
