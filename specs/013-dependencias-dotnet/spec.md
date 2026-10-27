# Spec 013: Dependências de biblioteca e versão do .NET (primeiro scanner)

**Status**: Implementada (2026-10-24); confirmada pelo usuário na tela com o GitHub real em 2026-10-26
**Criada em**: 2026-10-24
**Origem**: fase 2 do "Leitor de repositórios GitHub" (`backlog.md`) e a análise de 2026-10-24 sobre integrações e dependências. Entrega o primeiro **scanner** (.NET) sobre um contrato extensível por tecnologia, e a visão que responde "quem usa o pacote X, em qual versão, de qual squad". É também a base da futura "Detecção automática de status" das Checagens.

> Formato compatível com o template de spec do Spec-Kit (github/spec-kit). Ambiguidades estão marcadas com `[NEEDS CLARIFICATION]` e viram perguntas antes da aprovação.

## Escopo e fronteira

**Faz parte**

1. **Análise de repositórios do catálogo.** Uma ferramenta de linha de comando lê os arquivos de projeto de cada repositório já catalogado e grava **fatos**: a versão do .NET e os pacotes com a versão declarada. Lê o formato SDK (`TargetFramework`, `PackageReference`) **e o formato legado** do .NET Framework (`TargetFrameworkVersion`, `packages.config`).
2. **Scanner como componente extensível.** Contrato `IRepositoryScanner` com registro; o scanner **.NET** é o primeiro, embutido. A escolha do scanner é por **detecção de arquivos** (`*.csproj` → .NET). Outras tecnologias entram depois como scanners novos, sem mudar o modelo.
3. **Modelo de fatos neutro à tecnologia**, com **origem** e **confiança** em cada registro desde a primeira tabela, e **snapshot por análise** (histórico, como o `QualitySnapshot`).
4. **Acesso a arquivos por porta nova** (`IRepositoryContentSource`), com fonte `github` e fonte `fake`.
5. **Telas e API de leitura**: seção **Dependências** no detalhe do app; tela **Dependências** (menu) com a consulta "quem usa o pacote X"; panorama de versão do .NET por squad (P3).
6. **Marcação de pacote interno** por prefixo configurável (a convenção da empresa é o nome dela no início do namespace, como `Empresa.SDK.TokenManager`). Calculada na leitura, nunca gravada, e desligada enquanto o prefixo não estiver configurado.

**Não faz parte** (cada item é candidato a spec própria)

- Integrações entre apps (`appsettings`, Refit, `HttpClient`), apelidos host→app, mapa de aplicações e listas auxiliares nas Checagens.
- Dependências **transitivas** (o `.csproj` só mostra as diretas) e a API de SBOM do GitHub.
- Outras tecnologias (`package.json`, `pom.xml`…), IaC e qualquer uso de IA.
- Disparar a análise pela tela ou por agendador (só linha de comando, como a fase 1 começou).
- Regras declarativas configuráveis por squad.
- `ProjectReference`, `NuGet.config`, `packages.lock.json`, `*.fsproj`/`*.vbproj`.
- Resolver o que um pacote interno *faz* ou a quem pertence (a marcação só diz "interno").

## User Scenarios & Testing *(obrigatório)*

### User Story 1 — Analisar os repositórios de uma sigla (Prioridade: P1)

Como pessoa que mantém o catálogo, rodo a análise para uma sigla e o CheckApps registra, para cada repositório catalogado, o que ele usa: versão do .NET e pacotes. Rodar de novo não duplica nada se o código não mudou.

**Por que esta prioridade**: sem fatos gravados nada das outras histórias existe.

**Teste independente**: com `--source fake`, rodar a ferramenta e conferir no banco as análises e os fatos, sem API nem tela.

**Cenários de aceite**:

1. **Dado** repositórios catalogados da sigla `ab1`, **Quando** rodo `dotnet run --project tools/CheckApps.Analyze -- --sigla ab1 --source fake`, **Então** cada repositório recebe uma análise com os fatos dos seus `.csproj`, e a saída resume quantos foram analisados, sem alteração, parciais e com falha.
2. **Dado** um repositório já analisado no mesmo commit, **Quando** rodo de novo, **Então** nenhuma análise nova é criada (resultado "sem alteração").
3. **Dado** o repositório recebeu um commit novo, **Quando** rodo de novo, **Então** uma análise nova é criada e a anterior fica como histórico.
4. **Dado** `--dry-run`, **Quando** rodo, **Então** a saída mostra o que seria gravado e **nada é gravado**.
5. **Dado** uma sigla inválida ou ausente, **Então** a ferramenta recusa antes de pedir credencial ou ler qualquer coisa.
6. **Dado** a fonte `github` sem credencial, **Então** a ferramenta explica o que configurar, sem mostrar valores de configuração.

