# Regras — frontend (`frontend/**`)

Leia antes de editar código Angular. Só o que é específico deste projeto.

## Estrutura

- **Standalone components, sem NgModules.** Angular 20; nomes de arquivo sem sufixo `.component` (`app-list.ts`, classe `AppList`) — é a convenção padrão da CLI 20, mantenha.
- **Sem state management externo.** Signals (`signal`, `computed`) + serviços injetados com `inject()`. Estado de tela vive no componente; chamadas HTTP nos serviços em `core/`.
- **Gere componentes com `ng g component features/<nome> --skip-tests`.** Não há suíte de testes Angular no projeto (decisão consciente: o foco de testes é backend); não deixe `.spec.ts` gerados a esmo.
- **Shell em `app.html`**: menu lateral (`SidebarService`): marca → `/`, seletor de squad no topo e seções `Squads` | `Repositórios` | `Checagens` com ícone SVG embutido; recolhível (só ícones, `aria-label` + `title`, estado em `localStorage`) e gaveta em ≤ 768 px. Estado ativo sempre por fundo + barra, nunca só cor. A lista de squads fica em **`/squads`**; `/` redireciona para a última squad visitada (ou `/squads`). Links de "voltar para squads" apontam para `/squads`, nunca para `/`.
- **`CurrentSquadService`** é a squad em contexto (signal + `localStorage` com try/catch). Telas de squad e de app chamam `set(squadId)`; quem remove squad chama `clear`; quem cria ou altera squad chama `refresh()` para o seletor do menu refletir. Breadcrumbs usam `squadName()` — nunca "Dashboard".
- **`ParamAwareReuseStrategy`** (em `app.config.ts`) recria o componente quando só o parâmetro da rota muda. As telas leem o parâmetro do snapshot no construtor; sem essa estratégia, trocar de squad pelo seletor mostraria os dados da squad anterior.
- **Listagens existentes são a superfície de gerenciamento.** Botões de criar/editar/inativar/remover ficam em `squad-picker` e `app-list`. Não crie uma "área admin" paralela.

## Datas

- **Sempre `'UTC'` no `DatePipe`**: `{{ x | date:'MMM/yyyy':'UTC' }}`. A API devolve ISO em UTC (`2026-08-01T00:00:00+00:00`); sem o argumento, o navegador converte para o fuso local e agosto vira julho em UTC-3. Já aconteceu.
- **Exceção: instantes reais em horário local.** Eventos do histórico de checagem (`occurredAt`), `createdAt` e `closedAt` são momentos de verdade, não períodos de demonstração — mostre sem `'UTC'`. Datas de calendário (`startDate`/`endDate`, `yyyy-MM-dd`) continuam com `'UTC'`.

## API

- A URL base está em `core/api-config.ts` (`http://localhost:5232/api`). Só um lugar.
- **`AppSummary` não carrega `description` nem `externalId`.** Antes de fazer `PUT /api/apps/{id}` a partir da lista (ex.: inativar), busque o `AppDetail` completo — o PUT substitui o item inteiro, e enviar `null` apaga o campo.
- Erros 409 da API trazem mensagem em texto no body; mostre `err.error` ao usuário em vez de uma mensagem genérica.
- **O `.subtitle` global tem margem superior negativa** (cola no título). Dentro de um bloco que vem logo depois de um formulário, zere-a no CSS do componente — senão o texto sobrepõe o campo.
- **Disponibilidade vem da API, nunca presumida**: o bloco "Buscar no GitHub" consulta `GET …/scan/status` e mostra o motivo em texto quando o scan está desligado ou sem configuração. Mensagens de erro do servidor (400/409/502) são exibidas como vêm, em `role="alert"`.
- **`.tag` e `.inline-checkbox` são globais** (`styles.css`). Antes só existiam dentro de componentes específicos, e várias telas os usavam sem estilo. Estilo reutilizado por mais de uma tela vai para o global, não para o CSS de uma delas.
- **Tela "Repositórios" (`/repositories`; `/discovered` redireciona, é o nome antigo)**: lista **todos** os repositórios conhecidos, cada um com o estado dele — *novo*, *no catálogo* (etiqueta + link do app, **sem ações**), *de outra squad*, *ignorado* —, com "Novos" como filtro padrão. **As ações vêm do estado da linha** (um catalogado cujo status armazenado é `new` não oferece "Criar app"). Toda ação é explícita e "Criar app" **não grava nada** até a confirmação do painel. Sugestão aparece como sugestão (com confiança em texto — "alta"/"baixa" —, nunca só em cor) e é editável. O recurso da API continua `discovered-repositories`; só a tela mudou de nome.
- **Status de checagem circulam como códigos** (`em-andamento`…); rótulos de tela ficam em `core/check-labels.ts`, num lugar só.

## Visual

- **Paleta de Quality Level A–E** (`--quality-a` … `--quality-e` em `styles.css`) vem de uma metodologia validada de acessibilidade (contraste + daltonismo). Não troque os hex "no olho"; se precisar mudar, revalide com a skill `dataviz` ou equivalente.
- **Nunca cor sozinha para transmitir estado.** Badges de Quality Level sempre mostram a letra; gráficos têm legenda e rótulos.
- Tokens de tema (light/dark) estão em `:root` no `styles.css`. Componentes usam `var(--…)`, nunca hex direto.
- Sem lib de gráfico. `quality-history-chart` é SVG próprio; mantenha assim até haver necessidade real de interação (tooltip, zoom).

## Build

- Angular CLI **20**, não 22 — o Node da máquina (22.17) não roda a 22. Não faça `ng update` para 22 sem atualizar o Node antes.
- `npx ng build` para checar erros de compilação; `ng serve` recompila sozinho ao salvar.
- **Playwright e a gaveta do celular**: com o menu recolhido fora da tela (`translateX(-100%)`), `isVisible()` continua `true`; meça `boundingBox().x` (negativo = fechada) e espere a transição de 0,2 s antes de medir aberta.
- **Playwright: escope seletores de texto a `main`** (`main >> text=…`, `main select`). O seletor de squad do menu lateral tem um `<option>` com o nome de cada squad, e `text=Squad X` sem escopo casa com ele.
