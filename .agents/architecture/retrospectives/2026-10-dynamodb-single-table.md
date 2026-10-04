# Retrospectiva: o experimento DynamoDB single-table (set–out/2026)

**Período**: 2026-09-13 a 2026-10-04 · **Decisões relacionadas**: ADR-001, ADR-002 (substituída), ADR-004, ADR-009

> Documento **histórico**, não vivo: descreve um experimento concluído e por isso não envelhece. Não confundir com `../data-model/access-patterns.md`, que é registro vivo e deve acompanhar o código. Existe por dois motivos: preservar a análise arquitetural do MVP e servir de material de aprendizado.

## O que foi construído

Um design single-table completo, não um exercício de brinquedo. O "currículo" percorrido:

| Técnica | Onde |
|---|---|
| Tabela única com `PK`/`SK` e prefixos por tipo de item | `SQUAD#`, `APP#`, `REPO#`, `SONARPROJECT#`, `APPLINK#`, `SNAPSHOT#` |
| Coleção pai-filho numa partição | Repositórios, projetos Sonar e links de um App |
| Série temporal ordenável no SK | `SNAPSHOT#{iso}#{sonarProjectId}`, com `BETWEEN` para período e `ScanIndexForward=false, Limit=1` para o mais recente |
| GSI reaproveitado por múltiplos espaços de valor | GSI1 serve "listar squads" (`"SQUAD"`) e "apps de uma squad" (`"SQUAD#{id}"`) |
| GSI esparso | GSI2 só existe no item App e só depois do primeiro snapshot |
| Escrita transacional com condição | `TransactWriteItems`: grava snapshot + atualiza estado atual se `collectedAt` for mais recente |
| Escrita condicional para unicidade | `attribute_not_exists(PK)` com retry de slug, sem "checar-depois-gravar" |
| Desnormalização deliberada com invariante | Estado de qualidade atual no item App |
| Delete de partição em lote | `Query` + `BatchWriteItem` em lotes de 25 |

Resultado em produção de laboratório: 2 specs implementadas, 48 testes verdes, 11 endpoints, zero erro de modelagem encontrado.

## O que funcionou bem

- **Access-pattern-first funciona mesmo.** Listar os 14 padrões antes de desenhar a tabela evitou retrabalho: nenhum padrão do MVP precisou de GSI novo depois. A disciplina vale mesmo em banco relacional.
- **Dois GSIs para 14 padrões.** A regra "não criar índice por precaução" se sustentou — reaproveitar o GSI1 com espaços de valor distintos é elegante e cobriu três padrões.
- **Escrita condicional é superior a verificar-depois-gravar.** O retry de slug e a proteção contra backfill fora de ordem resolveram, no banco, dois problemas de concorrência que em código ficariam sutilmente errados.
- **Série temporal no SK é imbatível.** Histórico por período e "mais recente" saem de graça da ordenação da chave. Esse padrão eu levaria para qualquer banco.
- **A camada de abstração pagou.** Domain, Application, Api e frontend (2.586 linhas) não sabem que o banco existe; a migração inteira cabe em ~1.160 linhas de infraestrutura e testes.

## O que custou caro

- **A desnormalização gerou a única classe de bug do projeto.** `AppRepository.SaveAsync` é um `PutItem` completo; editar um App construindo um objeto novo apagava silenciosamente `currentQualityLevel`/`currentMetrics`. Virou regra escrita, teste dedicado e nota em três documentos — custo permanente de uma otimização para evitar uma consulta extra.
- **Toda consulta que cruza entidades virou N+1 em memória.** A evolução agregada da squad busca os apps e depois faz uma query por app. Em SQL é um `GROUP BY`.
- **"Listar todos os X" virou partição única** (`GSI1PK = "SQUAD"`), um anti-padrão conhecido que repetimos de propósito — e que a spec 003 repetiria de novo (`"CHECK"`).
- **Dois GSIs para o que em SQL seria um `WHERE`.** O GSI2 existe só para "apps por Quality Level".
- **Mapeamento manual.** `Keys.cs`, `TableSchema.cs`, `AttributeValueExtensions.cs` e `QualityMetricsMapper.cs` somam 115 linhas que existem apenas porque o banco é o que é.
- **Ferramental local atrapalhou o dia a dia.** DynamoDB Local em `-inMemory` perde os dados a cada reinício; testes de integração exigem Docker; e duas classes de teste recriando a mesma tabela em paralelo causaram falhas intermitentes até a correção com `[Collection]`.

## O achado que decidiu a troca

Avaliando os 29 access patterns, a linha ficou nítida:

> **Uma entidade e seus filhos** (partição, `begins_with`, série temporal, escrita condicional) → o DynamoDB resolve com elegância.
> **Cruzar entidades** (join, agregação transversal, filtro por atributo de outra entidade) → ele empurra para a memória ou para um índice novo.

O MVP ficou inteiro do lado bom. A spec 003 (Checagens) fica na v1 — mas suas evoluções óbvias ("o que está pendente para a minha squad em todas as checagens", painel de todas as checagens, checagem por regra) ficam do outro lado, assim como o estágio Context inteiro (solução, topologia, grafo de dependência).

E um segundo achado, mais incômodo: **na escala real do projeto — dezenas de apps, milhares de linhas — `Scan` custa frações de centavo e milissegundos.** A disciplina de nunca fazer `Scan`, que organizou todo o design, foi projetada para uma escala que este produto não tem e não terá. Quando a resposta para toda consulta difícil é "carrega tudo e filtra em memória", o banco virou uma camada de persistência cara e cerimoniosa.

## Quando eu escolheria DynamoDB de novo

O aprendizado que sobrevive ao projeto:

**Sim, se**: escala for grande ou imprevisível; os padrões de acesso forem poucos, conhecidos e estáveis; a forma dominante for "agregado e seus filhos"; houver série temporal por chave; precisar de zero operação; e a equipe aceitar que *mudar uma pergunta é mudar o modelo*.

**Não, se**: o volume for pequeno e conhecido; as perguntas forem exploratórias ou mudarem com frequência; houver relatórios, agregações ou cruzamento entre entidades; o modelo ainda estiver se formando; ou se a resposta honesta para "e se eu precisar de outra consulta?" for "carrego tudo e filtro".

**O teste de dez segundos**: conte quantas das suas perguntas são sobre *uma entidade e seus filhos* e quantas *cruzam entidades*. Se a segunda coluna for maior que zero e crescente, relacional.

**E o maior aprendizado de todos**: a pergunta certa não era "qual banco é melhor?", e sim "**o que acontece quando eu descobrir uma pergunta nova?**". No DynamoDB, a resposta é remodelar; em SQL, é escrever outra query. Para um produto em descoberta — que é o caso deste — essa diferença é a decisão inteira.

## O que levo para o Postgres/SQLite

- Listar access patterns antes de desenhar schema (continua valendo, com outro custo de erro).
- Snapshot imutável com a versão da regra congelada — nada de recalcular histórico.
- Escrita condicional/transacional para unicidade e para evitar regressão em backfill.
- Ids gerados na aplicação (slug e GUID) e datas em ISO-8601 — que, por acidente feliz, deixaram o modelo portável.
- Desconfiança de desnormalização sem necessidade medida: em SQL, começar normalizado e só desnormalizar com número na mão.
