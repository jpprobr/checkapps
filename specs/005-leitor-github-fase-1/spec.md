# Spec 005: Leitor de GitHub, fase 1 — scan por sigla, caixa de entrada e triagem

**Status**: Implementada (2026-10-12); primeira leitura real do GitHub validada no mesmo dia
**Criada em**: 2026-10-12
**Origem**: item "Leitor de repositórios GitHub" do backlog (análise de 2026-10-12). Um catálogo mantido à mão envelhece; ler os repositórios da organização ataca isso na origem e é o primeiro conector real do produto.

## Escopo e fronteira

O scan **descobre** repositórios de uma ou mais siglas e os coloca numa **caixa de entrada**. A pessoa decide o destino de cada um. **O scan nunca cria App** — repositório não é App (o domínio separou os dois de propósito), parte dos repositórios é infra, fork ou arquivado, e, pela remoção guardada (ADR-005), um app que recebe snapshot não sai mais: criação automática errada vira permanente.

**Não faz parte**: dependências de biblioteca (fase 2) e dependências entre apps (fase 3); busca por nome; agendador; GitHub App; CRUD manual de Repository; tags chave-valor; entidade Sigla e conta AWS por ambiente; "quem fez" (depende de autenticação).

**Duas fontes, uma porta.** O GitHub corporativo não é acessível do ambiente de desenvolvimento. O scan lê de uma porta (`IRepositoryDiscoverySource`) com duas implementações: um **fake**, com os casos difíceis, para testes e demonstração; e o **GitHub real**, apontado para uma organização de teste pessoal. Passar ao corporativo é mudança de configuração.

**Dados de configuração local, fora do Git.** Nome da organização, siglas, token e caminho do YAML ficam em `dotnet user-secrets` ou variáveis de ambiente. Exemplos em documentação usam valores fictícios (`AB1`, `AB1-1234`, `{org}-{sigla}-`).

## User Scenarios & Testing

### User Story 1 — Descobrir os repositórios de uma sigla (Prioridade: P1)

O Tech Lead roda a ferramenta de scan informando as siglas da squad (ex.: `ab1,cd2`) e vê, em segundos, quais repositórios existem nelas e ainda não estão no catálogo.

**Por que esta prioridade**: é o que tira o cadastro de apps da planilha e da memória das pessoas.

**Teste independente**: com a fonte fake, rodar o scan para `ab1` e conferir a lista de descobertos, com a contagem de novos, atualizados e inalterados.

**Cenários de aceite**:

1. **Dado** uma sigla com 5 repositórios dentro da convenção, **Quando** rodo o scan, **Então** os 5 aparecem como descobertos com status "novo", cada um com sigla, sigla-app (quando houver), indicador de arquivado e data do último push.
2. **Dado** um repositório já descoberto antes e já triado como "ignorado", **Quando** rodo o scan de novo, **Então** seus dados são atualizados (nome, url, último push) e o status "ignorado" **não** volta para "novo".
3. **Dado** um repositório renomeado no GitHub, **Quando** rodo o scan, **Então** continua sendo o mesmo descoberto (identidade pelo id do GitHub), com o nome novo.
4. **Dado** a opção de simulação (`--dry-run`), **Quando** rodo o scan, **Então** vejo o que seria gravado e **nada** é gravado.
5. **Dado** uma sigla fora do formato, vazia, ou nenhuma sigla informada, **Quando** rodo o scan, **Então** ele recusa com mensagem clara, sem consultar o GitHub.
6. **Dado** uma busca que encontraria mais repositórios que o limite (50 por padrão), **Quando** rodo o scan, **Então** nada é lido e a ferramenta pede para refinar o escopo.
7. **Dado** um token ausente, inválido ou sem permissão, ou limite de chamadas do GitHub esgotado, **Quando** rodo o scan, **Então** a mensagem diz o que fazer (e quando o limite reseta), e o token **nunca** aparece na saída.

### User Story 2 — Triar a caixa de entrada (Prioridade: P1)

A pessoa abre "Repositórios descobertos" e, para cada repositório, decide: criar um app, vincular a um app existente, marcar como de outra squad ou ignorar.

**Por que esta prioridade**: sem a triagem, o scan é um relatório. É ela que mantém o catálogo coerente.

**Teste independente**: com descobertos já gravados, criar um app a partir de um deles e ver o app na lista da squad, com o repositório vinculado.

**Cenários de aceite**:

