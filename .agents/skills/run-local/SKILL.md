---
name: run-local
description: Sobe o ambiente local completo do CheckApps (DynamoDB Local via Docker, seed de dados, API .NET e frontend Angular) com as checagens necessárias, ou reseta os dados de demonstração. Use quando pedirem para rodar, subir, iniciar, reiniciar ou resetar o ambiente, ou antes de verificar uma mudança no navegador.
compatibility: Windows com Docker Desktop, .NET SDK 10, Node 22 e npm. Shell bash (Git Bash) ou PowerShell.
---

# Subir o ambiente local do CheckApps

Sequência verificada. Cada passo tem uma checagem; não avance sem ela passar.

## 1. Docker Desktop

```bash
docker ps
```

Se falhar com `open //./pipe/dockerDesktopLinuxEngine: O sistema não pode encontrar o arquivo`, o Docker Desktop não está rodando:

```bash
"/c/Program Files/Docker/Docker/Docker Desktop.exe" &
timeout 90 bash -c 'until docker ps >/dev/null 2>&1; do sleep 3; done' && echo "DOCKER UP"
```

## 2. DynamoDB Local

```bash
cd <raiz-do-repo>
docker compose up -d
docker ps --filter name=checkapps-dynamodb-local --format "{{.Status}}"   # deve mostrar "Up …"
```

**O container roda em `-inMemory`.** Se ele foi recriado ou a máquina reiniciou, os dados sumiram — o passo 3 é obrigatório. Se o container já estava `Up` há tempo e ninguém reiniciou, os dados provavelmente ainda estão lá e o passo 3 é opcional (mas inofensivo).

## 3. Seed de dados de demonstração

```bash
dotnet run --project tools/CheckApps.Seed
```

Cria a tabela `CheckApps` se não existir e grava 2 squads / 5 apps / 3 meses de histórico. Idempotente. A saída termina com um resumo por squad — confira que aparecem `Squad Pagamentos (sq-1): 3 apps` e `Squad Plataforma (sq-2): 2 apps`.

## 4. API

Antes de iniciar, garanta que não há outra instância presa na porta (ela travaria o `dotnet build`):

```bash
netstat -ano | grep ':5232'          # se listar um PID, encerre: taskkill //PID <pid> //F
dotnet build                          # deve terminar com 0 erros
dotnet run --project src/CheckApps.Api --no-build --urls http://localhost:5232 &
timeout 30 bash -c 'until curl -sf http://localhost:5232/api/squads >/dev/null; do sleep 1; done' && echo "API UP"
```

Se `dotnet run` for iniciado sem `--urls`, o `launchSettings.json` escolhe outra porta e o frontend não encontra a API.

## 5. Frontend

```bash
cd frontend
npm install                           # só na primeira vez ou após mudar package.json
npx ng serve --port 4200 &
timeout 90 bash -c 'until curl -sf http://localhost:4200 >/dev/null; do sleep 2; done' && echo "NG UP"
```

Abrir `http://localhost:4200`. A landing lista as duas squads.

## Reset dos dados (apagar tudo e voltar ao estado de demo)

Apps com histórico não podem ser removidos pela API (por design). Para limpar dados de teste, reinicie o container e rode o seed de novo:

```bash
docker compose restart
sleep 3
dotnet run --project tools/CheckApps.Seed
```

## Parar tudo

```bash
for pid in $(netstat -ano | grep -E ':5232|:4200' | awk '{print $NF}' | sort -u); do
  [ "$pid" != "0" ] && taskkill //PID $pid //F
done
docker compose down                   # opcional; deixa o container parado (dados somem)
```

## Sintomas conhecidos

| Sintoma | Causa | Ação |
|---|---|---|
| `ResourceNotFoundException: Cannot do operations on a non-existent table` | Tabela não existe (container recriado) | Passo 3 |
| `MSB3027 … arquivo bloqueado por CheckApps.Api` no build | API antiga ainda rodando | Passo 4, primeira linha |
| API sobe em `:5232` mas frontend mostra erro de carga | CORS só aceita `http://localhost:4200` | Use exatamente essa origem |
| Datas no frontend um mês atrás | `DatePipe` sem `'UTC'` | Ver `.agents/rules/frontend.md` |
| `ng serve` reclama de versão do Node | Angular CLI 22 instalada por engano | Projeto é Angular 20; use `npx ng`, não uma CLI global |
