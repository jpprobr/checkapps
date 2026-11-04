# Glossário do domínio

Vocabulário do CheckApps. Serve a pessoas de squads diferentes (para que um termo signifique a mesma coisa para todos) e a agentes de IA (para interpretarem os termos como o domínio interpreta). Criado em 2026-10-12, a revisar.

**Status de cada termo**: **Definido** (em uso no código e nas specs) · **Em aberto** (decisão pendente — não usar como se estivesse resolvido) · **Evitar** (termo ambíguo, com o motivo).

## Entidades do catálogo

| Termo | Status | Significado |
|---|---|---|
| **Squad** | Definido | Time responsável por um conjunto de apps — eixo de **quem cuida**. Todo app pertence a exatamente uma squad. |
| **App** (aplicação) | Definido | Unidade central do catálogo: uma aplicação lógica. Tem **id estável** (slug), que **não** é o nome do repositório e não depende de identificador externo. Pode ser movido de squad sem perder id nem histórico. É **componente com squad responsável**, não só código que roda: uma fila SQS ou um barramento EventBridge com repositório próprio também é app. Tem sigla, derivada dos seus repositórios (todos na mesma sigla). |
| **Repositório** | Definido | Repositório de código de um app. Um app tem **N** repositórios (ex.: legado + novo durante uma migração). **Repositório não é app** — decidir que dois repos são o mesmo app é curadoria humana. Tem **sigla**, **papel** e, se for de aplicação, **sigla-app**. |
| **Papel do repositório** | Definido | Para que serve o repo dentro do app: `aplicacao`, `infra`, `contrato`, `secrets`… Texto livre, como `Status` (sem enum sem decisão). Diferente de **tipo de app**. Sugerido pelo segmento do nome, confirmado pela pessoa. |
| **Repositório ativo** | Em aberto | O repositório atual de um app, em oposição a legados. `Repository.status` existe, mas sem a regra "no máximo um ativo por app". Ver backlog (CRUD de Repository). |
| **Projeto Sonar** | Definido | Projeto no SonarQube associado a um app. Um app tem 0..N; **não** é 1:1 com repositório. |
| **Link** (`AppLink`) | Definido | Endereço corporativo relacionado a um app (Confluence, runbook, dashboard): id, descrição e URL. |
| **Tag** | Definido (chave-valor: Em aberto) | Rótulo livre para **pessoas** categorizarem e filtrarem. Complementa os links; não os substitui. Evolução proposta: `chave:valor` com valor opcional, em app e repositório, chaves conhecidas listadas neste glossário. **Regra campo × tag**: se o sistema toma decisões com o dado (valida, define escopo, garante unicidade), é **campo**, não tag. |
| **Sigla** | Definido | Agrupador corporativo de repositórios, gerado e gerido pela organização, com uma conta AWS por ambiente. É **campo do repositório** (repos de infra também têm); o app herda a sigla dos seus repositórios, que ficam todos na mesma. **Cruzada com squad** — uma sigla tem apps de várias squads, e uma squad tem apps em várias siglas; nunca derivar uma da outra. Vira entidade se algo precisar de dados dela (ex.: as contas AWS). |
| **Sigla-app** | Definido | Código gerado pela esteira corporativa para cada **repositório de aplicação** (ex.: `AB1-1234`), lido de um arquivo YAML do repo. Identifica o repositório, **não o app**: um repo novo do mesmo app recebe outra. Repos de infra não têm. O trecho inicial é a sigla. |
| **Conta AWS** | Em aberto | Atributo da **sigla por ambiente** (dev, hom, prod). Nunca tag de repositório. |
| **Solução** | Em aberto | Grupo nomeado de apps que, conectados, entregam um fluxo — eixo de **o que compõe**, diferente de Squad. Leitura atual: "modal" e "carrinho" são **soluções**, e no mapa de aplicações uma solução é um **subgrafo nomeado**. O `id-solution` corporativo seria identificador externo dela, nunca a chave. |
| **Tipo de app** (`appType`) | Em aberto | O tipo de componente/runtime: `ecs`, `lambda`, `api-gateway`, `microfrontend`, `sqs`, `eventbridge`… Diferente de **papel do repositório**. Cuidado: "BFF" é **papel na cadeia**, não tipo — um BFF pode rodar em ECS ou Lambda. |

## Qualidade e histórico

| Termo | Status | Significado |
|---|---|---|
| **Métrica** | Definido | Dado **observado** no Sonar: coverage, bugs, vulnerabilities, code smells, duplications, ratings. |
| **Quality Level** | Definido | Classificação **derivada** (A–E, A é melhor) a partir das métricas, por uma regra organizacional. **Métrica ≠ avaliação**: são camadas separadas de propósito. |
| **Quality Assessment** (regra) | Definido | A regra que transforma métricas em Quality Level. É **versionada** (`v1` hoje); regras evoluem sem invalidar o passado. |
| **Snapshot** (`QualitySnapshot`) | Definido | Registro **imutável** do que foi observado num instante: métricas, Quality Level e a versão da regra que o classificou. Nunca é editado nem recalculado. |
| **Estado atual** | Definido | O Quality Level e as métricas do snapshot mais recente de um app. É **derivado** por consulta, não armazenado (ADR-009). |
| **Tendência** | Definido | Comparação entre os dois snapshots mais recentes de um app: `Improved` (melhorou), `Worsened` (piorou), `Stable` (estável) ou `New` (só um snapshot). |
| **Evolução da squad** | Definido | Distribuição de Quality Level por data de coleta, somando os apps da squad, mais a tendência de cada um. |