### User Story 2 — Ver as dependências de um app (Prioridade: P1)

Como tech lead, abro o detalhe do meu app e vejo, por repositório, a versão do .NET e a lista de pacotes com versão, de onde vieram e o quanto o CheckApps confia em cada informação.

**Por que esta prioridade**: é a tela que mostra se o dado está bom; sem ela o scanner não é verificável por pessoas.

**Teste independente**: com os fatos da história 1 gravados, abrir `/apps/:id` e conferir a seção.

**Cenários de aceite**:

1. **Dado** um app cujo repositório foi analisado, **Quando** abro o detalhe, **Então** vejo a seção **Dependências** com: data e commit da análise, framework(s) e a tabela de pacotes (nome, versão, projeto de origem, confiança).
2. **Dado** um app com mais de um repositório (ex.: legado e novo), **Então** a seção agrupa por repositório e mostra a situação de cada um (`active`, `legacy`).
3. **Dado** um repositório sem análise, **Então** a seção diz "ainda não analisado" e como analisar, sem erro.
4. **Dado** uma análise parcial ou com aviso (ex.: um `.csproj` inválido), **Então** o aviso aparece em texto, e os fatos dos demais arquivos continuam visíveis.
5. **Dado** um fato com confiança menor que alta, **Então** a confiança aparece **em texto** ("alta", "média", "baixa"), nunca só por cor, com o motivo (ex.: "versão vem de `$(Variavel)` não resolvida").
6. **Dado** um repositório sem nenhum scanner aplicável (ex.: sem `.csproj`), **Então** a seção diz que nenhum scanner se aplica e não mostra tabela vazia como se fosse erro.
7. **Dado** um repositório com projeto legado (.NET Framework 4.8 com `packages.config`), **Então** a seção mostra o framework `net48` e os pacotes do `packages.config`, como nos projetos SDK.
8. **Dado** um prefixo de pacotes internos configurado, **Então** os pacotes que começam com ele aparecem com a etiqueta "interno" (em texto); sem prefixo configurado, nenhuma etiqueta aparece.

### User Story 3 — Quem usa este pacote? (Prioridade: P2)

Como coordenador, procuro um pacote (ex.: por causa de uma vulnerabilidade) e vejo quais apps o usam, em qual versão e de qual squad, em segundos.

**Por que esta prioridade**: é o caso de uso que mais justifica a fase 2, mas depende da história 1.

**Teste independente**: com fatos gravados, abrir **Dependências** no menu e buscar um pacote.

**Cenários de aceite**:

1. **Dado** o nome de um pacote, **Quando** busco, **Então** vejo uma linha por app e repositório com versão, projeto de origem, squad e data da análise; a busca ignora diferença de maiúsculas e minúsculas.
2. **Dado** o pacote usado em versões diferentes, **Então** vejo um resumo de versões com a contagem de apps por versão.
3. **Dado** um filtro de squad, **Então** só aparecem os apps dela.
4. **Dado** um pacote que ninguém usa, **Então** a tela diz que nenhum app usa e mostra a data da análise mais recente do catálogo, para a pessoa saber de quando é a resposta.
5. **Dado** repositórios nunca analisados, **Então** a tela avisa quantos repositórios catalogados ainda não têm análise (a resposta pode estar incompleta).
6. **Dado** o nome do pacote vazio, **Então** a tela não consulta e pede o nome.
7. **Dado** um prefixo de pacotes internos configurado, **Quando** marco "apenas internos" e deixo o nome em branco, **Então** vejo os pacotes internos mais usados com a contagem de apps (o nome só é obrigatório fora desse filtro).

### User Story 4 — Panorama de versão do .NET por squad (Prioridade: P3)

Como coordenador de grupo, vejo quantos apps de cada squad estão em cada versão do .NET e abro a lista de quem ainda está numa versão antiga.

**Por que esta prioridade**: aproveita os mesmos fatos e prepara a detecção automática nas Checagens, mas não bloqueia nada.

**Teste independente**: abrir a aba de panorama da tela **Dependências**.

**Cenários de aceite**:

