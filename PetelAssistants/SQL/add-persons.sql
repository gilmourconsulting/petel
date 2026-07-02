-- =============================================================================
-- PetelAssistants — Person domain (identity, versioned details, address/phone history)
-- Run after bootstrap.sql, add-years-and-menu.sql, and add-user-management.sql.
-- Idempotent: safe to re-run.
-- =============================================================================

-- ─── phone_types (shared_schema) ─────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'shared_schema' AND tablename = 'phone_types'
    ) THEN
        CREATE TABLE shared_schema.phone_types (
            id           SERIAL PRIMARY KEY,
            code         VARCHAR(50)  NOT NULL UNIQUE,
            display_name VARCHAR(100) NOT NULL,
            sort_order   INTEGER NOT NULL DEFAULT 0,
            is_active    BOOLEAN NOT NULL DEFAULT true
        );

        INSERT INTO shared_schema.phone_types (code, display_name, sort_order)
        VALUES
            ('mobile', E'\u05e0\u05d9\u05d9\u05d3', 10),
            ('home',   E'\u05d1\u05d9\u05ea',     20),
            ('work',   E'\u05e2\u05d1\u05d5\u05d3\u05d4', 30)
        ON CONFLICT (code) DO NOTHING;

        RAISE NOTICE 'Table phone_types created and seeded';
    ELSE
        RAISE NOTICE 'Table phone_types already exists';
    END IF;
END $$;

-- ─── persons (assist_schema) ──────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'assist_schema' AND tablename = 'persons'
    ) THEN
        CREATE TABLE assist_schema.persons (
            id          SERIAL PRIMARY KEY,
            entity_id   INTEGER NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            id_number   VARCHAR(100) NOT NULL,
            id_type     INTEGER NOT NULL DEFAULT 0,
            created_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id     INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT uq_persons_entity_id_number UNIQUE (entity_id, id_number)
        );

        CREATE INDEX idx_persons_entity_id ON assist_schema.persons(entity_id);

        RAISE NOTICE 'Table persons created';
    ELSE
        RAISE NOTICE 'Table persons already exists';
    END IF;
END $$;

-- ─── person_details (assist_schema) ───────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'assist_schema' AND tablename = 'person_details'
    ) THEN
        CREATE TABLE assist_schema.person_details (
            id              SERIAL PRIMARY KEY,
            entity_id       INTEGER NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            person_id       INTEGER NOT NULL REFERENCES assist_schema.persons(id) ON DELETE CASCADE,
            version         INTEGER NOT NULL DEFAULT 0,
            is_last_version BOOLEAN NOT NULL DEFAULT true,
            start_date      DATE NOT NULL,
            end_date        DATE NULL,
            first_name      VARCHAR(100) NOT NULL,
            last_name       VARCHAR(100) NOT NULL,
            gender          INTEGER NULL DEFAULT 0,
            date_of_birth   DATE NULL,
            email           VARCHAR(255) NULL,
            position        VARCHAR(100) NULL,
            created_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id         INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user     INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL
        );

        CREATE INDEX idx_person_details_entity_id ON assist_schema.person_details(entity_id);
        CREATE INDEX idx_person_details_person_id ON assist_schema.person_details(person_id);
        CREATE UNIQUE INDEX uq_person_details_last_version
            ON assist_schema.person_details (person_id)
            WHERE is_last_version = true;

        RAISE NOTICE 'Table person_details created';
    ELSE
        RAISE NOTICE 'Table person_details already exists';
    END IF;
END $$;

-- ─── person_addresses (assist_schema) ───────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'assist_schema' AND tablename = 'person_addresses'
    ) THEN
        CREATE TABLE assist_schema.person_addresses (
            id           SERIAL PRIMARY KEY,
            entity_id    INTEGER NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            person_id    INTEGER NOT NULL REFERENCES assist_schema.persons(id) ON DELETE CASCADE,
            street       VARCHAR(255) NULL,
            house_number VARCHAR(20) NULL,
            city         VARCHAR(100) NULL,
            post_code    VARCHAR(20) NULL,
            is_active    BOOLEAN NOT NULL DEFAULT true,
            start_date   DATE NOT NULL,
            end_date     DATE NULL,
            created_at   TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id      INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at   TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user  INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL
        );

        CREATE INDEX idx_person_addresses_entity_id ON assist_schema.person_addresses(entity_id);
        CREATE INDEX idx_person_addresses_person_id ON assist_schema.person_addresses(person_id);
        CREATE UNIQUE INDEX uq_person_addresses_active
            ON assist_schema.person_addresses (person_id)
            WHERE is_active = true;

        RAISE NOTICE 'Table person_addresses created';
    ELSE
        RAISE NOTICE 'Table person_addresses already exists';
    END IF;
END $$;

-- ─── person_phones (assist_schema) ───────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'assist_schema' AND tablename = 'person_phones'
    ) THEN
        CREATE TABLE assist_schema.person_phones (
            id                  SERIAL PRIMARY KEY,
            entity_id           INTEGER NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            person_id           INTEGER NOT NULL REFERENCES assist_schema.persons(id) ON DELETE CASCADE,
            phone_type_id       INTEGER NOT NULL REFERENCES shared_schema.phone_types(id) ON DELETE RESTRICT,
            phone_number_prefix VARCHAR(7) NULL,
            phone_number        VARCHAR(100) NULL,
            is_active           BOOLEAN NOT NULL DEFAULT true,
            start_date          DATE NOT NULL,
            end_date            DATE NULL,
            created_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id             INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user         INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL
        );

        CREATE INDEX idx_person_phones_entity_id ON assist_schema.person_phones(entity_id);
        CREATE INDEX idx_person_phones_person_id ON assist_schema.person_phones(person_id);
        CREATE UNIQUE INDEX uq_person_phones_active_per_type
            ON assist_schema.person_phones (person_id, phone_type_id)
            WHERE is_active = true;

        RAISE NOTICE 'Table person_phones created';
    ELSE
        RAISE NOTICE 'Table person_phones already exists';
    END IF;
END $$;
