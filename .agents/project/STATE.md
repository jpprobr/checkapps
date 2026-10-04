# STATE.md — estado atual do projeto

> Reescrito (não anexado) ao fim de cada sessão de trabalho. Se este arquivo estiver desatualizado em relação ao `git log`, o git está certo. Última atualização: **2026-10-04**, ao decidir a saída do DynamoDB (ADR-009) e aprovar a spec 004 de migração.

## Onde estamos

Estágio do roadmap: **Inventário + Health + Evolution concluídos** (ver `.agents/product/roadmap.md`). O projeto está em fase de laboratório, rodando só em máquina local, uma pessoa + agentes.

## O que funciona

- **MVP core** (`specs/001-mvp-core/`): squads, apps, repositórios, projetos Sonar, histórico de `QualitySnapshot`, dashboard da squad (distribuição por Quality Level, tendências), lista de apps com busca/filtro/ordenação, detalhe do app com gráfico de evolução e tabela de histórico.
- **Cadastro de Squads e Apps** (`specs/002-squad-app-management/`): criar/editar/inativar/remover pela UI e pela API, ids por slug, guardas de remoção (409 quando há histórico/vínculos), links corporativos por app (`AppLink`).
- **Testes**: 37 unitários + 11 de integração (contra DynamoDB Local), todos verdes.
- **Ambiente local**: skill `run-local` sobe tudo; `README.md` tem o passo a passo humano.

## O que está mockado / provisório

- **Dados de Sonar** vêm de `MockSonarProvider` + `DemoDataFactory`. Não há integração com SonarQube real.
- **Persistência** só em DynamoDB Local (`-inMemory`). Nunca foi validado contra uma conta AWS real.
- **Sem autenticação** (ADR-007). Sem deploy. Sem CI.

## Decisões pendentes / perguntas abertas

- `.github/copilot-instructions.md` ainda não existe — criar quando o Copilot passar a ser usado neste repo (máquina corporativa).
- Confirmar, na máquina corporativa, se Devin/Copilot realmente varrem `.agents/skills/` (premissa do ADR-008). Se não, o wrapper em `.claude/skills/` vira o modelo para eles também.

## Gotchas para a próxima sessão

Os críticos estão em `AGENTS.md`. Os que mais mordem hoje: Docker Desktop precisa estar rodando; DynamoDB Local perde dados ao reiniciar (rode o seed); API antiga presa em `:5232` trava o build; `DatePipe` sempre com `'UTC'`. **Os três primeiros deixam de existir com a spec 004** — ao implementá-la, atualize os gotchas do `AGENTS.md`.

## Decisão estrutural tomada (2026-10-04)

**Sair do DynamoDB** (ADR-009, substitui o ADR-002): SQLite agora, Postgres quando o modelo estabilizar. Motivos: o DynamoDB atende bem "entidade e filhos" e mal consultas que cruzam entidades — o que a spec 003 e o estágio Context vão cobrar; a escala real não exercita suas forças; e o processo corporativo para criar RDS (modelagem completa + duas aprovações, repetido a cada alteração) é incompatível com o método incremental do projeto. A retrospectiva do experimento está em `.agents/architecture/retrospectives/2026-10-dynamodb-single-table.md`.

## Specs aprovadas, não implementadas

- **Spec 004 — Migração para SQLite** (`specs/004-migracao-sqlite/`): aprovada, **vem primeiro**. Não altera comportamento observável; reescreve ~814 linhas de infra e ~349 de testes; Domain/Application/Api/frontend intactos. Litestream e deploy ECS ficam documentados como alvo, não implementados agora.
- **Spec 003 — Checagens** (`specs/003-checagens/`): em análise. Status dos itens já confirmados (`pendente`/`em-andamento`/`conforme`/`nao-se-aplica`/`concluido`); **faltam confirmar** as assumptions sobre remoção de app cascatear itens, reabrir sem trilha, remover checagem só sem itens e ausência de notificação. Os access patterns #21–#29 dela precisam ser revistos depois da migração.

## Próximos passos (em ordem)

1. **Implementar a spec 004** (migração SQLite) — 5 incrementos no `checklist.md`. Vem antes da 003 para que `Check` e `CheckItem` nasçam direto em SQL e não sejam escritos duas vezes.
2. **Confirmar as assumptions restantes da 003** e implementá-la pelo fluxo completo, já em SQL.
3. Ideias de Context (topologia, solução, dependências entre apps) e detecção automática seguem no backlog, com recomendação de começar por convenção de tags.
4. Na migração para a máquina corporativa: criar `.github/copilot-instructions.md`, confirmar se Devin/Copilot leem `.agents/skills/`.
5. Antes de qualquer uso compartilhado: substituir ADR-007 por uma decisão de autenticação.

Verificado em 2026-09-16: uma sessão nova do Claude Code, sem histórico, subiu o ambiente local só com `CLAUDE.md` → `AGENTS.md` → este arquivo → skill `run-local`.