## Ciclo de vida e operações

| Termo | Status | Significado |
|---|---|---|
| **Ativo / Inativo** | Definido | `status` de squad ou app. **Inativar** é a forma normal de tirar algo de circulação: reversível, não apaga nada, preserva histórico. |
| **Remover** | Definido | Exclusão definitiva, permitida **só** quando não há dependentes com valor histórico: app sem nenhum snapshot e fora de qualquer checagem; squad sem nenhum app; checagem sem nenhum item. Caso contrário, a resposta é inativar (ADR-005). |
| **Seed** | Definido | Massa de dados de demonstração (2 squads, 5 apps, 3 meses de histórico) carregada por `tools/CheckApps.Seed`. |

## Checagens (spec 003, implementada)

| Termo | Status | Significado |
|---|---|---|
| **Checagem** | Definido na spec | Lista de trabalho do time para responder a uma demanda que atinge vários apps (exigência de segurança, direcionamento da empresa, atualização de dependência). Substitui a planilha montada à mão. **Não** pertence a uma squad — é transversal. **Não** é sistema de registro de governança. |
| **Item de checagem** | Definido na spec | Um app dentro de uma checagem, com status e observação. Um app pode estar em várias checagens ao mesmo tempo. |
| **Status do item** | Definido na spec | `pendente` (não iniciado) · `em-andamento` (iniciado, não terminado) · `conforme` (já estava adequado) · `nao-se-aplica` · `concluido`. Contam como concluídos: `conforme`, `nao-se-aplica`, `concluido`. Nuances ("implantado, aguardando validação", "bloqueado pela lib X") vão na **observação**. |
| **Fechar / Reabrir** | Definido na spec | Encerramento **manual** de uma checagem, sem exigir 100% — permite manter aberta, por tempo indefinido, uma pendência que o time não consegue priorizar. Fechada é a forma de "inativar" uma checagem: sai da lista padrão e continua consultável. |
| **Histórico da checagem** | Definido na spec | Registro só de acréscimo, com data e hora, de fechar, reabrir, entrada e saída de app e mudança de status de item. Nunca editado. "Quem fez" entra com autenticação. |

## Integrações e dados (futuro próximo)

