# Spec 016: Navegação — Apps no menu, "Pacotes" e seções recolhíveis no detalhe do app

**Status**: Implementada (2026-10-27); confirmada pelo usuário na tela em 2026-11-02
**Criada em**: 2026-10-27
**Origem**: revisão do usuário depois de conferir as telas da spec 014 (2026-10-27): (1) Integrações também são um tipo de dependência, então o menu precisa de uma organização mais clara; (2) só os Apps ficaram sem item no menu principal, acessíveis apenas por dentro de uma squad; (3) os cartões Dependências e Integrações do detalhe do app ficaram colados e o detalhe ganhou muita informação. A numeração pula a 015, reservada à leitura de código (interfaces Refit e `AddHttpClient`).

> Formato compatível com o template de spec do Spec-Kit (github/spec-kit). Ambiguidades estão marcadas com `[NEEDS CLARIFICATION]` e viram perguntas antes da aprovação.

## Escopo e fronteira

**Faz parte**

1. **Item "Apps" no menu principal**: uma tela com **todos os apps de todas as squads**, com busca por nome, filtro de squad, filtro de Quality Level, ordenação e a squad em cada linha. O detalhe do app continua o mesmo (`/apps/:id`); muda só o caminho de acesso.
2. **"Dependências" passa a se chamar "Pacotes"** no menu e no título da tela (que continua com "Quem usa o pacote" e ".NET por squad"). **"Integrações" continua um item próprio.** O menu fica: Squads, Apps, Repositórios, Pacotes, Integrações, Checagens. Decidido pelo usuário em 2026-10-27 (alternativas descartadas: submenu e tela única com abas).
3. **Seções recolhíveis no detalhe do app**, só **Pacotes** e **Integrações**: vêm **fechadas por padrão**, com um **resumo na linha fechada**, e o estado aberto/fechado fica **lembrado no navegador**. Os demais cartões do app seguem abertos como hoje.
4. **Espaçamento**: os cartões Dependências e Integrações ficavam colados porque a margem entre cartões vinha de um CSS que não alcança componentes filhos. **Já corrigido** (2026-10-27, intervalo de 20 px entre todos os cartões); esta spec só o registra e o protege com um teste no navegador.

**Não faz parte**

- Criar app pela tela global (continua dentro da squad, onde a squad já está definida).
- Tornar recolhíveis os outros cartões do app (Checagens, Evolução, Histórico, Repositórios, Projetos Sonar, Links).
- Submenu no menu lateral, busca global no cabeçalho e persistência de filtros na URL.
- Renomear rotas de API, arquivos de spec e termos de código: **"Pacotes" é só o rótulo para o usuário**.

## User Scenarios & Testing *(obrigatório)*

### User Story 1 — Achar um app sem saber a squad (Prioridade: P1)

Como tech lead, abro **Apps** no menu e encontro um app digitando parte do nome, sem precisar lembrar a qual squad ele pertence.

**Por que esta prioridade**: hoje o caminho é Squads → squad → Apps → app, e não há busca entre squads; é o ganho de uso diário que motivou a revisão.

**Teste independente**: com apps em duas squads, abrir `/apps`, buscar e abrir um app.

**Cenários de aceite**:

1. **Dado** apps em várias squads, **Quando** abro **Apps** no menu, **Então** vejo todos os apps ativos, cada um com nome, squad, criticidade, Quality Level e último snapshot, ordenados por nome.
2. **Dado** o texto "pag" na busca, **Então** só aparecem apps cujo nome contém "pag", sem diferença de maiúsculas e minúsculas.
3. **Dado** um filtro de squad e/ou de Quality Level, **Então** a lista é restringida pelos dois filtros combinados com a busca.
4. **Dado** "Mostrar inativos" desmarcado (padrão), **Então** apps inativos não aparecem; marcado, aparecem com a etiqueta "inativo".
5. **Dado** um app na lista, **Quando** clico no nome, **Então** abro o mesmo detalhe de `/apps/:id` que se abre pela squad.
6. **Dado** nenhum app que atenda aos filtros, **Então** a tela diz "Nenhum app encontrado." em vez de uma tabela vazia.
7. **Dado** o menu lateral, **Então** o item **Apps** fica entre Squads e Repositórios, com ícone, `aria-current` na tela ativa e comportamento de menu recolhido como os demais itens.

