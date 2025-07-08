

-- Enable row level security for multi-tenancy
CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- Set the search path to our schema
SET search_path TO petel_schema, public;

-- Common timestamp columns
CREATE OR REPLACE FUNCTION trigger_set_timestamp()
RETURNS TRIGGER AS $$
BEGIN
  NEW.updated_at = NOW();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create sequences for IDs
CREATE SEQUENCE petel_schema.entity_types_seq;
CREATE SEQUENCE petel_schema.entities_seq;
CREATE SEQUENCE petel_schema.school_years_seq;
CREATE SEQUENCE petel_schema.users_seq;
CREATE SEQUENCE petel_schema.roles_seq;
CREATE SEQUENCE petel_schema.students_seq;
CREATE SEQUENCE petel_schema.teachers_seq;
CREATE SEQUENCE petel_schema.courses_seq;

-- Roles Table
CREATE TABLE petel_schema.roles (
    id INTEGER PRIMARY KEY DEFAULT nextval('petel_schema.roles_seq'),
    name VARCHAR(50) NOT NULL,
    description TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
	update_user INTEGER NULL REFERENCES petel_schema.users(id)
);

CREATE TRIGGER set_timestamp_roles
BEFORE UPDATE ON petel_schema.roles
FOR EACH ROW
EXECUTE PROCEDURE trigger_set_timestamp();

-- Enitities Table
CREATE TABLE petel_schema.entity_types (
    id INTEGER PRIMARY KEY DEFAULT nextval('petel_schema.schools_seq'),
    name VARCHAR(255) NOT NULL,
	update_user INTEGER NULL REFERENCES petel_schema.users(id),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TRIGGER set_timestamp_entities
BEFORE UPDATE ON petel_schema.entities
FOR EACH ROW
EXECUTE PROCEDURE trigger_set_timestamp();

-- Enitities Table
CREATE TABLE petel_schema.entities (
    id INTEGER PRIMARY KEY DEFAULT nextval('petel_schema.entities_seq'),
	entity_type_id INTEGER NOT NULL REFERENCES petel_schema.entity_types(id),
    name VARCHAR(255) NOT NULL,
    address TEXT,
    phone VARCHAR(50),
    email VARCHAR(255),
    principal_name VARCHAR(255),
    api_connection_id VARCHAR(255),
    is_active BOOLEAN DEFAULT TRUE,
	school_logo BYTEA,
		update_user INTEGER NULL REFERENCES petel_schema.users(id),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TRIGGER set_timestamp_entities
BEFORE UPDATE ON petel_schema.entities
FOR EACH ROW
EXECUTE PROCEDURE trigger_set_timestamp();

-- School Years Table
CREATE TABLE petel_schema.school_years (
    id INTEGER PRIMARY KEY DEFAULT nextval('petel_schema.school_years_seq'),
    school_id INTEGER NOT NULL REFERENCES petel_schema.schools(id),
    hebrew_year_name VARCHAR(50) NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    is_current BOOLEAN DEFAULT FALSE,
		update_user INTEGER NULL REFERENCES petel_schema.users(id),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT school_years_date_check CHECK (end_date > start_date),
    CONSTRAINT unique_school_year_per_school UNIQUE (school_id, hebrew_year_name)
);

CREATE TRIGGER set_timestamp_school_years
BEFORE UPDATE ON petel_schema.school_years
FOR EACH ROW
EXECUTE PROCEDURE trigger_set_timestamp();

-- Users Table
CREATE TABLE petel_schema.users (
    id INTEGER PRIMARY KEY DEFAULT nextval('petel_schema.users_seq'),
    school_id INTEGER NOT NULL REFERENCES petel_schema.schools(id),
    role_id INTEGER NOT NULL REFERENCES petel_schema.roles(id),
    username VARCHAR(50) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    email VARCHAR(255),
    phone VARCHAR(50),
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    last_login TIMESTAMP WITH TIME ZONE,
    is_active BOOLEAN DEFAULT TRUE,
	update_user INTEGER NULL REFERENCES petel_schema.users(id),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT unique_username_per_school UNIQUE (username, school_id)
);

CREATE TRIGGER set_timestamp_users
BEFORE UPDATE ON petel_schema.users
FOR EACH ROW
EXECUTE PROCEDURE trigger_set_timestamp();
