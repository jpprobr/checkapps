# Spec 014: Integrações declaradas (fatia 1 das integrações entre apps)

**Status**: Implementada (2026-10-27); confirmada pelo usuário na tela com o GitHub real em 2026-10-27
**Criada em**: 2026-10-27
**Origem**: pedido do usuário (2026-10-27) de começar as integrações pela fatia 1; fase 3 do "Leitor de repositórios GitHub" (`backlog.md`), que a spec 013 deixou de fora e cujo contrato de scanner e modelo de fatos (ADR-011) ela reaproveita. É a base do futuro "apelidos host→app" (fatia 2) e do "Mapa de aplicações" (fatia 3).

> Formato compatível com o template de spec do Spec-Kit (github/spec-kit). Ambiguidades estão marcadas com `[NEEDS CLARIFICATION]` e viram perguntas antes da aprovação.

## Escopo e fronteira

**Faz parte**

1. **Detectar integrações declaradas** em repositórios .NET já catalogados e gravá-las como **fatos** de uma análise: "este repositório chama o host X, e a evidência está em tal arquivo/chave/interface". Tudo **determinístico** (leitura de texto, sem palpite).
2. **Fonte de evidência: `appsettings*.json`**, incluindo os arquivos **por ambiente** (`appsettings.Development.json`, `appsettings.Production.json`…). O padrão do time (informado em 2026-10-27) é uma seção com o nome do cliente e um `BaseUrl` com o ambiente parametrizado: `"IPagamentosServiceCliente": { "BaseUrl": "http://empresa.{env}.exemplo.com" }`, e as rotas ficam numa interface Refit de mesmo nome. A leitura da **interface Refit e do `AddHttpClient`** no código fica para a **spec 015** (decisão do usuário de 2026-10-27).
3. **Mostrar** as integrações de um app (seção no detalhe, por repositório) e uma lista de **hosts** com quantos apps os chamam, sem dizer a que app cada host corresponde.
4. **Redação no núcleo** (ponto de extensão previsto na spec 013): valores sensíveis são descartados antes de gravar.

**Não faz parte** (cada item é candidato a spec própria)

- **Resolver o host para um app do catálogo**, apelidos host→app e **confirmação humana** do vínculo (fatia 2, nasce a tabela de vínculos com sugerido/confirmado/rejeitado).
- **Mapa de aplicações** e listas auxiliares nas Checagens (fatia 3).
- Qualquer uso de IA; heurística de nome de chave além de uma lista configurável.
- Outras tecnologias; IaC (Terraform); filas, bancos e demais tipos de integração que não sejam chamada HTTP.
- Resolver valores vindos de variável de ambiente, secret ou outro repositório (aparecem como "não resolvido").
- Disparar a análise pela tela (continua só por `tools/CheckApps.Analyze`).

## User Scenarios & Testing *(obrigatório)*

### User Story 1 — Detectar as integrações de um repositório (Prioridade: P1)

Como pessoa que mantém o catálogo, rodo a análise e o CheckApps registra, para cada repositório .NET, quais hosts ele chama e onde está escrito. Rodar de novo no mesmo commit não duplica nada.

**Por que esta prioridade**: sem os fatos nada das outras histórias existe, e é aqui que se prova se o formato dos repositórios reais permite extrair algo útil.

**Teste independente**: com `--source fake`, rodar a ferramenta e conferir no banco as integrações, sem API nem tela.

**Cenários de aceite**:

