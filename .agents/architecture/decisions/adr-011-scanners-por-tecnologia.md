# ADR-011: Scanners por tecnologia — núcleo determinístico, fatos com proveniência e IA só como sugestão

**Status**: Aceita · **Data**: 2026-10-24

## Contexto

A spec 013 abre a leitura do **conteúdo** dos repositórios já catalogados (a fase 1, ADR-010, só descobre repositórios por sigla). O primeiro caso é o .NET (versão do framework e pacotes do `.csproj`), mas a intenção é o mesmo mecanismo servir a outras tecnologias da empresa e, depois, a integrações entre apps (`appsettings`, Refit/`HttpClient`) e a um mapa de aplicações. Cinco decisões precisam ser tomadas antes do primeiro scanner, porque são caras de mudar depois:

1. **Como separar tecnologias** sem espalhar `if (dotnet)` pelo sistema.
2. **Que modelo de dados** guarda o que um scanner extrai, para servir a qualquer tecnologia.
3. **Determinismo × IA** na extração.
4. **O que se grava** do conteúdo lido, dado que arquivos de configuração podem conter segredos.
5. **Vocabulário**, já que "scan" significa hoje a descoberta da fase 1.

## Decisão

- **Contrato `IRepositoryScanner` + registro, scanners embutidos.** Cada scanner declara id, versão e se se aplica a um repositório a partir da **lista de arquivos** (o .NET se aplica a repositórios com `*.csproj`; um repositório pode ter mais de um scanner). Nenhum código fora do scanner conhece o formato de arquivo de uma tecnologia. O acesso aos arquivos fica atrás de outra porta, `IRepositoryContentSource` (provider-neutra, no molde da `IRepositoryDiscoverySource`). **Extensibilidade, por ora, é código embutido**: scanners carregáveis de fora (assemblies, processos) ficam descartados — sem autenticação (ADR-007) e sem governança de execução de código de terceiros, o risco não se paga. Configuração declarativa por squad (regras de chaves e padrões) é um segundo nível desejável, mas só depois da decisão de autenticação; até lá, regras ficam em arquivo versionado, revisadas por PR.
- **Modelo de fatos neutro à tecnologia.** Uma **análise** por execução sobre um repositório (data, commit, situação, avisos) e **fatos** que pertencem a ela. Cada fato tem **origem** (`PARSER`; futuramente `RULE` e `AI`), **confiança** (`alta`/`media`/`baixa`, sempre exibida em texto), scanner (id e versão) e arquivo de origem. A análise é **snapshot** (mesmo padrão do `QualitySnapshot`): a corrente é a mais recente por repositório, derivada por consulta, nunca armazenada. É idempotente por commit. Os fatos pertencem ao **repositório**; app e squad vêm por join.
- **Núcleo determinístico; IA nunca grava fato.** Scanners são parsers: mesmo commit, mesmo resultado, testáveis sem rede (regra do `backend.md`). Quando houver IA (decisão própria, exige a LLM aprovada pela empresa), ela será um **enriquecedor** separado, desligado por padrão, que recebe só as evidências já extraídas (nunca o repositório inteiro) e produz **sugestões** com `origem = AI` e confiança, que entram na triagem e dependem de confirmação de uma pessoa. Consistente com o backlog (fase 3) e com a `vision.md` ("AI-friendly não é ter IA dentro").
- **O conteúdo lido não é gravado; só os fatos.** Código-fonte e arquivos inteiros nunca vão para o banco, log ou mensagem de erro. **A redação de segredos pertence ao núcleo**, como um filtro aplicado a todo fato antes de gravar (ex.: de uma connection string guarda-se só host e nome do banco), e **não pode ser desligada por regra nem por scanner**. Há teste de que valores sensíveis não aparecem, como já há para o token. Mensagens de erro de integração continuam montadas por nós.
- **Vocabulário.** **Descoberta** = o que o "scan" da fase 1 faz (achar repositórios de uma sigla); **Análise** = ler os arquivos de um repositório catalogado; **Scanner** = o componente de análise de uma tecnologia. Os nomes de código da fase 1 (`ScanService`, `GitHubScan`, `Scan:Enabled`) não mudam. "Skill" continua significando instrução para agente (`.agents/skills/`), não funcionalidade do produto.

