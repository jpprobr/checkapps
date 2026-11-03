# Checklist — Spec 018: Tela de vínculos host→app (parte 1)

Critério de "concluído". Cada item deve ser verificável por alguém que não escreveu o código.

Status: **implementada** em 2026-11-03 (plano aprovado em plan mode no mesmo dia). Confirmada pelo usuário na tela em 2026-11-03.

## Qualidade da spec (antes de codar)

- [x] User stories têm cenários de aceite em Dado/Quando/Então
- [x] Nenhum `[NEEDS CLARIFICATION]` restante
- [x] Cada FR é testável
- [x] Success Criteria são mensuráveis e não citam tecnologia
- [x] Plano aprovado pelo usuário (plan mode)

## Implementação

- [x] 1. Modelos, serviço de API (`linkStatus`, `setLink`, `removeLink`) e rótulos (`integration-labels.ts`)
- [x] 2. Filtro "Vínculo" e coluna "App" na lista de hosts
- [x] 3. Painel de vínculo no detalhe do host (confirmar, rejeitar, remover, nota, erro em alerta)
- [x] 4. Subtítulos obsoletos reescritos
- [x] Documentação: `docs/leitor-github.md`, `AGENTS.md` (mapa), `backlog.md`, `STATE.md`

## Verificação (skill `verify-change`)

- [x] `npx ng build` sem erros nem avisos
- [x] Backend intocado (nenhum arquivo `.cs` alterado)
- [x] Roteiro no navegador em banco isolado (e2e.db, fonte `fake`): filtro "Sem app", confirmar no modelo, "pelo modelo" no host `hom`, 409 em alerta, rejeitar com "desfazer", remover pelo modelo, celular 390 px sem estouro; único erro de console é o 409 esperado
- [x] Confirmação do usuário na tela (2026-11-03), com apontamentos de melhoria registrados no backlog (celular, densidade das colunas, tag "modelo")

## Encerramento (`close-spec`)

- [x] `Status` da spec atualizado
- [x] `.agents/project/STATE.md` reescrito (próximo passo = parte 2)
- [x] `.agents/product/backlog.md` atualizado
- [x] Mapa do `AGENTS.md`
