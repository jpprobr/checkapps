# Spec 022: Jornadas (cadastro em árvore, passos em endpoints e impacto de mudanças de contrato)

**Status**: Implementada (2026-11-09); falta a confirmação do usuário na tela
**Criada em**: 2026-11-09
**Origem**: pedido do usuário (2026-11-02 a 2026-11-09). A spec 021 mostra **o que mudou** num contrato, mas não **o que isso afeta**: uma quebra aparece como "mudou o endpoint X", sem ligação com o fluxo de negócio que depende dele, e o impacto continua sendo descoberto em homologação. Jornadas são essa ligação e também o contexto de negócio de cada app (`vision.md`). Decisões de modelo registradas em `backlog.md` (2026-11-08), a validar com o uso.

> Formato compatível com o template de spec do Spec-Kit (github/spec-kit). Dividida como as specs 019 e 020: **022a** (backend) e **022b** (tela).

## Escopo e fronteira

**Faz parte**

1. **Cadastro de jornadas em árvore** (pai opcional), com **nível em texto livre**, squad dona opcional e tags opcionais.
2. **Passos**: lista ordenada de endpoints (de um app) numa jornada-folha. O passo guarda uma **identidade estável** (app + método + rota), não o id do endpoint, que muda a cada análise.
3. **Resolução dos passos** contra o contrato corrente do app, calculada na leitura.
4. **Impacto**: o que mudou nos contratos (spec 021) e atinge os passos de uma jornada e da subárvore dela, calculado na leitura.
5. **Apps derivados** de uma jornada (os apps dos passos da subárvore), calculados, não digitados.
6. **Tela** para navegar a árvore, cadastrar e ver o impacto (022b).

**Não faz parte** (cada item é candidato a spec própria)

- Massas, payload de exemplo e exportação; executar chamadas.
- Reordenar jornadas irmãs (ordem por nome); jornada em dois pais (grafo); mais de uma squad por jornada.
- Seção "Jornadas" no detalhe do app e "quem é afetado" dentro da seção Contratos (a leitura por app fica possível pelo índice em `journey_steps(app_id)`).
- Jornadas no seed; autenticação; dono por squad com autorização; id ou vocabulário de cadastros de outros times (desconsiderados por ora).
- Gravar a resolução ou o impacto; alertas.

## User Scenarios & Testing *(obrigatório)*

### User Story 1 — Cadastrar a árvore de jornadas (Prioridade: P1)

Como pessoa que mantém o catálogo, cadastro uma plataforma, um sistema dentro dela e as jornadas dentro do sistema, cada um com um nível que eu escolho.

**Por que esta prioridade**: sem a árvore nada das outras histórias existe.

**Teste independente**: pela API (`curl`) ou pela tela, criar a árvore da jornada de Fundos e ler de volta.

**Cenários de aceite**:

1. **Dado** nenhuma jornada, **Quando** crio "Plataforma de investimentos" com nível `plataforma`, **Então** ela existe sem pai, com id por slug (`plataforma-de-investimentos`) e origem `MANUAL`.
2. **Dado** essa plataforma, **Quando** crio "Sistema de boletagem" (nível `sistema`) com ela como pai e depois "Compra de Fundos" (nível `jornada`) com o sistema como pai, **Então** a árvore tem três níveis.
3. **Dado** o nível em branco, **Então** a jornada é criada sem nível; um nível com mais de 40 caracteres é recusado (400).
4. **Dado** uma jornada com filhos, **Quando** tento removê-la, **Então** recebo 409 dizendo quantos filhos tem; sem filhos, a remoção leva os passos e as tags junto.
5. **Dado** uma jornada, **Quando** tento mudar o pai dela para ela mesma ou para um descendente, **Então** recebo 409 (ciclo).
6. **Dado** duas jornadas irmãs, **Quando** crio ou renomeio uma com o nome da outra (sem diferença de caixa), **Então** recebo 409; o mesmo nome sob pais diferentes é aceito.
7. **Dado** uma árvore já com 8 níveis, **Quando** crio um filho do último, **Então** recebo 409 (profundidade máxima).
8. **Dado** um `squadId` ou `parentId` que não existe, **Então** recebo 400 (é entrada do cliente); jornada inexistente na URL é 404.

### User Story 2 — Registrar os passos de uma jornada (Prioridade: P1)

Como pessoa que conhece o fluxo, registro, em ordem, os endpoints que a jornada "Compra de Fundos" chama: consultar fundos, simular e solicitar a ordem.

**Por que esta prioridade**: é o que liga negócio a contrato.

**Teste independente**: substituir os passos pela API e ler o detalhe com a resolução.

**Cenários de aceite**:

