---
name: skill-discovery
description: Audita o histórico do CheckApps (STATE.md, specs, regras, ADRs) atrás de procedimentos recorrentes que valham virar skill, e cria a skill aprovada no padrão deste repositório (.agents/skills + wrapper em .claude/skills). Use quando pedirem para descobrir, sugerir ou criar uma skill nova, ou para revisar se o que já foi construído justifica uma.
---

# Descoberta e criação de skills (CheckApps)

## Contexto

As skills ficam em dois lugares, ambos versionados (referência: `run-local`):

- `.agents/skills/<nome>/SKILL.md` — a skill **canônica**: frontmatter (`name`, `description`) e as instruções. Copilot e Devin leem esta pasta direto.
- `.claude/skills/<nome>/SKILL.md` — wrapper fino que o Claude Code carrega: o mesmo frontmatter e um parágrafo mandando ler o arquivo canônico e segui-lo, sem reproduzir comandos de memória. Sem symlink (Windows).

Escreva em português, como o restante de `.agents/`. Não ponha as instruções só no wrapper.

## Quando algo merece virar skill

- **Mecânico e repetível**: dá para escrever como lista ordenada de passos que outra sessão segue sem refazer o raciocínio.
- **Já recorreu pelo menos duas vezes**, ou é a forma que as próximas vão ter.
- **Tem uma armadilha real**, que já deu problema (as regras "conquistadas" em `.agents/rules/` e os gotchas do `STATE.md` são o melhor indício).

Não vira skill o que é só "leia este documento": o `AGENTS.md` e as regras já fazem isso. Skill carrega **procedimento**, não referência.

## Como descobrir

1. Liste as skills existentes (`.agents/skills/*/SKILL.md`) para não duplicar.
2. Leia `.agents/project/STATE.md`: "Gotchas para a próxima sessão" e "Próximos passos".
3. Leia `.agents/rules/backend.md` e `frontend.md`: cada regra aprendida com bug real é candidata.
4. Passe pelos `checklist.md` das últimas specs em `specs/`: passos que se repetem de spec em spec.
5. Confira `git log --oneline` para ver o que se repete em commits.
6. Para cada candidato, escreva uma ou duas frases: o que se repete, onde foi visto e o que dá errado sem a skill. **Apresente a lista ao usuário antes de criar qualquer arquivo.**

## Candidatos conhecidos (lista de partida; confira contra o estado atual, ela envelhece)

1. **Coluna nova em tabela existente.** Toda coluna nova entra em `AdditiveColumns` (`SqliteSchemaBootstrapper`) **e** no `CREATE TABLE` do `schema.sql`; índice que depende da coluna nova fica no bootstrapper, não no schema. Esquecer um dos dois quebra banco novo ou banco existente.
2. **Fechar a sessão.** Reescrever (não anexar) o `STATE.md`, conferir o mapa do `AGENTS.md` (specs novas, contagem de testes) e o roteiro de próximos passos.
3. **Spec nova.** Copiar `specs/_template/` para `specs/NNN-nome/`, preencher antes de codar, e registrar a spec no mapa do `AGENTS.md`.
4. **Gate antes de dar por pronto.** `dotnet build` sem erros, `dotnet test`, build do Angular e um roteiro no navegador (Playwright, com os gotchas do `STATE.md`).

## Criar a skill (só depois de o usuário aprovar o candidato)

1. Escolha um nome em kebab-case que descreva a tarefa, não um substantivo vago.
2. Escreva `.agents/skills/<nome>/SKILL.md`: frontmatter com `name` e `description` (a descrição é o que decide **quando** disparar: diga a condição, não só o tema), depois `## Contexto` (de onde vem, qual bug ou sessão) e `## Passos` (ordenados, com comandos e caminhos exatos, cada passo com sua checagem).
3. Escreva `.claude/skills/<nome>/SKILL.md`: o mesmo frontmatter e o parágrafo do wrapper, no formato do `run-local`.
4. Se a skill muda o fluxo de trabalho, proponha a linha correspondente no mapa do `AGENTS.md`; não edite sem perguntar.
5. Mostre os arquivos ao usuário. Não faça `git add` nem commit sem pedido.

## Limites

- Só cria ou edita arquivos em `.agents/skills/` e `.claude/skills/`. Nunca toca em `src/`, `frontend/` ou `tools/`.
- Edições em outros documentos (`AGENTS.md`, regras, specs) são só propostas.
- Antes de propor, confira se já existe skill cobrindo o mesmo assunto.