1. **Dado** um `appsettings.json` com `"Pagamentos": { "BaseUrl": "https://pagamentos.exemplo.com/v1" }`, **Quando** rodo a análise, **Então** é gravada uma integração com host `pagamentos.exemplo.com`, evidência `Pagamentos:BaseUrl` em `appsettings.json` e confiança **alta**.
2. **Dado** `appsettings.json` e `appsettings.Development.json` com a mesma chave e valores diferentes, **Então** há uma integração por arquivo, cada uma com o próprio arquivo de origem (a análise não decide qual vale em produção).
3. **Dado** um valor com placeholder de substituição que não é o `{env}` do padrão (`"${PAGAMENTOS_URL}"`, `"#{Url}#"`) ou valor vazio, **Então** a integração é registrada como **não resolvida**, com confiança **baixa**, a evidência é só o arquivo e a chave (o texto cru **não** é guardado) e um aviso; nenhum host é inventado.
3b. **Dado** `"BaseUrl": "http://empresa.{env}.exemplo.com"` (padrão do time), **Então** a integração é gravada no estado **parametrizada**, host-modelo `empresa.{env}.exemplo.com`, confiança **média**, seção `IPagamentosServiceCliente`.
3c. **Dado** `appsettings.Production.json` com `"BaseUrl": "https://empresa.exemplo.com"`, **Então** a integração tem `environment = Production`, `environment_kind = prod`, estado `resolvida` e confiança **alta**, e convive com a do arquivo base.
3d. **Dado** `appsettings.Staging.json`, `appsettings.Homolog.json` ou `appsettings.Hom.json`, **Então** `environment` guarda o nome do arquivo e `environment_kind = hom`; **Dado** `appsettings.Uat.json` (fora da tabela), **Então** só `environment` é preenchido e `environment_kind` fica vazio.
3e. **Dado** `"BaseUrl": "http://localhost:5001"`, **Então** nada é gravado e uma **nota informativa** diz quantas URLs locais foram ignoradas (a nota não torna a análise `parcial`; aviso de verdade, como JSON inválido ou integração não resolvida, torna).
4. **Dado** um valor com credencial na URL (`https://usuario:senha@host/`) ou token na query (`?key=abc`), **Então** só esquema, host, porta e caminho são gravados; credencial e query são descartadas, e **não aparecem** no banco, em log ou em mensagem.
5. **Dado** um repositório já analisado no mesmo commit, **Então** nada é gravado (sem alteração); com `--dry-run` nada é gravado nunca.
6. **Dado** um repositório sem `appsettings*.json` (e sem código de cliente HTTP), **Então** a análise segue válida, com zero integrações, sem erro.
7. **Dado** `appsettings.json` inválido, **Então** aviso com o caminho (nunca o conteúdo), análise `parcial`, e os demais arquivos seguem.

### User Story 2 — Ver o que um app chama (Prioridade: P1)

Como tech lead, abro o detalhe do meu app e vejo, por repositório, os hosts que ele chama, de onde vem cada um e o quanto o CheckApps confia.

**Por que esta prioridade**: é a tela que mostra se o dado está bom; sem ela o scanner não é verificável por pessoas.

**Teste independente**: com os fatos da história 1 gravados, abrir `/apps/:id` e conferir a seção **Integrações**.

**Cenários de aceite**:

1. **Dado** um app com repositório analisado, **Quando** abro o detalhe, **Então** vejo a seção **Integrações** com: host, caminho base, mecanismo (configuração, Refit, cliente HTTP), evidência (arquivo e chave ou interface), confiança **em texto** e data/commit da análise.
2. **Dado** integração não resolvida, **Então** aparece "não resolvida" com o motivo em texto, sem tratar como erro.
3. **Dado** um repositório sem análise ou sem scanner aplicável, **Então** a seção diz isso, como na seção Dependências.
4. **Dado** um app com mais de um repositório, **Então** a seção agrupa por repositório e mostra a situação de cada um.
5. **Dado** que o catálogo não sabe a que app um host pertence, **Então** a tela mostra apenas o host, sem sugerir vínculo (isso é a fatia 2).

### User Story 3 — Quais hosts existem e quem os chama? (Prioridade: P2)

Como coordenador, abro uma lista dos hosts detectados e vejo, para cada um, quantos apps e repositórios o chamam, de quais squads, para identificar serviços muito usados e hosts desconhecidos.

**Por que esta prioridade**: aproveita os mesmos fatos e prepara a fatia 2 (a lista é a "fila de trabalho" dos apelidos), mas não bloqueia a história 2.

