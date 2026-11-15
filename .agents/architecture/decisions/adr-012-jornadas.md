# ADR-012: Jornadas em árvore, passo por identidade estável e impacto derivado

**Status**: Aceita · **Data**: 2026-11-09

## Contexto

A spec 021 registra os contratos e o que mudou neles, mas não liga isso ao que o negócio faz com eles. É preciso um cadastro de **jornadas** (fluxos vistos pelo usuário ou pelo negócio) que atravessam endpoints. O vocabulário varia entre squads: num exemplo real há três níveis (plataforma, sistema, jornada), em outros um ou dois, e "produto" e "operação" dizem quase a mesma coisa que "jornada". Quatro decisões são caras de mudar depois:

1. **Estrutura**: tipos fixos (Produto → Operação → Jornada) × uma estrutura genérica.
2. **O que o passo referencia**: o id do endpoint da análise × uma identidade estável.
3. **Onde mora o impacto de uma mudança de contrato**: gravado × derivado.
4. **Vocabulário**: enum de níveis × texto livre.

## Decisão

- **Uma entidade em árvore, com pai opcional.** Produto e Operação **não** são entidades. O tipo de produto, quando existe, é uma **tag** opcional (classificação para pessoas), nunca estrutura nem campo obrigatório. A hierarquia é **campo**, porque o sistema navega por ela.
- **Nível em texto livre** (`plataforma`, `sistema`, `jornada`…), sem enum, como o papel do repositório. Começa assim **para avaliar com o uso**; se as squads divergirem demais, vira uma lista sugerida no glossário ou um enum. A tela sugere os três nomes sem obrigar.
- **Passo = identidade estável**: `app_id` + método + chave da rota (a mesma da comparação de contratos, `ApiPath.KeyOf`), nunca o id de um endpoint de análise, que é uma foto e muda a cada reanálise. O passo sobrevive a reanálises e a renomeação de parâmetro de rota.
- **Passos só em jornadas-folha**; plataforma e sistema agrupam. Um nó com filhos não tem passos e um nó com passos não tem filhos. Os apps de um sistema são **derivados** dos passos da subárvore.
- **Resolução e impacto são derivados na leitura, nunca gravados**, no mesmo espírito do `QualitySnapshot` e da comparação de contratos: uma regra melhor vale para o histórico sem migração. A resolução (`catalogado`, `fora-do-contrato`, `app-sem-contrato`) olha o contrato corrente do app; o impacto cruza os passos com as mudanças da spec 021 por (método, chave da rota).
- **A árvore é montada em memória**, sem SQL recursivo, por portabilidade para Postgres (ADR-009).
- **Guardas nos cadastros vizinhos**: remover um app usado em passo, ou uma squad dona de jornada, devolve 409, como já acontece com apps em checagens.
- **O serviço de jornadas lê contratos por `IContractReader`** (implementada pelo `ContractService`), para a regra não depender dos detalhes da análise.
- **Sem vocabulário nem id de cadastros de outros times** na primeira versão (decisão do usuário).

## Consequências

- Squads sem produto nem operação usam o modelo sem nenhum campo vazio obrigatório; squads com três níveis também.
- **Riscos assumidos**: o nível em texto livre vai divergir entre squads; passo só em folha pode ser restritivo; uma jornada compartilhada por dois sistemas não cabe numa árvore (viraria grafo). Os três estão em `backlog.md` como premissas a validar.
- O impacto depende das análises dos contratos estarem em dia: um app sem análise aparece como "sem comparação", nunca como "sem impacto".
- Uma mudança futura de identidade de endpoint (por exemplo, a spec 015 passar a ler rotas do código) tem de preservar a chave (método + rota neutralizada), senão invalida os passos.
- **Fora desta decisão**: massas, payload e exportação (a ficha do endpoint usará os passos), a leitura por app ("em quais jornadas este app entra"), jornada em dois pais e dono por squad com autorização.
