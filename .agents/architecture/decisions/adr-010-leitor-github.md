# ADR-010: Leitor de GitHub — porta provider-neutra, busca por sigla e token somente leitura

**Status**: Aceita · **Data**: 2026-10-12

## Contexto

A spec 005 introduz o primeiro conector real do produto (o Sonar ainda é mock). Cinco decisões afetam segurança, custo e o que acontece quando o ambiente for o corporativo:

1. **De onde o scan lê**: o GitHub corporativo não é acessível do ambiente de desenvolvimento.
2. **Como se limita o escopo**: a organização corporativa é grande e organizada por siglas; varrer tudo é lento, esbarra em limite de chamadas e expõe repositórios que não são da área da pessoa.
3. **Com que credencial**: o caminho correto no corporativo (GitHub App) tende a passar por aprovação de segurança; uma credencial pessoal prova valor mais cedo.
4. **O que o scan grava**: repositório não é App, e a remoção guardada (ADR-005) torna permanente um app criado por engano.
5. **Como ler a sigla-app**: ela é gerada pela esteira corporativa e fica num arquivo YAML do repositório.

## Decisão

- **Porta provider-neutra.** `IRepositoryDiscoverySource` devolve um tipo nosso (`DiscoveredRepositoryInfo`), nunca o JSON do GitHub — mesmo molde de `ISonarProvider`. Duas implementações: `FakeRepositorySource` (testes e demonstração, com os casos difíceis) e `GitHubRepositorySource`. Trocar para o corporativo é configuração (organização, token, caminho do YAML, convenção de nome).
- **Escopo por sigla, nunca a organização.** O scan recebe siglas validadas por formato e usa a **Search API** (`org:{org} {sigla} in:name`), filtrando no código o prefixo exato da convenção configurada. Um **limite de resultados** (padrão 50), medido pelo `total_count` da busca, recusa o scan antes de ler dados em volume. A listagem da organização foi descartada: ler tudo para filtrar localmente contradiz o objetivo. Custo aceito: atraso de indexação para repositórios recém-criados.
- **Token fine-grained somente leitura** (`Metadata` e `Contents`: leitura) em configuração local (`GitHub:Token` via `dotnet user-secrets` ou variável de ambiente). Nunca em arquivo versionado, log ou mensagem de erro. **No corporativo, o alvo é um GitHub App** instalado na organização (permissão fina, não amarrado a uma pessoa); a porta não muda.
- **O scan propõe, a pessoa decide.** Descobertos vão para uma tabela própria; só a confirmação humana cria App ou Repository. Todo dado do scan nasce com `source = GITHUB_SCAN`. O estado "já no catálogo" é derivado da existência de `Repository`, não de um status duplicado. Sugestões (papel, nome, confiança) são calculadas na leitura e nunca armazenadas.
- **Sigla-app lida do YAML do repositório**, com caminho e chave configuráveis. Ausência = sem sigla-app, o que é sinal útil (repositórios de infra não têm).
- **Primeiro acesso ao corporativo em `--dry-run`**: lê e mostra, não grava.

## Consequências

- O desenvolvimento avança sem acesso ao ambiente corporativo, e o que se valida (fluxo, triagem, convenção) vale para ele.
- Nenhum teste automatizado usa rede: o cliente HTTP é testado com `HttpMessageHandler` falso.
- O formato real do YAML e a convenção de nome do corporativo ainda não são conhecidos; ficam isolados em configuração e na função de convenção, que é pura e testável.
- Um token pessoal é amarrado a uma pessoa e expira. É aceitável no laboratório; **não** deve ser a credencial de qualquer uso compartilhado.
- Esta ADR não cobre as fases 2 e 3 (dependências de biblioteca e entre apps), que podem exigir leitura de mais arquivos e, na fase 3, uma LLM aprovada pela empresa — decisão própria.
