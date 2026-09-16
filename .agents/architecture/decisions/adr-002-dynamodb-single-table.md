# ADR-002: DynamoDB em tabela única, desenhada a partir dos access patterns

**Status**: Aceita · **Data**: 2026-09-13

## Contexto

O brief pedia explicitamente: não transformar entidades de domínio em tabelas; analisar os access patterns antes de criar a tabela; não criar GSI por precaução. Os 14 access patterns do MVP estão em `specs/001-mvp-core/`.

## Decisão

Uma tabela (`CheckApps`), chave `PK`+`SK`, dois GSIs:

- **GSI1** resolve "listar squads" e "listar apps de uma squad" (dois padrões, um índice, reaproveitando `GSI1PK` com espaços de valor distintos: `SQUAD` vs `SQUAD#{id}`).
- **GSI2** resolve "apps por Quality Level" — esparso, só no item App.

O estado atual de qualidade é **desnormalizado no item App** para que "situação de todos os apps da squad" seja uma única `Query`, mantido em sincronia por uma transação na escrita do snapshot. Detalhes em `../data-model/dynamodb-single-table.md`.

Cliente: AWS SDK de baixo nível (`AttributeValue`), **não** `DynamoDBContext` — o modelo de objeto do SDK briga com single-table e esconde as chaves, que é justamente o que queremos ver.

## Consequências

- Adicionar um access pattern novo é uma decisão de modelagem, não um `WHERE` novo. Se um padrão não couber nas chaves atuais, é um ADR novo (possivelmente um GSI novo), não um `Scan`.
- Listar squads usa uma partição única (`GSI1PK = "SQUAD"`) — funciona para dezenas, não para milhares de squads. Limitação aceita na escala do MVP.
- A evolução agregada por squad é calculada em memória a cada request (busca apps + histórico de cada um). Simples agora; pré-agregar só se virar gargalo — não antes.
- Se o estágio **Context** do roadmap exigir buscas livres (full-text, joins entre muitas entidades), este design vai apertar. Nesse momento, avaliar um banco complementar em vez de forçar o DynamoDB.
