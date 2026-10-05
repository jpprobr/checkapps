---
name: run-local
description: Sobe o ambiente local completo do CheckApps (seed de dados, API .NET e frontend Angular), ou reseta os dados de demonstração. Use quando pedirem para rodar, subir, iniciar, reiniciar ou resetar o ambiente, ou antes de verificar uma mudança no navegador.
compatibility: .NET SDK 10, Node 22 e npm. Shell bash (Git Bash) ou PowerShell. Não requer Docker.
---

# Subir o ambiente local do CheckApps

Sequência verificada. Cada passo tem uma checagem; não avance sem ela passar.

Persistência é SQLite em arquivo (ADR-009) — **não há banco para subir, nem Docker**.

## 1. Seed de dados de demonstração

```bash
cd <raiz-do-repo>
dotnet run --project tools/CheckApps.Seed
```

Cria o schema se não existir e grava 2 squads / 5 apps / 3 meses de histórico. Idempotente. A saída termina com um resumo — confira que aparecem `Squad Pagamentos (sq-1): 3 apps` e `Squad Plataforma (sq-2): 2 apps`.

Diferente do DynamoDB Local de antes, **os dados persistem entre reinícios**: só rode o seed na primeira vez ou quando quiser resetar.

O arquivo do banco fica em `%LOCALAPPDATA%\CheckApps\checkapps.db` (ou equivalente no SO), não na pasta do repositório — caminho resolvido de forma determinística, independente de onde o comando é executado.

## 2. API

Antes de iniciar, garanta que não há outra instância presa na porta (ela travaria o `dotnet build`):

```bash
netstat -ano | grep ':5232'          # se listar um PID, encerre: taskkill //PID <pid> //F
dotnet build                          # deve terminar com 0 erros
dotnet run --project src/CheckApps.Api --no-build --urls http://localhost:5232 &
timeout 30 bash -c 'until curl -sf http://localhost:5232/api/squads >/dev/null; do sleep 1; done' && echo "API UP"
```

Se `dotnet run` for iniciado sem `--urls`, o `launchSettings.json` escolhe outra porta e o frontend não encontra a API.

## 3. Frontend

```bash
cd frontend
npm install                           # só na primeira vez ou após mudar package.json
npx ng serve --port 4200 &
timeout 120 bash -c 'until curl -sf http://localhost:4200 >/dev/null; do sleep 3; done' && echo "NG UP"
```

Abrir `http://localhost:4200`. A landing lista as duas squads.

## Reset dos dados (apagar tudo e voltar ao estado de demo)

Apps com histórico não podem ser removidos pela API (por design). Para limpar dados de teste, apague o arquivo e rode o seed:

```bash
rm -f "$LOCALAPPDATA/CheckApps/checkapps.db"*    # inclui -wal e -shm
dotnet run --project tools/CheckApps.Seed
```

## Parar tudo

```bash
for pid in $(netstat -ano | grep -E ':5232|:4200' | awk '{print $NF}' | sort -u); do
  [ "$pid" != "0" ] && taskkill //PID $pid //F
done
```

## Sintomas conhecidos

| Sintoma | Causa | Ação |
|---|---|---|
| `MSB3027 … arquivo bloqueado por CheckApps.Api` no build | API antiga ainda rodando | Passo 2, primeira linha |
| API sobe em `:5232` mas frontend mostra erro de carga | CORS só aceita `http://localhost:4200` | Use exatamente essa origem |
| API e seed parecem ver dados diferentes | Alguém configurou `Sqlite:DatabasePath` relativo esperando que resolvesse pelo diretório atual | O caminho relativo resolve contra `%LOCALAPPDATA%\CheckApps`, de propósito — veja `SqliteOptions` |
| Datas no frontend um mês atrás | `DatePipe` sem `'UTC'` | Ver `.agents/rules/frontend.md` |
| `ng serve` reclama de versão do Node | Angular CLI 22 instalada por engano | Projeto é Angular 20; use `npx ng`, não uma CLI global |