1. **Dado** a caixa com repositórios novos, **Quando** abro a tela, **Então** vejo só os novos (padrão), com filtro por sigla e por status, e cada linha mostra a sugestão de papel com a confiança (alta ou baixa).
2. **Dado** um repositório novo, **Quando** escolho "Criar app", **Então** vejo um formulário já preenchido (nome sugerido, squad do seletor do header, papel sugerido), e **nada é gravado** até eu confirmar.
3. **Dado** o formulário confirmado, **Quando** salvo, **Então** o app existe na squad escolhida, o repositório fica vinculado a ele (com sigla, sigla-app, papel e origem `GITHUB_SCAN`) e o descoberto sai da caixa.
4. **Dado** um repositório novo e um app existente, **Quando** escolho "Vincular a app existente", **Então** o repositório passa a ser do app; se a sigla for diferente da dos outros repositórios do app, vejo um **aviso**, sem bloqueio.
5. **Dado** um repositório que é de outra squad, **Quando** marco "De outra squad", **Então** ele sai da caixa e **não reaparece** como novo nos próximos scans.
6. **Dado** um repositório ignorado ou "de outra squad", **Quando** filtro por esse status e escolho "Restaurar", **Então** ele volta a "novo".
7. **Dado** um app criado a partir de um descoberto, **Quando** abro o detalhe do app, **Então** vejo o repositório com sigla, sigla-app e papel.

### User Story 3 — Papel e sugestões de nome (Prioridade: P2)

O nome do repositório (`{org}-{sigla}-{segmento}-{nome}`) e a presença de sigla-app sugerem o papel e o nome do app, para a pessoa corrigir em vez de digitar.

**Por que esta prioridade**: reduz o trabalho da triagem, mas a triagem funciona sem isso.

**Teste independente**: testes unitários da função de convenção, sem rede nem banco.

**Cenários de aceite**:

1. **Dado** `{org}-ab1-app-pagamentos` com sigla-app `AB1-0001`, **Então** papel sugerido `aplicacao`, nome sugerido "Pagamentos", confiança alta.
2. **Dado** `{org}-ab1-infra-fila` sem sigla-app, **Então** papel sugerido `infra`, confiança alta.
3. **Dado** um nome que diz `app` mas sem sigla-app, ou `infra` com sigla-app, **Então** a sugestão sai com confiança **baixa** — sinal de que a pessoa precisa olhar.
4. **Dado** um segmento desconhecido, **Então** nenhum papel é sugerido.

### Edge Cases

- Repositório fora da convenção de nome (ex.: `playground`): nunca aparece — o scan filtra o prefixo exato.
- Repositório que o token não enxerga: não aparece, sem erro (acesso parcial é o caso normal no corporativo).
- Repositório descoberto que depois some do GitHub: permanece, com `last_seen_at` antigo; esta fase não trata remoção.
- Sigla-app diferente do trecho inicial da sigla: **aviso**, não erro (certo numa squad, provável na organização).
- Sigla-app duplicada ao criar app ou vincular: recusada com conflito (a sigla-app identifica um único repositório).
- Repositório já no catálogo (existe `repositories` com o mesmo provedor e id externo): sai da caixa; tentar criar app ou vincular de novo responde conflito.
- Arquivo YAML ausente ou sem a chave: sem sigla-app, sem erro. YAML inválido: sem sigla-app e um aviso na saída do scan.
- App criado a partir de descoberto e a gravação do repositório falha no meio: o app fica sem repositório; a pessoa vincula depois (documentado).
- Busca do GitHub com atraso de indexação: repositório criado há minutos pode não aparecer; rodar de novo depois.

## Requirements

### Functional Requirements

