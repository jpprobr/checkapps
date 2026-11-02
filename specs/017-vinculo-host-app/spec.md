# Spec 017: Vínculo host→app (fatia 2a das integrações, backend)

**Status**: Implementada e confirmada (2026-11-02): o usuário confirmou o contrato dos endpoints
**Criada em**: 2026-11-02
**Origem**: fatia 2 das integrações (spec 014, `STATE.md`): dizer a que app do catálogo cada host pertence, por decisão humana. Esta spec é a parte **backend (2a)**; a tela de confirmação é a 2b e o mapa de aplicações vem depois. Decisões do usuário em 2026-11-02: vínculos feitos só por pessoa (sem sugestão automática por nome), um vínculo **por host** (não por seção), `{env}` = ambiente com os valores padrão `dev`, `hom`, `prod` (configuráveis).

> Formato compatível com o template de spec do Spec-Kit (github/spec-kit).

## Escopo e fronteira

**Faz parte**

1. Guardar o **vínculo** "o host `X` é o app `Y`" (Y **recebe** a chamada), com estado `confirmado` ou `rejeitado`, feito por pessoa.
2. **Casar** um host concreto (`empresa.dev.exemplo.com`) com um vínculo declarado sobre o **modelo** (`empresa.{env}.exemplo.com`), substituindo `{env}` pelos valores configurados (`dev`, `hom`, `prod`).
3. API para criar, trocar, remover e listar vínculos, e para ver o vínculo efetivo de cada host e de cada integração de um app.

**Não faz parte**

- Tela de confirmação (2b) e mapa de aplicações.
- Sugestão automática (por nome, sigla ou IA): o estado `sugerido` fica reservado, não é produzido.
- Vínculo por seção de configuração ou por caminho base (limite conhecido: gateway com vários serviços atrás do mesmo host).
- Resolver o host por consulta de DNS ou por qualquer fonte externa.

## User Scenarios & Testing *(obrigatório)*

### User Story 1 — Dizer que um host é um app (Prioridade: P1)

Uma pessoa vê que vários apps chamam `empresa.{env}.exemplo.com` e registra que esse host é o app "Pagamentos". Dali em diante, todo host concreto que o modelo cobre (`empresa.dev…`, `empresa.hom…`, `empresa.prod…`) aparece ligado ao app.

**Por que esta prioridade**: sem o vínculo não há quem-chama-quem, e o mapa não existe.

**Teste independente**: `PUT` do vínculo e `GET` da lista de hosts mostrando o host ligado.

**Cenários de aceite**:

1. **Dado** o host `empresa.{env}.exemplo.com` sem vínculo, **Quando** a pessoa confirma o app Pagamentos, **Então** o host aparece com o vínculo `confirmado`.
2. **Dado** o vínculo no modelo, **Quando** a lista traz `empresa.dev.exemplo.com`, **Então** ele aparece ligado ao app Pagamentos, com a indicação de que veio **do modelo**.
3. **Dado** um vínculo `confirmado` no modelo e um vínculo `confirmado` exato no host `empresa.dev.exemplo.com` para outro app, **Então** vale o exato.
4. **Dado** um host já `confirmado` para um app, **Quando** a pessoa tenta confirmar outro app para o mesmo host, **Então** a API recusa (409); a pessoa remove o anterior primeiro.

### User Story 2 — Dizer que um host NÃO é um app (Prioridade: P2)

A pessoa descarta uma hipótese ("`gateway.exemplo.com` não é o app X") e isso fica registrado, sem apagar o host da lista.

**Cenários de aceite**:

1. **Dado** um host, **Quando** a pessoa rejeita o app A e depois o app B, **Então** os dois rejeitos ficam registrados.
2. **Dado** um rejeito, **Quando** a pessoa confirma o mesmo app, **Então** o estado vira `confirmado` (o par host+app é um só registro).

### User Story 3 — Ver o destino de cada integração de um app (Prioridade: P2)

No detalhe do app, cada integração mostra para qual app ela vai (quando confirmado) ou "sem app".

**Cenários de aceite**:

1. **Dado** o app com integração para `empresa.dev.exemplo.com` e o vínculo no modelo, **Quando** a API devolve as integrações do app, **Então** a integração traz o app destino.
2. **Dado** uma integração não resolvida (sem host), **Então** não há destino e nada quebra.

### Edge Cases

