# Spec 019: Decisão em lote, host externo e destino no app (fatia 2b, parte 2)

**Status**: Em implementação. **019a (backend)** implementada e verificada (2026-11-03), aguarda a confirmação do usuário sobre o contrato; **019b (frontend)** ainda não começou
**Criada em**: 2026-11-03
**Origem**: achados do usuário ao confirmar a spec 018 (registrados no `backlog.md`): nos projetos da squad só existem hosts concretos (um por arquivo de ambiente), então cada serviço pede três confirmações do mesmo app; e muitas integrações vão para apps de outras squads que talvez nunca sejam cadastrados, então o host fica "sem app" para sempre e a fila não esvazia. Decisões do usuário: só pessoa decide (sem sugestão automática); trabalho em duas sessões (019a backend, 019b frontend).

> Formato compatível com o template de spec do Spec-Kit (github/spec-kit).

## Escopo e fronteira

**Faz parte**

1. **019a (backend)**: marcar um host (ou o modelo `{env}`) como **externo ao catálogo** (outra squad, terceiro, legado), em tabela própria `host_externals`; **decidir vários hosts de uma vez** (`confirmado`, `rejeitado` ou `externo`); o filtro `linkStatus` ganha `externo`; as integrações de um app trazem a marca de externo (`externals`), ao lado dos `destinations` da 017.
2. **019b (frontend)**: seleção em lote na tela Integrações, "Marcar como externo", painel do host com o externo, bloco de vínculos "não vistos" e a coluna "Destino" (app, externo ou "sem app") na seção Integrações do detalhe do app.

**Não faz parte**

- Sugestão automática por nome ou IA; criar vínculo direto num modelo pela tela; a apresentação "modelo × hosts concretos" e a densidade/celular da tela (itens de UX no backlog); o mapa; a spec 015.
- Externos "não vistos" na lista de não vistos (só vínculos de app entram; limite conhecido).

## User Scenarios & Testing *(obrigatório)*

### User Story 1 — Decidir vários hosts de uma vez (P1)

**Cenários de aceite**:

1. **Dado** três hosts de ambientes diferentes do mesmo serviço, **Quando** a pessoa confirma o mesmo app para os três num só pedido, **Então** os três ficam `confirmado`.
2. **Dado** um lote em que um host já está confirmado para outro app, **Quando** o lote é aplicado, **Então** só esse host falha (com o texto do erro) e os demais são gravados.
3. **Dado** um lote sem hosts, com mais de 200, ou com decisão inválida (estado desconhecido, `sugerido`, app ausente), **Então** o pedido todo é recusado (400).

### User Story 2 — Marcar um host como externo (P1)

**Cenários de aceite**:

1. **Dado** um host de um serviço de outra squad, **Quando** a pessoa o marca como externo (com nome opcional), **Então** ele sai da fila `sem-vinculo` e aparece como `externo`.
2. **Dado** um modelo `{env}` marcado como externo, **Então** os hosts `dev`, `hom` e `prod` que ele cobre aparecem como externos (`via` = `template`).
3. **Dado** um host confirmado para um app, **Quando** se tenta marcá-lo como externo (ou o inverso), **Então** a API recusa (409) até a decisão anterior ser removida.
4. **Dado** um host externo, **Quando** a pessoa remove a marca, **Então** ele volta à fila (204; 404 se não estava marcado).

### User Story 3 — Ver o destino de cada integração de um app (P2)

1. **Dado** uma integração para um host externo, **Quando** a API devolve as integrações do app, **Então** a seção traz a marca em `externals` (e não em `destinations`).

### Edge Cases

- Lote com chaves repetidas (ou só diferentes na caixa): cada host é aplicado uma vez.
- Chave inválida dentro do lote: falha só aquele host.
- **Rejeitos convivem com externo** (exclusão mútua é só entre `confirmado` e `externo`).
- O exato vence o modelo, como no vínculo; o confirmado vence o externo no status efetivo (`confirmado` > `externo` > `rejeitado` > `sem-vinculo`).
- Reanálise não altera nem apaga marcas de externo.

## Requirements *(obrigatório)*

- **FR-001**: O sistema DEVE guardar marcas de externo em tabela própria `host_externals` (id, `host_key` único, `name` opcional, `origin = HUMAN`, `decided_at`, `note`), fora das análises e sem FK para integrações.
- **FR-002**: `PUT /api/integrations/hosts/{host}/external` DEVE criar ou trocar a marca (corpo `name`, `note`); 400 para host inválido ou texto longo, 409 se o host está confirmado para um app. `DELETE` remove (204/404/400).
- **FR-003**: Confirmar um app para um host marcado como externo DEVE ser recusado (409) com o nome do externo na mensagem.
- **FR-004**: `POST /api/integrations/links/batch` (corpo `hostKeys`, `state` = `confirmado` | `rejeitado` | `externo`, `appId`, `name`, `note`) DEVE aplicar a decisão a cada host reutilizando as regras individuais e devolver 200 com `[{hostKey, ok, error}]`; decisão inválida, lista vazia ou acima de 200 → 400.
- **FR-005**: O vínculo efetivo (`link`) de um host DEVE trazer `external` (com `via` e modelo) e o `status` DEVE seguir a precedência `confirmado` > `externo` > `rejeitado` > `sem-vinculo`; `linkStatus=externo` filtra a lista.
- **FR-006**: As seções de `GET /api/apps/{appId}/integrations` DEVEM trazer `externals` (por id de integração) para hosts externos sem app; `destinations` não muda.
- **FR-007**: Consultas com SQL portável; tabela nova em `schema.sql` com teste em `SchemaEvolutionTests`; nenhum teste usa rede.
- **FR-008 (019b)**: A tela Integrações DEVE permitir selecionar vários hosts e aplicar confirmar, rejeitar ou marcar como externo, mostrando o resultado por host e as falhas em alerta; o detalhe do app DEVE mostrar o destino de cada integração.

## Success Criteria *(obrigatório)*

- **SC-001**: Os três hosts de ambiente de um serviço são ligados ao mesmo app numa única ação.
- **SC-002**: Hosts de serviços fora do catálogo saem da fila sem inventar apps.
- **SC-003**: Nenhum host é decidido sem ação explícita de uma pessoa.

## Assumptions

- O externo é uma decisão sobre o host, não uma entidade com identidade própria (um "cadastro de serviço externo" com vários hosts fica para depois, se precisar).
- O nome do externo é texto livre, só para leitura humana (e para o nó do mapa no futuro).

## Design notes

- Tabela própria porque `host_links.app_id` é obrigatório com FK (afrouxar exigiria recriar a tabela no SQLite). Ver o adendo da 019 no ADR-011.
- O contrato da 017 foi preservado (aditivo): `destinations` igual; `externals` e `link.external` são campos novos.