- **FR-001**: O sistema DEVE oferecer uma ferramenta de linha de comando que recebe uma ou mais siglas e a fonte (`github` ou `fake`) e grava os repositórios descobertos.
- **FR-002**: O sistema DEVE recusar sigla vazia ou fora do formato configurado, antes de qualquer consulta externa. O escopo nunca é texto livre de busca nem a organização inteira.
- **FR-003**: O sistema DEVE montar o filtro a partir da convenção de nome configurável (`{org}-{sigla}-`) e DEVE descartar resultados que não comecem exatamente com esse prefixo.
- **FR-004**: O sistema DEVE recusar o scan, sem ler nada além da contagem, quando o total de resultados da busca exceder o limite configurado (50 por padrão).
- **FR-005**: O sistema DEVE ler a sigla-app de um arquivo YAML do repositório (caminho e chave configuráveis); ausência do arquivo ou da chave significa "sem sigla-app".
- **FR-006**: O sistema DEVE gravar cada repositório descoberto por upsert em `(provider, external_id)`, atualizando nome, url, sigla, sigla-app, arquivado, último push e `last_seen_at`, e **nunca** alterando o status de triagem.
- **FR-007**: O sistema DEVE oferecer o modo `--dry-run`, que executa a leitura e mostra o resultado sem gravar.
- **FR-008**: O sistema DEVE ler o token de configuração local (`GitHub:Token`), recusar-se a rodar sem ele e **nunca** exibi-lo, nem parcialmente, nem em mensagens de erro.
- **FR-009**: O sistema DEVE listar descobertos por status (`new` por padrão; `ignored`, `other-squad` e `all`) e por sigla, excluindo os que já estão no catálogo, e devolver a sugestão de papel, nome e confiança calculada na leitura.
- **FR-010**: O sistema DEVE criar um app a partir de um descoberto: gera o id por slug, cria o App na squad informada (que deve existir) e grava o Repository com sigla, sigla-app, papel e origem `GITHUB_SCAN`.
- **FR-011**: O sistema DEVE vincular um descoberto a um app existente, avisando (sem bloquear) quando a sigla difere da dos demais repositórios do app.
- **FR-012**: O sistema DEVE permitir marcar um descoberto como "de outra squad" ou "ignorado", e restaurá-lo para "novo".
- **FR-013**: O sistema DEVE garantir a unicidade da sigla-app entre repositórios (quando presente).
- **FR-014**: O sistema DEVE exibir sigla, sigla-app e papel de cada repositório no detalhe do app.
- **FR-015**: O sistema DEVE oferecer a tela "Repositórios descobertos" no menu, com as ações da triagem e o formulário de "Criar app" pré-preenchido e sem gravação até a confirmação.

### Key Entities

- **DiscoveredRepository** (Repositório descoberto): `id` (`github:{externalId}` — estável a renomeações), `provider`, `externalId`, `name`, `url`, `sigla`, `siglaApp?`, `archived`, `pushedAt?`, `status` (`new` | `ignored` | `other-squad`), `firstSeenAt`, `lastSeenAt`, `source` (`GITHUB_SCAN`). Relação com o catálogo **derivada**: está no catálogo quando existe `Repository` com o mesmo provedor e id externo.
- **Repository**: ganha `sigla`, `siglaApp?` (único quando presente), `role?` (`aplicacao`, `infra`, `contrato`, `secrets`…, texto livre) e `source` (`GITHUB_SCAN` ou `MANUAL`).
- **RepositoryNameConvention**: função pura, sem estado. Do nome tira sigla, segmento, papel sugerido, nome sugerido e confiança. Nada disso é armazenado.

**AI-friendly deste dado novo**: identidade estável (`github:{id numérico}`), relação por provedor + id externo com `Repository`, proveniência em `source` e na própria origem do scan, tempo em `first_seen_at`/`last_seen_at`/`pushed_at`. A confiança da sugestão é explícita (alta/baixa) e a sugestão nunca é gravada como verdade — só o que a pessoa confirma vira dado do catálogo.

## Success Criteria

- **SC-001**: Um Tech Lead leva uma sigla de dezenas de repositórios do GitHub ao catálogo em minutos, sem digitar nomes à mão.
- **SC-002**: Nenhum App é criado sem uma confirmação humana explícita.
- **SC-003**: Rodar o scan duas vezes seguidas não duplica nada e não desfaz nenhuma decisão de triagem.
- **SC-004**: Um scan mal escopado (sigla vazia, genérica ou resultado enorme) é recusado antes de ler dados em volume.
- **SC-005**: Nenhum teste automatizado depende de rede, e o token nunca aparece em saída, log ou mensagem de erro.

## Assumptions

- **Escopo por sigla, conforme o backlog.** Busca por nome fica para depois, com o mesmo limite de resultados.
- **Siglas no scan, squad na triagem.** O backlog descrevia o contexto do scan como "squad + siglas"; na implementação a squad só importa na triagem e vem do seletor do header, sem ser gravada no descoberto. Simplificação: dispensa uma coluna e a ferramenta não precisa conhecer squads.
- **Papel é texto livre**, como `Status`. Vira enum só com decisão explícita.
- **Token pessoal fine-grained (somente leitura)** nesta fase; GitHub App é para o corporativo.
- **Busca do GitHub (Search API)** em vez de listar a organização: listar tudo para filtrar localmente contradiria o objetivo de não varrer o ambiente. Custo: atraso de indexação para repositórios novos.
- **Esquema evolui por colunas aditivas** nesta fase; migrations numeradas começam no primeiro deploy com dados reais.

## Design notes

