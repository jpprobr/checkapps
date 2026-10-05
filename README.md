# CheckApps

MVP para visualizar a composição de apps de uma squad, acompanhar a saúde e evolução
histórica da qualidade dessas aplicações (dados de Sonar, mockados neste MVP) e
gerenciar manualmente o cadastro de squads e apps (criar, editar, inativar, remover).

- **Specs por feature**: `specs/` — `001-mvp-core` (inventário, health, evolution) e
  `002-squad-app-management` (cadastro de squads/apps, links corporativos). Feature nova
  começa copiando `specs/_template/`.
- **Contexto para agentes de IA e pessoas chegando agora**: `AGENTS.md` (porta de entrada),
  `.agents/product/` (visão, roadmap), `.agents/architecture/` (estrutura, modelo de dados,
  ADRs), `.agents/rules/`, `.agents/project/STATE.md` (estado atual).

## Stack

- Backend: .NET 10 / ASP.NET Core Web API, arquitetura em módulos (Domain / Application /
  Infrastructure / Api)
- Persistência: SQLite com Dapper e SQL explícito (ver ADR-009; Postgres quando o modelo estabilizar)
- Frontend: Angular 20 (standalone components, sem NgModules)
- Dados Sonar: mockados (`MockSonarProvider`), trocáveis por uma integração real futura
  via a interface `ISonarProvider`

## Pré-requisitos

- .NET SDK 10
- Node.js 22+ e npm

## Rodando localmente

### 1. Popular a massa de dados de demonstração

```bash
dotnet run --project tools/CheckApps.Seed
```

Cria o schema (se não existir) e popula 2 squads e 5 apps com histórico de 3 meses,
cobrindo evolução positiva, negativa e estável, um app com múltiplos repositórios e
outro com múltiplos projetos Sonar.

### 2. Rodar a API

```bash
dotnet run --project src/CheckApps.Api --urls http://localhost:5232
```

Endpoints em `http://localhost:5232/api` (ver `src/CheckApps.Api/CheckApps.Api.http`
para exemplos, ou o Swagger/OpenAPI em `/openapi/v1.json` em ambiente Development).

### 3. Rodar o frontend

```bash
cd frontend
npm install
npm start   # ng serve, http://localhost:4200
```

O frontend espera a API em `http://localhost:5232/api` (`frontend/src/app/core/api-config.ts`).

## Testes

```bash
dotnet test tests/CheckApps.UnitTests            # regras de domínio, assessment engine, seed data, agregação de squad
dotnet test tests/CheckApps.IntegrationTests      # repositórios SQLite — banco temporário por classe, sem dependência externa
```

## Estrutura

```
src/
  CheckApps.Domain          entidades e regras de negócio (Quality Assessment)
  CheckApps.Application     casos de uso e portas (interfaces de persistência/Sonar)
  CheckApps.Infrastructure  SQLite (Dapper), MockSonarProvider, gerador de massa de dados
  CheckApps.Api             ASP.NET Core Web API (controllers, DTOs)
tests/
  CheckApps.UnitTests
  CheckApps.IntegrationTests
tools/
  CheckApps.Seed             popula o banco com dados de demonstração
frontend/                    Angular 20
```