**Teste independente**: abrir a tela **Integrações** (ou aba em **Dependências**, ver pergunta) e buscar um host.

**Cenários de aceite**:

1. **Dado** análises gravadas, **Quando** abro a lista, **Então** vejo uma linha por host com a contagem de apps e repositórios, as squads e a análise mais recente, ordenadas por quem mais usa.
2. **Dado** um host, **Quando** abro o detalhe, **Então** vejo cada app e repositório que o chama, com evidência e confiança.
3. **Dado** um filtro de squad ou de texto no host, **Então** a lista é restringida; busca sem diferença de caixa.
4. **Dado** repositórios nunca analisados, **Então** a tela avisa quantos catalogados ainda não têm análise (a lista pode estar incompleta).

### Fora desta spec: interfaces Refit e clientes HTTP no código

Decisão de 2026-10-27: vira a **spec 015**. Ela vai ligar a seção de configuração (`IPagamentosServiceCliente`) à interface Refit de mesmo nome, contar as rotas e achar o `AddHttpClient`/`BaseAddress` literal. Nesta spec, o nome da seção fica guardado como `reference` justamente para essa ligação ser possível depois, sem reanálise.

### Edge Cases

- **`localhost`, `127.0.0.1`, `::1`** (decisão de 2026-10-27): **ignorados**; a análise conta quantos foram ignorados no aviso (sem listar valores). Gravá-los como "local" fica como melhoria futura.
- **Ambiente parametrizado** (`http://empresa.{env}.exemplo.com`, padrão do time): o `{env}` não é um host válido de URL, então o host é gravado **como modelo** (`empresa.{env}.exemplo.com`) no estado `parametrizada`, confiança **média** ("host depende do ambiente"). Não se substitui `{env}` por palpite.
- **Arquivos por ambiente** (decisão de 2026-10-27: o nome **não segue convenção única** entre os repositórios; é `Dev`/`Hom`/`Prod` ou `Development`/`Homolog`/`Production`, e às vezes `Staging` para homologação): cada `appsettings.<Nome>.json` gera as próprias integrações. Guardam-se **dois campos**: `environment` = o nome **como está no arquivo** (`Homolog`) e `environment_kind` = a categoria **normalizada** (`dev`, `hom`, `prod`) por uma tabela de apelidos, sem diferença de caixa: `dev` ← `Dev`, `Development`; `hom` ← `Hom`, `Homolog`, `Homologacao`, `Staging`; `prod` ← `Prod`, `Production`. Nome fora da tabela (`Local`, `Test`, `UAT`…) fica só em `environment`, com `environment_kind` vazio (nunca palpite). A tabela é configurável (`Analysis:EnvironmentAliases`). O `appsettings.json` base fica sem ambiente. Quando um arquivo de ambiente traz um host **concreto** (`empresa.dev.exemplo.com`) e o base traz o modelo (`{env}`), são duas integrações da mesma seção; a análise não decide qual vale, e a seção do app as mostra juntas pela **seção** (`reference`), e a lista de hosts agrupa por host literal (`host[:porta]`) e mostra as seções e categorias de ambiente vistas.
- **Chave que guarda host** (padrão do time: `BaseUrl` dentro de uma seção com o nome do cliente): só entra a chave cujo **último segmento** está na lista `Analysis:IntegrationKeyHints` (padrão `BaseUrl`, `Url`, `Endpoint`, `Host`). Links de ajuda e documentação (`LinkAjuda`) não entram, mesmo sendo `https://`. Valor `http(s)` de uma chave fora da lista é ignorado. A **seção-pai** (`IPagamentosServiceCliente`) é guardada como `reference`; a chave completa (`IPagamentosServiceCliente:BaseUrl`) como evidência.
- **URLs não HTTP** (connection string, `amqp://`, `redis://`, `Server=...`): **não** são integração HTTP; connection strings nunca são lidas nem gravadas (contêm credenciais). Nesta spec, são ignoradas, com contagem no aviso da análise.
- **Mesmo host em várias chaves ou vários arquivos**: uma integração por (arquivo, chave); a lista de hosts agrega.
- **Array e objeto aninhado em JSON**: chave composta com `:` e índice (`Servicos:0:Url`), como o `IConfiguration` do .NET.
- **JSON com comentários e vírgula sobrando** (aceito pelo .NET): lido de forma tolerante; falha de leitura vira aviso.
- **Host com maiúsculas, porta ou barra final**: normalizados (host em minúsculas, porta explícita preservada, sem barra final); `https://A.exemplo.com:443/` e `https://a.exemplo.com/` são o mesmo host.
- **Caminhos e arquivos grandes**: mesmos limites da spec 013 (número de arquivos, tamanho), com aviso e análise `parcial`.
- **Pastas `bin/`, `obj/`, `node_modules/`, `.git/`**: ignoradas.
- **`appsettings.*.json` de ambientes** (`Production`): lidos como qualquer outro; o ambiente aparece na evidência (nome do arquivo).
- **Repositório removido do catálogo**: as integrações acompanham a análise (cascata).

