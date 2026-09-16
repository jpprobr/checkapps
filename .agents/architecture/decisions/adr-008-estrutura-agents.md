# ADR-008: Contexto para agentes em `.agents/` + `specs/` na raiz, sem Spec-Kit

**Status**: Aceita · **Data**: 2026-09-16

## Contexto

O projeto será migrado para uma máquina corporativa e provavelmente trabalhado por mais de um agente (Claude Code, GitHub Copilot, Devin). Informação crítica — visão do produto, gotchas operacionais, regras aprendidas com bugs — existia só no histórico de conversa com um único agente, o que não sobrevive a troca de máquina nem de ferramenta. Alternativas avaliadas:

- **Spec-Kit** (GitHub): formato compatível com o ecossistema, mas com processo pesado (7 arquivos por feature, comandos `/specify`, `/plan`, `/tasks`) desproporcional a um projeto solo em laboratório.
- **Pasta proprietária `.ai/`**: tool-agnostic, mas inventa uma convenção que nenhuma ferramenta reconhece.
- **`.agents/` + `AGENTS.md`**: convenção emergente de mercado — `AGENTS.md` é lido por várias ferramentas; `.agents/skills/` é varrido por Copilot e Devin.

## Decisão

- **`AGENTS.md` na raiz** é a fonte canônica de instruções. `CLAUDE.md` e `.github/copilot-instructions.md` são ponteiros finos para ele, com só o que é específico de cada ferramenta.
- **`.agents/`** guarda o contexto para agentes: `product/` (visão, roadmap, backlog), `architecture/` (estrutura, modelo de dados, ADRs), `rules/` (regras não-óbvias por área), `project/STATE.md` (estado da sessão), `skills/` (skills no formato aberto Agent Skills).
- **`specs/` na raiz**, uma pasta por feature (`NNN-nome/spec.md` + `checklist.md`), no formato do template do Spec-Kit — **formato compatível, processo próprio**. Spec é artefato do projeto, não configuração de agente; por isso não fica em `.agents/`.
- Nenhum arquivo é criado sem conteúdo real esperando. Estrutura vazia é dívida.

Limites conhecidos: (1) só `.agents/skills/` tem descoberta nativa — o resto precisa ser apontado do `AGENTS.md`; o Claude Code não varre `.agents/` e usa wrappers em `.claude/skills/`. (2) `.agents/` é namespace compartilhado com fornecedores de ferramenta — se alguma reivindicar uma subpasta com semântica própria, a nossa é renomeada.

## Consequências

- Uma sessão nova, em qualquer ferramenta, deve conseguir se orientar só com `AGENTS.md` → `STATE.md` → skill `run-local`, sem histórico de conversa.
- Cada arquivo em `.agents/` é uma promessa de manutenção. `STATE.md` em especial é reescrito ao fim de cada sessão — se ficar velho, é pior que não existir.
- Migrar para o Spec-Kit no futuro é barato: as specs já estão no formato dele; faltaria só o tooling. Não há lock-in em nenhuma direção.
