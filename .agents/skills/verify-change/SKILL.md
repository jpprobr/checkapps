---
name: verify-change
description: Verifica uma mudança do CheckApps antes de dá-la por pronta - build sem erros nem avisos, testes, curl nos endpoints, roteiro no navegador em banco isolado e confirmação do usuário. Use ao terminar de implementar um incremento ou uma spec, antes de marcar o bloco "Verificação" do checklist, ou quando pedirem para validar, testar de ponta a ponta ou conferir uma mudança no navegador.
---

# Verificar uma mudança (CheckApps)

## Contexto

O bloco "Verificação" das checklists das specs 009 a 012 é o mesmo, e as mesmas armadilhas se repetiram: testes manuais rodando contra o banco real da máquina, API antiga travando o build, seletores do Playwright casando com o menu lateral. Esta skill junta o roteiro num lugar só. Para só subir o ambiente, use `run-local`.

## Passos

Cada passo tem uma checagem. Não avance sem ela passar, e diga ao usuário o que falhou ou o que foi pulado.

### 1. Build e testes

1. Garanta que nenhuma API está rodando: `netstat -ano | grep ':5232'`; se houver PID, `taskkill //PID <pid> //F`. O processo trava os DLLs (`MSB3027`).
2. `dotnet build` — **0 erros e 0 avisos**.
3. `dotnet test` — tudo verde. Anote os números (unitários e integração); eles vão para o `STATE.md` e o `AGENTS.md`. Nenhum teste usa rede ou Docker; se um precisar, está errado.
4. Se mexeu em `frontend/`: `cd frontend && npx ng build` — sem erros. Use `npx ng`, nunca uma CLI global (o projeto é Angular 20).

### 2. Banco isolado

Para não sujar a massa real, suba tudo contra um banco descartável. A variável tem de valer nos **três** lugares (seed, scan e API); o nome é relativo e resolve contra `%LOCALAPPDATA%\CheckApps`:

```bash
export Sqlite__DatabasePath=e2e.db
dotnet run --project tools/CheckApps.Seed
dotnet run --project tools/CheckApps.GitHubScan -- --sigla ab1,cd2 --source fake    # só se a tela de Repositórios entra no roteiro
dotnet run --project src/CheckApps.Api --no-build --urls http://localhost:5232 &
```

Frontend como em `run-local` (`npx ng serve --port 4200 &` dentro de `frontend/`). Checagem: `curl -sf http://localhost:5232/api/squads` e `curl -sf http://localhost:4200`.

Se for usar o banco real de propósito, diga isso ao usuário antes e remova o resíduo que criar.

### 3. Endpoints (`curl`)

Exercite cada endpoint novo ou alterado, **incluindo os caminhos de erro**: 201/200/204, 400 (input inválido), 404 (recurso inexistente), 409 (conflito). Para integração externa vale a convenção do `backend.md`: 400, 409 e 502.

### 4. Navegador (Playwright)

O Playwright **não está no repo**. Instale numa pasta fora dele e aponte `executablePath` para o Chromium já baixado em `%LOCALAPPDATA%\ms-playwright\chromium-*`.

O roteiro percorre o fluxo inteiro da mudança e confere **zero erros de console** (exceto erros esperados, como o 409 de um teste de conflito — cite-os). Armadilhas:

- Escope seletores de texto a `main` (`main >> text=…`, `main select`); o seletor de squad do menu lateral tem um `<option>` por squad e casa com `text=Squad X`.
- Gaveta do celular fora da tela ainda é "visível" para `isVisible()`: meça `boundingBox().x` (negativo = fechada) e espere a transição de 0,2 s antes de medir aberta.
- Datas: confira que períodos aparecem com o mês certo (`DatePipe` com `'UTC'`).

Se mudou algo estrutural no shell (menu, rotas), repita também menu recolhido, recarregar a página e largura de celular.

### 5. Confirmação do usuário

Dizer "funciona" não basta: peça ao usuário que confirme **na tela** e registre a data no checklist (`Confirmação do usuário na tela (AAAA-MM-DD)`). Enquanto não confirmar, a spec fica "implementada, falta confirmação" e a checklist mantém o item desmarcado.

### 6. Limpeza

Pare API e frontend (`run-local`, seção "Parar tudo") e apague o banco descartável: `rm -f "$LOCALAPPDATA/CheckApps/e2e.db"*` (inclui `-wal` e `-shm`). Remova a variável: `unset Sqlite__DatabasePath`.

## Limites

- Esta skill verifica, não corrige: se algo falhar, relate com a saída e proponha a correção.
- Não marque item da checklist que não foi de fato executado.
- Não faz commit; isso só quando o usuário pedir.
