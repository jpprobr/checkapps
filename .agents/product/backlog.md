# Backlog — itens conhecidos, ainda não priorizados

Demandas identificadas e conscientemente adiadas. Nenhuma tem spec ainda; quando uma for puxada, ela vira `specs/NNN-…/` copiando `specs/_template/`. Ordem aproximada de valor, não compromisso.

Nenhuma spec pendente de implementação. A última, **Checagens** (`specs/003-checagens/`), foi implementada em 2026-10-12 junto com a navegação global.

## Estágio Health (completa o que o MVP mockou)

- **Integração real com SonarQube** — `SonarQubeProvider` implementando `ISonarProvider`. Decisões em aberto: autenticação (token), endpoint de métricas, mapeamento de campos (a POC original não está disponível para conferir nomes), cadência de coleta (job agendado? webhook? manual?), e como tratar projetos Sonar que não existem mais. É o item que tira o produto do modo demonstração. Pré-requisito: acesso a um SonarQube real (provavelmente só na máquina corporativa).
- **Regras de Quality Assessment v2** — as regras da v1 (pior rating + rebaixamento por coverage < 60% e duplicação > 20%) são placeholder. A regra corporativa real da POC precisa ser transcrita como `QualityAssessmentEngineV2`, mantendo a v1 para snapshots antigos. Hoje as regras só existem no código; a spec deveria documentá-las em linguagem de negócio.

## Estágio Inventário (completa o cadastro)

- **CRUD de Repository e SonarProject** — hoje só entram pelo seed. Mesma forma da spec 002: ações inline no detalhe do app, formulários simples, filhos de App na mesma partição. Sem guarda de remoção necessária (não têm histórico próprio). Provavelmente a spec mais barata da lista. **Inclui "repositório ativo"**: `Repository.status` já existe (`active`/`legacy` no seed) mas não há invariante nem UI — a spec deve garantir no máximo um repositório ativo por app e uma ação "marcar como ativo". Com isso, "quais repositórios já podem ser arquivados" vira um filtro (`status != active`) ou uma checagem (spec 003) — não precisa de feature própria. **Inclui o cadastro manual dos campos `sigla`, `siglaApp` e `role`** do repositório — eles já existem (spec 005) e são preenchidos pela triagem do scan, mas quem não usa o scan não tem como informá-los.
- **Tags chave-valor** — hoje uma tag é string solta; passar a `chave:valor`, no estilo Datadog (`solucao:carrinho`, `criticidade-negocio:alta`), com **valor opcional** para que as tags atuais continuem válidas. No banco, `app_tags` ganha a coluna `value` (melhor que `"chave:valor"` numa string só: filtrar por chave vira consulta simples). Tags em **app e repositório**, com o mesmo mecanismo, cada uma onde o fato pertence: no app, o que ele é e para quem (`solucao`, `produto`, `jornada`); no repositório, onde e como o código vive (`runtime`, `deploy`, `iac`). Sem herança automática entre os níveis, e a mesma chave não aparece nos dois. As chaves conhecidas ficam listadas no `glossary.md` — é o que torna as tags interpretáveis por pessoas de outras squads e por uma LLM.
  **Regra campo × tag**: se o sistema **toma decisões** com o dado (valida, define escopo de scan, garante unicidade, filtra checagem), é **campo**; se serve para **pessoas categorizarem e filtrarem** livremente, é **tag**. Tag é livre de propósito — nada impede `sigla-app`, `Sigla-App` e `siglaapp` em repos diferentes —, então dado de que o sistema depende como tag vira bug silencioso. Por isso sigla, sigla-app e conta AWS **não** são tags.
- **Import em lote** — CSV ou JSON de squads/apps para carga inicial em ambiente novo. **Provavelmente substituído pela fase 1 do "Leitor de repositórios GitHub"** (abaixo), que descobre os repositórios em vez de exigir que alguém os liste. Só vale fazer se a fase 1 atrasar (ex.: acesso ao GitHub corporativo demorar).

## Operação (necessário antes de qualquer uso compartilhado)

- **Autenticação e autorização** — substitui ADR-007. **Pré-requisito explícito de uso compartilhado**: o endpoint de scan da API (spec 006) gasta o limite de chamadas de um token real e hoje qualquer pessoa que abra o sistema pode dispará-lo; só deve ficar ligado (`Scan:Enabled`) em uso individual. Provavelmente integração com IdP corporativo. A modelagem de permissões (quem edita o quê, por squad? por papel?) é decisão de produto, não detalhe técnico.
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

