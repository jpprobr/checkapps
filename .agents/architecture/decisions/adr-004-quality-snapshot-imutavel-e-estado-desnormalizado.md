# ADR-004: QualitySnapshot imutável; Quality Level congelado na escrita; estado atual desnormalizado no App

**Status**: Aceita, exceto a cláusula 3 (desnormalização), revogada pelo ADR-009 · **Data**: 2026-09-13

> As cláusulas 1 e 2 — snapshot imutável e Quality Level congelado na escrita com a versão da regra — continuam valendo e são o coração do produto. A cláusula 3 existia para evitar uma consulta extra no DynamoDB; em SQL, "app com seu snapshot mais recente" é um join.

## Contexto

O maior gap da POC era não ter histórico. O brief exigia que o histórico **não** fosse modelado como "estado atual do App", e que métricas (dado observado) e Quality Assessment (classificação derivada de regras) fossem camadas separadas — com as regras podendo evoluir sem alterar snapshots antigos.

## Decisão

1. **`QualitySnapshot` é imutável.** Nunca é editado ou apagado individualmente. Representa "o que foi observado em `collectedAt`".
2. **O Quality Level e a versão da regra são gravados no snapshot** (`qualityLevel`, `assessmentRulesVersion`) no momento da coleta, calculados por `IQualityAssessmentEngine`. Uma regra v2 no futuro classifica snapshots novos; os antigos continuam dizendo "fui classificado como B pela v1". Nada é recalculado retroativamente.
3. **O item App carrega o estado atual desnormalizado** (`currentQualityLevel`, `currentMetrics`, `lastSnapshotAt`, GSI2) para leitura rápida, atualizado **na mesma transação** que grava o snapshot, condicionado a ser mais recente que o atual. `QualitySnapshotService.RegisterAsync` é o único ponto de entrada.

## Consequências

- Perguntas de "como estava em X" e "como está agora" são respondidas por caminhos diferentes e ambos são baratos.
- Existe uma invariante de consistência a proteger: qualquer processo futuro (import, sync real com Sonar) **precisa** gravar snapshot pelo `QualitySnapshotService`, senão o estado atual do App fica desatualizado.
- `AppRepository.SaveAsync` grava o item App inteiro, inclusive os campos desnormalizados. Editar um App = carregar, mutar, salvar o mesmo objeto. Regra registrada em `.agents/rules/backend.md`.
- Backfill de snapshots antigos é seguro: a condição da transação impede que um snapshot de junho "regrida" um estado atual de agosto.
