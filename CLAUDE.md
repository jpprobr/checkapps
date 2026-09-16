# CLAUDE.md

Leia **`AGENTS.md`** antes de qualquer coisa — é a fonte canônica de instruções deste repositório, compartilhada entre Claude Code, Copilot e Devin. Este arquivo só adiciona o que é específico do Claude Code.

## Específico do Claude Code

- **Skills**: as skills canônicas ficam em `.agents/skills/` (padrão Agent Skills). O Claude Code não varre essa pasta, então `.claude/skills/<nome>/SKILL.md` contém wrappers finos que apontam para lá. Ao criar uma skill nova, crie a canônica em `.agents/skills/` e o wrapper em `.claude/skills/`. Não use symlink (Windows).
- **Plan mode**: para qualquer mudança não trivial, entre em plan mode, escreva o plano, obtenha aprovação via ExitPlanMode e só então implemente. Foi assim que todo o projeto foi construído até aqui.
- **Fim de sessão**: reescreva `.agents/project/STATE.md` como último passo.
- **Commits**: só quando o usuário pedir explicitamente.
