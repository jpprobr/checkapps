# Retrospectiva: a migração DynamoDB → SQLite

**Data**: 2026-10-04 · **Spec**: `specs/004-migracao-sqlite/` · **Decisão**: ADR-009

> Documento histórico. A retrospectiva do experimento que precedeu esta migração está em `2026-10-dynamodb-single-table.md`.

## O que foi feito

Troca completa da camada de persistência, em uma sessão. `Domain`, `Application` e `frontend` não foram tocados; no `Api` mudaram duas linhas (referência ao bootstrapper e a seção de configuração).

## Custo real vs. estimado

| | Estimado na spec | Real |
|---|---|---|
| Linhas de infraestrutura | "menos da metade de 814" (<407) | **637 de C#** (−22%), ou 728 com o `schema.sql` (−11%) |
| Testes de integração | reescrita | 325 linhas (eram 349), **zero asserção alterada** |
| Camadas acima | intocadas | confirmado |

**A estimativa de tamanho estava errada, e vale entender por quê.** Some a cerimônia do DynamoDB (construção de chaves, mapeamento de `AttributeValue`, bootstrap de tabela e GSIs: 115 linhas que simplesmente desaparecem), mas entra a verbosidade do SQL explícito: lista de colunas no `SELECT` e no `INSERT`, aliases para mapear `snake_case` em `PascalCase`, e um `record` de linha por consulta que retorna formato composto. Com Dapper, "menos código" não é o ganho — o ganho é **o que o código consegue expressar**.

Lição para a próxima estimativa: contar linhas é um proxy ruim para custo de migração de persistência. O número honesto de antemão seria "mesma ordem de grandeza, com SQL no lugar de chaves".

## O que ganhamos de fato

- **Testes de integração 5x mais rápidos e sem Docker**: 659ms contra ~3s, e a suíte inteira roda em máquina sem Docker instalado. A `[Collection]` compartilhada — que existia só para evitar que duas classes recriassem a mesma tabela em paralelo — deixou de ser necessária: cada classe tem seu arquivo temporário.
- **Uma classe de bug eliminada estruturalmente.** A armadilha do `SaveAsync` (carregar-mutar-salvar, sob pena de apagar o estado de qualidade) não existe mais, porque `SaveAsync` não escreve esses campos — o estado atual é derivado por join. Não foi "cuidado redobrado", foi o problema deixar de ser possível.
- **Ciclo de desenvolvimento sem reseed.** Os dados sobrevivem a reiniciar a máquina; resetar virou apagar um arquivo.
- **Consultas que cruzam entidades deixaram de ser barreira.** Os ❌ do registro de access patterns agora são decisão de produto, não limitação técnica.

## O que deu errado no caminho (e o que ensinou)

**Ordenação implícita.** A primeira versão ordenava apps por nome; o DynamoDB ordenava por id (via SK). Teste nenhum pegou — foi o resumo do seed, comparado com a baseline, que denunciou. *Lição: ordenação é comportamento observável e raramente tem teste. Em migração, replicar a ordem anterior é obrigação, não detalhe.*

**O diretório de trabalho não é confiável.** API e seed acabaram abrindo bancos diferentes: o ASP.NET Core troca o CWD para o diretório do projeto no startup, o console do seed não. Resolver caminho relativo contra o CWD parecia natural e estava errado. Hoje o caminho relativo resolve contra `%LOCALAPPDATA%/CheckApps`, de forma determinística. *Lição: com banco em arquivo, "onde fica o arquivo" é decisão de arquitetura, não de configuração.*

**`PRAGMA foreign_keys` é por conexão e vem desligado.** Fácil de declarar cascata no schema e nunca descobrir que ela não acontece. Pegou porque os testes de delete afirmavam que os filhos sumiam.

## O que provou valor

**A baseline capturada antes de mexer.** Guardar o JSON real dos endpoints permitiu comparar byte a byte: formato de data (`+00:00`), double sem `.0`, enums como string, ordem dos itens, nulls. Sem ela, "nada mudou" seria opinião.

**Separar troca de motor de mudança de consulta.** A otimização do N+1 em `GetQualityEvolutionAsync` ficou deliberadamente fora desta migração (incremento 6, pendente). Quando algo falhasse, a causa seria uma só.

**A arquitetura em camadas cobrou a promessa.** As portas em `Application` existiam desde o MVP sem benefício visível. Aqui pagaram tudo de uma vez: 2.586 linhas acima da infraestrutura não precisaram ser lidas, muito menos alteradas.

## Pendências desta migração

- ~~Incremento 6~~ **concluído logo em seguida**: o N+1 virou duas consultas (`GetHistoryBySquadAsync`). Vale registrar um desvio consciente do plano, que dizia "uma consulta com `GROUP BY`": a classificação de tendência precisa das linhas individuais de qualquer forma, então um `GROUP BY` no banco significaria ou uma terceira consulta buscando dados já carregados, ou mover regra de negócio para o SQL. Duas consultas com agregação na Application é o desenho correto; a letra do plano estava errada.
- Litestream e o deploy stop-then-start no ECS seguem documentados como alvo, não implementados — e **o restore precisa ser testado antes de confiar dado real a ele**.
