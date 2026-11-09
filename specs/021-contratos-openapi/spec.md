# Spec 021: Contratos OpenAPI (catálogo de endpoints e mudanças entre commits)

**Status**: Implementada (2026-11-05); lida no GitHub real e confirmada pelo usuário na tela em 2026-11-08 (ajustes visuais ficam para depois, no backlog)
**Criada em**: 2026-11-05
**Origem**: pedido do usuário (2026-11-02 a 2026-11-05), a partir da análise de massas e contratos de testes integrados. Hoje, achar o contrato atual de um endpoint exige procurar em repositórios, wiki e coleções, e uma mudança de contrato de outro time costuma aparecer só quando algo falha em homologação. É a base da futura "ficha do endpoint" (contrato, massa e payload), que não entra aqui.

## Escopo e fronteira

**Faz parte**

1. **Refatoração prévia**: cada tipo de fato da análise passa a ter processador e escritor próprios, para um tipo novo não mexer no núcleo (OCP). Sem mudança de comportamento.
2. **Scanner OpenAPI**: lê contratos OpenAPI 3.x e Swagger 2.0, em YAML ou JSON, nos repositórios já catalogados, e grava endpoints e campos como fatos da análise.
3. **Catálogo**: seção **Contratos** no detalhe do app.
4. **Mudanças**: o que mudou desde o commit analisado anteriormente, classificado em quebra, atenção e segura. Calculado na leitura.

**Não faz parte** (cada item é candidato a spec própria)

- Massas, payload de exemplo e exportação (curl, `.http`, coleções) e jornadas.
- Ler o contrato da API em execução e comparar declarado × implementado.
- Comparar contratos com stubs de teste; alertas; tela com todas as quebras; item de menu com busca global.
- Dizer quais apps consomem cada **rota** (depende da spec 015).
- Qualquer uso de IA. Disparar a análise pela tela (continua só por `tools/CheckApps.Analyze`).

## User Scenarios & Testing *(obrigatório)*

### User Story 1 — Registrar os contratos de um repositório (Prioridade: P1)

Como pessoa que mantém o catálogo, rodo a análise e o CheckApps registra os endpoints e campos declarados nos contratos de cada repositório. Rodar de novo no mesmo commit não duplica nada.

**Por que esta prioridade**: sem os fatos nada mais existe, e é aqui que se prova que os arquivos reais podem ser lidos.

**Teste independente**: com `--source fake`, rodar a ferramenta e conferir as tabelas, sem API nem tela.

**Cenários de aceite**:

1. **Dado** um `openapi.yaml` 3.0 com `POST /simulacoes`, **Quando** rodo a análise, **Então** é gravado um endpoint com método, rota, identificador declarado, tags, esquemas de segurança, arquivo de origem e confiança **alta**, com os campos do pedido e da resposta.
2. **Dado** um Swagger 2.0 em JSON com o mesmo conteúdo, **Então** os endpoints e campos gravados são equivalentes.
3. **Dado** um YAML qualquer sem `openapi` nem `swagger` na raiz, **Então** nada é gravado e uma **nota** conta os arquivos ignorados; a análise não fica parcial.
4. **Dado** um contrato inválido, **Então** há um aviso com o caminho (nunca o conteúdo), a análise fica **parcial** e os outros arquivos seguem.
5. **Dado** um `$ref` para outro arquivo, **Então** nada é buscado fora, há um aviso e a análise fica parcial.
6. **Dado** esquema com ciclo, **Então** a leitura termina, os campos até o ciclo são gravados e há uma **nota**: nada se perdeu, então a análise não fica parcial.
7. **Dado** `example`, `default`, `description`, `servers` ou extensões `x-amazon-apigateway-*` com segredos plantados, **Então** nenhum desses valores aparece no banco, em aviso, em nota ou na saída da ferramenta.
8. **Dado** um repositório já analisado no mesmo commit, **Então** nada é gravado; com `--dry-run` nada é gravado nunca.

### User Story 2 — Consultar o contrato de um app (Prioridade: P1)

Como dev ou QA, abro o app e vejo os endpoints dos seus contratos, com os campos de cada um e de onde vieram, sem procurar em repositórios.

**Por que esta prioridade**: é a dor principal: achar o contrato atual.

**Teste independente**: com os fatos da história 1, abrir `/apps/:id` e usar a seção **Contratos**.

**Cenários de aceite**:

1. **Dado** um app com contrato analisado, **Quando** abro a seção, **Então** vejo os endpoints por repositório e tag, com método, rota, resumo, segurança, se é repositório de contrato ou da aplicação, commit e data da análise.
2. **Dado** um endpoint, **Quando** o expando, **Então** vejo seus campos: lugar, status, caminho, tipo, se é obrigatório, enum e nota de confiança, quando houver.
3. **Dado** um texto no filtro, **Então** só ficam os endpoints cuja rota contém o texto, sem diferença de caixa.
4. **Dado** repositório sem análise ou sem contrato, **Então** a seção diz isso em texto.
5. **Dado** a seção fechada, **Então** a linha resume endpoints e mudanças que quebram, e o estado aberto ou fechado é lembrado.

