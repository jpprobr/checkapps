# Spec 006: Scan pela tela

**Status**: Implementada (2026-10-12) — falta apenas o scan pela tela contra o GitHub real, a cargo do usuário
**Criada em**: 2026-10-12
**Origem**: decisão do usuário após a spec 005. A leitura real do GitHub foi validada pela ferramenta de linha de comando; disparar o scan pela tela "Descobertos" tira a dependência de terminal do Tech Lead. O CLI **continua**.

## Escopo e fronteira

Um endpoint da API e um bloco na tela "Descobertos" que executam o **mesmo** `ScanService` da ferramenta de linha de comando — a lógica não é duplicada. A API passa a precisar de credencial do GitHub.

**O custo, assumido de olhos abertos**: sem autenticação (ADR-007), qualquer pessoa que abra o sistema poderia disparar scans com o token configurado, gastando o limite de chamadas e lendo a organização. Mitigações desta spec: o endpoint é **desligado por padrão** (`Scan:Enabled`); o escopo continua protegido (siglas validadas, limite de resultados); só **um scan por vez**; e fica registrado que **antes de qualquer uso compartilhado o endpoint exige autenticação** — pré-requisito, não opcional.

**Não faz parte**: autenticação (spec própria, junto do ADR que substituir o 007); agendador; scan assíncrono com progresso; fases 2 e 3 do leitor; busca por nome; GitHub App.

## User Scenarios & Testing

### User Story 1 — Escanear pela tela (Prioridade: P1)

O Tech Lead abre "Descobertos", informa as siglas e escaneia. Os repositórios aparecem na caixa de entrada, prontos para a triagem, sem terminal.

**Por que esta prioridade**: é o motivo da spec.

**Teste independente**: com a fonte fake e o scan habilitado, escanear `ab1` pela tela e ver os repositórios na caixa.

**Cenários de aceite**:

1. **Dado** o scan habilitado e configurado, **Quando** informo `ab1, cd2` e clico "Escanear", **Então** vejo "Lendo o GitHub…", depois o resultado (quantos novos, atualizados e inalterados, mais os avisos) e a tabela já mostra os repositórios.
2. **Dado** o mesmo cenário, **Quando** clico "Pré-visualizar", **Então** vejo o que seria gravado e **nada** é gravado.
3. **Dado** repositórios já triados (ignorados, de outra squad, já no catálogo), **Quando** escaneio de novo, **Então** nenhuma decisão é desfeita e os já catalogados continuam fora da caixa.
4. **Dado** uma sigla fora do formato ou nenhuma sigla, **Quando** tento escanear, **Então** vejo a mensagem de recusa e nada foi consultado.
5. **Dado** uma busca que passaria do limite de resultados, **Quando** escaneio, **Então** vejo que o escopo é amplo demais e que nada foi lido.
6. **Dado** uma falha do GitHub (token inválido ou sem permissão, limite de chamadas, rede), **Quando** escaneio, **Então** vejo a mensagem do que fazer, e o token **não** aparece em lugar nenhum.
7. **Dado** um repositório que **já virou app**, **Quando** escaneio ou pré-visualizo, **Então** o item aparece com o resultado de sempre (ex.: "inalterado") **e** a marca "já no catálogo" com o link do app, o resumo diz quantos são, e a tela explica que repositório que já virou app não aparece na tabela de baixo.
8. **Dado** a caixa de entrada sem nada para decidir, **Quando** abro a tela, **Então** a mensagem diz que não há repositório **novo** e que os que já viraram app não aparecem ali.

### User Story 2 — Saber por que o scan não está disponível (Prioridade: P1)

Se o scan estiver desligado ou sem credencial, a tela diz isso e o que fazer, em vez de falhar.

**Por que esta prioridade**: sem isso, o estado normal de uma instalação nova (sem token) seria um erro confuso.

**Teste independente**: abrir "Descobertos" com o scan desligado e ver o motivo.

**Cenários de aceite**:

1. **Dado** o scan desligado (`Scan:Enabled` ausente ou `false`), **Quando** abro a tela, **Então** os botões ficam desabilitados e a tela explica como habilitar, apontando a documentação.
2. **Dado** o scan habilitado mas sem token ou organização (fonte GitHub), **Quando** abro a tela, **Então** os botões ficam desabilitados e o motivo aparece em texto, **sem** qualquer valor de configuração.
3. **Dado** a convenção de nome (`Scan:NameTemplate`) ausente, **Quando** abro a tela, **Então** o motivo diz que ela precisa ser configurada.