## Consequências

- Uma tecnologia nova é um scanner novo mais seus testes, sem mudar tabelas, API de leitura ou telas. A spec 013 prova isso com um scanner mínimo de `package.json` **só nos testes**.
- Origem e confiança existem desde a primeira tabela, mesmo que no início tudo seja `PARSER`; isso paga o mapa de aplicações e a IA futura sem construir nada delas agora.
- Resolução de versão e de destino é **limitada por natureza** (propriedades não resolvidas, hosts que vêm da infraestrutura): o resultado é exibido com confiança em texto, e o que não se resolve aparece como tal, nunca escondido.
- Só dependências diretas nesta etapa; as transitivas dependem de outra fonte (a API de SBOM do GitHub, a verificar se está habilitada na organização) e de decisão própria.
- A análise roda por linha de comando; um endpoint de disparo, quando existir, herda o pré-requisito de autenticação da ADR-010.
- Esta ADR não cobre a leitura de código (Refit/`HttpClient` por Roslyn, que exigirá baixar o repositório como arquivo compactado), integrações entre apps nem o mapa; cada um pede spec própria e, se mudar estas decisões, ADR própria.

## Adendo (2026-10-27, spec 014): integrações como segundo tipo de fato

As integrações declaradas (hosts que um repositório chama, vistos em `appsettings*.json`) ganham **tabela própria**, `repository_integrations`, ligada à mesma análise e com a mesma proveniência (origem, confiança, scanner, arquivo). Não reaproveitam `repository_dependencies` porque host, porta, caminho base e ambiente não são nome e versão de pacote, e a próxima fatia (apelidos host→app) precisa de índice por host. O contrato `IRepositoryScanner` continua um parser: ele devolve **candidatos brutos** e o **núcleo** (`IntegrationNormalizer`) decide o que é gravado, de modo que a redação de segredos (usuário e senha em URL, query, connection strings) não depende de cada scanner.

## Adendo (2026-11-02, spec 017): o vínculo host→app é decisão humana, em tabela própria

O vínculo "este host é o app Y" (Y **recebe** a chamada) **não é um fato lido**: é uma decisão de pessoa, com ciclo de vida diferente das análises. Por isso ganha a tabela própria `host_links`, **fora** de `repository_analyses`/`repository_integrations`:

- **Sem FK para a integração**: o vínculo precede e sobrevive ao fato (reanalisar não apaga decisão). Só o app tem FK (cascata).
- **Chave de texto**: `host_key` = `host[:porta]` em minúsculas, que pode ser o **modelo** com `{env}`. O casamento do modelo com o host concreto é uma função pura (`HostKeyMatcher`) que troca `{env}` pelos valores de `Analysis:EnvPlaceholderValues` (padrão `dev`, `hom`, `prod`). Nunca por palpite fora dessa lista.
- **Estados**: `confirmado` e `rejeitado`. `sugerido` fica reservado ao domínio (sugestão por nome ou IA no futuro, com `origin` e confiança); nada o produz hoje. `origin = HUMAN`.
- **Regras no serviço** (SQL portável, sem índice parcial): par `(host_key, app_id)` único; no máximo **um `confirmado` por host** (o segundo é conflito, 409); vários `rejeitado`. O vínculo **exato** vence o do modelo.
- **"Não visto na última análise"** é derivado (nenhuma integração corrente casa), nunca gravado.
- **Limite aceito**: um vínculo por **host**, não por seção nem por caminho. Um gateway com vários serviços atrás do mesmo host não se expressa. A evolução (host + caminho base) acrescenta uma coluna sem quebrar a tabela.

### Por que o host não é um app (pergunta recorrente)

- **Natureza**: host é um endereço de texto **lido** das análises (fato, muda a cada rodada); app é uma entidade **cadastrada** por pessoas (squad, repositórios, histórico, checagens).
- **Cardinalidade**: um serviço tem vários hosts (modelo `{env}`, `dev`, `hom`, `prod`); um host pode ser de serviço **fora do catálogo** (terceiro, legado). Tratar cada host como app encheria o catálogo de itens sem dono e poluiria as telas de qualidade e checagem.
- **Ciclo de vida**: o fato vem e vai com a análise; a decisão humana "este host é aquele app" não pode se perder, por isso o **vínculo** é a ponte, em tabela própria.
- **Alternativa não adotada**: cadastrar em cada app os seus endereços (direção inversa). Mais simples de pensar, mas exige digitar endereços antes de saber quais existem e perde a fila de "hosts que ninguém ligou". Pode ser revista.
- **Quem chama não é limitado**: o vínculo diz só quem **recebe**; um app pode chamar vários hosts (um destino por integração) e um host pode ser chamado por vários apps.

