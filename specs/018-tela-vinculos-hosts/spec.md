# Spec 018: Tela de vínculos host→app (fatia 2b das integrações, parte 1: fila de hosts)

**Status**: Implementada e confirmada pelo usuário na tela (2026-11-03)
**Criada em**: 2026-11-03
**Origem**: fatia 2b das integrações. A spec 017 criou o vínculo host→app no backend; aqui uma pessoa passa a decidir pela tela, sem `curl`. Trabalho dividido em duas partes (decisão do usuário, para caber em sessões curtas): esta é a **parte 1** (fila de hosts + confirmar/rejeitar/remover). A parte 2 (selo "→ App X" por seção no detalhe do app e "não visto na última análise") vem depois.

> Formato compatível com o template de spec do Spec-Kit (github/spec-kit).

## Escopo e fronteira

**Faz parte**

1. Na tela **Integrações** (`/integrations`, já a lista de hosts da spec 014): filtro **Vínculo** (Todos | Sem app | Confirmado | Só rejeitado), coluna **App** e um **painel de vínculo** no detalhe do host.
2. Confirmar, rejeitar e remover o vínculo de um host (com nota opcional), usando os endpoints da spec 017.
3. Mostrar o erro do servidor (409 "já confirmado para outro app", 400, 404) em alerta.

**Não faz parte**

- Item novo no menu (a tela existente é a superfície de gerenciamento; a regra do menu proíbe submenu).
- Selo "→ App X" no detalhe do app e lista de vínculos não vistos (parte 2), mapa de aplicações, sugestão automática, spec 015.
- Mudança de backend.

## User Scenarios & Testing *(obrigatório)*

### User Story 1 — Atender a fila de hosts sem app (Prioridade: P1)

A pessoa filtra por "Sem app", abre um host, escolhe o app que o recebe e confirma. O host sai da fila.

**Cenários de aceite**:

1. **Dado** hosts sem vínculo, **Quando** a pessoa filtra "Sem app", **Então** só eles aparecem, com "sem app" na coluna App.
2. **Dado** um host aberto, **Quando** escolhe um app e confirma, **Então** o painel mostra "→ App X (confirmado neste host)" e a lista é atualizada.
3. **Dado** um host já confirmado para o app A, **Quando** tenta confirmar o app B, **Então** aparece o texto do servidor em alerta, nomeando o app A.
4. **Dado** um vínculo feito no modelo `{env}`, **Quando** a pessoa abre o host `hom`, **Então** ele aparece ligado ao app com a tag "pelo modelo" e o aviso de que vale para dev, hom e prod.

### User Story 2 — Rejeitar e desfazer (Prioridade: P2)

**Cenários de aceite**:

1. **Dado** um host, **Quando** a pessoa rejeita um app, **Então** o rejeito aparece listado com a ação "desfazer".
2. **Dado** um vínculo vindo do modelo, **Quando** a pessoa remove, **Então** o vínculo do **modelo** é removido (vale para todos os ambientes) e o texto avisa isso antes.

### Edge Cases

- Sem app escolhido, Confirmar e Rejeitar ficam desabilitados.
- Falha de rede ou erro sem texto: mensagem genérica em alerta.
- Estado nunca só por cor: "sem app", "→ App", "rejeitado: …" são texto.
- Celular (390 px): sem estouro horizontal da página (a tabela rola dentro de `.table-scroll`).
- Com o filtro "Sem app" ativo, o host confirmado sai da lista e o painel continua aberto (a fila anda).

## Requirements *(obrigatório)*

- **FR-001**: A lista de hosts DEVE aceitar o filtro de vínculo e enviá-lo como `linkStatus`.
- **FR-002**: A tabela DEVE mostrar o vínculo efetivo de cada host em texto, com a tag "pelo modelo" quando vier do modelo.
- **FR-003**: O detalhe do host DEVE mostrar o vínculo confirmado (app, origem exata ou modelo, data em horário local, nota) e os rejeitos.
- **FR-004**: A tela DEVE confirmar e rejeitar (PUT) e remover (DELETE) vínculos; a remoção usa a chave do vínculo (a do modelo quando `via = template`).
- **FR-005**: Erros do servidor DEVEM aparecer em `role="alert"` com o texto recebido.
- **FR-006**: Após gravar ou remover, o detalhe e a lista DEVEM ser recarregados com os filtros atuais, sem fechar o host aberto.
- **FR-007**: Os textos obsoletos ("o CheckApps ainda não diz a que app…") DEVEM ser reescritos.

## Success Criteria *(obrigatório)*

- **SC-001**: Uma pessoa liga um host modelo a um app e vê os três ambientes ligados sem usar `curl`.
- **SC-002**: Nenhum vínculo é criado sem uma ação explícita da pessoa.

## Assumptions

- A lista de apps para escolha é a de `GET /api/apps` (todos), ordenada por nome.
- Não há suíte de testes Angular no projeto (decisão registrada); a verificação é o build e o roteiro no navegador.
