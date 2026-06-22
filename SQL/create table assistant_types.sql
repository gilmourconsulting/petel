-- Table: assist_schema.assistant_types

-- DROP TABLE IF EXISTS assist_schema.assistant_types;

CREATE TABLE IF NOT EXISTS assist_schema.assistant_types
(
    id integer NOT NULL , --DEFAULT,  --nextval('assist_schema.assistant_types_id_seq'::regclass),
    name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    display_name character varying(150) COLLATE pg_catalog."default",
    description character varying(255) COLLATE pg_catalog."default",
    sort_order integer DEFAULT 0,
    is_active boolean DEFAULT true,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    user_id integer,
    CONSTRAINT assistant_types_pkey PRIMARY KEY (id),
    CONSTRAINT assistant_types_name_key UNIQUE (name),
    CONSTRAINT assistant_types_user_id_fkey FOREIGN KEY (user_id)
        REFERENCES assist_schema.users (id) MATCH SIMPLE
        ON UPDATE CASCADE
        ON DELETE SET NULL
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS assist_schema.assistant_types
    OWNER to postgres;
-- Index: idx_assistant_types_assistant_types_id

-- DROP INDEX IF EXISTS assist_schema.idx_assistant_types_id;

-- Index: idx_assistant_types_is_active

-- DROP INDEX IF EXISTS assist_schema.idx_assistant_types_is_active;

CREATE INDEX IF NOT EXISTS idx_assistant_types_is_active
    ON assist_schema.assistant_types USING btree
    (is_active ASC NULLS LAST)
    WITH (fillfactor=100, deduplicate_items=True)
    TABLESPACE pg_default;
-- Index: idx_assistant_types_reference

-- SEQUENCE: assist_schema.assistant_types_id_seq

-- DROP SEQUENCE IF EXISTS assist_schema.assistant_types_id_seq;

CREATE SEQUENCE IF NOT EXISTS assist_schema.assistant_types_id_seq
    INCREMENT 1
    START 1
    MINVALUE 1
    MAXVALUE 32767
    CACHE 1;

ALTER SEQUENCE assist_schema.assistant_types_id_seq
    OWNED BY assist_schema.assistant_types.id;

ALTER SEQUENCE assist_schema.assistant_types_id_seq
    OWNER TO postgres;