## Requirements *(obrigatório)*

### Functional Requirements

- **FR-001**: O sistema DEVE estender o contrato de scanner (`IRepositoryScanner`) sem quebrar a spec 013: o scanner .NET passa a poder emitir também fatos de **integração**; nenhum código fora do scanner conhece o formato de `appsettings` ou de Refit.
- **FR-002**: O scanner DEVE ler todo `appsettings*.json` do repositório (qualquer pasta, exceto as ignoradas, **inclusive os de ambiente**) e extrair os pares cujo **último segmento da chave** está na lista configurável (`Analysis:IntegrationKeyHints`, padrão `BaseUrl`, `Url`, `Endpoint`, `Host`) e cujo valor é URL `http(s)` (com ou sem o placeholder `{env}`), vazio ou com placeholder de substituição (estes viram "não resolvida"). URLs de `localhost`, `127.0.0.1` e `::1` são ignoradas e contadas.
- **FR-003**: Cada integração DEVE guardar: ecossistema, **mecanismo** (`config` nesta spec; `refit` e `httpclient` na 015), **esquema, host (ou host-modelo), porta e caminho base** normalizados, **evidência** (arquivo e chave de configuração), **seção** (`reference`), **ambiente** (`environment` = nome do arquivo; `environment_kind` = `dev`/`hom`/`prod` normalizado por apelidos, vazio se desconhecido; ambos vazios no base), estado (`resolvida` | `parametrizada` | `nao-resolvida`), confiança (alta/média/baixa) com motivo, origem `PARSER`, scanner (id e versão) e projeto/arquivo de origem.
- **FR-004**: O sistema DEVE aplicar redação **no núcleo**, antes de gravar: descartar userinfo e query string de qualquer URL, ignorar connection strings e valores de chaves com nomes de segredo (`Password`, `Secret`, `Token`, `ApiKey`, `ConnectionString`, lista configurável), e nunca incluir valor bruto em log, aviso ou mensagem de erro. Há teste de que um segredo plantado na fixture não aparece no banco nem na saída.
- **FR-005**: A análise DEVE continuar idempotente por commit (FR-006 da spec 013) e a integração pertence à **mesma** análise dos pacotes (um retrato por commit); `--dry-run` não grava.
- **FR-006**: A fonte de conteúdo DEVE ler `appsettings*.json` pelo mesmo mecanismo da 013 (árvore + leitura pontual), respeitando os mesmos limites, sem baixar o repositório inteiro. Nenhum arquivo `.cs` é lido nesta spec.
- **FR-007**: `GET /api/apps/{appId}/integrations` DEVE devolver, por repositório do app, a análise corrente e as integrações; 404 se o app não existe.
- **FR-008**: `GET /api/integrations/hosts?host=&squadId=` DEVE devolver os hosts agregados (apps e repositórios que chamam, squads, análise mais recente), o total de repositórios sem análise e filtros de texto e squad; `GET /api/integrations/hosts/{host}` devolve o detalhe de um host.
- **FR-009**: A API de leitura NÃO DEVE disparar análise nem exigir credencial do provedor.
- **FR-010**: O detalhe do app DEVE ter a seção **Integrações** (US2) e o menu a entrada correspondente (US3), com ícone SVG embutido, `aria-current` e comportamento de menu recolhido como as demais; a confiança sempre em texto.
- **FR-011**: Consultas DEVEM usar SQL portável (sem `GROUP_CONCAT`, datas ISO-8601 UTC, comparação por `lower(...)`), conforme `backend.md`, e as tabelas novas entram no `schema.sql` com teste em `SchemaEvolutionTests` sobre banco antigo.
- **FR-012**: A fonte `fake` e as fixtures em `tests/CheckApps.UnitTests/Fixtures/Dotnet/` DEVEM cobrir os cenários desta spec (incluindo um `appsettings` com segredo plantado, placeholder, connection string, valor vazio e comentário em JSON); nenhum teste usa rede.
- **FR-013**: O sistema NÃO DEVE gravar código-fonte nem o conteúdo bruto dos arquivos lidos; apenas os campos de FR-003.
- **FR-014**: A ferramenta `CheckApps.Analyze` DEVE aceitar `--force`: reanalisa mesmo quando o commit é o da análise corrente, gravando uma análise nova (necessário porque as análises feitas antes desta spec não têm integrações). `--dry-run` continua sem gravar.
- **FR-015**: O scanner DEVE devolver também **notas informativas** (ex.: quantos valores foram ignorados e por quê), gravadas junto dos avisos com o prefixo `nota:`, que **não** tornam a análise `parcial`.