1. **Dado** análises gravadas, **Quando** abro o panorama, **Então** vejo uma tabela framework × squad com a contagem de apps, e cada célula abre a lista de apps.
2. **Dado** um app com repositórios em frameworks diferentes, **Então** conta uma vez em cada framework em que há repositório **ativo**; repositórios `legacy` e `archived` não entram na contagem, e um app que só tem repositórios assim cai em "Sem informação" (a situação de cada repositório aparece na seção Dependências do app).
3. **Dado** apps sem análise ou sem scanner aplicável, **Então** aparecem em "sem informação", não somem.

### Edge Cases

- **Projeto legado, fora do formato SDK** (.NET Framework, decisão do usuário de 2026-10-24): `TargetFrameworkVersion` (`v4.8`) vira o framework `net48` (`v4.7.2` → `net472`, `v4.0` → `net40`; versão que não segue o padrão fica como declarada, confiança baixa); os pacotes vêm do `packages.config` ao lado do projeto (versão literal, confiança alta) e de `PackageReference`, se houver; `Reference` com `HintPath` não gera fato (evita duplicar o pacote). O arquivo declara o namespace `http://schemas.microsoft.com/developer/msbuild/2003`: o parser compara pelo nome local dos elementos. Fixture: `tests/CheckApps.UnitTests/Fixtures/Dotnet/05-legado-nao-sdk`.
- **`packages.config` sem projeto ao lado, ou inválido**: aviso e análise `parcial`.
- **Repositório sem `.csproj`** (infra, front-end): análise com situação `sem-scanner`, nenhum fato, sem erro.
- **`.csproj` com XML inválido**: aviso na análise (situação `parcial`); os outros arquivos seguem.
- **Gerenciamento central de pacotes** (`Directory.Packages.props` com `ManagePackageVersionsCentrally`): `PackageReference` sem `Version` usa o `PackageVersion` do arquivo mais próximo acima do projeto; confiança **média**.
- **Versão por propriedade** (`$(Variavel)`): resolvida só se a propriedade está no próprio `.csproj`, no `Directory.Build.props` ou no `Directory.Packages.props` mais próximos, com valor literal; senão fica o texto cru, confiança **baixa**, com aviso.
- **Versão flutuante ou intervalo** (`1.*`, `[1.0,2.0)`): guardada como declarada, confiança **média**; o CheckApps não resolve versão.
- **`TargetFrameworks` com vários valores**: uma linha de framework por valor.
- **`TargetFramework` ausente no projeto**: usa o do `Directory.Build.props` mais próximo; sem ele, aviso e nenhum framework.
- **Mesmo pacote em vários projetos do repositório**: uma linha por projeto.
- **Nome de pacote com caixa diferente** (`Newtonsoft.Json` × `newtonsoft.json`): mesmo pacote.
- **`Condition` em `ItemGroup`/`PropertyGroup`**: o item entra mesmo assim; a condição não é avaliada (limite conhecido).
- **Pastas `bin/`, `obj/`, `node_modules/`**: ignoradas.
- **Muitos arquivos ou arquivo grande**: limite de arquivos de projeto por repositório e de tamanho por arquivo; excedido = aviso e análise `parcial`, nunca leitura em volume.
- **Árvore de arquivos truncada pela API do GitHub**: aviso e análise `parcial`.
- **Repositório arquivado**: é analisado normalmente e a tela mostra a situação.
- **Repositório vazio, sem ramo padrão ou sem acesso** (acesso parcial é o normal): falha **daquele** repositório, registrada na saída; os demais continuam.
- **Repositório renomeado**: lido pelo id externo do provedor, não pelo nome.
- **Repositório removido do catálogo depois**: as análises acompanham o repositório (cascata), sem órfãos.

## Requirements *(obrigatório)*

### Functional Requirements

