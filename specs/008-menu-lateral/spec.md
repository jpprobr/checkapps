# Spec 008: Menu lateral recolhível, com ícones

**Status**: Implementada (2026-10-17)
**Criada em**: 2026-10-17
**Origem**: combinado com o usuário ao fim da spec 007. O menu horizontal no topo não comporta mais seções e o seletor de squad; um menu lateral com ícones escala melhor e libera a largura da tela.

## Escopo e fronteira

O header horizontal vira um **menu lateral**: marca, seletor de squad no topo, e as seções Squads, Repositórios e Checagens, cada uma com ícone. O menu pode ser **recolhido** (só ícones) e o estado é lembrado no navegador. No celular vira uma **gaveta**.

**Não faz parte**: árvore de squads/apps no menu (descartado no backlog); biblioteca de ícones; tema escuro manual; qualquer mudança de API ou de rota.

## User Scenarios & Testing

### User Story 1 — Navegar pelo menu lateral (Prioridade: P1)

**Teste independente**: abrir qualquer tela e usar o menu.

**Cenários de aceite**:

1. **Dado** o sistema aberto, **Quando** a tela carrega, **Então** vejo à esquerda a marca, o seletor de squad e as seções Squads, Repositórios e Checagens com ícone e texto.
2. **Dado** a seção da tela atual, **Quando** olho o menu, **Então** ela está marcada como ativa por fundo e barra lateral (não só por cor) e tem `aria-current="page"`.
3. **Dado** o seletor de squad, **Quando** troco a squad, **Então** o comportamento é o de antes (lista de apps → lista da outra squad; resto → dashboard).

### User Story 2 — Recolher o menu (Prioridade: P1)

**Cenários de aceite**:

1. **Dado** o menu expandido, **Quando** clico em "Recolher menu", **Então** ficam só os ícones, cada um com `aria-label` e dica (`title`).
2. **Dado** o menu recolhido, **Quando** recarrego a página, **Então** ele continua recolhido.
3. **Dado** o menu recolhido, **Quando** clico no ícone da squad, **Então** o menu expande para eu escolher.
4. **Dado** armazenamento do navegador bloqueado, **Quando** uso o menu, **Então** tudo funciona; só não lembra.

### User Story 3 — Gaveta no celular (Prioridade: P2)

**Cenários de aceite**:

1. **Dado** largura ≤ 768 px, **Quando** a tela carrega, **Então** o menu está oculto e há uma barra superior com botão de menu.
2. **Dado** a gaveta aberta, **Quando** clico num link, no fundo escurecido ou aperto Esc, **Então** ela fecha.

### Edge Cases

- Nome de squad muito longo no seletor: truncado, sem quebrar o layout.
- Squad inativa continua marcada "(inativa)" no seletor.

## Requirements

### Functional Requirements

- **FR-001**: O menu lateral lista Squads, Repositórios e Checagens com ícone SVG embutido (sem biblioteca) e `routerLinkActive`.
- **FR-002**: O seletor de squad fica no topo do menu, com o mesmo `aria-label` e a mesma lógica de troca.
- **FR-003**: Recolhido mostra só ícones, com `aria-label` e `title`.
- **FR-004**: O estado recolhido é lembrado em `localStorage` (`checkapps.sidebarCollapsed`), tolerando falha de acesso.
- **FR-005**: Em ≤ 768 px o menu é uma gaveta com fundo escurecido, fecha ao navegar, no fundo e com Esc.
- **FR-006**: Estado ativo nunca depende só de cor; só tokens `var(--…)`, claro e escuro.

### Key Entities

Nenhuma entidade de domínio. Estado de interface: `collapsed` (persistido, por navegador) e `drawerOpen` (volátil).

## Success Criteria

- **SC-001**: Todas as telas existentes continuam alcançáveis em até um clique.
- **SC-002**: Recolher e recarregar mantém o estado.
- **SC-003**: Nenhum erro de console nas telas, em largura de desktop e de celular.

## Assumptions

- Sem testes automatizados de Angular (decisão do projeto); verificação no navegador.
