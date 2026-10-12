# Glossário do domínio

Vocabulário do CheckApps. Serve a pessoas de squads diferentes (para que um termo signifique a mesma coisa para todos) e a agentes de IA (para interpretarem os termos como o domínio interpreta). Criado em 2026-10-12, a revisar.

**Status de cada termo**: **Definido** (em uso no código e nas specs) · **Em aberto** (decisão pendente — não usar como se estivesse resolvido) · **Evitar** (termo ambíguo, com o motivo).

## Entidades do catálogo

| Termo | Status | Significado |
|---|---|---|
| **Squad** | Definido | Time responsável por um conjunto de apps — eixo de **quem cuida**. Todo app pertence a exatamente uma squad. |
| **App** (aplicação) | Definido | Unidade central do catálogo: uma aplicação lógica. Tem **id estável** (slug), que **não** é o nome do repositório e não depende de identificador externo. Pode ser movido de squad sem perder id nem histórico. |
| **Repositório** | Definido | Repositório de código de um app. Um app tem **N** repositórios (ex.: legado + novo durante uma migração). **Repositório não é app** — decidir que dois repos são o mesmo app é curadoria humana. |
| **Repositório ativo** | Em aberto | O repositório atual de um app, em oposição a legados. `Repository.status` existe, mas sem a regra "no máximo um ativo por app". Ver backlog (CRUD de Repository). |
| **Projeto Sonar** | Definido | Projeto no SonarQube associado a um app. Um app tem 0..N; **não** é 1:1 com repositório. |
| **Link** (`AppLink`) | Definido | Endereço corporativo relacionado a um app (Confluence, runbook, dashboard): id, descrição e URL. |
| **Tag** | Definido | Rótulo livre para categorizar um app. Complementa os links; não os substitui. Convenção de prefixo (`solucao:`) é o primeiro passo cogitado para classificar por solução. |
| **Sigla** | Definido | Agrupador corporativo de repositórios, normalmente associado a uma conta AWS. É atributo do **repositório**: a sigla-app identifica o repositório, não o app. **Cruzada com squad** — uma sigla tem apps de várias squads, e uma squad tem apps em várias siglas; nunca derivar uma da outra. Hoje é atributo e parâmetro do scan; vira entidade se algo precisar de dados dela (ex.: a conta AWS). Perguntas em aberto no backlog (leitor de GitHub). |
| **Solução** | Em aberto | Grupo nomeado de apps que, conectados, entregam um fluxo — eixo de **o que compõe**, diferente de Squad. Leitura atual: "modal" e "carrinho" são **soluções**, e no mapa de aplicações uma solução é um **subgrafo nomeado**. O `id-solution` corporativo seria identificador externo dela, nunca a chave. |
| **Tipo de app** (`appType`) | Em aberto | O tipo de componente/runtime: `ecs`, `lambda`, `api-gateway`, `microfrontend`, `infra`… Cuidado: "BFF" é **papel na cadeia**, não tipo — um BFF pode rodar em ECS ou Lambda. |

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
| **Remover** | Definido | Exclusão definitiva, permitida **só** quando não há dependentes com valor histórico: app sem nenhum snapshot; squad sem nenhum app. Caso contrário, a resposta é inativar (ADR-005). |
| **Seed** | Definido | Massa de dados de demonstração (2 squads, 5 apps, 3 meses de histórico) carregada por `tools/CheckApps.Seed`. |

## Checagens (spec 003, em análise)

| Termo | Status | Significado |
|---|---|---|
| **Checagem** | Definido na spec | Lista de trabalho do time para responder a uma demanda que atinge vários apps (exigência de segurança, direcionamento da empresa, atualização de dependência). Substitui a planilha montada à mão. **Não** pertence a uma squad — é transversal. **Não** é sistema de registro de governança. |
| **Item de checagem** | Definido na spec | Um app dentro de uma checagem, com status e observação. Um app pode estar em várias checagens ao mesmo tempo. |
| **Status do item** | Definido na spec | `pendente` (não iniciado) · `em-andamento` (iniciado, não terminado) · `conforme` (já estava adequado) · `nao-se-aplica` · `concluido`. Contam como concluídos: `conforme`, `nao-se-aplica`, `concluido`. Nuances ("implantado, aguardando validação", "bloqueado pela lib X") vão na **observação**. |
| **Fechar / Reabrir** | Definido na spec | Encerramento **manual** de uma checagem, sem exigir 100% — permite manter aberta, por tempo indefinido, uma pendência que o time não consegue priorizar. |

## Integrações e dados (futuro próximo)

| Termo | Status | Significado |
|---|---|---|
| **Proveniência** (`source`) | Definido | De onde um dado veio: `SONAR_MOCK`, futuramente `SONAR`, `GITHUB_SCAN`, cadastro manual. Para dado inferido, inclui **confiança**. É o que permite distinguir confirmado de inferido. |
| **Repositório descoberto** | Em aberto | Repositório encontrado pelo leitor de GitHub e ainda não triado. A triagem decide: criar app, vincular a app existente, de outra squad ou ignorar. **O scan propõe, a pessoa decide.** |
| **Escopo do scan** | Em aberto | O que o scan pode ler: **squad + siglas**. Nunca a organização inteira; protegido por limite de resultados. A squad vem do contexto do scan, não do repositório. |
| **Pré-cadastro** | Em aberto | Sugestão de app (nome, squad, tipo) montada a partir de um repositório descoberto. Vive na caixa de entrada, **não** na tabela de apps; o app só passa a existir quando uma pessoa confirma. |
| **Dependência de biblioteca** | Em aberto | Pacote declarado num manifesto (`PackageReference` do `.csproj`, `package.json`…), com versão. Guardada como snapshot por scan. |
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
