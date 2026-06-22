-- Table: assist_schema.<TABLE_NAME>

-- DROP TABLE IF EXISTS assist_schema.<TABLE_NAME>;

CREATE TABLE IF NOT EXISTS assist_schema.<TABLE_NAME>
(
    id integer NOT NULL DEFAULT nextval('assist_schema.<TABLE_NAME>_id_seq'::regclass),
    name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    display_name character varying(150) COLLATE pg_catalog."default",
    description character varying(255) COLLATE pg_catalog."default",
    action_type_id smallint NOT NULL,
    reference character varying(200) COLLATE pg_catalog."default",
    onclick_name character varying(100) COLLATE pg_catalog."default",
    sort_order integer DEFAULT 0,
    is_active boolean DEFAULT true,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    user_id integer,
    CONSTRAINT <TABLE_NAME>_pkey PRIMARY KEY (id),
    CONSTRAINT <TABLE_NAME>_name_key UNIQUE (name),
    CONSTRAINT <TABLE_NAME>_action_type_id_fkey FOREIGN KEY (action_type_id)
        REFERENCES assist_schema.action_types (id) MATCH SIMPLE
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT <TABLE_NAME>_user_id_fkey FOREIGN KEY (user_id)
        REFERENCES assist_schema.users (id) MATCH SIMPLE
        ON UPDATE CASCADE
        ON DELETE SET NULL
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS assist_schema.<TABLE_NAME>
    OWNER to postgres;
-- Index: idx_<TABLE_NAME>_action_type_id

-- DROP INDEX IF EXISTS assist_schema.idx_<TABLE_NAME>_action_type_id;

CREATE INDEX IF NOT EXISTS idx_<TABLE_NAME>_action_type_id
    ON assist_schema.<TABLE_NAME> USING btree
    (action_type_id ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_<TABLE_NAME>_is_active

-- DROP INDEX IF EXISTS assist_schema.idx_<TABLE_NAME>_is_active;

CREATE INDEX IF NOT EXISTS idx_<TABLE_NAME>_is_active
    ON assist_schema.<TABLE_NAME> USING btree
    (is_active ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_<TABLE_NAME>_reference

-- DROP INDEX IF EXISTS assist_schema.idx_<TABLE_NAME>_reference;

CREATE INDEX IF NOT EXISTS idx_<TABLE_NAME>_reference
    ON assist_schema.<TABLE_NAME> USING btree
    (reference COLLATE pg_catalog."default" ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
	
	
-- SEQUENCE: assist_schema.assistant_types_id_seq

-- DROP SEQUENCE IF EXISTS assist_schema.<TABLE_NAME>_id_seq;

CREATE SEQUENCE IF NOT EXISTS assist_schema.<TABLE_NAME>_id_seq
    INCREMENT 1
    START 1
    MINVALUE 1
    MAXVALUE 32767
    CACHE 1;

ALTER SEQUENCE assist_schema.<TABLE_NAME>_id_seq
    OWNED BY assist_schema.<TABLE_NAME>.id;

ALTER SEQUENCE assist_schema.<TABLE_NAME>_id_seq
    OWNER TO postgres;