- **FR-001**: O sistema DEVE definir o contrato `IRepositoryScanner` (id, versão, "aplica-se a esta lista de arquivos?", "analise estes arquivos") e um registro de scanners; o scanner .NET se aplica a repositórios com ao menos um `*.csproj`. Nenhum código fora do scanner conhece o formato de `.csproj`.
- **FR-002**: O sistema DEVE definir a porta `IRepositoryContentSource` (listar arquivos de um repositório e ler um arquivo, por repositório, ramo padrão e commit), devolvendo tipos nossos e nunca o JSON do provedor, com implementações `github` e `fake`. A porta expõe `ConfigurationProblem`, como a `IRepositoryDiscoverySource`.
- **FR-003**: O scanner .NET DEVE extrair de cada `.csproj`: os `TargetFramework`/`TargetFrameworks` (ou o `TargetFrameworkVersion` normalizado, em projeto legado), cada `PackageReference` (nome e versão declarada), os pacotes do `packages.config` do mesmo diretório e o caminho do projeto, aplicando a resolução de versão descrita em *Edge Cases* (versão do próprio item, `VersionOverride`, `Directory.Packages.props`, propriedades).
- **FR-004**: Cada fato DEVE ter **origem** (`PARSER` nesta spec), **confiança** (`alta`, `media`, `baixa`), scanner (id e versão) e arquivo de origem. Confiança: **alta** = valor literal no projeto; **média** = vindo do gerenciamento central, flutuante ou intervalo; **baixa** = texto cru de propriedade não resolvida.
- **FR-005**: Cada execução sobre um repositório DEVE gerar uma **análise** com data, commit analisado, situação (`ok`, `parcial`, `sem-scanner`, `falha`) e avisos; os fatos pertencem a ela. Análises anteriores permanecem como histórico. A análise **corrente** é a mais recente cuja situação não é `falha`: uma falha é gravada (a tela mostra "a última tentativa falhou") mas não esconde a última análise boa.
- **FR-006**: A análise DEVE ser idempotente por commit: se o commit analisado for o da análise **corrente** do repositório, nada é gravado (e `--dry-run` não grava nunca).
- **FR-007**: A ferramenta `tools/CheckApps.Analyze` DEVE aceitar `--sigla` (obrigatório, validado por formato **antes** de qualquer credencial ou leitura), `--source github|fake` e `--dry-run`, e analisar os repositórios do **catálogo** daquela sigla cujo provedor a fonte atende. Falha de um repositório não interrompe os outros.
- **FR-008**: O scan e a análise DEVEM compartilhar a composição (`AddCheckAppsInfrastructure`), sem que a fonte lance exceção no construtor sem credencial.
- **FR-009**: A fonte `github` DEVE ler com o mesmo token somente leitura da fase 1 (`Contents` e `Metadata`), listar a árvore do ramo padrão e ler **apenas** os arquivos de interesse (`*.csproj`, `packages.config`, `Directory.Packages.props`, `Directory.Build.props`), respeitando os limites de FR-010. Mensagens de erro são montadas por nós, sem corpo de resposta nem cabeçalhos; há teste de que o token não aparece.
- **FR-010**: O sistema DEVE limitar o volume lido por repositório (número máximo de arquivos de projeto e tamanho máximo por arquivo, configuráveis, com padrões conservadores); ao exceder, a análise é `parcial` com aviso.
- **FR-011**: O sistema NÃO DEVE gravar código-fonte nem o conteúdo dos arquivos lidos: apenas os fatos extraídos.
- **FR-012**: `GET /api/apps/{appId}/dependencies` DEVE devolver, por repositório do app, a análise mais recente (ou `null`), os frameworks e os pacotes; 404 se o app não existe.
- **FR-013**: `GET /api/dependencies/packages?name=&squadId=` DEVE devolver as linhas de FR/US3 a partir da análise **mais recente de cada repositório**; `name` obrigatório (400 se vazio, salvo com `internalOnly=true`), nome **exato** sem diferença de caixa; devolve também o total de repositórios catalogados sem análise e a data da análise mais recente.
- **FR-014**: `GET /api/dependencies/frameworks` DEVE devolver o panorama de US4 (framework × squad com os apps), considerando repositórios `active`; apps sem informação em grupo próprio.
- **FR-015**: A API de leitura NÃO DEVE disparar análise nem exigir credencial do provedor.
- **FR-016**: O detalhe do app DEVE ter a seção **Dependências** (US2) e o menu lateral a entrada **Dependências** (US3, US4), com ícone SVG embutido, `aria-current` e comportamento de menu recolhido como as demais.
- **FR-017**: Consultas DEVEM usar SQL portável (sem `GROUP_CONCAT`, datas ISO-8601 UTC via `IsoTimestamp`, comparação de nome por `lower(...)`), conforme `backend.md`.
- **FR-018**: A coluna/tabela nova DEVE entrar no `schema.sql` (`CREATE TABLE IF NOT EXISTS`); como são tabelas novas, não exigem `AdditiveColumns`, mas um teste sobre banco antigo (`SchemaEvolutionTests`) DEVE provar que subir a versão nova cria as tabelas sem tocar nas existentes.
- **FR-019**: Nenhum teste automatizado DEVE usar rede; o cliente HTTP do GitHub é testado com `HttpMessageHandler` falso e JSON no formato real.
- **FR-020**: As fixtures de teste e da fonte `fake` DEVEM cobrir os casos de `tests/CheckApps.UnitTests/Fixtures/Dotnet/` (ver `EXPECTED.md` de cada cenário). As fixtures são inventadas (decisão do usuário de 2026-10-24: bastam por enquanto; casos reais são acrescentados quando necessário); a verificação da spec inclui conferir o parser contra um repositório real com `--dry-run`.
- **FR-021**: O sistema DEVE marcar um pacote como **interno** quando o nome começa (sem diferença de caixa) com um dos prefixos de `Analysis:InternalPackagePrefixes`. A marcação é **calculada na leitura** (mudar o prefixo vale também para o histórico) e **nunca gravada**. O prefixo real da empresa é configuração local (`dotnet user-secrets` ou variável de ambiente) e **não entra em arquivo versionado nem na documentação**; exemplos usam `Empresa.`. Sem prefixo configurado, nada é marcado. A API de pacotes aceita `internalOnly=true` e devolve o indicador `internal` em cada linha.