1. **Dado** uma jornada sem filhos e um app com contrato analisado, **Quando** substituo os passos por três itens (app, método, rota, rótulo), **Então** o detalhe os mostra na mesma ordem, cada um **catalogado**, com resumo, segurança e se está deprecated.
2. **Dado** um passo cuja rota usa `{id}` e um contrato que a chama `{fundoId}`, **Então** o passo continua **catalogado** (a chave ignora o nome do parâmetro).
3. **Dado** um passo cujo endpoint não está no contrato corrente do app, **Então** o estado é **fora do contrato**, em texto, sem erro.
4. **Dado** um passo de um app que nunca teve contrato analisado, **Então** o estado é **app sem contrato**.
5. **Dado** uma jornada que tem filhos, **Quando** tento definir passos, **Então** recebo 409; **Dado** uma jornada com passos, **Quando** crio um filho dela, **Então** recebo 409.
6. **Dado** um app inexistente, um método que não é HTTP ou uma rota que não começa com `/`, **Então** recebo 400, e nada é gravado.
7. **Dado** um app usado em algum passo, **Quando** tento removê-lo, **Então** recebo 409; **Dado** uma squad dona de jornada, **Quando** tento removê-la, **Então** recebo 409.

### User Story 3 — Ver o impacto de uma mudança de contrato (Prioridade: P1)

Como dev, QA ou tech lead, abro uma jornada e vejo quais passos dela foram afetados pelas mudanças de contrato mais recentes e o que mudou, antes de descobrir em homologação.

**Por que esta prioridade**: é o motivo de ligar jornadas a contratos.

**Teste independente**: analisar `--fake-revision 0` e depois `1`, cadastrar passos nos endpoints do contrato de demonstração e ler `GET /api/journeys/{id}/impact`.

**Cenários de aceite**:

1. **Dado** um passo `POST /simulacoes` e uma mudança "campo opcional virou obrigatório" nesse endpoint, **Então** o impacto lista a mudança com a severidade **quebra** e conta uma quebra para a jornada.
2. **Dado** um passo num endpoint removido do contrato, **Então** o passo aparece como **endpoint removido** (quebra).
3. **Dado** um sistema com várias jornadas, **Então** o impacto do sistema soma o das jornadas da subárvore, por passo e no total.
4. **Dado** um app que só tem a primeira análise, ou nenhum contrato, **Então** os passos dele aparecem como "sem comparação ainda", e **não** como "sem impacto".
5. **Dado** uma análise parcial, **Então** o que sumiu aparece como atenção, como já é na spec 021 (a regra é a mesma, vem do comparador).
6. **Dado** o mesmo endpoint em dois passos, **Então** a mesma mudança aparece nos dois.

### User Story 4 — Navegar pelas jornadas (Prioridade: P2, 022b)

Como pessoa do time, abro **Jornadas** no menu, vejo a árvore, entro numa jornada e vejo dados, filhos, passos, apps derivados e impacto, e edito.

**Teste independente**: Playwright em banco isolado com os dados da jornada de Fundos.

**Cenários de aceite**:

1. **Dado** jornadas cadastradas, **Quando** abro a lista, **Então** vejo a árvore, com nível, squad, tags e quantidade de passos, e posso filtrar por texto e por squad.
2. **Dado** nenhuma jornada, **Então** a tela explica como começar.
3. **Dado** uma jornada, **Quando** abro o detalhe, **Então** vejo o caminho até a raiz, os filhos, os passos com o estado em texto, os apps derivados (com link) e o impacto.
4. **Dado** o editor de passos, **Quando** escolho um app, **Então** vejo os endpoints do contrato dele para escolher, e posso digitar método e rota quando não estão no contrato.
5. **Dado** o menu recolhido ou o celular, **Então** o item Jornadas se comporta como os demais.

### Edge Cases

- **Jornada sem squad**: aparece como "sem squad".
- **Mesmo endpoint em dois contratos do mesmo app**: o passo resolve contra o conjunto de endpoints do app (qualquer repositório); as mudanças somam, como na spec 021.
- **Tag**: aparada, sem duplicata (sem diferença de caixa); a convenção `chave:valor` é só orientação.
- **Nível**: aparado; vazio vira nulo; a tela sugere `plataforma`, `sistema` e `jornada` sem obrigar.
- **Remover um app ou squad referenciado**: 409 nos dois, antes de qualquer FK.
- **Mover uma jornada** para outro pai que tem passos: 409.
- **Nome com acentos**: o id é um slug sem acentos; colisão ganha sufixo (`-2`), como app e squad.

## Requirements *(obrigatório)*

### Functional Requirements