### User Story 3 — O CLI continua igual (Prioridade: P1)

A ferramenta de linha de comando se comporta como antes. Os dois caminhos compartilham a composição.

**Por que esta prioridade**: o CLI é o caminho do primeiro acesso ao ambiente corporativo (`--dry-run`) e da automação.

**Teste independente**: repetir os casos da spec 005 no CLI e comparar a saída.

**Cenários de aceite**:

1. **Dado** `--source fake`, com e sem `--dry-run`, **Quando** rodo, **Então** a saída e o comportamento são idênticos aos da spec 005.
2. **Dado** sigla inválida, limite excedido ou ausência de token, **Quando** rodo, **Então** as mensagens e os códigos de saída são os mesmos.

### Edge Cases

- Dois scans ao mesmo tempo (dois cliques, duas pessoas): o segundo é recusado com conflito, para não gastar o limite de chamadas em paralelo.
- Scan que demora: a requisição é síncrona (segundos, até 50 repositórios) e respeita o cancelamento.
- Mudar `Scan:Source` ou o `Scan:Enabled` exige reiniciar a API (a configuração é lida na subida).
- Fonte `fake` na API (`Scan:Source=fake`): funciona sem rede nem token, com a convenção padrão `fake-{sigla}-`, para demonstração.

## Requirements

### Functional Requirements

- **FR-001**: A API DEVE expor `POST /api/discovered-repositories/scan` com `{ siglas, dryRun }`, que executa o `ScanService`, e DEVE devolver contagens por resultado (novo, atualizado, inalterado), os itens com sugestão e os avisos.
- **FR-002**: A API DEVE expor `GET /api/discovered-repositories/scan/status` com `{ enabled, source, ready, problem, maxResults, siglaPattern }`, para a tela decidir o que habilitar e o que explicar. `problem` é um texto sem valores de configuração.
- **FR-003**: O endpoint DEVE ficar **desligado por padrão** e só funcionar com `Scan:Enabled = true`. Desligado, o `POST` responde 409. A ferramenta de linha de comando ignora essa configuração.
- **FR-004**: Os erros DEVEM seguir: sigla inválida ou limite de resultados excedido → 400; scan desligado ou não configurado → 409; scan já em andamento → 409; falha da fonte (token, permissão, limite de chamadas, rede) → 502, com a mensagem já saneada.
- **FR-005**: O sistema DEVE executar **no máximo um scan por vez**; o segundo recebe 409 sem executar nada.
- **FR-006**: O token DEVE permanecer só na configuração da API: nenhuma resposta, log ou mensagem de erro o contém.
- **FR-007**: A API e a ferramenta de linha de comando DEVEM compartilhar a mesma composição (fonte, opções e `ScanService`); `Scan:Source` (`github` padrão | `fake`) escolhe a fonte, e `--source` do CLI o sobrepõe.
- **FR-008**: A fonte DEVE expor um problema de configuração (`ConfigurationProblem`) em vez de lançar exceção no construtor, para que a API suba sem credencial e a tela explique o motivo.
- **FR-009**: A tela "Descobertos" DEVE ter o bloco "Buscar no GitHub" com campo de siglas, botões "Pré-visualizar" e "Escanear", estados (indisponível, lendo, erro, resultado) e recarregar a tabela após um scan real.
- **FR-010**: Com a fonte `fake` e `Scan:NameTemplate` ausente, a composição DEVE aplicar a convenção padrão `fake-{sigla}-` (a API e o CLI).
- **FR-011**: O resultado do scan (real e pré-visualização) DEVE marcar cada item que já está no catálogo com o app em que está (`catalogApp`) e informar a contagem (`counts.inCatalog`). É informação **adicional**: o resultado (novo, atualizado, inalterado) continua descrevendo os dados observados, e as contagens se sobrepõem, não se somam. A consulta é uma só, em lote.

### Key Entities

Nenhuma entidade nova. Reaproveita `DiscoveredRepository`, `ScanReport`, `ScanItem` e a sugestão da spec 005.

**AI-friendly**: nenhum dado novo é gravado por esta spec; a proveniência (`GITHUB_SCAN`), a identidade e o tempo continuam os da spec 005.

## Success Criteria