## Adendo (2026-11-03, spec 019): host externo ao catálogo e decisão em lote

- **Host externo** = decisão humana de que o host é de um serviço que ninguém vai cadastrar como app (outra squad, terceiro, legado). Tira o host da fila "sem app" sem inventar um app. Fica na tabela própria `host_externals` (e não como estado de `host_links`) porque `host_links.app_id` é obrigatório com FK, e afrouxar isso exigiria recriar a tabela no SQLite. Mesmas regras do vínculo: fora das análises, sem FK para integrações, chave `host[:porta]` em minúsculas (pode ser modelo `{env}`), nome livre opcional.
- **Exclusão mútua** (regra do serviço, 409): um host exato não é `confirmado` e `externo` ao mesmo tempo; rejeitos convivem com externo. Status efetivo: `confirmado` > `externo` > `rejeitado` > `sem-vinculo`. O contrato da 017 foi preservado (`destinations` igual; `externals` e `link.external` são campos novos).
- **Lote**: `POST /api/integrations/links/batch` aplica a mesma decisão (`confirmado`, `rejeitado` ou `externo`) a vários hosts, com sucesso parcial (cada host segue as regras individuais; um 409 não desfaz os outros). Motivo: nos projetos reais só existem hosts concretos, um por ambiente.
- **Limite**: externos "não vistos" na última análise não aparecem na lista de vínculos não vistos (só vínculos de app).

## Adendo (2026-11-05, spec 020): o mapa de aplicações é derivado, não armazenado

- O mapa é calculado a cada leitura (`GET /api/map`) a partir das integrações das análises correntes e das decisões humanas host→app (specs 017 e 019). **Não há tabela de ligações**: guardar o grafo duplicaria um fato que já tem fonte e envelheceria; se algum dia houver ligação que não vem de uma integração (cadastrada à mão, IaC, IA), aí nasce a tabela de ligações com origem e confiança (backlog).
- O **ambiente** de uma aresta é o `environmentKind` da integração (arquivo de origem), nunca do nome do host. Arquivo base e ambiente desconhecido têm ambiente nulo e valem para todos. A inferência por nome de arquivo (`appsettings.{Nome}.json`) é do scanner **.NET**; outras tecnologias só entram quando tiverem scanner, e até lá o repositório aparece como "scanner indisponível" na cobertura do mapa.
- O destino sem decisão é um nó único "Sem app": o mapa nunca desenha uma ligação que nenhuma pessoa decidiu.

## Adendo (2026-11-05, spec 021): contratos como terceiro tipo de fato

- **Cada tipo de fato tem processador e escritor próprios.** O scanner devolve saídas (`IScannerOutput`); `IAnalysisFactProcessor` (Application) transforma a saída bruta em fatos, com avisos e notas; `IAnalysisFactWriter` (Infrastructure) grava o tipo na transação da análise. O `AnalysisService` e o `SaveAsync` deixam de conhecer os tipos. Tipo sem escritor lança exceção e desfaz a transação. Tabela, API e tela continuam específicas de cada tipo: não há framework genérico.
- **Endpoints e campos declarados** ganham tabelas próprias, `repository_api_endpoints` e `repository_api_fields`, com a proveniência de sempre. O termo é **endpoint**, porque **Operação** é termo de negócio.
- **Parser**: `Microsoft.OpenApi`, na mesma versão da API, com YAML convertido para JSON pelo YamlDotNet. `$ref` externo nunca é buscado.
- **A comparação entre commits é derivada, não armazenada.** A base é a análise não falhada mais recente de commit diferente que tenha endpoints. Numa análise parcial, o que sumiu nunca é classificado como quebra.
- **O que é declarado não é o que está implementado.** O contrato pode estar desatualizado; `mechanism = openapi` deixa espaço para comparar os dois depois.