### User Story 2 — Seções longas do app recolhidas (Prioridade: P1)

Como tech lead, abro o detalhe de um app e vejo primeiro o essencial; abro **Pacotes** e **Integrações** só quando preciso, e a minha escolha é lembrada.

**Por que esta prioridade**: o detalhe do app tem sete cartões, e as duas seções novas são as mais longas.

**Teste independente**: abrir um app com análise, conferir as duas seções fechadas, abrir uma, recarregar.

**Cenários de aceite**:

1. **Dado** um app com análise, **Quando** abro o detalhe pela primeira vez, **Então** **Pacotes** e **Integrações** aparecem fechadas, cada uma com um resumo na linha fechada (ex.: "net9.0, net8.0 · 7 pacotes" e "4 integrações em 2 seções · 1 não resolvida").
2. **Dado** uma seção fechada, **Quando** clico no título (ou uso Enter/Espaço com o foco nele), **Então** ela abre e mostra o conteúdo de hoje, sem mudança.
3. **Dado** que abri **Integrações**, **Quando** recarrego a página ou abro outro app, **Então** **Integrações** continua aberta (a escolha vale para o navegador, por seção, não por app).
4. **Dado** um app sem análise ou sem scanner aplicável, **Então** o resumo diz isso ("ainda não analisado", "nenhum scanner se aplica") e abrir mostra a explicação atual.
5. **Dado** uma falha ao carregar a seção, **Então** o resumo diz "não foi possível carregar" e a mensagem de erro aparece ao abrir.
6. **Dado** o navegador sem armazenamento disponível (modo privado, bloqueado), **Então** as seções funcionam normalmente e voltam fechadas a cada visita, sem erro.
7. **Dado** os cartões do detalhe do app, **Então** o intervalo vertical entre quaisquer dois cartões consecutivos é o mesmo (inclusive antes e depois de Pacotes e Integrações).

### User Story 3 — "Pacotes" e "Integrações" no menu (Prioridade: P2)

Como pessoa que usa o CheckApps, vejo no menu **Pacotes** e **Integrações** como dois assuntos distintos e com nomes diretos.

**Por que esta prioridade**: é renomeação de rótulo, sem mudar o que as telas fazem.

**Teste independente**: abrir o menu e as duas telas.

**Cenários de aceite**:

1. **Dado** o menu, **Então** o item antes chamado "Dependências" aparece como **Pacotes**, com o mesmo ícone, e abre a tela com "Quem usa o pacote" e ".NET por squad".
2. **Dado** um favorito antigo para `/dependencies`, **Então** ele continua funcionando (redireciona para a rota nova).
3. **Dado** o detalhe do app, **Então** a seção antes chamada "Dependências" aparece como **Pacotes** (framework e pacotes diretos), ao lado de **Integrações**.

### Edge Cases

- **Muitos apps**: a lista global não pagina nesta spec (o volume esperado é de dezenas a poucas centenas); a tela rola normalmente.
- **App sem squad ou com squad removida**: a coluna Squad mostra "—"; o app não some da lista.
- **Squad inativa**: seus apps aparecem normalmente; o filtro de squad lista todas as squads.
- **Estado lembrado de seção**: chave por seção (`pacotes`, `integracoes`), não por app; valor desconhecido ou corrompido vale como "fechada".
- **Rota antiga `/squads/:squadId/apps`**: continua existindo, com os dados só daquela squad e o botão "+ Novo App".
- **Menu recolhido e celular**: o item **Apps** segue o mesmo padrão dos demais (só ícone com `aria-label` e `title`; gaveta no celular).

## Requirements *(obrigatório)*

### Functional Requirements

