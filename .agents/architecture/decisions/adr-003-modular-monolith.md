# ADR-003: Monólito modular em quatro projetos

**Status**: Aceita · **Data**: 2026-09-13

## Contexto

O brief pedia um monólito modular simples, sem microservices, Kubernetes, event bus ou arquitetura distribuída — e pedia para avaliar se a divisão Domain/Application/Infrastructure/Api era adequada ao tamanho real do MVP antes de adotá-la, sem usar abstrações "por tradição".

## Decisão

Quatro projetos (`Domain`, `Application`, `Infrastructure`, `Api`) com dependência estritamente para dentro, mais dois de teste. Adotado porque **modelagem de domínio e testabilidade são objetivos explícitos** do laboratório: manter `Domain` sem dependência de framework e `Application` sem saber que o banco é DynamoDB é o que permite testar regras de negócio com fakes de 30 linhas em vez de mocks de SDK.

Serviços de aplicação são classes simples (`SquadService`, `AppService`, `QualitySnapshotService`), não CQRS/MediatR — não há justificativa para o padrão nesta escala.

Repositórios são **primitivos "burros"**: fazem I/O, não aplicam regra de negócio. Guardas (ex.: "não remover app com histórico") vivem nos serviços de aplicação.

## Consequências

- Quatro projetos é mais estrutura do que um MVP mínimo estritamente precisa. Se o escopo encolher, colapsar em menos projetos é simplificação legítima, não retrocesso.
- A regra "repositório burro, serviço guarda" precisa ser mantida conscientemente: `DeleteAllAsync` apaga a partição inteira, inclusive snapshots, se chamado direto — o único protetor é `AppService.DeleteAsync`. Há teste que documenta exatamente isso.
