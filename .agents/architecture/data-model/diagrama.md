# Diagrama do modelo de dados

> **Fonte da verdade**: `src/CheckApps.Infrastructure/Sqlite/schema.sql`. Os diagramas abaixo mostram as relações e as colunas-chave; as colunas completas e as decisões estão em `schema.sql` e em `sqlite-schema.md`. Se divergirem, o `schema.sql` está certo. **Mudou uma tabela, uma chave ou uma relação? Atualize o diagrama da área na mesma mudança.**
>
> Diagramas em Mermaid: o GitHub e o VS Code (com a extensão *Markdown Preview Mermaid Support*) renderizam direto.

**Como ler**

- Linha **cheia** (`--`): chave estrangeira (FK) no banco.
- Linha **pontilhada** (`..`): ligação **lógica**, sem FK, de propósito. O rótulo diz por quê. Em geral é um fato ou uma decisão que precisa sobreviver a quem ele aponta.
- `||` exatamente um, `o|` zero ou um, `o{` zero ou muitos, `|{` um ou muitos.
- Os valores de data são TEXT ISO-8601 UTC; os ids são gerados na aplicação.

## 1. Visão geral

Só as tabelas-chave e as ligações entre as áreas. Cada área tem o seu detalhe nas seções seguintes.

```mermaid
erDiagram
    squads ||--o{ apps : "tem"
    apps ||--o{ repositories : "tem"
    apps ||--o{ sonar_projects : "tem"
    apps ||--o{ quality_snapshots : "histórico de qualidade"
    repositories ||--o{ repository_analyses : "uma por commit analisado"
    repository_analyses ||--o{ repository_dependencies : "fatos"
    repository_analyses ||--o{ repository_integrations : "fatos"
    repository_analyses ||--o{ repository_api_endpoints : "fatos"
    apps ||--o{ host_links : "host aponta para o app"
    checks ||--o{ check_items : "apps da checagem"
    apps ||--o{ check_items : "está em"
    squads |o--o{ journeys : "dona (opcional)"
    journeys |o--o{ journeys : "pai"
    journeys ||--o{ journey_steps : "passos"
    apps ||--o{ journey_steps : "usado em"
    discovered_repositories }o..o| repositories : "mesmo (provider, external_id), sem FK"
```

## 2. Catálogo e qualidade (specs 001, 002)

O App não guarda estado de qualidade: o nível atual é o snapshot mais recente, derivado por consulta (ADR-009).

```mermaid
erDiagram
    squads {
        string id PK
        string name
        string status
    }
    apps {
        string id PK
        string squad_id FK
        string name
        string criticality
        string status
    }
    app_tags {
        string app_id PK, FK
        string tag PK
    }
    app_links {
        string id PK
        string app_id FK
        string description
        string uri
    }
    repositories {
        string id PK
        string app_id FK
        string name
        string provider
        string sigla
        string sigla_app "único quando presente"
        string role
    }
    sonar_projects {
        string id PK
        string app_id FK
        string project_key
        string source
    }
    quality_snapshots {
        string id PK
        string app_id FK
        string sonar_project_id "sem FK"
        string collected_at
        float coverage
        string quality_level "congelado na coleta"
        string assessment_rules_version
    }

    squads ||--o{ apps : "tem"
    apps ||--o{ app_tags : "tags"
    apps ||--o{ app_links : "links"
    apps ||--o{ repositories : "repositórios"
    apps ||--o{ sonar_projects : "projetos Sonar"
    apps ||--o{ quality_snapshots : "imutável (ADR-004)"
    sonar_projects ||..o{ quality_snapshots : "sonar_project_id, sem FK: há massa apontando para projeto inexistente"
```

## 3. Repositórios, análises, integrações e contratos (specs 005, 013, 014, 017, 019, 021)

Uma análise é uma foto de um repositório num commit. A análise **corrente** é a mais recente que não falhou, derivada por consulta. O que mudou entre dois commits é calculado na leitura, nunca gravado.

