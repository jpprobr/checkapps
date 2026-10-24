---
name: close-spec
description: Abre uma spec nova do CheckApps a partir do template, ou encerra uma spec/sessão deixando a documentação consistente - Status da spec, checklist, STATE.md, backlog.md e o mapa do AGENTS.md. Use ao começar uma feature nova (criar specs/NNN-nome), ao terminar a implementação de uma spec, ou ao fim de qualquer sessão de trabalho.
---

# Abrir e encerrar specs (CheckApps)

## Contexto

Toda spec (001 a 012) passou pelo mesmo ciclo, e o "Encerramento" de cada checklist repete os mesmos itens. O que costuma divergir: o `STATE.md` fica defasado em relação ao `git log` (o `CLAUDE.md` exige **reescrever**, não anexar), a contagem de testes mora em dois arquivos, e specs novas ficam fora do mapa do `AGENTS.md`. Verificar a mudança em si é a skill `verify-change`; esta cuida só do papel.

## A. Abrir uma spec

1. Próximo número: o maior em `specs/` mais um, com três dígitos. Nome em kebab-case descrevendo a feature.
2. Copie `specs/_template/` para `specs/NNN-nome/` (`spec.md` e `checklist.md`).
3. Preencha `spec.md` **antes de codar**: `Status: Rascunho`, `Criada em` (data de hoje), `Origem` (pedido, item do backlog, ADR, incidente). Ambiguidade vira `[NEEDS CLARIFICATION: pergunta]` para o usuário, não suposição.
4. Preencha o `checklist.md` com os incrementos reais no lugar dos marcadores.
5. Se a spec exige decisão estrutural (stack, modelo, padrão), proponha um ADR novo em `.agents/architecture/decisions/` em vez de decidir no texto da spec.
6. Para mudança não trivial, o `CLAUDE.md` pede plan mode: o plano é aprovado antes da implementação. Atualize `Status` para `Aprovada` e depois `Em implementação` conforme avança.

Checagem: `spec.md` sem nenhum `NNN` ou `[Nome da feature]` restante e sem `[NEEDS CLARIFICATION]` aberto.

## B. Encerrar uma spec

Só depois de `verify-change` passar. Não marque o que não foi feito.

1. **Checklist**: marque cada item realmente concluído. Item pendente (em geral a confirmação do usuário na tela) fica desmarcado, e a linha `Status:` do topo diz isso: `implementada em AAAA-MM-DD. Resta confirmação do usuário na tela.` Quando o usuário confirmar, atualize para `Confirmada pelo usuário na tela em AAAA-MM-DD`.
2. **`spec.md`**: `Status` → `Implementada`.
3. **Regras conquistadas**: se um bug real ensinou algo não óbvio, acrescente em `.agents/rules/backend.md` ou `frontend.md` (só regra específica do projeto, em uma ou duas linhas).
4. **Modelo de dados**: se mudou chave, tabela ou coluna, atualize `.agents/architecture/data-model/sqlite-schema.md` (e `access-patterns.md` se o padrão de acesso mudou).
5. **`.agents/product/backlog.md`**: remova o item entregue ou marque-o como feito com a spec e a data; acrescente o que foi descoberto no caminho.
6. **Mapa do `AGENTS.md`**: se a spec é de uma área já listada (ex.: leitor de GitHub), acrescente-a na linha; se é área nova, crie a linha. A contagem de testes do bloco "Como rodar e testar" tem de bater com a do `dotnet test` mais recente.
7. **`README.md`**: atualize se mudou como rodar ou testar.

## C. Encerrar a sessão (sempre, como último passo)

Reescreva `.agents/project/STATE.md` **inteiro** (nunca anexe), mantendo as seções existentes:

- **Cabeçalho**: data da última atualização e a frase de estado (o que foi implementado e o que falta confirmar).
- **O que funciona**: uma entrada por spec, com o essencial; contagem de testes igual à do último `dotnet test`.
- **Mockado/provisório** e **Decisões pendentes**: o que mudou nesta sessão.
- **Gotchas**: acrescente os novos que custaram tempo; remova os que deixaram de valer.
- **Próximos passos**: em ordem, com os itens já feitos removidos.

Checagem final: compare o `STATE.md` com `git log --oneline | head` — se divergirem, o git está certo.

## Limites

- Só edita documentação (`specs/`, `.agents/`, `AGENTS.md`, `README.md`); nunca `src/`, `frontend/` ou `tools/`.
- Não faz commit sem o usuário pedir.
- Não registra no repositório planos pessoais nem nomes de empresa (ver memória do projeto sobre docs neutros).