### User Story 3 — Saber o que mudou no contrato (Prioridade: P2)

Como dev, QA ou tech lead, vejo o que mudou no contrato desde o commit analisado anteriormente e o que pode quebrar quem o consome, antes de descobrir em homologação.

**Por que esta prioridade**: antecipa o problema, mas depende do catálogo.

**Teste independente**: analisar `--fake-revision 0` e depois `--fake-revision 1`, e conferir `GET /api/apps/{id}/contracts/changes`.

**Cenários de aceite**:

1. **Dado** um campo opcional do pedido que virou obrigatório, **Então** a mudança aparece como **quebra**, com o commit anterior e o atual.
2. **Dado** cada linha da tabela de classificação (ver Design notes), **Então** a classificação é a da tabela.
3. **Dado** um parâmetro de rota renomeado (`{id}` → `{fundoId}`), **Então** o endpoint e o campo são os mesmos: parâmetro de rota é identificado pela posição, e nenhuma mudança aparece.
4. **Dado** só uma análise com endpoints, **Então** a tela diz "primeira análise do contrato", sem listar tudo como novo.
5. **Dado** `--force` no mesmo commit, **Então** a base da comparação continua sendo o último commit **diferente**.
6. **Dado** uma das análises **parcial**, **Então** o que sumiu aparece como **atenção**, com a explicação, nunca como quebra.
7. **Dado** versões diferentes do scanner nas duas análises, **Então** a comparação traz um aviso.

### Edge Cases

- **Mesmo método e rota em mais de um arquivo** (versões da API, por exemplo): o arquivo entra na chave da comparação e há aviso.
- **Contrato maior que o limite de leitura** (512 KB): aviso e análise parcial, como nas specs 013 e 014.
- **`oneOf`/`anyOf`**: lista os campos de cada variante, com confiança média e nota. **`allOf`**: une os campos.
- **Profundidade acima de 12 ou mais de 2.000 campos por endpoint**: para de descer e registra aviso.
- **Campo renomeado**: aparece como remoção e criação. Não se tenta adivinhar.
- **Pastas `bin/`, `obj/`, `node_modules/`, `.git/`, e `appsettings*`, `package*.json`, `tsconfig*`**: nunca são candidatos.
- **Repositório removido do catálogo**: os endpoints acompanham a análise (cascata).
- **Análise feita antes desta spec**: não tem endpoints e não serve de base. O primeiro uso pede `--force`.

## Requirements *(obrigatório)*

### Functional Requirements

- **FR-001**: Um tipo novo de fato NÃO DEVE exigir mudança em `AnalysisService` nem no método de gravação de `AnalysisRepository`: basta uma saída de scanner, um processador, um escritor e o registro deles. Tipo de fato sem escritor DEVE lançar exceção e desfazer a transação.
- **FR-002**: A refatoração NÃO DEVE mudar comportamento: os testes existentes passam sem alterar asserções.
- **FR-003**: O scanner DEVE ser parser puro: sem rede, relógio ou disco, sem buscar `$ref` externo e com limites de profundidade e de campos.
- **FR-004**: Os candidatos DEVEM vir de padrões configuráveis (`Analysis:OpenApiFilePatterns`) e ser confirmados pelo conteúdo (raiz com `openapi` ou `swagger`).
- **FR-005**: Cada endpoint DEVE guardar: mecanismo (`openapi`), método, rota, chave da rota, identificador declarado, resumo (truncado), tags, esquemas de segurança (só nome e tipo), `deprecated`, título e versão da API, arquivo de origem, origem `PARSER`, confiança com nota, scanner e versão.
- **FR-006**: Cada campo DEVE guardar: lugar (`path`, `query`, `header`, `cookie`, `pedido`, `resposta`), status HTTP, tipo de mídia, caminho, tipo, formato, obrigatório, valores de enum e nota de confiança.
- **FR-007**: O sistema NÃO DEVE gravar `example`, `default`, `description`, `servers`, valores de segurança nem extensões `x-*`, e há teste com segredos plantados que prova isso.
- **FR-008**: Repositório "de contrato" ou "da aplicação" DEVE ser derivado na leitura do papel do repositório, sem coluna nova. A tela não afirma "gateway".
- **FR-009**: `GET /api/apps/{appId}/contracts` DEVE devolver, por repositório, a análise corrente, os endpoints e os campos; 404 se o app não existe.
- **FR-010**: `GET /api/apps/{appId}/contracts/changes` DEVE devolver, por repositório, os commits comparados, os avisos e as mudanças classificadas; 404 se o app não existe.
- **FR-011**: A base da comparação DEVE ser a análise não falhada mais recente do repositório, de commit diferente do corrente, que tenha endpoints. A comparação é calculada na leitura e não é gravada.
- **FR-012**: A API de leitura NÃO DEVE disparar análise nem exigir credencial do provedor.
- **FR-013**: `CheckApps.Analyze` DEVE mostrar a contagem de endpoints e aceitar `--fake-revision <n>` (só com a fonte fake), para demonstrar uma mudança de ponta a ponta.
- **FR-014**: As tabelas novas DEVEM entrar no `schema.sql`, com SQL portável e teste sobre banco antigo.
- **FR-015**: A classificação DEVE aparecer **em texto** na tela, nunca só por cor.