```mermaid
erDiagram
    discovered_repositories {
        string id PK "github e o id externo"
        string provider
        string external_id "UNIQUE com provider"
        string name
        string sigla
        string status "new, ignored ou other-squad"
    }
    repositories {
        string id PK
        string app_id FK
        string provider
        string external_id
        string role
    }
    repository_analyses {
        string id PK
        string repository_id FK
        string analyzed_at
        string commit_sha
        string status "ok, parcial, sem-scanner ou falha"
    }
    repository_dependencies {
        string id PK
        string analysis_id FK
        string ecosystem
        string kind
        string name
        string version
        string confidence
    }
    repository_integrations {
        string id PK
        string analysis_id FK
        string mechanism
        string host "normalizado, nunca o valor bruto"
        string reference
        string state
        string environment_kind
    }
    repository_api_endpoints {
        string id PK
        string analysis_id FK
        string method
        string path
        string path_key "rota com parâmetros neutralizados"
        string declared_id
    }
    repository_api_fields {
        string id PK
        string endpoint_id FK
        string location
        string path
        string type
        int required
    }
    host_links {
        string id PK
        string host_key "host e porta, pode ser modelo com env"
        string app_id FK
        string state "confirmado ou rejeitado"
        string origin "HUMAN"
    }
    host_externals {
        string id PK
        string host_key "UNIQUE"
        string name
        string origin "HUMAN"
    }
    apps {
        string id PK
    }

    apps ||--o{ repositories : "repositórios"
    repositories ||--o{ repository_analyses : "uma por commit"
    repository_analyses ||--o{ repository_dependencies : "pacotes e versão do .NET"
    repository_analyses ||--o{ repository_integrations : "hosts chamados"
    repository_analyses ||--o{ repository_api_endpoints : "endpoints do contrato"
    repository_api_endpoints ||--o{ repository_api_fields : "campos achatados"
    apps ||--o{ host_links : "decisão humana"
    discovered_repositories }o..o| repositories : "mesmo (provider, external_id): o descoberto precede o vínculo"
    repository_integrations }o..o{ host_links : "host = host_key, sem FK: a decisão sobrevive à reanálise"
    repository_integrations }o..o| host_externals : "host = host_key, sem FK: exclusão mútua com vínculo é regra do serviço"
```

## 4. Checagens e jornadas (specs 003, 022)

Os passos de uma jornada apontam para a **identidade estável** de um endpoint (app + método + `path_key`), nunca para a linha de uma análise. A resolução contra o contrato e o impacto das mudanças são calculados na leitura.

```mermaid
erDiagram
    checks {
        string id PK "slug"
        string title
        string status "open ou closed"
        string created_at
        string closed_at
    }
    check_items {
        string check_id PK, FK
        string app_id PK, FK
        string status
        string notes
    }
    check_events {
        string id PK
        string check_id FK
        string app_id "sem FK"
        string type
        string from_status
        string to_status
        string occurred_at
    }
    journeys {
        string id PK "slug"
        string name
        string level "texto livre"
        string squad_id FK "opcional"
        string parent_id FK "opcional"
        string source
    }
    journey_tags {
        string journey_id PK, FK
        string tag PK
    }
    journey_steps {
        string id PK
        string journey_id FK
        int position "UNIQUE com journey_id"
        string app_id FK
        string method
        string path_key "identidade estável do endpoint"
    }
    apps {
        string id PK
    }
    squads {
        string id PK
    }
    repository_api_endpoints {
        string app_method_pathkey "identidade, não id"
    }

    checks ||--o{ check_items : "sem cascata: checagem com itens não é removida"
    apps ||--o{ check_items : "sem cascata: app em checagem não é removido"
    checks ||--o{ check_events : "só de acréscimo"
    apps |o..o{ check_events : "app_id, sem FK: o evento sobrevive à remoção do app"
    squads |o--o{ journeys : "sem cascata"
    journeys |o--o{ journeys : "pai, sem cascata"
    journeys ||--o{ journey_tags : "tags"
    journeys ||--o{ journey_steps : "só as folhas têm passos"
    apps ||--o{ journey_steps : "sem cascata: app em passo não é removido"
    journey_steps }o..o| repository_api_endpoints : "resolvido na leitura contra o contrato corrente"
```

## Regras de remoção, em uma olhada

| Ao remover… | O que acontece |
|---|---|
| **squad** | Recusado (409) se tiver apps ou jornadas |
| **app** | Recusado (409) se tiver snapshot de qualidade, estiver numa checagem ou num passo de jornada. Sem isso, leva tags, links, repositórios, projetos Sonar, vínculos de host e análises |
| **checagem** | Só vazia; leva o histórico dela |
| **jornada** | Leva tags e passos; recusado (409) se tiver filhas |
| **repositório** | Leva as análises e tudo que elas contêm |
