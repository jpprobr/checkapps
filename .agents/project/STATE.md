# STATE.md — estado atual do projeto

> Reescrito (não anexado) ao fim de cada sessão de trabalho. Se este arquivo estiver desatualizado em relação ao `git log`, o git está certo. Última atualização: **2026-09-16**, após o commit que introduziu a estrutura `.agents/` + `specs/`.

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
- `setup/prompt-ai-structure-review.md` está no working tree, fora do git — é o prompt da rodada que criou esta estrutura. Decidir se apaga ou move para fora do repo.

## Gotchas para a próxima sessão

Os críticos estão em `AGENTS.md`. Os que mais mordem: Docker Desktop precisa estar rodando; DynamoDB Local perde dados ao reiniciar (rode o seed); API antiga presa em `:5232` trava o build; `DatePipe` sempre com `'UTC'`.

## Próximos passos (em ordem de valor)

1. **Retrofit das duas specs existentes** para `specs/001-…` e `specs/002-…` no formato do template (user stories, FR, SC, checklist) — passo 7 do plano em andamento.
2. `.agents/product/backlog.md` — passo 8 do plano.
3. Primeira feature nova pelo fluxo completo (spec → plano → implementação): candidatas mais prováveis são **CRUD de Repository/SonarProject** (mesma forma do que já existe) ou **integração real com Sonar** (destrava o estágio Health de verdade).
4. Antes de qualquer uso compartilhado: substituir ADR-007 por uma decisão de autenticação.