### Key Entities

- **RepositoryIntegration** (`repository_integrations`): uma integração declarada, extraída numa análise.
  - **Identidade**: id estável (guid) + (análise, mecanismo, arquivo, chave ou interface). **Relações**: pertence a uma `RepositoryAnalysis` (cascata), que pertence a um `Repository` → `App` → squad. **Proveniência**: `origin = PARSER`, `scanner_id`, `scanner_version`, `source_file`; confiança `alta|media|baixa` com `confidence_note`. **Tempo**: o da análise (histórico por commit; a corrente é a mais recente que não falhou, como na 013).
  - Atributos: `mechanism`, `scheme`, `host`, `port`, `base_path`, `reference` (seção de configuração; na 015, também o nome da interface), `config_key`, `environment`, `environment_kind`, `state` (`resolvida` | `parametrizada` | `nao-resolvida`), `project_path`.
  - **Decisão a registrar no plano**: tabela própria × reaproveitar `repository_dependencies` com `kind = integration`. A inclinação é por tabela própria, porque o host não é nome/versão de pacote e a fatia 2 precisa de índice por host `[NEEDS CLARIFICATION: confirmar no plano; mudança estrutural vira ADR]`.
- **Host** *(derivado, não persistido nesta spec)*: o par normalizado (esquema, host, porta) agregado por consulta. A **identidade do host como entidade** (apelidos e vínculo com app) é da fatia 2.

Para cada dado novo: identidade, relações, proveniência e tempo estão acima. A confirmação humana **não** existe nesta spec; os fatos nascem como `PARSER` e nada neles diz que o host é um app do catálogo.

## Success Criteria *(obrigatório)*

- **SC-001**: Em até 30 segundos depois da análise, uma pessoa descobre os hosts que um app chama e de que arquivo vêm, sem abrir o repositório.
- **SC-002**: Em até 30 segundos, uma pessoa descobre quantos apps e de quais squads chamam um host qualquer.
- **SC-003**: Rodar a análise duas vezes sobre código inalterado não cria nenhum registro novo.
- **SC-004**: Nenhum segredo, credencial, token, connection string ou código-fonte aparece no banco, em log ou em mensagem de erro (verificado por fixture com segredos plantados).
- **SC-005**: O que o CheckApps não conseguiu resolver aparece em texto ("não resolvida", com o motivo), nunca como host inventado.
- **SC-006**: Adicionar a leitura de integrações de outra tecnologia não exige mudar tabelas, API de leitura ou telas; só um scanner novo e seus testes.

