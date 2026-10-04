# ADR-009: Sair do DynamoDB — SQLite no MVP, Postgres quando o modelo estabilizar

**Status**: Aceita · **Data**: 2026-10-04 · **Substitui**: ADR-002 · **Revisa parcialmente**: ADR-001, ADR-004

## Contexto

O ADR-002 escolheu DynamoDB em tabela única, com o aprendizado de modelagem access-pattern-first como objetivo declarado (ADR-001). A decisão foi correta para o que se sabia então. Quatro coisas mudaram desde:

1. **A avaliação de fit** (registrada no ADR-002 em 2026-09-17 e detalhada em `../retrospectives/2026-10-dynamodb-single-table.md`): o DynamoDB atende bem "uma entidade e seus filhos" e mal tudo que cruza entidades. O MVP ficou do lado bom dessa linha; a spec 003 (Checagens) e o estágio Context ficam do outro.
2. **O destino de deploy ficou conhecido**: ECS com instância única, ambiente dev/hom, poucas squads, por um período longo até provar valor. Escala definitivamente pequena — as forças do DynamoDB nunca serão exercitadas, e seus custos são pagos integralmente.
3. **O processo corporativo para RDS é incompatível com o método do projeto**: exige desenhar o modelo completo numa ferramenta corporativa, com avaliação de duas pessoas, antes de gerar o script de criação — e repetir o ciclo a cada alteração. O projeto tem pelo menos quatro mudanças de modelo já previstas (Check/CheckItem, `solutionId`/`appType`, `Repository.kind`, Solution como entidade). Isso levaria o custo de adicionar um campo de minutos para dias, e levaria a agrupar mudanças para evitar a burocracia — destruindo a disciplina de incrementos pequenos e verificáveis.
4. **O aprendizado de DynamoDB foi essencialmente concluído**: single-table, GSIs com múltiplos espaços de valor, GSI esparso, transação condicional, escrita condicional para unicidade, série temporal em SK, desnormalização com invariante, delete de partição em lote. O ganho marginal de continuar é baixo.

## Decisão

**Migrar para SQLite agora; Postgres quando o modelo estabilizar.**

- **Acesso a dados**: Dapper + SQL explícito, não ORM com migrations geradas. Mantém o gosto do projeto por ver o que acontece (mesmo motivo de ter escolhido o SDK de baixo nível no DynamoDB) e produz um DDL limpo — que é exatamente o que o processo corporativo vai pedir na migração para o Postgres.
- **Schema**: `schema.sql` consolidado e idempotente para bootstrap; scripts numerados de migration a partir do primeiro deploy com dados reais.
- **Portabilidade deliberada**: nada de função específica do SQLite nas queries; tipos declarados explicitamente; datas em ISO-8601 texto e ids gerados na aplicação (já é o caso hoje) — as duas maiores fricções de SQLite→Postgres não se aplicam.
- **Fim da desnormalização do estado de qualidade no App**: em SQL, "app com seu snapshot mais recente" é um join. Isso revoga a cláusula de desnormalização do ADR-004 (as outras cláusulas — snapshot imutável, nível congelado na escrita com a versão da regra — continuam valendo e são o coração do produto).
- **Produção (quando houver)**: Litestream replicando o WAL para S3 com restore no boot — sem volume persistente — e deploy ECS configurado como stop-then-start (`minimumHealthyPercent=0`, `maximumPercent=100`, `desiredCount=1`) para garantir escritor único.

Postgres entra quando o modelo tiver estabilizado por uso real. Nesse momento, submete-se ao processo corporativo **um modelo validado, uma vez** — em vez de um palpite, N vezes. A migração SQLite→Postgres é mecânica (ambos SQL, `pgloader` existe).

## Consequências

**Ganhos imediatos**

- Consultas que cruzam entidades voltam a ser triviais: a evolução agregada da squad vira `GROUP BY` em vez de N+1; "apps por Quality Level" vira `WHERE` (o GSI2 deixa de existir); os ❌ do registro de access patterns (relatório transversal, checagem por regra, apps sem snapshot) deixam de ser impossíveis.
- Somem `Keys.cs`, `TableSchema.cs`, `AttributeValueExtensions.cs`, `QualityMetricsMapper.cs` e as partições únicas (`"SQUAD"`, `"CHECK"`).
- Some a invariante do estado desnormalizado — e com ela a classe de bug que o `SaveAsync` causou.
- Testes de integração deixam de precisar de Docker e da `[Collection]` compartilhada: cada teste pode ter seu próprio arquivo temporário.
- O ciclo de desenvolvimento perde o reseed a cada reinício do DynamoDB Local.

**Custos assumidos**

- Reescrita de ~814 linhas de infraestrutura e ~349 de testes de integração (≈30% do código; o terço mais mecânico). Domain, Application, Api e frontend não mudam — a arquitetura em camadas está sendo cobrada da promessa que fez.
- Em produção, backup e restore passam a ser responsabilidade do projeto. **Testar o restore é obrigatório antes de confiar**, e a replicação do Litestream falha em silêncio se ninguém observar — é o que o RDS daria de graça.
- Instância única deixa de ser escolha e vira restrição: SQLite com dois escritores corrompe. Qualquer autoscaling futuro exige migrar para Postgres antes.
- `ALTER TABLE` do SQLite é limitado (mudança de tipo ou constraint exige recriar tabela e copiar). Mecânico, mas relevante porque o modelo vai mudar bastante.

**Quando revisitar**

- Necessidade de mais de uma instância, ou de concorrência de escrita real.
- Modelo estabilizado (poucas mudanças por trimestre) — é o gatilho para pagar a burocracia do RDS de uma vez.
- Volume que torne o restore do Litestream lento o bastante para incomodar no boot.
