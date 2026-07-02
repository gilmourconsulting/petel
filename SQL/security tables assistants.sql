-- Table: shared_schema.action_types

-- DROP TABLE IF EXISTS shared_schema.action_types;

CREATE TABLE IF NOT EXISTS shared_schema.action_types
(
    id smallint NOT NULL DEFAULT nextval('shared_schema.action_types_id_seq'::regclass),
    name character varying(50) COLLATE pg_catalog."default" NOT NULL,
    description character varying(255) COLLATE pg_catalog."default",
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    user_id integer DEFAULT 1,
    is_active boolean DEFAULT true,
    CONSTRAINT action_types_pkey PRIMARY KEY (id),
    CONSTRAINT action_types_name_key UNIQUE (name),
    CONSTRAINT action_types_user_id_fkey FOREIGN KEY (user_id)
        REFERENCES assist_schema.users (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS shared_schema.action_types
    OWNER to "PetelAdmin";

GRANT ALL ON TABLE shared_schema.action_types TO "PetelAdmin";

-- Table: shared_schema.actions

-- DROP TABLE IF EXISTS shared_schema.actions;

CREATE TABLE IF NOT EXISTS shared_schema.actions
(
    id integer NOT NULL DEFAULT nextval('shared_schema.actions_id_seq'::regclass),
    name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    display_name character varying(150) COLLATE pg_catalog."default",
    description character varying(255) COLLATE pg_catalog."default",
    action_type_id smallint NOT NULL,
    reference character varying(200) COLLATE pg_catalog."default",
    sort_order integer DEFAULT 0,
    is_active boolean DEFAULT true,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    user_id integer DEFAULT 1,
    onclick_name character varying(100) COLLATE pg_catalog."default",
    CONSTRAINT actions_pk PRIMARY KEY (id),
    CONSTRAINT actions_unique_pk UNIQUE (name),
    CONSTRAINT actions_action_type_id_fkey FOREIGN KEY (action_type_id)
        REFERENCES shared_schema.action_types (id) MATCH SIMPLE
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT actions_user_id_fkey FOREIGN KEY (user_id)
        REFERENCES assist_schema.users (id) MATCH SIMPLE
        ON UPDATE CASCADE
        ON DELETE SET NULL
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS shared_schema.actions
    OWNER to "PetelAdmin";

GRANT ALL ON TABLE shared_schema.actions TO "PetelAdmin";
-- Index: actions_action_type_id_idx

-- DROP INDEX IF EXISTS shared_schema.actions_action_type_id_idx;

CREATE INDEX IF NOT EXISTS actions_action_type_id_idx
    ON shared_schema.actions USING btree
    (action_type_id ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: actions_action_type_id_is_active_idx

-- DROP INDEX IF EXISTS shared_schema.actions_action_type_id_is_active_idx;

CREATE INDEX IF NOT EXISTS actions_action_type_id_is_active_idx
    ON shared_schema.actions USING btree
    (action_type_id ASC NULLS LAST, is_active ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: actions_is_active_idx

-- DROP INDEX IF EXISTS shared_schema.actions_is_active_idx;

CREATE INDEX IF NOT EXISTS actions_is_active_idx
    ON shared_schema.actions USING btree
    (is_active ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: actions_name_uq

-- DROP INDEX IF EXISTS shared_schema.actions_name_uq;

CREATE UNIQUE INDEX IF NOT EXISTS actions_name_uq
    ON shared_schema.actions USING btree
    (name COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: actions_reference_idx

-- DROP INDEX IF EXISTS shared_schema.actions_reference_idx;

CREATE INDEX IF NOT EXISTS actions_reference_idx
    ON shared_schema.actions USING btree
    (reference COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;

-- Table: assist_schema.roles

-- DROP TABLE IF EXISTS assist_schema.roles;

CREATE TABLE IF NOT EXISTS assist_schema.roles
(
    id integer NOT NULL DEFAULT nextval('assist_schema.roles_id_seq'::regclass),
    name character varying(50) COLLATE pg_catalog."default" NOT NULL,
    description text COLLATE pg_catalog."default",
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    update_user integer,
    entity_id integer,
    CONSTRAINT roles_pkey PRIMARY KEY (id),
    CONSTRAINT roles_update_user_fkey FOREIGN KEY (update_user)
        REFERENCES assist_schema.users (id) MATCH SIMPLE
        ON UPDATE CASCADE
        ON DELETE SET NULL
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS assist_schema.roles
    OWNER to postgres;

-- Trigger: set_timestamp_roles

-- DROP TRIGGER IF EXISTS set_timestamp_roles ON assist_schema.roles;

CREATE OR REPLACE TRIGGER set_timestamp_roles
    BEFORE UPDATE 
    ON assist_schema.roles
    FOR EACH ROW
    EXECUTE FUNCTION assist_schema.trigger_set_timestamp();
	
	

-- Table: assist_schema.roles_actions

-- DROP TABLE IF EXISTS assist_schema.roles_actions;

CREATE TABLE IF NOT EXISTS assist_schema.roles_actions
(
    id integer NOT NULL DEFAULT nextval('assist_schema.roles_actions_id_seq'::regclass),
    role_id integer NOT NULL,
    action_id integer NOT NULL,
    action_level integer NOT NULL DEFAULT 0,
    updated_at timestamp with time zone DEFAULT now(),
    update_user integer,
    entity_id integer,
    CONSTRAINT roles_actions_pkey PRIMARY KEY (id),
    CONSTRAINT uk_role_action UNIQUE (role_id, action_id),
    CONSTRAINT roles_actions_action_id_fkey FOREIGN KEY (action_id)
        REFERENCES shared_schema.actions (id) MATCH SIMPLE
        ON UPDATE CASCADE
        ON DELETE CASCADE,
    CONSTRAINT roles_actions_role_id_fkey FOREIGN KEY (role_id)
        REFERENCES assist_schema.roles (id) MATCH SIMPLE
        ON UPDATE CASCADE
        ON DELETE CASCADE,
    CONSTRAINT roles_actions_update_user_fkey FOREIGN KEY (update_user)
        REFERENCES assist_schema.users (id) MATCH SIMPLE
        ON UPDATE CASCADE
        ON DELETE SET NULL
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS assist_schema.roles_actions
    OWNER to postgres;
-- Index: idx_roles_actions_action_id

-- DROP INDEX IF EXISTS assist_schema.idx_roles_actions_action_id;

CREATE INDEX IF NOT EXISTS idx_roles_actions_action_id
    ON assist_schema.roles_actions USING btree
    (action_id ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_roles_actions_role_id

-- DROP INDEX IF EXISTS assist_schema.idx_roles_actions_role_id;

CREATE INDEX IF NOT EXISTS idx_roles_actions_role_id
    ON assist_schema.roles_actions USING btree
    (role_id ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
	
	
-- Table: assist_schema.action_audit_logs

-- DROP TABLE IF EXISTS assist_schema.action_audit_logs;

CREATE TABLE IF NOT EXISTS assist_schema.action_audit_logs
(
    id bigint NOT NULL DEFAULT nextval('assist_schema.action_audit_logs_id_seq'::regclass),
    user_id integer NOT NULL,
    action_name character varying(200) COLLATE pg_catalog."default" NOT NULL,
    screen_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    function_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    event_type character varying(50) COLLATE pg_catalog."default" NOT NULL,
    result character varying(20) COLLATE pg_catalog."default" NOT NULL,
    "timestamp" timestamp with time zone NOT NULL DEFAULT now(),
    ip_address character varying(45) COLLATE pg_catalog."default",
    action_params character varying(500) COLLATE pg_catalog."default",
    description character varying(1000) COLLATE pg_catalog."default",
    entity_id integer,
    CONSTRAINT action_audit_logs_pkey PRIMARY KEY (id),
    CONSTRAINT action_audit_logs_user_id_fkey FOREIGN KEY (user_id)
        REFERENCES assist_schema.users (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS assist_schema.action_audit_logs
    OWNER to postgres;

COMMENT ON TABLE assist_schema.action_audit_logs
    IS 'Audit log for all action authorization attempts – tracks GRANTED and DENIED access';

COMMENT ON COLUMN assist_schema.action_audit_logs.event_type
    IS 'Authorization type: ONCLICK_BUTTON, MENU_NAVIGATION, API_CALL, FILE_UPLOAD, etc.';

COMMENT ON COLUMN assist_schema.action_audit_logs.result
    IS 'Authorization result: GRANTED or DENIED';
-- Index: idx_audit_action

-- DROP INDEX IF EXISTS assist_schema.idx_audit_action;

CREATE INDEX IF NOT EXISTS idx_audit_action
    ON assist_schema.action_audit_logs USING btree
    (action_name COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_audit_event

-- DROP INDEX IF EXISTS assist_schema.idx_audit_event;

CREATE INDEX IF NOT EXISTS idx_audit_event
    ON assist_schema.action_audit_logs USING btree
    (event_type COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_audit_result

-- DROP INDEX IF EXISTS assist_schema.idx_audit_result;

CREATE INDEX IF NOT EXISTS idx_audit_result
    ON assist_schema.action_audit_logs USING btree
    (result COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_audit_timestamp

-- DROP INDEX IF EXISTS assist_schema.idx_audit_timestamp;

CREATE INDEX IF NOT EXISTS idx_audit_timestamp
    ON assist_schema.action_audit_logs USING btree
    ("timestamp" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_audit_user_id

-- DROP INDEX IF EXISTS assist_schema.idx_audit_user_id;

CREATE INDEX IF NOT EXISTS idx_audit_user_id
    ON assist_schema.action_audit_logs USING btree
    (user_id ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_audit_user_res

-- DROP INDEX IF EXISTS assist_schema.idx_audit_user_res;

CREATE INDEX IF NOT EXISTS idx_audit_user_res
    ON assist_schema.action_audit_logs USING btree
    (user_id ASC NULLS LAST, result COLLATE pg_catalog."default" ASC NULLS LAST, "timestamp" DESC NULLS FIRST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_audit_user_ts

-- DROP INDEX IF EXISTS assist_schema.idx_audit_user_ts;

CREATE INDEX IF NOT EXISTS idx_audit_user_ts
    ON assist_schema.action_audit_logs USING btree
    (user_id ASC NULLS LAST, "timestamp" DESC NULLS FIRST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
	
	
	-- Table: shared_schema.user_lock_reasons

-- DROP TABLE IF EXISTS shared_schema.user_lock_reasons;

CREATE TABLE IF NOT EXISTS shared_schema.user_lock_reasons
(
    id integer NOT NULL DEFAULT nextval('shared_schema.user_lock_reasons_id_seq'::regclass),
    code character varying(50) COLLATE pg_catalog."default" NOT NULL,
    name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    description character varying(200) COLLATE pg_catalog."default",
    allow_forgot_password boolean NOT NULL DEFAULT true,
    is_active boolean NOT NULL DEFAULT true,
    sort_order integer NOT NULL DEFAULT 0,
    created_at timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT user_lock_reasons_pkey PRIMARY KEY (id),
    CONSTRAINT uk_user_lock_reasons_code UNIQUE (code)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS shared_schema.user_lock_reasons
    OWNER to postgres;
-- Index: idx_user_lock_reasons_code

-- DROP INDEX IF EXISTS shared_schema.idx_user_lock_reasons_code;

CREATE INDEX IF NOT EXISTS idx_user_lock_reasons_code
    ON shared_schema.user_lock_reasons USING btree
    (code COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;