## Assumptions

- A análise lê o **ramo padrão**, como na 013, e só repositórios **catalogados**.
- A integração é **chamada HTTP de saída** declarada; filas, bancos e outros tipos ficam fora.
- Os valores de `appsettings.json` versionado são, em geral, endereços de ambientes de desenvolvimento ou placeholders; a análise registra o que está escrito e **não afirma** qual vale em produção.
- O mecanismo mais comum do time é `appsettings` e Refit/`HttpClient` (informado em 2026-10-24). O **padrão de chaves** (informado em 2026-10-27): seção com o nome da interface Refit (`INomeServiceCliente`) contendo `BaseUrl` com `{env}` no host; as rotas ficam na interface. O usuário **não tinha um exemplo real**: as fixtures desta spec são **inventadas a partir dessa descrição** e serão ajustadas quando houver um modelo do time.
- Os valores reais de URLs e hosts da empresa não entram em arquivo versionado nem em documentação; fixtures e exemplos usam `exemplo.com`.
- O vocabulário da 013 se mantém: **análise**, **scanner**, **fato**.

## Decisões do usuário (2026-10-27)

1. **Escopo**: `appsettings` fica nesta spec; Refit e `AddHttpClient` no código viram a **spec 015** (leitura de código pede um analisador de sintaxe ou busca textual e um ADR próprio).
2. **Exemplos**: o usuário não tem um exemplo real agora; a fixture é inventada (`07-integracoes-appsettings`), com arquivos **por ambiente**, e será ajustada depois.
3. **Padrão de chaves**: seção com o nome da interface (`INomeServiceCliente`) com `BaseUrl` e `{env}` no host; rotas na interface Refit.
4. **Localhost**: ignorar por ora.
5. **Tela**: ainda indefinida. Nesta spec fica o item de menu **Integrações** (nome a confirmar; "Serviços" é a alternativa); as opções de layout (aba em **Dependências** × item próprio, e o nome) são apresentadas ao usuário **na hora de construir a tela**, para ele avaliar.

## Perguntas em aberto

- `[NEEDS CLARIFICATION: valores de {env}]` Os **nomes de arquivo** de ambiente não seguem convenção (respondido em 2026-10-27; ver *Edge Cases*). Falta saber **o que o `{env}` vira dentro da URL** (`dev`, `hom`/`hml`, `prod`?): decide se a lista de hosts consegue casar `empresa.{env}.exemplo.com` com `empresa.dev.exemplo.com`. Até saber, não se casa por palpite: agrupa-se pela **seção** (`reference`) e, dentro dela, pela categoria `environment_kind`.
- `[NEEDS CLARIFICATION: tabela própria]` Confirmar no plano: `repository_integrations` (inclinação) × reaproveitar `repository_dependencies`.

## Design notes *(opcional)*

- **Reaproveita** da 013: `IRepositoryScanner`, `IRepositoryContentSource`, `ScannerRegistry`, `AnalysisService` (idempotência por commit, falha isolada, limites), `GitHubApiClient`, convenções de confiança em texto e `Fixtures/Dotnet/`.
- **Redação no núcleo**: a 013 deixou o ponto de extensão registrado (filtro aplicado a todo fato antes de gravar). Esta spec o implementa; o scanner devolve fatos brutos e o núcleo os saneia, para nenhum scanner futuro esquecer.
- **Núcleo determinístico, IA depois**: nesta fatia nada é palpite. A fatia 2 introduz o primeiro passo humano (confirmar o vínculo host→app) e, só depois, um enriquecedor de IA poderia sugerir vínculos (`origin = AI`, sempre sugestão).
- **Fatias seguintes**: 2 = apelidos host→app e confirmação (tabela de vínculos, estados sugerido/confirmado/rejeitado, "não visto na última análise"); 3 = mapa de aplicações (arestas confirmadas em traço cheio, sugeridas tracejadas, hosts sem app como nós à parte).