- **Detecção automática de status** — pré-preencher uma checagem com os apps afetados e marcar como `conforme` os que já atendem (ex.: "Atualizar para .NET 10"). É a metade "detectar" que a spec 003 deixou de fora ("rastrear ≠ detectar"). **Depende da fase 2 do "Leitor de repositórios GitHub"** (abaixo), que é quem extrai as dependências — este item é só a ponte entre os dois. Provavelmente a evolução de maior valor das Checagens.
- **Export de checagens** (Excel/CSV) — a POC exportava; reporte em reunião pode pedir. Só depois de validar que os indicadores de tela não bastam.
- **Histórico de percentual no tempo** — "como estava semana passada". O histórico de eventos da spec 003 (mudanças de status com data e hora) permite reconstruí-lo sem snapshot periódico; falta só a consulta e a visualização.
- **Filtro por sigla no seletor de apps** — demandas que seguem a conta AWS ("tudo na sigla X precisa de Y") chegam por sigla. Depende de a sigla estar registrada nos repositórios (fase 1 do leitor de GitHub).
- **Checagens na massa de demonstração** — o seed não cria nenhuma; numa instalação nova a tela começa vazia. Barato: uma checagem aberta com alguns status variados.
- **Alertas de prazo** — cor de "vencido", lembrete. Cuidado: é o primeiro passo em direção a governança; decidir explicitamente.

## Navegação — evoluções (a base foi entregue com a spec 003)

Entregue em 2026-10-12: shell com `Squads` | `Checagens`, seletor de squad ciente de contexto no header, `/` → última squad visitada (lembrada no navegador), lista de squads em `/squads`, breadcrumbs com o nome da squad. Detalhes em `specs/003-checagens/spec.md` (Notas da implementação).

**Personas e quantas squads cada uma vê** (respostas do usuário, 2026-10-12) — continua sendo a referência para evoluir a navegação:

| Persona | Squads | Observação |
|---|---|---|
| Tech Lead | sempre 1 | usuário mais frequente; `/` já abre na squad dele |
| Coordenador de grupo | 2 a 5 | trocar de squad **é** a navegação primária — é o seletor do header |
| Coordenação ampla (futuro) | ~10 a 15 | a partir daí um dropdown plano incomoda; prever filtro |
| Dev | 1–2 apps | é quem **menos** precisa de menu: favorita `/apps/{id}` e resolve |

**Próximos passos, com gatilho**:
- **Filtro no seletor de squad** — quando a visão ampla (10–15 squads) existir.
- **"Minha squad" de verdade** — hoje é a última visitada, lembrada no navegador. Vira o default real quando houver autenticação (ADR que substituir o 007).
- **Busca global de apps** — parecia a dor central até sabermos que os usuários frequentes são Tech Lead e Coordenador, que pensam por squad, e que o dev favorita o app dele. Reavaliar quando existir a visão ampla ou se o uso real mostrar gente caçando app.
- **Escopo por usuário e restrição de dev** (ex.: dev só escreve em item de checagem) — decisão de autenticação.

**Descartado**: sidebar com árvore de squads expandindo apps — duplica as telas de lista e vira parede de links na escala real.

## Leitor de repositórios GitHub (fundação de vários itens deste backlog)

**Status (2026-10-12): fase 1 implementada** — `specs/005-leitor-github-fase-1/`, ADR-010, guia em `docs/leitor-github.md`. Ferramenta de scan por sigla (`tools/CheckApps.GitHubScan`, com `--dry-run`), caixa de entrada com triagem (menu **Descobertos**) e fonte `fake` para demonstração. Primeira leitura real do GitHub validada (busca por sigla, YAML da sigla-app). **Simplificação em relação ao texto abaixo**: o contexto do scan não é "squad + siglas" — são **siglas no scan, squad na triagem** (a squad vem do seletor do header ao criar o app). **Pendências da fase 1**: exercitar com o GitHub real os casos ainda cobertos só por teste (arquivado, paginação, limite, acesso parcial); tratar repositório que some do GitHub (hoje só `last_seen_at` envelhece); agendador (o scan já pode ser disparado pela tela, spec 006, desligado por padrão e **sem autenticação**); busca por nome (abaixo); GitHub App no corporativo.

