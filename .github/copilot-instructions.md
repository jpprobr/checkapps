# Instruções para o GitHub Copilot

Leia **`AGENTS.md`**, na raiz, antes de qualquer coisa. Ele é a fonte canônica de instruções deste repositório, compartilhada entre Copilot, Claude Code e Devin (ADR-008). Este arquivo só aponta para ele e acrescenta o que é específico do Copilot.

## Por onde começar

1. `AGENTS.md`: o que é o projeto, como rodar e testar, o mapa "antes de mexer em X, leia Y" e os gotchas críticos.
2. `.agents/project/STATE.md`: o estado atual, as pendências e os próximos passos. Se ele divergir do `git log`, o git está certo.
3. O resto de `.agents/` **não é descoberto sozinho**: leia o que o mapa do `AGENTS.md` indicar para a tarefa.
   - `.agents/product/`: visão, roadmap, backlog e glossário. Use os termos do glossário.
   - `.agents/architecture/`: estrutura, modelo de dados e ADRs. Uma decisão registrada num ADR se respeita, ou se propõe um ADR novo.
   - `.agents/rules/`: regras aprendidas com bugs reais, por área (`backend.md`, `frontend.md`).
   - `.agents/skills/`: procedimentos recorrentes no padrão Agent Skills (`run-local`, `verify-change`, `close-spec`, `skill-discovery`). Siga a skill quando a tarefa for a dela.
4. `specs/NNN-nome/`: a especificação e o checklist de cada feature.

## Como trabalhar aqui

- Para mudança não trivial, proponha um plano (ou uma spec a partir de `specs/_template/`) e espere aprovação antes de escrever código.
- Incrementos pequenos e verificáveis: build sem avisos, `dotnet test` verde e, se mexer na tela, `npx ng build` dentro de `frontend/`.
- Nenhum teste usa rede, Docker ou serviço externo.
- Segredos (token e organização do GitHub, por exemplo) ficam só em `dotnet user-secrets` ou em variável de ambiente, nunca em arquivo versionado nem em exemplo de documentação.
- A documentação do repositório é neutra: nada de nome de empresa nem de valores reais de hosts; use placeholders como `exemplo.com` e `{org}-{sigla}-`.
- Commit só quando a pessoa pedir.
- Ao fim de uma sessão de trabalho, reescreva `.agents/project/STATE.md` (substitua, não anexe).