- **SC-001**: Um Tech Lead escaneia uma sigla e triagem os repositórios inteiramente pela tela, sem terminal.
- **SC-002**: Numa instalação sem credencial, a tela explica o que falta em vez de falhar.
- **SC-003**: O token nunca aparece em resposta, log ou mensagem de erro.
- **SC-004**: O CLI produz a mesma saída de antes nos casos da spec 005.
- **SC-005**: Nenhum teste automatizado depende de rede.

## Assumptions

- **Desligado por padrão, mesmo no laboratório local.** A fricção é pequena (um comando de `user-secrets`) e força a decisão consciente de expor o scan na API.
- **Requisição síncrona.** Uma busca mais até 50 leituras de arquivo levam segundos; progresso assíncrono fica para quando houver necessidade real.
- **Credencial da API em user-secrets próprios** (`UserSecretsId` do `CheckApps.Api`), separados dos da ferramenta de linha de comando. Quem usa os dois configura os dois.
- **Autenticação é pré-requisito de uso compartilhado**, não desta spec.

## Design notes

### Decisões que merecem registro

- Token também na API, endpoint desligado por padrão e autenticação exigida antes de uso compartilhado: **nota no ADR-010**.
- **Composição única** (`AddCheckAppsInfrastructure`): registra as opções, o `HttpClient` tipado, a fonte escolhida e o `ScanService`. Substitui a regra anterior de manter o `ScanService` fora da injeção de dependências da API.
- **Falha de integração externa responde 502**, convenção nova em `rules/backend.md`.

### API

```
GET  /api/discovered-repositories/scan/status
POST /api/discovered-repositories/scan          # { siglas: ["ab1","cd2"], dryRun: true|false }
```

### Telas

- `/discovered`: bloco recolhível "Buscar no GitHub" acima da tabela — campo de siglas, "Pré-visualizar", "Escanear", resultado com contagens, avisos e itens; indisponível, mostra o motivo.

### Notas da implementação (2026-10-12)

- **Nome da tela (2026-10-13)**: "Descobertos" passou a se chamar **Repositórios** (spec 007); o bloco "Buscar no GitHub" continua nela.

- **Quando faltam token e convenção de nome, o erro da convenção vem primeiro** (antes, com a fonte construída no início, vinha o do token). A ordem de validação do escopo não mudou: sigla inválida continua sendo a primeira recusa, antes de qualquer configuração.
- **`--source` do CLI vira um valor em memória de `Scan:Source`**, que vence a configuração; sem o argumento vale `Scan:Source` (padrão `github`). A linha de ajuda do CLI ganhou uma explicação disso — a única diferença na saída.
- **Fonte sem credencial não manda cabeçalho de autorização** (nem tenta a rede): `DiscoverAsync` lança antes de qualquer requisição, e há teste disso.
- **A API sobe sem credencial** e `GET …/scan/status` informa `ready = false` e o `problem`; foi o que permitiu a tela explicar em vez de falhar. Um teste de composição com `ValidateOnBuild`/`ValidateScopes` ligados cobre isso, sem rede.
- **O resultado anterior é limpo ao iniciar um novo scan** (a tela mostra "Lendo o GitHub…" em vez de números velhos).
- **As siglas digitadas ficam lembradas no navegador** (`localStorage`, com `try/catch`), conveniência por navegador e não dado do sistema.
- **A guarda de concorrência é estática no controller**: vale para o processo da API (instância única, ADR-009).
- **Defeito de layout encontrado no navegador**: o `.subtitle` global (margem negativa) sobrepunha o campo de siglas; corrigido no CSS do bloco e registrado em `rules/frontend.md`.
- **"Já no catálogo" no resultado (melhoria, 2026-10-12)**: no primeiro uso real o repositório escaneado já tinha virado app; por regra ele sai da caixa de entrada, a tabela de baixo ficou vazia e o resultado mostrava só "inalterado" — a regra estava certa, a tela não a comunicava. Agora o item traz `catalogApp` (app e nome) e o resumo traz `inCatalog`; a tela mostra a marca com o link do app e explica o estado vazio; o CLI imprime `[já no catálogo: app X]`. Consulta em lote (`GetCatalogAppsAsync`), sem schema novo.
- **Não verificado ainda**: o scan pela tela contra o GitHub real (depende dos segredos da API, que são separados dos da ferramenta de linha de comando).

### Fora desta spec (backlog)

Autenticação; agendador; scan assíncrono com progresso; fases 2 e 3; busca por nome; GitHub App.