| Termo | Status | Significado |
|---|---|---|
| **Proveniência** (`source`) | Definido | De onde um dado veio: `SONAR_MOCK`, futuramente `SONAR`, `GITHUB_SCAN`, cadastro manual. Para dado inferido, inclui **confiança**. É o que permite distinguir confirmado de inferido. |
| **Repositório descoberto** | Definido | Repositório encontrado pelo leitor de GitHub. A tela que os lista se chama **Repositórios**; o filtro "Novos" é a **caixa de entrada** (o que ainda precisa de decisão). Identidade `github:{id do GitHub}` (estável a renomeações). Status: `new`, `ignored`, `other-squad`; "já no catálogo" é derivado da existência de `Repository`, não um status. **O scan propõe, a pessoa decide.** |
| **No catálogo** | Definido | Um repositório descoberto está no catálogo quando existe um `Repository` com o mesmo provedor e id externo — **derivado, não um status**. Por isso ele não aparece em "Novos", mas **continua na tela Repositórios** (filtros "No catálogo" e "Todos"), com o link do app e sem ações de triagem; aparece no resultado do scan com a marca "já no catálogo"; e **volta** a "Novos" se o app for removido. |
| **Triagem** | Definido | A decisão da pessoa sobre cada descoberto: criar app, vincular a app existente (aviso, sem bloqueio, se a sigla difere da dos outros repositórios do app), de outra squad, ignorar, ou restaurar para "novo". |
| **Sugestão** | Definido | Papel, nome e **confiança** (alta/baixa) calculados na leitura a partir do nome do repositório e da presença de sigla-app; nunca armazenados. Confiança baixa = nome e sigla-app discordam (ex.: `infra` com sigla-app) ou segmento desconhecido. |
| **Escopo do scan** | Definido | O que o scan pode ler: **siglas** validadas por formato, nunca a organização inteira nem texto livre; protegido por limite de resultados (50 por padrão). **Siglas no scan, squad na triagem** — a squad vem do seletor do header ao criar o app, não do repositório. |
| **Pré-cadastro** | Definido | Sugestão de app (nome, squad, papel) montada a partir de um repositório descoberto: o painel "Criar app", pré-preenchido. Vive na caixa de entrada, **não** na tabela de apps; o app só passa a existir quando uma pessoa confirma. |
| **Descoberta** | Definido | O que o "scan" da fase 1 faz: achar repositórios de uma sigla e levá-los à triagem. Os nomes do código (`ScanService`, `GitHubScan`, `Scan:Enabled`) não mudam. |
| **Análise** | Definido | Ler os arquivos de um repositório **já catalogado** e extrair fatos (versão do .NET, pacotes). Uma por execução; cada uma é um snapshot (histórico), e a **corrente** é a mais recente que **não falhou** (uma falha fica no histórico mas não esconde a última análise boa). Idempotente por commit. |
| **Scanner** (tipo de scanner) | Definido | Componente de análise de **uma tecnologia** (primeiro: .NET). Escolhido por detecção de arquivos (`*.csproj` → .NET). Determinístico; toda saída diz qual scanner e versão a gerou. Outras tecnologias entram como scanners novos. Não confundir com as skills de `.agents/skills/`, que são instruções para agentes. |
| **Fato** (de análise) | Definido | Registro extraído por um scanner, sempre com **origem** (`PARSER`, futuramente `RULE`/`AI`), **confiança** (alta/média/baixa, mostrada em texto) e arquivo de origem. IA, quando houver, só sugere. |
| **Integração declarada** | Definido | Endereço que um repositório **declara** chamar, lido de um arquivo de configuração (hoje `appsettings*.json`) — não é uma chamada observada em execução. Guarda só o endereço normalizado, o arquivo e a chave de origem, o ambiente e a confiança. Não diz a que app do catálogo o host pertence. |
| **Host** (de integração) | Definido | Chave literal `host[:porta]` de uma integração declarada, inclusive o **modelo** com `{env}` (ex.: `empresa.{env}.exemplo.com`). O host em si não é uma entidade: o que existe é o **vínculo** dele com um app (linha seguinte). |
| **Host externo** | Definido | Decisão **humana** (spec 019) de que um host é de um serviço **fora do catálogo** (outra squad, terceiro, legado): sai da fila "sem app" sem virar app. Nome livre opcional; vale por host ou por modelo `{env}`; não convive com um app confirmado no mesmo host (rejeitos convivem). Guardado em `host_externals`. |
| **Vínculo host→app** | Definido | Decisão **humana** (spec 017) de que um host é o app que **recebe** a chamada: `confirmado` ou `rejeitado` (`sugerido` reservado, ainda sem uso). Vale por host (não por seção); um confirmado por host; o do host exato vence o do modelo `{env}`, que cobre `dev`, `hom` e `prod`. Guardado em `host_links`, fora das análises. |
| **Pacote interno** | Definido | Pacote cujo nome começa com o prefixo da empresa (configuração local, não versionada). Marcação calculada na leitura. É o sinal mais forte de acoplamento entre apps. |
| **Dependência de biblioteca** | Definido (na tela: **Pacotes**) | Pacote declarado num manifesto (`PackageReference` do `.csproj`, `package.json`…), com a versão **declarada** (só diretas; transitivas ficam de fora por ora). Guardada como fato de uma análise, portanto como snapshot. |
| **Ligação** (dependência entre apps) | Em aberto | Aresta do mapa: o app A depende do app B. Sempre com origem (manual, IaC, inferida por IA) e confiança. **Diferente** de dependência de biblioteca. |
| **Mapa de aplicações** | Em aberto | Grafo de **apps** (não de repositórios) e suas ligações, colorível por Quality Level, versão, vulnerabilidade ou status de checagem. |

## Personas

| Termo | Significado |
|---|---|
| **Tech Lead** | Usuário mais frequente. Cuida de **uma** squad. |
| **Coordenador de grupo** | Usuário frequente. Acompanha de **2 a 5** squads; trocar de squad é sua navegação principal. |
| **Coordenação ampla** | Futuro. Na faixa de **10 a 15** squads. |
| **Dev** | Uso provável, ainda não confirmado: acompanhar o próprio app e atuar nos itens de checagem. Pode ter acesso restrito — decisão que depende de autenticação. |

## Termos a evitar

| Termo | Por quê | Usar no lugar |
|---|---|---|
| **Arquitetura** (como nome de campo ou classificação) | É o termo mais sobrecarregado do projeto: pilar do estágio Context, nome de `.agents/architecture/`, nome dos diagramas futuros. | **Solução** (grupo de apps) ou **tipo de app** (runtime) |
| **Topologia** (como atributo do app) | Significa arranjo de conexões entre nós — exatamente o que o mapa de aplicações é. Usar para classificar um app isolado cria ambiguidade. | **Solução**, se for um grupo; reservar "topologia" para falar do mapa como um todo |
| **Dashboard** (em navegação) | Hoje aparece em breadcrumb no lugar do nome da squad, o que não diz onde a pessoa está. | O **nome da squad** |
| **Demanda** (como entidade) | A spec 003 decidiu que a checagem **é** a demanda vista pelo time; uma entidade a mais não paga o custo. | **Checagem** |