- **FR-001**: O sistema DEVE guardar jornadas em tabela própria, com id por slug, nome, descrição, nível (texto livre, até 40 caracteres), squad dona opcional, pai opcional, origem, criação e última alteração, e tags em tabela própria.
- **FR-002**: O sistema DEVE recusar com 409: ciclo, nome repetido entre irmãos (sem diferença de caixa), profundidade acima de 8, remoção de jornada com filhos, passos em jornada com filhos e filhos em jornada com passos.
- **FR-003**: O sistema DEVE responder 400 para entrada inválida (nome vazio, nível longo demais, squad, pai ou app inexistentes, método ou rota inválidos, mais de 100 passos) e 404 para a jornada da URL inexistente.
- **FR-004**: O passo DEVE guardar app, método (em maiúsculas), rota, chave da rota (a mesma da comparação de contratos, `ApiPath.KeyOf`), posição, rótulo e nota opcionais. `PUT /api/journeys/{id}/steps` DEVE substituir a lista inteira numa transação.
- **FR-005**: A resolução de cada passo DEVE ser calculada na leitura contra o contrato corrente do app, com os estados `catalogado`, `fora-do-contrato` e `app-sem-contrato`, e NÃO DEVE ser gravada.
- **FR-006**: O impacto DEVE ser calculado na leitura, cruzando os passos da jornada e da subárvore com as mudanças de contrato por (método, chave da rota), com contagem por severidade, passos removidos e o estado da comparação de cada app (`comparado`, `primeira-analise`, `sem-contrato`). NÃO DEVE ser gravado.
- **FR-007**: Os apps derivados DEVEM sair dos passos da subárvore, com a contagem de passos de cada um.
- **FR-008**: Remover um app usado em passo, ou uma squad dona de jornada, DEVE devolver 409 com mensagem, nunca 500 por chave estrangeira.
- **FR-009**: A API DEVE ser: `GET`/`POST /api/journeys`, `GET`/`PUT`/`DELETE /api/journeys/{id}`, `PUT /api/journeys/{id}/steps` e `GET /api/journeys/{id}/impact`. Nenhuma dispara análise nem exige credencial do provedor.
- **FR-010**: As tabelas novas DEVEM entrar no `schema.sql`, com SQL portável (sem consulta recursiva nem `GROUP_CONCAT`; a árvore é montada em memória) e teste sobre banco antigo.
- **FR-011**: A tela (022b) DEVE ter item **Jornadas** no menu, carregada sob demanda, com a classificação do impacto sempre **em texto**.

### Key Entities

- **Jornada** (`journeys`): um nó da árvore (plataforma, sistema, jornada…). **Identidade**: id por slug, estável e legível, independente de nomes externos. **Relações**: `parent_id` (para ela mesma), `squad_id` opcional (quem cuida), passos (quando folha). **Proveniência**: `source = MANUAL`, criação e alteração. **Tempo**: sobrescreve (não é histórico). Nível e tags são texto livre, classificação para pessoas.
- **Passo** (`journey_steps`): um endpoint de um app numa jornada-folha. **Identidade**: id (guid) + (`journey_id`, posição); aponta para (`app_id`, método, `path_key`), **nunca** para o id de um endpoint de análise. **Relações**: `journey_id` (cascata) e `app_id` (sem cascata, com guarda). **Proveniência**: decisão humana. **Tempo**: o da jornada.
- **Resolução** e **Impacto** *(derivados, não persistidos)*: o estado de cada passo contra o contrato corrente e o cruzamento dos passos com as mudanças da spec 021.

## Success Criteria *(obrigatório)*

- **SC-001**: Em até 1 minuto, uma pessoa cadastra a árvore e os passos de uma jornada de 3 etapas, sem conhecer o banco.
- **SC-002**: Ao abrir uma jornada, em até 30 segundos a pessoa descobre se alguma mudança de contrato recente a quebra e em que passo.
- **SC-003**: Reanalisar um contrato nunca invalida um passo cujo endpoint continua no contrato.
- **SC-004**: Um app ou uma squad em uso nunca é removido por engano.
- **SC-005**: Um passo cujo contrato não foi analisado aparece como "sem comparação", nunca como "sem impacto".

## Assumptions

- Uma jornada tem **um** pai (árvore); se precisar estar em dois sistemas, vira grafo. **A validar com o uso.**
- A plataforma é opcional; o nível é texto livre e vai divergir entre squads; passo só em jornada-folha pode ser restritivo. **A validar com o uso** (anotado em `backlog.md`).
- O catálogo de contratos ainda não cobre tudo, por isso o passo livre é permitido.
- Cadastros de outros times (ex.: observabilidade) ficam fora: sem id externo nesta versão.
- Valores reais de nomes de sistemas e da empresa não entram em arquivo versionado; exemplos usam a jornada de Fundos de Investimento, genérica.

## Decisões do usuário (2026-11-08 e 2026-11-09)

1. Uma entidade em árvore; Produto e Operação não são entidades (a operação e a jornada dizem quase a mesma coisa nos exemplos reais).
2. Nível em texto livre, **registrado para validação futura**.
3. Tipo de produto como tag opcional.
4. Cadastros de outros times desconsiderados por ora.
5. Plano aprovado: divisão em 022a (backend) e 022b (tela), impacto incluído.

## Design notes *(opcional)*

- **Por que o passo não guarda o id do endpoint**: cada análise grava endpoints novos (é uma foto, spec 021). A identidade do endpoint entre commits já existe no comparador (método + rota com parâmetros neutralizados), e o passo a reaproveita.
- **Por que derivar a resolução e o impacto**: como na comparação de contratos, uma regra melhor vale para o histórico sem migração, e nada fica velho no banco.
- **Árvore em memória**: o número de jornadas é pequeno e SQL recursivo diverge entre bancos (ADR-009).
- **Por que o serviço lê contratos por uma interface** (`IContractReader`): isola a regra de jornadas dos detalhes da análise e permite testá-la sem banco.
- Decisão estrutural em `.agents/architecture/decisions/adr-012-jornadas.md`.
