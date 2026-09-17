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

- Adicionar um access pattern novo é uma decisão de modelagem, não um `WHERE` novo. Se um padrão não couber nas chaves atuais, é um ADR novo (possivelmente um GSI novo), não um `Scan`. Todo padrão é registrado em `../data-model/access-patterns.md`, com o fit de cada um.
- Listar squads usa uma partição única (`GSI1PK = "SQUAD"`) — funciona para dezenas, não para milhares de squads. Limitação aceita na escala do MVP. A spec 003 repete o padrão para checagens (`"CHECK"`).
- A evolução agregada por squad é calculada em memória a cada request (busca apps + histórico de cada um). Simples agora; pré-agregar só se virar gargalo — não antes.
- Se o estágio **Context** do roadmap exigir buscas livres (full-text, joins entre muitas entidades), este design vai apertar. Nesse momento, avaliar um banco complementar em vez de forçar o DynamoDB.

### Avaliação de 2026-09-17 e gatilhos de troca

O DynamoDB atende bem tudo que é **uma entidade e seus filhos** (coleções por partição, série temporal de snapshots, escritas condicionais) e mal tudo que **cruza entidades** (joins, agregação transversal, filtro por atributo de outra entidade), que ele empurra para memória ou para um GSI novo. O MVP e a v1 de Checagens ficam do lado bom dessa linha; as evoluções naturais de Checagens e o estágio Context ficam do outro. O volume real (dezenas de apps, milhares de linhas) não exercita as forças do DynamoDB e paga todos os seus custos — a escolha se sustenta pelo objetivo de laboratório (ADR-001), não pela forma dos dados.

A troca não é beco sem saída: `Domain`, `Application`, `Api` e `frontend` não conhecem o banco; migrar é reescrever os repositórios em `Infrastructure/DynamoDb`, o bootstrapper e os testes de integração. Destino provável: Postgres (ou SQLite no laboratório, que também elimina o reseed a cada reinício).

**Revisitar esta decisão quando dois destes ocorrerem:**

1. Um access pattern obrigatório exigir `Scan` ou um terceiro GSI.
2. Uma segunda desnormalização se provar desatualizada na prática (a primeira é o estado de qualidade no App; a candidata é `appName` nos itens de checagem).
3. O primeiro pedido real de relatório transversal — por squad entre checagens, painel de todas as checagens, "apps que não estão em X".
4. Uma entidade do estágio Context (solução, topologia, dependência entre apps) virar requisito de verdade, não tag.