- **FR-001**: O menu lateral DEVE ter o item **Apps** (rota `/apps`) entre Squads e Repositórios, com ícone SVG embutido, `aria-current` e o comportamento de recolhido/gaveta dos demais itens.
- **FR-002**: A tela **Apps** DEVE listar todos os apps de todas as squads com nome (link para `/apps/:id`), squad, criticidade, Quality Level e último snapshot; busca por nome sem diferença de caixa; filtros de squad e de Quality Level; ordenação por nome ou por Quality Level; "Mostrar inativos" desmarcado por padrão; e a mensagem "Nenhum app encontrado." quando nada atende.
- **FR-003**: A tela **Apps** DEVE reaproveitar a lista e as regras da lista por squad (`AppList`), que continua em `/squads/:squadId/apps` com o botão "+ Novo App"; a tela global NÃO cria app.
- **FR-004**: A API DEVE oferecer a listagem de todos os apps: `GET /api/apps` com `qualityLevel` **opcional** (hoje obrigatório na prática, pois o binder assume o primeiro valor do enum); sem `qualityLevel`, devolve todos. Nenhum consumidor atual depende do comportamento antigo.
- **FR-005**: O item de menu e o título da tela "Dependências" DEVEM passar a **Pacotes**; a rota passa a `/packages` e `/dependencies` DEVE redirecionar para ela. O item **Integrações** não muda.
- **FR-006**: No detalhe do app, **Pacotes** e **Integrações** DEVEM ser seções recolhíveis (elemento nativo `details`/`summary`, acessível por teclado), **fechadas por padrão**, cada uma com um **resumo** na linha fechada derivado dos dados já carregados.
- **FR-007**: O estado aberto/fechado de cada seção DEVE ser lembrado no navegador (uma chave por seção), com leitura e escrita protegidas: sem armazenamento, a seção funciona e começa fechada.
- **FR-008**: O espaçamento vertical entre os cartões do detalhe do app DEVE ser o mesmo para todos, inclusive os que vivem em componentes filhos (cada componente de seção dá a margem ao próprio elemento).
- **FR-009**: Os demais cartões do detalhe do app NÃO DEVEM mudar de comportamento nem ficar recolhíveis.
- **FR-010**: Nenhuma rota, nome de API ou documentação técnica de código muda por causa do rótulo "Pacotes"; só o texto mostrado ao usuário e a rota de tela com redirecionamento.

### Key Entities

Nenhuma entidade nova nem tabela nova. O estado das seções é preferência de **navegador** (por visualizador), não dado do sistema: não vai para o banco nem é compartilhado. Identidade: chave de seção estável (`pacotes`, `integracoes`). Proveniência e tempo: não se aplicam (preferência de interface).

## Success Criteria *(obrigatório)*

- **SC-001**: Uma pessoa chega ao detalhe de um app qualquer, de qualquer squad, em até 10 segundos a partir de qualquer tela, digitando parte do nome na tela Apps.
- **SC-002**: Ao abrir o detalhe de um app, o essencial (qualidade, checagens, repositórios) aparece sem rolar além das seções recolhidas; Pacotes e Integrações exigem um clique para abrir.
- **SC-003**: Reabrir um app depois de recarregar a página mantém as seções como a pessoa as deixou.
- **SC-004**: O intervalo entre quaisquer dois cartões consecutivos do detalhe do app é igual (verificado por medida no navegador).
- **SC-005**: Nenhum favorito antigo (`/dependencies`) deixa de funcionar.

## Assumptions

- O usuário escolheu (2026-10-27): dois itens de menu (Pacotes e Integrações), tela Apps com lista geral, busca e filtros, e recolher só Pacotes e Integrações, fechadas por padrão.
- O volume de apps cabe numa lista sem paginação.
- Não há autenticação (ADR-007); a preferência de seção é por navegador.
- O resumo da seção fechada usa os dados que o componente já busca ao carregar o app (uma chamada por seção, como hoje).

## Design notes *(opcional)*

- A lista por squad e a global podem ser o mesmo componente, com o `squadId` opcional na rota; a diferença é o título/trilha de navegação, a coluna Squad e o botão "+ Novo App", que só existem no modo por squad.
- A coluna Squad usa a lista de squads já carregada (nome por id), sem mudar o contrato de `AppSummary`.
- Padrão de armazenamento: `localStorage` com `try/catch` em toda leitura e escrita (browser pode bloquear); é preferência do visualizador, não estado que o sistema precise guardar.
- O espaçamento já foi corrigido com `:host { display: block; margin-top: 1.25rem }` nos dois componentes de seção; um teste no navegador mede o intervalo para não voltar a quebrar.
