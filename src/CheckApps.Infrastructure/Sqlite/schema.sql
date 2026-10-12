-- Schema do CheckApps. Fonte de criação, não documentação paralela: é aplicado
-- de forma idempotente no bootstrap (SqliteSchemaBootstrapper).
--
-- Portabilidade (ADR-009): datas em TEXT ISO-8601 UTC, ids gerados na aplicação,
-- tipos declarados explicitamente, nada específico do SQLite. Este arquivo é o que
-- será submetido ao processo corporativo de modelagem quando migrarmos para Postgres.

CREATE TABLE IF NOT EXISTS squads (
    id          TEXT PRIMARY KEY,
    name        TEXT NOT NULL,
    description TEXT NULL,
    status      TEXT NULL
);

CREATE TABLE IF NOT EXISTS apps (
    id          TEXT PRIMARY KEY,
    squad_id    TEXT NOT NULL REFERENCES squads(id),
    name        TEXT NOT NULL,
    description TEXT NULL,
    criticality TEXT NULL,
    status      TEXT NULL,
    external_id TEXT NULL
);

-- Tags normalizadas (e não CSV/JSON) porque o backlog prevê filtrar por convenção
-- de tag (solucao:, topologia:) — retrofit depois seria caro.
CREATE TABLE IF NOT EXISTS app_tags (
    app_id TEXT NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
    tag    TEXT NOT NULL,
    PRIMARY KEY (app_id, tag)
);

-- sigla, sigla_app, role e source (spec 005) também são acrescentadas por
-- SqliteSchemaBootstrapper a bancos criados antes delas: CREATE TABLE IF NOT EXISTS não
-- altera uma tabela existente. O índice único de sigla_app mora lá, pelo mesmo motivo.
CREATE TABLE IF NOT EXISTS repositories (
    id          TEXT PRIMARY KEY,
    app_id      TEXT NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
    name        TEXT NOT NULL,
    provider    TEXT NULL,
    url         TEXT NULL,
    external_id TEXT NULL,
    status      TEXT NULL,
    sigla       TEXT NULL,
    sigla_app   TEXT NULL,
    role        TEXT NULL,
    source      TEXT NULL
);

CREATE TABLE IF NOT EXISTS sonar_projects (
    id          TEXT PRIMARY KEY,
    app_id      TEXT NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
    project_key TEXT NOT NULL,
    name        TEXT NULL,
    url         TEXT NULL,
    external_id TEXT NULL,
    status      TEXT NULL,
    source      TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS app_links (
    id          TEXT PRIMARY KEY,
    app_id      TEXT NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
    description TEXT NOT NULL,
    uri         TEXT NOT NULL
);

-- Imutável por contrato (ADR-004): nunca editado, nunca recalculado. O quality_level
-- e a versão da regra são congelados no momento da coleta.
--
-- sonar_project_id NÃO tem FK de propósito: o DynamoDB nunca validou essa referência
-- e há massa (testes, backfills) com snapshots apontando para projetos inexistentes.
-- Adicionar a constraint seria mudança de comportamento, não migração.
CREATE TABLE IF NOT EXISTS quality_snapshots (
    id                       TEXT PRIMARY KEY,
    app_id                   TEXT NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
    sonar_project_id         TEXT NOT NULL,
    collected_at             TEXT NOT NULL,
    source                   TEXT NOT NULL,
    coverage                 REAL NOT NULL,
    bugs                     INTEGER NOT NULL,
    vulnerabilities          INTEGER NOT NULL,
    code_smells              INTEGER NOT NULL,
    duplications             REAL NOT NULL,
    reliability              TEXT NOT NULL,
    maintainability          TEXT NOT NULL,
    security                 TEXT NOT NULL,
    quality_level            TEXT NOT NULL,
    assessment_rules_version TEXT NOT NULL
);

-- Checagens (spec 003). As FKs de check_items NÃO cascateiam de propósito: um app
-- em checagem não pode ser removido (FR-014) e uma checagem com itens também não
-- (FR-012). Os serviços verificam antes e respondem 409; a FK é a rede de segurança.
CREATE TABLE IF NOT EXISTS checks (
    id          TEXT PRIMARY KEY,
    title       TEXT NOT NULL,
    description TEXT NULL,
    category    TEXT NULL,
    author      TEXT NULL,
    start_date  TEXT NULL,
    end_date    TEXT NULL,
    status      TEXT NOT NULL,
    created_at  TEXT NOT NULL,
    closed_at   TEXT NULL
);

CREATE TABLE IF NOT EXISTS check_items (
    check_id   TEXT NOT NULL REFERENCES checks(id),
    app_id     TEXT NOT NULL REFERENCES apps(id),
    status     TEXT NOT NULL,
    notes      TEXT NULL,
    updated_at TEXT NOT NULL,
    PRIMARY KEY (check_id, app_id)
);

-- Histórico só de acréscimo (FR-015), gravado na mesma transação da mudança.
-- app_id SEM FK, como quality_snapshots.sonar_project_id: o evento registra um fato
-- passado e deve sobreviver à remoção do app. Cascateia com a checagem, que só pode
-- ser removida vazia.
CREATE TABLE IF NOT EXISTS check_events (
    id          TEXT PRIMARY KEY,
    check_id    TEXT NOT NULL REFERENCES checks(id) ON DELETE CASCADE,
    app_id      TEXT NULL,
    type        TEXT NOT NULL,
    from_status TEXT NULL,
    to_status   TEXT NULL,
    occurred_at TEXT NOT NULL
);

-- Caixa de entrada do leitor de GitHub (spec 005). O scan nunca cria App: ele grava aqui, e a
-- pessoa decide. status guarda só o que não dá para derivar (new, ignored, other-squad); "já no
-- catálogo" é a existência de uma linha em repositories com o mesmo (provider, external_id).
-- Sem FK para repositories de propósito: o descoberto precede e sobrevive ao vínculo.
CREATE TABLE IF NOT EXISTS discovered_repositories (
    id            TEXT PRIMARY KEY,
    provider      TEXT NOT NULL,
    external_id   TEXT NOT NULL,
    name          TEXT NOT NULL,
    url           TEXT NULL,
    sigla         TEXT NOT NULL,
    sigla_app     TEXT NULL,
    archived      INTEGER NOT NULL,
    pushed_at     TEXT NULL,
    status        TEXT NOT NULL,
    first_seen_at TEXT NOT NULL,
    last_seen_at  TEXT NOT NULL,
    source        TEXT NOT NULL,
    UNIQUE (provider, external_id)
);

CREATE INDEX IF NOT EXISTS ix_apps_squad_id
    ON apps (squad_id);

-- Serve tanto o histórico por período quanto a busca do snapshot mais recente,
-- que é como o estado de qualidade atual de um App é derivado.
CREATE INDEX IF NOT EXISTS ix_quality_snapshots_app_collected
    ON quality_snapshots (app_id, collected_at DESC);

CREATE INDEX IF NOT EXISTS ix_check_items_app_id
    ON check_items (app_id);

CREATE INDEX IF NOT EXISTS ix_check_events_check_occurred
    ON check_events (check_id, occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_discovered_repositories_status_sigla
    ON discovered_repositories (status, sigla);
