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

CREATE TABLE IF NOT EXISTS repositories (
    id          TEXT PRIMARY KEY,
    app_id      TEXT NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
    name        TEXT NOT NULL,
    provider    TEXT NULL,
    url         TEXT NULL,
    external_id TEXT NULL,
    status      TEXT NULL
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

CREATE INDEX IF NOT EXISTS ix_apps_squad_id
    ON apps (squad_id);

-- Serve tanto o histórico por período quanto a busca do snapshot mais recente,
-- que é como o estado de qualidade atual de um App é derivado.
CREATE INDEX IF NOT EXISTS ix_quality_snapshots_app_collected
    ON quality_snapshots (app_id, collected_at DESC);