### Key Entities

- **Endpoint declarado** (`repository_api_endpoints`): um método e uma rota declarados num contrato, numa análise. "Endpoint", nunca "operação": **Operação** é termo de negócio. **Identidade**: id (guid) + (análise, arquivo, método, rota). **Relações**: pertence a uma `RepositoryAnalysis` (cascata) → `Repository` → `App` → squad. **Proveniência**: `origin = PARSER`, scanner, versão, arquivo e confiança. **Tempo**: o da análise; cada commit analisado é uma foto.
- **Campo declarado** (`repository_api_fields`): um campo achatado de um endpoint (`cliente.contas[].numero`). **Identidade**: id (guid) + (endpoint, lugar, status, mídia, caminho). **Relações**: pertence a um endpoint (cascata). **Proveniência e tempo**: os do endpoint.
- **Mudança de contrato** *(derivada, não persistida)*: diferença entre duas fotos, com classificação, calculada por função pura.

## Success Criteria *(obrigatório)*

- **SC-001**: Em até 30 segundos após a análise, uma pessoa acha um endpoint de um app e vê seus campos, sem abrir o repositório.
- **SC-002**: Em até 30 segundos, uma pessoa descobre o que mudou no contrato desde o commit anterior e o que quebra.
- **SC-003**: Rodar a análise duas vezes sobre o mesmo commit não cria nenhum registro novo.
- **SC-004**: Nenhum segredo plantado num contrato aparece no banco, em log ou em mensagem.
- **SC-005**: Uma análise parcial nunca faz um endpoint que não pôde ser lido aparecer como quebra.
- **SC-006**: Um tipo novo de fato não exige alterar o serviço de análise.

## Assumptions

- A análise lê o **ramo padrão** e só repositórios **catalogados**, como nas specs 013 e 014. O repositório de contrato é catalogado sob o app, com o papel `contrato`, que já existe.
- O contrato pode estar desatualizado em relação à aplicação (informado pelo usuário). Esta spec registra o que está **declarado** e não afirma o que está implementado; `mechanism` deixa espaço para comparar os dois depois.
- O usuário ainda não tem um contrato real à mão: as fixtures são **inventadas** e serão ajustadas com um exemplo real. Isso inclui os padrões de nome de arquivo.
- Valores reais de hosts e nomes da empresa não entram em arquivo versionado; fixtures e exemplos usam `exemplo.com`.

## Decisões do usuário (2026-11-05)

1. **Extensibilidade**: refatorar os tipos de fato antes do scanner.
2. **Escopo**: catálogo + mudanças, sem item de menu com busca global.

## Design notes *(opcional)*

**Parser**: `Microsoft.OpenApi`, na mesma versão da API, cobre Swagger 2.0 e OpenAPI 3.x e resolve `$ref`. YAML é convertido para JSON com o YamlDotNet, que já é dependência. A confirmar por protótipo no início do incremento 2.

**Classificação das mudanças**:

| Mudança | Pedido / parâmetro | Resposta |
|---|---|---|
| Endpoint removido | quebra | quebra |
| Campo novo obrigatório | quebra | segura |
| Campo novo opcional | segura | segura |
| Opcional → obrigatório | quebra | segura |
| Obrigatório → opcional | segura | atenção |
| Campo removido | atenção | quebra |
| Tipo ou formato alterado | quebra | quebra |
| Valor de enum removido | quebra | segura |
| Valor de enum novo | segura | atenção |
| Endpoint novo | segura | segura |
| Marcado como `deprecated` | atenção | atenção |

**Identidade na comparação**: endpoint = método + rota com parâmetros neutralizados; campo = lugar + status + mídia + caminho; parâmetro de rota = sua posição na rota.

**Padrões de arquivo sem `*api*`**: o plano previa esse padrão, mas ele pegaria todo projeto `Nome.Api/`. Um repositório sem contrato passaria a contar como coberto por um scanner, o que mascararia a cobertura do mapa (spec 020). Ficou de fora, e os padrões são configuráveis.

**Por que não gravar a comparação**: se a regra melhorar, ela vale para o histórico sem migração. Gravar só passa a compensar com alerta ou tela de todas as quebras, e dá para gerar depois a partir das fotos.
