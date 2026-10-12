# CheckApps — Roadmap por estágios

A evolução do produto segue uma sequência deliberada. Cada estágio só faz sentido depois do anterior estar provado com usuários reais. **Não** construir infraestrutura de um estágio futuro para "preparar" — só evitar decisões que impeçam chegar lá (ver princípios em `vision.md`).

```
Inventário → Health → Evolution → Context → Intelligence → AI
    ✅          ✅         ✅          ○            ○         ○
```

## ✅ Inventário

Saber quais apps existem, de qual squad, com quais repositórios e projetos Sonar.

- Entregue no MVP (`specs/001-mvp-core/`) via seed de dados de demonstração.
- Cadastro manual (criar/editar/inativar/remover squads e apps, links corporativos por app) entregue em `specs/002-squad-app-management/`.
- Ainda pendente: cadastro manual de Repository e SonarProject (adiado de propósito — hoje só via seed). Ver `backlog.md`.

## ✅ Health

Saber como está a qualidade atual de cada app: métricas (coverage, bugs, vulnerabilities, code smells, duplications, ratings) e um Quality Level derivado por regra.

- Entregue no MVP. Dados vêm de um `MockSonarProvider`; a integração real com o SonarQube é o próximo passo natural deste estágio (ver `backlog.md`).
- As regras de Quality Assessment (v1) são simples e versionadas — a expectativa é que evoluam (v2, v3…) sem invalidar snapshots antigos.

## ✅ Evolution

Saber se os apps estão melhorando ou piorando ao longo do tempo, e quais indicadores explicam a mudança.

- Entregue no MVP: histórico de `QualitySnapshot` por app, gráfico de evolução do Quality Level, tabela de histórico de métricas, tendência por app (melhorou / piorou / estável) e distribuição por Quality Level no dashboard da squad.
- Limitação conhecida: a evolução agregada da squad é calculada em memória a cada request, não pré-agregada. Adequado à escala atual; revisitar só se virar gargalo.

## ✅ Checagens (fora da sequência)

Não é um estágio: é a primeira feature operacional — acompanhar, app a app, uma demanda que atinge vários apps (substitui a planilha). Entregue em `specs/003-checagens/` (2026-10-12), junto com a navegação global. A metade "detectar" (pré-preencher status a partir dos repositórios) depende do leitor de GitHub; ver `backlog.md`.

## ✅ Leitor de GitHub, fase 1 (fora da sequência)

Primeiro conector real do produto: descobre repositórios por sigla e os coloca numa caixa de entrada, onde a pessoa decide criar app, vincular, marcar como de outra squad ou ignorar — o scan nunca cria app. Entregue em `specs/005-leitor-github-fase-1/` (ADR-010). Dependências de biblioteca (fase 2) e entre apps (fase 3) seguem no `backlog.md`.

**O MVP termina aqui.** Os estágios abaixo são direção, não compromisso.

## ○ Context

Relacionar um App a mais do que Squad/Repository/Sonar: Produto, Operação de negócio, Jornadas, Dependências, Integrações, Documentação, Arquitetura.

- É o estágio em que o CheckApps deixa de ser "dashboard de qualidade" e vira "camada de contexto".
- A persistência já foi revisitada por conta disso: o single-table DynamoDB não atendia bem consultas que cruzam entidades, e o projeto migrou para SQLite (ADR-009), com Postgres previsto quando o modelo estabilizar. Joins e agregações deixaram de ser restrição.
- Pré-requisito prático: integrações reais (Sonar, catálogo corporativo de apps) para que o contexto não dependa de cadastro manual.
- **Porta de entrada provável (2026-10)**: o **leitor de repositórios GitHub** — primeiro conector real do produto. Em fases: inventário assistido (metadados, dono), dependências de biblioteca, e por último dependências entre apps. É a fonte que viabiliza o **mapa de aplicações** sem que ele vire planilha desenhada. Detalhes em `backlog.md`.

## ○ Intelligence

Análises que cruzam contexto: quais jornadas são impactadas por um app com qualidade em queda, qual operação de negócio depende de uma aplicação crítica, onde a arquitetura está frágil.

- Depende inteiramente do estágio Context existir com dados reais.

## ○ AI

Agentes e RAG operando sobre a camada de contexto: gerar SDD, interagir com Harness/CI/CD, responder perguntas em linguagem natural sobre o ecossistema.

- É o motivo de o modelo de dados ter sido desenhado com identidade estável, relações explícitas, proveniência e temporalidade desde o MVP.
- **Ajuste de 2026-10**: este estágio pode começar antes do previsto, mas não como IA dentro do CheckApps — e sim como o CheckApps **alimentando as ferramentas de IA que o time já usa** (Claude, Copilot, Devin) via servidor MCP, expondo consultas determinísticas. É barato, não traz custo de modelo nem questão nova de governança, e entrega valor assim que houver dados reais (leitor de GitHub, qualidade, checagens). Agentes e RAG próprios continuam dependendo de Context e Intelligence provados. Ver "AI-friendly não é ter IA dentro" em `vision.md`.

## O que está explicitamente fora de qualquer estágio próximo

Microservices, Kubernetes, event bus, vector database, knowledge graph, integração com Jira, automação de SDD, arquitetura distribuída complexa. Podem aparecer em estágios futuros se um requisito concreto pedir — não antes.
