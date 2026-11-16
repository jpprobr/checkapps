# Spec 007: Menu "Repositórios" com todos os status

**Status**: Implementada (2026-10-13); fluxo verificado no navegador em 2026-11-15
**Criada em**: 2026-10-13
**Origem**: primeiro uso real do scan. O menu "Descobertos" nomeava um *estado* (o que ainda não foi decidido) e o repositório que virava app **desaparecia** da tela: a tabela ficava vazia e a tela parecia "não ter mudado". A regra de negócio estava certa; a tela não a comunicava. Decisão do usuário, depois de avaliar o nome e a estrutura.

## Escopo e fronteira

O menu passa a se chamar **"Repositórios"** e a tela mostra **todos os repositórios conhecidos**, cada um com o seu status — *novo*, *no catálogo* (com link para o app), *de outra squad*, *ignorado*. O filtro padrão é **"Todos"** (alterado em 2026-10-18; era "Novos", o que precisa de decisão, que segue como filtro). O bloco "Buscar no GitHub" não muda.

**Limite aceito** (superado pela spec 010, 2026-10-18): "conhecidos" eram os que **passaram pelo scan**; hoje a tela também lista os cadastrados à mão e os do seed, nos filtros Todos e No catálogo.

**Não faz parte**: o menu lateral com ícones (spec seguinte); listar repositórios cadastrados à mão ou pelo seed; desvincular um repositório pela tela (hoje se remove o app); renomear o recurso da API (`discovered-repositories` continua sendo o que é: repositórios descobertos pelo scan).

## User Scenarios & Testing

### User Story 1 — Ver o que aconteceu com um repositório (Prioridade: P1)

Depois de criar um app a partir de um repositório, a pessoa continua vendo esse repositório na tela, marcado como **no catálogo** e com o link do app, em vez de ele sumir.

**Por que esta prioridade**: é a confusão real que motivou a spec.

**Teste independente**: com a fonte fake, criar um app a partir de um item e ver o item em "No catálogo" e em "Todos", com link para o app.

**Cenários de aceite**:

1. **Dado** um repositório que virou app, **Quando** escolho o filtro "No catálogo", **Então** vejo-o com a etiqueta "no catálogo" e o link para o app, **sem** botões de triagem.
2. **Dado** o mesmo repositório, **Quando** escolho "Todos", **Então** vejo-o junto com os que aguardam decisão, cada linha com as ações que cabem ao estado dela.
3. **Dado** o mesmo repositório, **Quando** deixo o filtro em "Novos", **Então** ele não aparece (não há decisão a tomar), e o estado vazio, se for o caso, aponta o filtro "No catálogo".
4. **Dado** o link do app numa linha "no catálogo", **Quando** clico, **Então** abro o detalhe do app.

### User Story 2 — O nome do menu diz onde estou (Prioridade: P1)

O menu e o título da tela se chamam "Repositórios".

**Por que esta prioridade**: é o pedido; o nome anterior descrevia um estado, não a coisa.

**Teste independente**: abrir o menu e a tela.

**Cenários de aceite**:

1. **Dado** o menu, **Então** vejo "Repositórios" no lugar de "Descobertos".
2. **Dado** um favorito antigo em `/discovered`, **Quando** abro, **Então** sou levado a `/repositories`.
3. **Dado** a tela, **Então** o texto explica que ela lista os repositórios que o scan encontra, e que o scan propõe e a pessoa decide.

### Edge Cases

- Repositório catalogado cujo status armazenado é `new` ou outro: aparece "no catálogo", sem ações — o estado derivado vale mais que o armazenado.
- Repositório que volta para a caixa porque o app foi removido: reaparece em "Novos" (o estado é derivado).
- Dois provedores na mesma instalação (fake e github): o app de cada repositório é buscado por provedor.
- Resultado do scan com repositório já no catálogo: a mensagem aponta o filtro "No catálogo".

## Requirements

### Functional Requirements