Análise de 2026-10-12. Motivação: catálogo mantido à mão envelhece — é o risco que aparece em vários itens deste backlog. Ler os repositórios da organização ataca isso na origem, e é o primeiro conector real do produto (o Sonar ainda é mock), alinhado à visão de "camada de contexto sobre ferramentas existentes". Contexto adicional: existe na organização uma iniciativa que lê repositórios do GitHub com IA e desenha um mapa de aplicações. O que o CheckApps acrescenta a um levantamento pontual é evolução no tempo, dono por squad, qualidade do Sonar e checagens que transformam o achado em ação acompanhada — e essa iniciativa pode virar uma fonte alimentando o CheckApps pela mesma porta.

**Princípio central: o scan propõe, a pessoa decide.** O scan não cria Apps. Ele popula uma área de **repositórios descobertos**, e a pessoa escolhe para cada um: *criar app*, *vincular a app existente*, *de outra squad* ou *ignorar*. Motivos: repositório não é App (o domínio separou os dois de propósito — App tem id estável e N repositórios); parte dos repos é infra, biblioteca, fork ou arquivado; e, pela remoção guardada (ADR-005), um app que recebe snapshot não sai mais — auto-criação errada vira permanente. A triagem é **recorrente**, não carga única: cada scan mostra "repositórios novos desde o último scan, ainda não triados", que é o que mantém o catálogo coerente com repos sendo criados com frequência.

**Escopo do scan: por sigla, nunca a organização inteira.** Sigla é o agrupador corporativo de repositórios, normalmente associado a uma conta AWS. É **cruzada com squad**: uma sigla tem apps de várias squads, e uma squad tem apps em várias siglas (ex.: microfrontends e BFFs numa sigla, APIs e Lambdas em outra). Por isso:

- A pessoa informa **siglas** (ex.: `AB1, CD2`), não trechos de nome. A convenção de nome fica numa configuração única (ex.: `{org}-{sigla}-`) e o código monta o filtro a partir dela. O formato da sigla é validado — entrada vazia ou genérica não vira scan da organização.
- **Limite de resultados** como proteção real, para qualquer forma de busca: acima de N repositórios (ex.: 50), o scan não traz nada e pede refinamento. Tamanho mínimo de entrada não substitui isso — tamanho não mede o quanto a busca é restritiva.
- **O contexto do scan é squad + siglas.** A squad **não vem do repositório**: o `CODEOWNERS` lista desenvolvedores, não squads. Ela vem do contexto ("minha squad, olhando AB1 e CD2") e é confirmada repo a repo. Como a sigla é compartilhada, o scan traz repos de outras squads — daí a opção *de outra squad* na triagem, que evita que reapareçam como novos a cada scan.
- O que vem depois da sigla no nome ajuda a triagem: o segmento seguinte (`app-`, `infra-`…) sugere o tipo; o restante sugere o nome e o id do app.
- No código, o conceito é **escopo do scan**; *sigla* é o rótulo exibido.

**Pré-cadastro com confirmação.** *Criar app* abre o formulário de cadastro já preenchido com as sugestões (nome, squad do contexto, tipo), cada uma marcada como sugestão — nada é gravado até a pessoa confirmar. A sugestão vive no **repositório descoberto**, não na tabela de apps: um status "pré-cadastrado" no App espalharia filtros por todas as consultas e esbarraria na remoção guardada (ADR-005). O app confirmado guarda a proveniência (`GITHUB_SCAN`, confirmado por uma pessoa). Confirmação em lote para sugestões de alta confiança é aval humano mais rápido, não automação.

**Busca por nome (depois)**: para trazer um repo específico que não segue a convenção. Cai na mesma caixa de entrada e é protegida pelo mesmo limite de resultados. Entra quando aparecer o primeiro repo relevante fora da convenção.

**Sigla, sigla-app e papel do repositório** (esclarecido em 2026-10-12):

