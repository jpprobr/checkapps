# Regras — frontend (`frontend/**`)

Leia antes de editar código Angular. Só o que é específico deste projeto.

## Estrutura

- **Standalone components, sem NgModules.** Angular 20; nomes de arquivo sem sufixo `.component` (`app-list.ts`, classe `AppList`) — é a convenção padrão da CLI 20, mantenha.
- **Sem state management externo.** Signals (`signal`, `computed`) + serviços injetados com `inject()`. Estado de tela vive no componente; chamadas HTTP nos serviços em `core/`.
- **Gere componentes com `ng g component features/<nome> --skip-tests`.** Não há suíte de testes Angular no projeto (decisão consciente: o foco de testes é backend); não deixe `.spec.ts` gerados a esmo.
- **Listagens existentes são a superfície de gerenciamento.** Botões de criar/editar/inativar/remover ficam em `squad-picker` e `app-list`. Não crie uma "área admin" paralela.

## Datas

- **Sempre `'UTC'` no `DatePipe`**: `{{ x | date:'MMM/yyyy':'UTC' }}`. A API devolve ISO em UTC (`2026-08-01T00:00:00+00:00`); sem o argumento, o navegador converte para o fuso local e agosto vira julho em UTC-3. Já aconteceu.

## API

- A URL base está em `core/api-config.ts` (`http://localhost:5232/api`). Só um lugar.
- **`AppSummary` não carrega `description` nem `externalId`.** Antes de fazer `PUT /api/apps/{id}` a partir da lista (ex.: inativar), busque o `AppDetail` completo — o PUT substitui o item inteiro, e enviar `null` apaga o campo.
- Erros 409 da API trazem mensagem em texto no body; mostre `err.error` ao usuário em vez de uma mensagem genérica.

## Visual

- **Paleta de Quality Level A–E** (`--quality-a` … `--quality-e` em `styles.css`) vem de uma metodologia validada de acessibilidade (contraste + daltonismo). Não troque os hex "no olho"; se precisar mudar, revalide com a skill `dataviz` ou equivalente.
- **Nunca cor sozinha para transmitir estado.** Badges de Quality Level sempre mostram a letra; gráficos têm legenda e rótulos.
- Tokens de tema (light/dark) estão em `:root` no `styles.css`. Componentes usam `var(--…)`, nunca hex direto.
- Sem lib de gráfico. `quality-history-chart` é SVG próprio; mantenha assim até haver necessidade real de interação (tooltip, zoom).

## Build

- Angular CLI **20**, não 22 — o Node da máquina (22.17) não roda a 22. Não faça `ng update` para 22 sem atualizar o Node antes.
- `npx ng build` para checar erros de compilação; `ng serve` recompila sozinho ao salvar.