- **Chave inválida** (com esquema `https://`, caminho, espaço, porta não numérica, vazia): 400.
- **App inexistente**: 404. **Vínculo inexistente ao remover**: 404.
- **Host que nenhuma análise corrente chama** (vínculo antecipado, ou análise que deixou de ver o host): o vínculo é aceito e aparece na lista de vínculos como **não visto na última análise** (derivado, nunca gravado).
- **Mesma porta**: `a.{env}.x.com:8080` só cobre `a.dev.x.com:8080`.
- **Caixa**: a chave é guardada e comparada em minúsculas.
- **Valor fora da lista**: `empresa.qa.exemplo.com` **não** casa com o modelo (nunca por palpite), a menos que `qa` esteja em `Analysis:EnvPlaceholderValues`.
- **Host que é modelo**: casa só por igualdade com o vínculo de mesmo modelo.
- **App removido**: os vínculos dele somem (cascata).
- **Reanálise**: não altera nem apaga vínculos (a tabela é independente das análises).

## Requirements *(obrigatório)*

### Functional Requirements

- **FR-001**: O sistema DEVE guardar vínculos em tabela própria `host_links` (id, `host_key`, `app_id`, `state`, `origin = HUMAN`, `decided_at`, `note`), independente das análises, com `UNIQUE (host_key, app_id)` e remoção em cascata com o app.
- **FR-002**: O sistema DEVE permitir no máximo um vínculo `confirmado` por `host_key`; confirmar outro app para o mesmo host é conflito (409). Vários `rejeitado` são permitidos.
- **FR-003**: O casamento DEVE ser uma função pura: exato primeiro; depois modelo com `{env}` preenchido por cada valor de `Analysis:EnvPlaceholderValues` (padrão `dev`, `hom`, `prod`; a configuração **substitui** o padrão), com a mesma porta, sem diferença de caixa.
- **FR-004**: `PUT /api/integrations/hosts/{host}/link` DEVE criar ou trocar o estado do par host+app (corpo `appId`, `state` = `confirmado` | `rejeitado`, `note` opcional); 400 para chave ou estado inválidos, 404 para app inexistente, 409 para segundo confirmado.
- **FR-005**: `DELETE /api/integrations/hosts/{host}/link/{appId}` DEVE remover o vínculo (204; 404 se não existe).
- **FR-006**: `GET /api/integrations/links` DEVE listar todos os vínculos com o nome do app e a indicação `seen` (alguma integração corrente casa com ele).
- **FR-007**: `GET /api/integrations/hosts` e `GET /api/integrations/hosts/{host}` DEVEM trazer o vínculo efetivo do host (estado, app, `via` = `exact` | `template`, modelo usado). A lista aceita `linkStatus` opcional (`sem-vinculo` | `confirmado` | `rejeitado`; inválido → 400).
- **FR-008**: `GET /api/apps/{appId}/integrations` DEVE anotar cada integração com o app destino quando houver vínculo `confirmado`.
- **FR-009**: Consultas DEVEM usar SQL portável (`backend.md`); a tabela nova entra no `schema.sql` com teste em `SchemaEvolutionTests` sobre banco antigo.
- **FR-010**: Nenhum teste usa rede; nada nesta spec dispara análise.

### Key Entities

- **HostLink** (`host_links`): decisão humana sobre um host. **Identidade**: id guid; par (`host_key`, `app_id`) único. **Relações**: `app_id` → `apps` (o app que **recebe** a chamada); o host é uma chave de texto que casa com `repository_integrations.host[:port]`, sem FK de propósito (o vínculo precede e sobrevive às análises). **Proveniência**: `origin = HUMAN`. **Tempo**: `decided_at` (sobrescreve ao trocar o estado).

## Success Criteria *(obrigatório)*

- **SC-001**: Com um único vínculo no modelo `{env}`, os três ambientes (dev, hom, prod) do mesmo serviço aparecem ligados ao app, sem três cadastros.
- **SC-002**: Reanalisar os repositórios não perde nenhum vínculo.
- **SC-003**: Nenhum host é ligado a um app sem decisão de uma pessoa.

## Assumptions

- O app que **recebe** a chamada já existe no catálogo; host de serviço fora do catálogo continua sem vínculo.
- Um host é um serviço (um app). O gateway com vários serviços atrás do mesmo host não se expressa e fica registrado como limite no ADR-011.
- Os valores de `{env}` são `dev`, `hom`, `prod` (decisão de 2026-10-27); outros nomes entram por configuração.

## Design notes

- Tabela própria e fora das análises porque decisão humana tem ciclo de vida diferente de fato lido (ADR-011, adendo).
- O estado `sugerido` existe na lista de estados do domínio para a evolução (sugestão por nome ou por IA, com `origin` e confiança), mas nada o produz nesta spec.