### Key Entities

- **RepositoryAnalysis** (`repository_analyses`): uma execução de análise sobre um `Repository`.
  - **Identidade**: id estável (guid). **Relações**: pertence a um `Repository` (cascata), que pertence a um `App` e este a uma squad. **Proveniência**: `source = GITHUB_SCAN` (a análise só lê do provedor). **Tempo**: `analyzed_at`, `commit_sha`; **histórico** (cada análise nova é uma linha nova, a corrente é a mais recente por repositório, derivada por consulta, nunca armazenada).
  - Atributos: `status` (`ok` | `parcial` | `sem-scanner` | `falha`), `warnings` (texto, um aviso por linha).
- **RepositoryDependency** (`repository_dependencies`): um fato extraído numa análise.
  - **Identidade**: id + (análise, ecossistema, tipo, nome, projeto). **Relações**: pertence a uma `RepositoryAnalysis` (cascata). **Proveniência**: `origin` (`PARSER`), `scanner_id`, `scanner_version`, `source_file`; **confiança** `alta|media|baixa` com `confidence_note` em texto. **Tempo**: o da análise.
  - Atributos: `ecosystem` (`nuget`, `dotnet` para framework), `kind` (`package` | `framework`), `name`, `version` (declarada, nula para framework), `project_path`. O indicador "interno" **não é coluna**: deriva do nome e da configuração (FR-021).
- Índices: `repository_analyses (repository_id, analyzed_at DESC)`; `repository_dependencies (analysis_id)` e `(ecosystem, kind, lower(name))`. Ver `.agents/architecture/data-model/sqlite-schema.md` para atualizar.

## Success Criteria *(obrigatório)*

- **SC-001**: Em até 30 segundos depois de a análise terminar, uma pessoa descobre quais apps de todas as squads usam um pacote qualquer, em que versão e de qual squad.
- **SC-002**: O detalhe de um app mostra a versão do .NET e os pacotes com versão sem a pessoa abrir nenhum repositório, e deixa claro o que o CheckApps não conseguiu resolver, em texto.
- **SC-003**: Rodar a análise duas vezes sobre código inalterado não cria nenhum registro novo.
- **SC-004**: Nenhum segredo, token ou código-fonte aparece no banco, em log ou em mensagem de erro.
- **SC-005**: Adicionar um scanner de outra tecnologia não exige mudar tabelas, API de leitura ou telas; só um scanner novo e seus testes (provado por um scanner mínimo de `package.json` no projeto de testes).

## Assumptions

- A análise lê o **ramo padrão** do repositório; análise de outros ramos ou tags fica para depois.
- O repositório catalogado tem `provider` e `external_id`; a fonte `github` obtém o nome completo (organização/nome) pelo id externo.
- A fonte `fake` serve fixtures por nome de repositório (cobre os repositórios do scan fake depois de vinculados e os do seed); o mapeamento exato é detalhe de implementação.
- Só dependências **diretas** nesta spec; a limitação é mostrada na tela ("dependências transitivas não incluídas").
- A análise roda por linha de comando em máquina de quem opera, no modelo da fase 1; sem endpoint de disparo, portanto **sem a questão de autenticação** da ADR-007.
- Pacote interno = nome que começa com o nome da empresa (convenção informada em 2026-10-24, ex.: `{empresa}.SDK.TokenManager`); é o sinal mais forte de acoplamento entre apps. O valor real do prefixo fica só em configuração local.
- Projetos legados do .NET Framework fazem parte do escopo, para o panorama de versão não ignorar o legado.
- O vocabulário muda: **descoberta** (scan atual de repositórios), **análise** (ler arquivos de um repositório catalogado) e **scanner** (componente de análise de uma tecnologia). O código da fase 1 mantém os nomes `Scan*`.