### Access patterns

| # | Padrão | Resolução |
|---|---|---|
| AP-1 | Upsert de descoberto | `SELECT` + `INSERT`/`UPDATE` em transação, sem tocar em `status` (lê antes para informar novo/atualizado/inalterado — `ON CONFLICT` não informaria) |
| AP-2 | Caixa de entrada por status e sigla | `discovered_repositories` com `NOT EXISTS` em `repositories` (mesmo provedor e id externo) |
| AP-3 | "Já está no catálogo" | `EXISTS` em `repositories` por `(provider, external_id)` |
| AP-4 | Unicidade de sigla-app | índice único parcial em `repositories(sigla_app)` |
| AP-5 | Repositórios de um app com sigla | `repositories` por `app_id` (já existe), com colunas novas |

### Decisões que merecem registro

- Integração externa, credencial e fonte de dados: **ADR-010**.
- **Estado de triagem derivado onde possível**: "no catálogo" não é um status duplicado, é a existência de `Repository`. Evita inconsistência quando um app é removido.
- **Sugestões calculadas na leitura**, não armazenadas: nunca ficam velhas quando a convenção muda.
- **Evolução aditiva do schema**: `SqliteSchemaBootstrapper` confere `PRAGMA table_info` e acrescenta as colunas novas de `repositories`; `CREATE TABLE IF NOT EXISTS` sozinho não faria isso num banco existente.

### API (esboço)

```
GET  /api/discovered-repositories?status=&sigla=       # padrão status=new; com sugestões
POST /api/discovered-repositories/{id}/create-app      # { name, squadId, description?, role? } → 201 app
POST /api/discovered-repositories/{id}/link            # { appId, role? } → 200 { warnings: [...] }
POST /api/discovered-repositories/{id}/other-squad
POST /api/discovered-repositories/{id}/ignore
POST /api/discovered-repositories/{id}/restore
```

### Telas

- `/discovered` ("Repositórios descobertos", no menu): tabela com filtro por sigla e status, indicador de arquivado, sugestão com confiança, e as ações da triagem; "Criar app" abre um painel inline pré-preenchido.
- `app-detail`: repositórios mostram sigla, sigla-app e papel.

### Notas da implementação (2026-10-12)

- **Nome da tela (2026-10-13)**: o menu **Descobertos** passou a se chamar **Repositórios**, com todos os status e o filtro "No catálogo" (spec 007). O texto desta spec descreve a tela como foi entregue na época.

- **`ScanService` ficava fora de `AddCheckAppsApplication`** (a fonte só a ferramenta registrava, e a validação de DI em Development quebraria a API); **superado pela spec 006**, que criou a composição única e fez a fonte expor `ConfigurationProblem` em vez de lançar no construtor.
- **Sem `Scan:NameTemplate` a triagem funciona, sem sugestões** (papel nulo, confiança baixa); o scan recusa rodar. Com a fonte `fake` (padrão `fake-{sigla}-` desde a spec 006, na composição). A API lê `Scan:NameTemplate` de user-secrets próprios (`UserSecretsId` no `CheckApps.Api`).
- **O escopo é validado antes de montar a fonte**: sigla vazia ou fora do formato é recusada sem pedir token nem consultar nada.
- **Toda a leitura acontece antes de qualquer gravação**: um limite estourado na segunda sigla não deixa um scan pela metade.
- **`--dry-run` não grava dados**, mas garante o schema (DDL idempotente), porque classificar novo/atualizado/inalterado exige ler a tabela.
- **Fonte `fake`** tem provedor `fake` (ids `fake:{n}`), nomes montados a partir do prefixo recebido, ruído fora da convenção e respeita o limite — mostra os mesmos casos da fonte real.
- **Estilos globais**: `.tag` e `.inline-checkbox` viraram globais; várias telas (inclusive de specs anteriores) os usavam sem estilo.
- **Leitura real**: Validado em 2026-10-12 contra uma organização de teste: a busca por sigla encontrou o repositório (inclusive recém-criado), o prefixo exato e o token fine-grained funcionaram, e a sigla-app foi lida do `config.yaml` (confiança subiu de baixa para alta). **Ainda não exercitados com o GitHub real**: repositório arquivado, paginação, limite de resultados, limite de chamadas, repositório fora do acesso do token e YAML inválido. Os testes cobrem esses casos só com JSON fixo.

### Fora desta spec (backlog)

Fases 2 e 3 do leitor; busca por nome; agendador; GitHub App; CRUD de Repository; tags chave-valor; entidade Sigla e conta AWS por ambiente.
