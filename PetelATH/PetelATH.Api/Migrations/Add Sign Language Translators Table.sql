-- Migration: Add Sign Language Translators Table
-- Description: Stores sign language translators assigned to schools for specific school years
-- Date: 2025-12-18

-- Create sequence
CREATE SEQUENCE IF NOT EXISTS petel_schema.sign_language_translators_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

ALTER SEQUENCE petel_schema.sign_language_translators_seq OWNER TO "PetelAdmin";

-- Create table
CREATE TABLE IF NOT EXISTS petel_schema.sign_language_translators (
    id INTEGER DEFAULT nextval('petel_schema.sign_language_translators_seq'::regclass) NOT NULL,
    school_year_id INTEGER NOT NULL,
    person_id INTEGER NOT NULL,
    hours_employed DECIMAL(6,2) NOT NULL,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    user_id INTEGER NOT NULL,
    CONSTRAINT sign_language_translators_pk PRIMARY KEY (id),
    CONSTRAINT fk_sign_language_translators_school_year 
        FOREIGN KEY (school_year_id) 
        REFERENCES petel_schema.school_years(id) 
        ON DELETE CASCADE,
    CONSTRAINT fk_sign_language_translators_person 
        FOREIGN KEY (person_id) 
        REFERENCES petel_schema.persons(id) 
        ON DELETE RESTRICT,
    CONSTRAINT unique_translator_per_year 
        UNIQUE (school_year_id, person_id)
);

ALTER TABLE petel_schema.sign_language_translators OWNER TO "PetelAdmin";

-- Create index for faster lookups
CREATE INDEX IF NOT EXISTS idx_sign_language_translators_school_year 
    ON petel_schema.sign_language_translators(school_year_id);

CREATE INDEX IF NOT EXISTS idx_sign_language_translators_person 
    ON petel_schema.sign_language_translators(person_id);

-- Add comment
COMMENT ON TABLE petel_schema.sign_language_translators IS 'Stores sign language translators employed by schools per school year';
COMMENT ON COLUMN petel_schema.sign_language_translators.hours_employed IS 'Number of hours employed for the school year';