## Design notes *(opcional)*

### Notas da implementação (2026-10-24)

- **Corrente = a mais recente que não falhou** (decisão de implementação; ver FR-005/FR-006). Uma falha de leitura cria uma linha de histórico, mas o commit é comparado com a corrente boa.
- **Busca de pacote por nome exato**, sem diferença de caixa; com "apenas internos" e nome em branco lista os pacotes internos em uso (descoberta de nomes). Sem prefixo configurado, "apenas internos" responde vazio e a tela explica o motivo.
- **`CheckApps.Analyze` compartilha o `UserSecretsId` do `CheckApps.GitHubScan`**: um token configurado serve às duas. A API continua com o armazenamento dela (é nela que o prefixo de pacotes internos precisa estar).
- **Uma só chave `Scan:Source` (`github` | `fake`)** escolhe a fonte de descoberta e a de conteúdo.
- **A ferramenta analisa os repositórios do catálogo cujo `sigla` bate.** O seed não preenche `sigla` nos repositórios; a demonstração é: `GitHubScan --source fake` → criar apps pela triagem → `Analyze --source fake`. A fonte `fake` só lê repositórios de provedor `fake`.
- **A fonte `fake` serve um subconjunto em texto dentro do código** (padrão do `FakeRepositorySource`); o conjunto completo e comentado fica em `tests/CheckApps.UnitTests/Fixtures/Dotnet/`.
- **Leitura de arquivos no GitHub**: `repositories/{id}` → `commits/{ramo}` → `git/trees/{sha}?recursive=1` → `contents/{caminho}?ref={sha}` só do que o scanner pediu. `GitHubApiClient` é compartilhado com a descoberta.
- **Limites conhecidos**: dependências transitivas, `Condition` não avaliada, só o `Directory.*.props` mais próximo vale, e **não há como reanalisar o mesmo commit** (corrigir um scanner só reflete em commits novos; uma opção de forçar fica como melhoria).
- **Painel .NET por squad** considera só repositórios ativos e apps ativos (decisão de leitura, ver US4).

- **Camadas**: acesso a arquivos (`IRepositoryContentSource`) → scanner (`IRepositoryScanner`) → fatos neutros. O serviço de aplicação (`AnalysisService`) orquestra: lista repositórios do catálogo, lê a árvore, escolhe os scanners que se aplicam, grava a análise. Não há `if (dotnet)` fora do scanner.
- **Núcleo determinístico, IA depois**: os scanners são parsers; `origin` e `confidence` existem desde já para um enriquecedor futuro gravar `origin = AI` como sugestão na triagem, nunca como fato. Nada de IA nesta spec.
- **Segurança**: a redação de segredos pertence ao **núcleo**, não a cada scanner; esta spec não lê arquivos com segredo, mas o ponto de extensão (filtro aplicado a todo fato antes de gravar) deve existir para a spec de `appsettings`.
- **Leitura de arquivos**: Git Trees API (`git/trees/{sha}?recursive=1`) para listar e leitura pontual dos arquivos de interesse; `zipball` só será necessário quando houver análise de código (Refit), em spec futura.
- **ADR-011** (`.agents/architecture/decisions/adr-011-scanners-por-tecnologia.md`, *Aceita* em 2026-10-24): contrato do scanner e seleção por detecção de arquivos; núcleo determinístico com IA só como sugestão com proveniência; modelo de fatos com origem/confiança; regra de não persistir conteúdo e de redação no núcleo; vocabulário descoberta × análise × scanner.
- **Glossário**: este rascunho já acrescenta Descoberta, Análise e Scanner e muda "Dependência de biblioteca" para "Em definição (spec 013)".
- **Evolução prevista** (fora desta spec): integrações por `appsettings` com apelidos host→app confirmados por pessoa, Roslyn para Refit/`HttpClient`, regras declarativas (primeiro em arquivo versionado, por squad só depois da decisão de autenticação), mapa de aplicações e listas auxiliares nas Checagens.