- **Siglas e sigla-apps são geradas e geridas pela organização** — a esteira corporativa cria a sigla-app junto com o repositório. O CheckApps **lê, não gera**: registra o valor com proveniência (lido pelo scan ou digitado à mão).
- **Sigla-app** é um código à parte (ex.: `AB1-1234`), **por repositório de aplicação**: um repo novo para o mesmo app recebe outra sigla-app. Por isso identifica o repositório, **nunca o app** — reforça o id próprio do App. Fica num **arquivo YAML de configuração** do repo: o scan a lê de forma determinística, e a convenção de nome serve só para **encontrar candidatos**. O caminho do arquivo é configuração do scan.
- **Repositórios de infra não têm sigla-app**, mas pertencem a uma sigla. Logo, **sigla é campo próprio do repositório**: em repo de aplicação, derivada do trecho inicial da sigla-app (certo numa squad, provável na organização — divergência gera **aviso**, não erro); em repo de infra, vem do contexto do scan ou do nome. A ausência de sigla-app também é sugestão útil de tipo na triagem.
- **Todos os repositórios de um app ficam na mesma sigla** (MFE e BFF numa sigla, API em outra — cada um é um app). Então o **app tem sigla, derivada dos repositórios**, e o filtro de checagens por sigla fica direto. Vincular a um app um repo de outra sigla gera aviso na triagem, sem bloquear. Confirma também que sigla ≠ solução: a solução atravessa siglas porque é composta por apps diferentes.
- **Conta AWS** é atributo da **sigla por ambiente** (uma conta de dev, uma de hom, uma de prod por sigla). Entra quando a sigla virar entidade; nunca como tag por repositório, que duplicaria cada conta em dezenas de repos.
- **Papel do repositório**: `aplicacao`, `infra`, `contrato`, `secrets`… Um API Gateway pode ter um repo de infra e um de contrato OpenAPI; um app ECS, o repo da aplicação e outro(s) de secrets. Papel ≠ **tipo de app**: o tipo diz o que o app é; o papel, para que serve cada repo dele. A triagem sugere o papel (sigla-app presente → provavelmente `aplicacao`; segmento do nome para os demais).
- **Infra com repo próprio é app**: uma fila SQS ou um barramento EventBridge com repositório de infra separado entra como app de tipo `sqs`/`eventbridge`, com squad responsável. Motivos: no mapa de aplicações eles são nós (BFF → SQS → worker), e todo repo tem uma squad responsável. Assim, todo repositório continua pertencendo a um app. **Ponto de atenção**: esses apps nunca terão Quality Level — o dashboard precisará filtrar por tipo ou indicar "não se aplica", para que uma squad com várias filas não veja linhas "sem dados" para sempre.
- **Fora do CheckApps por enquanto**: infra sem app nem squad (ex.: configuração base da conta da sigla) — na triagem, *ignorar*. **Exceção futura**: repositório compartilhado entre squads, se aparecer.

**Pergunta em aberto**: a convenção de nome é uniforme na organização, ou há siglas com padrões diferentes? Define se a configuração é um padrão único ou um por sigla.

**Três coisas diferentes, com dificuldades diferentes**:

| Fase | O que se lê | Exemplo | Como |
|---|---|---|---|
| **1 — Inventário assistido** | metadados do repositório | sigla (extraída do nome), arquivado, último push, linguagem, topics | API do GitHub, determinístico |
| **2 — Dependências de biblioteca** | manifestos | `TargetFramework` e `PackageReference` do `.csproj`; `package.json`, `pom.xml` | parsing, determinístico |
| **3 — Dependências entre apps** | configuração, IaC, código | o BFF chama a API de pagamentos | parte determinística (Terraform referenciando outro serviço, integração de API Gateway com destino explícito); parte heurística ou IA |

Fase 1 já entrega: cadastro facilitado (pré-cadastro), repos da sigla ainda não catalogados, repos parados (o cenário de arquivamento que originou a spec 003). O `CODEOWNERS` não indica squad; pode servir no futuro como "quem mantém" o repo, se o modelo um dia tiver pessoas. Começar como ferramenta de linha de comando, no molde do seed (`tools/CheckApps.GitHubScan`), sem agendador.

Fase 2 guarda dependências como **snapshot por scan** (versões mudam — mesmo padrão temporal do `QualitySnapshot`) e entrega o caso de uso que mais pesa para a coordenação: **"saiu uma vulnerabilidade na biblioteca X — quais dos nossos apps são afetados, de quais squads, em que versão?"**, respondido em segundos. Também o panorama de versão de .NET por squad e o pré-preenchimento de checagens. É dependência → repositório → app → squad: três joins, trivial em SQL (ADR-009 pagando dividendo).