- **FR-001**: `GET /api/discovered-repositories` DEVE aceitar `status` = `new` (padrão), `ignored`, `other-squad`, `catalog` ou `all`. `new`, `ignored` e `other-squad` DEVEM listar só os que ainda aguardam decisão (como antes); `catalog` DEVE listar só os que já estão no catálogo; `all` DEVE listar **todos**, inclusive os catalogados. Valor desconhecido → 400.
- **FR-002**: Cada item DEVE trazer `catalogApp` (`appId` e `appName`) quando o repositório está no catálogo, e nulo caso contrário, calculado em lote.
- **FR-003**: A tela DEVE oferecer os filtros *Novos*, *No catálogo*, *De outra squad*, *Ignorados* e *Todos*, com "Todos" como padrão, além do filtro por sigla.
- **FR-004**: Uma linha no catálogo DEVE mostrar a etiqueta "no catálogo" e o link do app, e NÃO DEVE oferecer ações de triagem. Linhas ignoradas ou de outra squad mantêm "Restaurar"; as novas mantêm as quatro ações.
- **FR-005**: O menu e o título DEVEM se chamar "Repositórios"; a rota é `/repositories`, e `/discovered` DEVE redirecionar para ela.
- **FR-006**: A tela DEVE dizer que lista os repositórios que passaram pelo scan, e o estado vazio de "Novos" DEVE apontar o filtro "No catálogo".

### Key Entities

Nenhuma entidade nova. **"No catálogo" continua derivado** (existe um `Repository` com o mesmo provedor e id externo), nunca um status armazenado. A API ganha o escopo `catalog` e o `catalogApp` por item.

**AI-friendly**: nenhum dado novo é gravado.

## Success Criteria

- **SC-001**: Depois de criar um app a partir de um repositório, a pessoa o encontra na tela, com o link do app, em até dois cliques.
- **SC-002**: Nenhuma pessoa precisa saber a regra interna ("já no catálogo sai da caixa") para entender o que vê.
- **SC-003**: O favorito antigo continua funcionando.

## Assumptions

- **"Todos" muda de significado** na API: antes excluía os catalogados, agora os inclui. É intencional e o ponto da spec.
- **Só entram os que passaram pelo scan.** Os cadastrados à mão ou pelo seed dependem do item "CRUD de Repository".
- **Pastas e componente do frontend são renomeados** para o código dizer o mesmo que a tela; o contrato da API não.

## Design notes

### Access patterns

| # | Padrão | Resolução |
|---|---|---|
| AP-1 | Pendentes de decisão (`new`, `ignored`, `other-squad`) | `discovered_repositories` com `NOT EXISTS` em `repositories` (como antes) |
| AP-2 | Já no catálogo | `discovered_repositories` com `EXISTS` em `repositories` |
| AP-3 | Todos | `discovered_repositories` sem filtro de catálogo |
| AP-4 | App de cada item | `GetCatalogAppsAsync` em lote, agrupado por provedor |

### API

```
GET /api/discovered-repositories?status=new|catalog|other-squad|ignored|all&sigla=
```

### Notas da implementação (2026-10-13)

- **`all` mudou de significado** na API (antes excluía os catalogados, agora os inclui) e **`catalog`** é novo. O roteiro de navegador da spec 005 usava `status=all` para provar "o catalogado não reaparece na caixa"; a intenção é a caixa (`new`), então a consulta do roteiro foi trocada — sem mudança de comportamento da caixa.
- **O escopo é um parâmetro com padrão** (`CatalogScope.Pending`), então os chamadores e testes anteriores não mudaram de comportamento. O app de cada item é resolvido em lote **por provedor** (fake e github coexistem e podem repetir o id externo); a listagem de pendentes não consulta o catálogo (por definição, não estão nele).
- **O estado derivado vale mais que o armazenado**: um catalogado cujo status armazenado é `new` aparece "no catálogo" e sem ações. Removido o app, o repositório volta a "Novos" sem nenhuma migração — verificado em teste de integração e no navegador.
- **Pasta e componente do frontend renomeados** (`features/repositories`, `Repositories`) para o código dizer o que a tela diz; o serviço de API e o recurso continuam `discovered-repositories`.
- **`/discovered` redireciona para `/repositories`** (`pathMatch: 'full'`), para favoritos antigos.
- **Os roteiros de navegador das specs 005 e 006 foram ajustados** só nos seletores de menu e URL e na mensagem do resultado do scan, que agora aponta o filtro "No catálogo".

### Fora desta spec (backlog)

Menu lateral com ícones; CRUD de Repository (repositórios cadastrados à mão ou pelo seed na mesma lista); desvincular pela tela.