Fase 3 é onde a IA tem papel real: descrever o que o app faz, inferir o tipo quando a infraestrutura é ambígua, sugerir que dois repos são o mesmo app, inferir ligações entre apps. **Sempre como sugestão com proveniência e confiança, nunca como verdade gravada.** Observação: a maior parte do que se lê no GitHub é parsing, não IA.

**Encaixe na arquitetura**: porta em `Application` no mesmo molde de `ISonarProvider`; implementação em `Infrastructure`. `Repository` já tem `Provider` e `ExternalId` desde o MVP — vincular ao id do repositório no GitHub não exige mudar schema. Todo dado do scan nasce com `source = GITHUB_SCAN`. Integração com sistema externo, credencial e proveniência merecem ADR quando for implementado.

**Restrições práticas — onde deve demorar**:
- **Acesso ao GitHub corporativo**: o caminho correto é um *GitHub App* instalado na organização (permissão fina, não amarrado a uma pessoa), que tende a passar por aprovação de segurança — pode ser o item mais lento, como o RDS foi. Para provar valor, um token pessoal somente leitura, lendo só metadados e manifestos, deve bastar.
- **Governança de dados com IA**: enviar código ou configuração de repositórios de banco para uma LLM externa é questão de política. A fase 3 exige a LLM aprovada pela empresa; as fases 1 e 2 não têm essa questão — mais um motivo para virem primeiro.
- Limites da API do GitHub não preocupam nessa escala.

**Iniciativas existentes**: se outra iniciativa da organização já resolveu o acesso ao GitHub corporativo e tem prompts de IA funcionando, uma conversa curta antes das fases 2 ou 3 pode poupar justamente as partes mais lentas. A fase 1 é feita de qualquer forma e já é algo concreto para mostrar.

## Mapa de aplicações

Também visto na iniciativa de leitura de repositórios citada no item anterior (lá, construído a partir de repositórios). Depende do leitor de GitHub: sem fonte automática de ligações, vira planilha desenhada.

- **Mapa de apps, não de repositórios.** Um App agrega N repositórios (legado + novo, componentes); o mapa mostra a arquitetura lógica, não a dispersão de repos.
- **Colorível pelo que só o CheckApps sabe**: Quality Level, versão de .NET, apps afetados por uma vulnerabilidade, status numa checagem ("mostre no mapa quem está pendente na checagem X").
- **Solução = subgrafo nomeado** (ver item Solução no estágio Context).
- **Toda ligação diz de onde veio**: cadastrada à mão, extraída de IaC ou inferida por IA, com confiança. O mapa desenha confirmado e inferido de forma diferente.
- **Modelagem**: uma tabela de ligações com origem e confiança, criada junto com a primeira ligação app→app — e não antes. É a semente do grafo; em Postgres, consultas recursivas atendem sem banco de grafo.

## Estágio Context (visão, não plano)

Produto, Operação, Jornadas, Dependências, Integrações, Documentação, Arquitetura relacionados a um App. Depende de integrações reais existirem primeiro. Ver `roadmap.md`. Ideias já levantadas (2026-09-17), em ordem de custo:

- **Topologia / arquitetura do app** ("modal" legado vs "carrinho" recente) — **provavelmente absorvido por Solução** (próximo item), pendente de revisão. A leitura original era classificação por tag (`topologia:carrinho`); a leitura atual é que "modal" e "carrinho" são soluções. "Topologia" e "arquitetura" estão na lista de termos a evitar do `glossary.md`.
- **Solução (`id-solution`)** — agrupar apps e repositórios que compõem uma mesma solução. **Leitura mais recente (2026-10)**: "modal" e "carrinho" são exemplos de Solução, não de atributo do app — um caminho de apps conectados (microfrontend → API Gateway BFF → BFF → API Gateway → API no ECS). No mapa de aplicações, uma Solução é um **subgrafo nomeado**, o que tende a encerrar a dúvida topologia × solução. É um segundo eixo de agrupamento ao lado de Squad (Squad = quem cuida; Solução = o que compõe) e por isso merece ADR. **Começar também por tag** (`solucao:pagamentos`); promover a entidade quando uma checagem precisar selecionar "por solução" com dados que a solução carrega, ou quando diagramas precisarem dela.
- **Dependências entre apps** (A depende de B) — absorvido pelo **"Mapa de aplicações"** (abaixo). O risco registrado aqui continua valendo: grafo mantido à mão envelhece em semanas. Por isso o mapa depende da fase 3 do leitor de GitHub como fonte de ligações.
