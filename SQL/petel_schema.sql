-- ============================================
-- Schema DDL Export: petel_schema
-- Generated: 2025-11-03 15:17:29.897348+02
-- ============================================


-- ============================================
-- TABLES
-- ============================================


-- Table: petel_schema.budget_statuses
CREATE TABLE petel_schema.budget_statuses (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.roles_seq'::regclass),
    name character varying(50) NOT NULL,
    description text,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    update_user integer(32,0)
);


-- Table: petel_schema.councils
CREATE TABLE petel_schema.councils (
    id integer(32,0) NOT NULL,
    council_code integer(32,0) NOT NULL,
    council_type character varying(25),
    council_short_name character varying(25),
    council_long_name character varying(50),
    council_district character varying(25),
    council_HP_number integer(32,0)
);


-- Table: petel_schema.entities
CREATE TABLE petel_schema.entities (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.entities_seq'::regclass),
    entity_type_id integer(32,0) NOT NULL,
    name character varying(255) NOT NULL,
    address text,
    phone character varying(50),
    email character varying(255),
    principal_name character varying(255),
    api_connection_id character varying(255),
    is_active boolean DEFAULT true,
    school_logo bytea,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    owner integer(32,0),
    council integer(32,0),
    inspector_name character varying(50),
    characterization character varying(24),
    contact_person character varying(50),
    education_stage character varying(25),
    symbol character(8)
);


-- Table: petel_schema.entity_types
CREATE TABLE petel_schema.entity_types (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.entity_types_seq'::regclass),
    name character varying(255) NOT NULL,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


-- Table: petel_schema.genders
CREATE TABLE petel_schema.genders (
    id integer(32,0) NOT NULL,
    description character varying(255) NOT NULL,
    external_code character varying(10),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


-- Table: petel_schema.hebrew_years
CREATE TABLE petel_schema.hebrew_years (
    id integer(32,0) NOT NULL,
    hebrew_year character varying NOT NULL
);


-- Table: petel_schema.persons
CREATE TABLE petel_schema.persons (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.persons_seq'::regclass),
    id_number character varying(50) NOT NULL DEFAULT 0,
    id_type integer(32,0) NOT NULL DEFAULT 0,
    first_name character varying(100) NOT NULL,
    last_name character varying(100) NOT NULL,
    gender integer(32,0) DEFAULT 0,
    date_of_birth date,
    user_id integer(32,0),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    phone_number_prefix character varying(7),
    phone_number character varying(10),
    email character varying(50),
    office_number_prefix character varying(3),
    office_number character varying(10)
);


-- Table: petel_schema.roles
CREATE TABLE petel_schema.roles (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.roles_seq'::regclass),
    name character varying(50) NOT NULL,
    description text,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    update_user integer(32,0)
);


-- Table: petel_schema.roles_actions
CREATE TABLE petel_schema.roles_actions (
    id integer(32,0) NOT NULL,
    role_id integer(32,0) NOT NULL,
    action_id integer(32,0) NOT NULL,
    action_level integer(32,0) NOT NULL DEFAULT 0,
    updated_at timestamp with time zone,
    update_user integer(32,0)
);


-- Table: petel_schema.school_additional_study_programs
CREATE TABLE petel_schema.school_additional_study_programs (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.school_additional_study_programs_id_seq'::regclass),
    school_year_id integer(32,0) NOT NULL,
    name character varying(255) NOT NULL,
    class_id integer(32,0) NOT NULL,
    weekly_hours integer(32,0) NOT NULL,
    number_of_class_students integer(32,0) NOT NULL,
    created_at time with time zone NOT NULL DEFAULT now(),
    updated_at time with time zone NOT NULL DEFAULT now()
);


-- Table: petel_schema.school_attribute_types_values
CREATE TABLE petel_schema.school_attribute_types_values (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.school_attribute_types_values_seq'::regclass),
    school_attribute_id integer(32,0) NOT NULL,
    value character varying(50),
    created_at time with time zone,
    is_valid boolean DEFAULT true,
    sort_order integer(32,0) DEFAULT 10
);


-- Table: petel_schema.school_attributes
CREATE TABLE petel_schema.school_attributes (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.school_attributes_seq'::regclass),
    school_year_id integer(32,0) NOT NULL,
    school_attribute_type_id integer(32,0) NOT NULL,
    version integer(32,0) NOT NULL DEFAULT 0,
    value character varying(50),
    created_at time with time zone DEFAULT now(),
    updated_at time with time zone DEFAULT now(),
    user_id integer(32,0) NOT NULL,
    is_last_version boolean DEFAULT true
);


-- Table: petel_schema.school_attributes_types
CREATE TABLE petel_schema.school_attributes_types (
    id integer(32,0) NOT NULL,
    name character varying(25),
    created_at time with time zone,
    attribute_value_type character varying(25),
    hebrew_name character varying,
    year_id integer(32,0)
);


-- Table: petel_schema.school_classes
CREATE TABLE petel_schema.school_classes (
    id integer(32,0) NOT NULL,
    school_year_id integer(32,0) NOT NULL,
    name character varying(6) NOT NULL,
    level character varying(3) NOT NULL,
    class_number character varying(3) NOT NULL,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


-- Table: petel_schema.school_grades
CREATE TABLE petel_schema.school_grades (
    id integer(32,0) NOT NULL,
    name character varying(3) NOT NULL,
    external_code character varying(10),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


-- Table: petel_schema.school_hours_budget
CREATE TABLE petel_schema.school_hours_budget (
    id integer(32,0) NOT NULL,
    school_year integer(32,0),
    version character(10),
    status integer(32,0),
    name character varying(50),
    description character varying(255),
    is_main_budget boolean,
    created_at timestamp with time zone,
    update_at timestamp with time zone,
    update_user integer(32,0)
);


-- Table: petel_schema.school_students
CREATE TABLE petel_schema.school_students (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.school_students_id_seq'::regclass),
    id_number character varying(15) NOT NULL,
    school_year_id integer(32,0) NOT NULL,
    version integer(32,0) NOT NULL DEFAULT 1,
    first_name character varying(50) NOT NULL,
    last_name character varying(50) NOT NULL,
    gender integer(32,0) NOT NULL DEFAULT 99,
    class_id integer(32,0) NOT NULL DEFAULT 0,
    start_date date,
    end_date date NOT NULL,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    school_grade_id integer(32,0),
    disability_category integer(32,0),
    street character varying(50),
    house_number character varying(6),
    city character varying(50),
    post_code character varying(10),
    sending_council integer(32,0),
    is_last_version boolean DEFAULT true
);


-- Table: petel_schema.school_tracks
CREATE TABLE petel_schema.school_tracks (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.school_tracks_seq'::regclass),
    school_year_id integer(32,0) NOT NULL,
    track_id integer(32,0) NOT NULL,
    track_level_id integer(32,0) NOT NULL DEFAULT 0,
    class_id integer(32,0) NOT NULL,
    weekly_hours integer(32,0) NOT NULL,
    user_id integer(32,0) NOT NULL,
    created_at time with time zone NOT NULL DEFAULT now()
);


-- Table: petel_schema.school_years
CREATE TABLE petel_schema.school_years (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.school_years_seq'::regclass),
    school_id integer(32,0) NOT NULL,
    hebrew_year_name character varying(50) NOT NULL,
    start_date date NOT NULL,
    end_date date NOT NULL,
    is_current boolean DEFAULT false,
    update_user integer(32,0),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    status integer(32,0),
    year_id integer(32,0)
);


-- Table: petel_schema.schools
CREATE TABLE petel_schema.schools (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.schools_seq'::regclass),
    entity_id integer(32,0) NOT NULL,
    school_year_id integer(32,0) NOT NULL,
    version integer(32,0) NOT NULL DEFAULT 0,
    entity_type_id integer(32,0) NOT NULL,
    name character varying(255) NOT NULL,
    street character varying(50),
    house_number character varying(6),
    city character varying(50),
    post_code character varying(10),
    council integer(32,0) NOT NULL DEFAULT 0,
    phone character varying(50),
    email character varying(255),
    principal integer(32,0) NOT NULL DEFAULT 0,
    api_connection_id character varying(255),
    is_active boolean DEFAULT true,
    school_logo bytea,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    owner integer(32,0) NOT NULL DEFAULT 0,
    inspector integer(32,0) NOT NULL DEFAULT 0,
    characterization character varying(24),
    contact_person integer(32,0) NOT NULL DEFAULT 0,
    education_stage character varying(25),
    symbol character(8),
    is_last_version boolean DEFAULT true
);


-- Table: petel_schema.student_school_years
CREATE TABLE petel_schema.student_school_years (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.student_school_years_id_seq'::regclass),
    student_id integer(32,0) NOT NULL,
    school_year_id integer(32,0) NOT NULL,
    track_id integer(32,0) NOT NULL DEFAULT 0,
    status integer(32,0) NOT NULL DEFAULT 0,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    school_grade_id integer(32,0)
);


-- Table: petel_schema.students
CREATE TABLE petel_schema.students (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.students_seq'::regclass),
    person_id integer(32,0) NOT NULL,
    user_id integer(32,0),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


-- Table: petel_schema.system_actions
CREATE TABLE petel_schema.system_actions (
    id integer(32,0) NOT NULL,
    name character varying(50) NOT NULL,
    action_type character varying(50) NOT NULL,
    description text,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    update_user integer(32,0)
);


-- Table: petel_schema.system_attributes
CREATE TABLE petel_schema.system_attributes (
    id integer(32,0) NOT NULL,
    description character varying(50) NOT NULL,
    value character varying(25) NOT NULL,
    value_type character varying(25),
    created_at timestamp with time zone,
    updated_at timestamp with time zone,
    name character varying(50),
    update_user integer(32,0),
    foreign_id integer(32,0)
);


-- Table: petel_schema.tracks
CREATE TABLE petel_schema.tracks (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.tracks_seq'::regclass),
    name character varying(255) NOT NULL,
    year_id integer(32,0) NOT NULL,
    external_code character varying(10),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    available_for_classes _varchar
);


-- Table: petel_schema.tracks_levels
CREATE TABLE petel_schema.tracks_levels (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.tracks_level_seq'::regclass),
    school_track_id integer(32,0) NOT NULL,
    level character varying(15),
    min_hours integer(32,0) NOT NULL,
    max_hours integer(32,0),
    available_for_classes _varchar
);


-- Table: petel_schema.tracks_pricing
CREATE TABLE petel_schema.tracks_pricing (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.tracks_pricing_seq'::regclass),
    school_track_id integer(32,0) NOT NULL,
    price numeric(10,2),
    category integer(32,0),
    level_id integer(32,0)
);


-- Table: petel_schema.user_roles
CREATE TABLE petel_schema.user_roles (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.user_roles_seq'::regclass),
    user_id integer(32,0) NOT NULL,
    role_id integer(32,0) NOT NULL,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    update_user integer(32,0),
    is_active boolean
);


-- Table: petel_schema.users
CREATE TABLE petel_schema.users (
    id integer(32,0) NOT NULL DEFAULT nextval('petel_schema.users_seq'::regclass),
    entity_id integer(32,0) NOT NULL,
    username character varying(50) NOT NULL,
    password_hash character varying(255) NOT NULL,
    email character varying(255),
    phone character varying(50),
    first_name character varying(100),
    last_name character varying(100),
    last_login timestamp with time zone,
    is_active boolean DEFAULT true,
    update_user integer(32,0),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


-- ============================================
-- PRIMARY KEYS
-- ============================================


ALTER TABLE petel_schema.budget_statuses
  ADD CONSTRAINT budget_statuses_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.councils
  ADD CONSTRAINT councils_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.entities
  ADD CONSTRAINT entities_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.entity_types
  ADD CONSTRAINT entity_types_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.genders
  ADD CONSTRAINT genders_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.hebrew_years
  ADD CONSTRAINT hebrew_years_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.persons
  ADD CONSTRAINT persons_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.roles
  ADD CONSTRAINT roles_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.roles_actions
  ADD CONSTRAINT action_roles_PK
  PRIMARY KEY (id);

ALTER TABLE petel_schema.school_additional_study_programs
  ADD CONSTRAINT school_additional_study_programs_pk
  PRIMARY KEY (id);

ALTER TABLE petel_schema.school_attribute_types_values
  ADD CONSTRAINT school_attribute_types_values_pk
  PRIMARY KEY (id);

ALTER TABLE petel_schema.school_attributes
  ADD CONSTRAINT school_attributes_pk
  PRIMARY KEY (id);

ALTER TABLE petel_schema.school_attributes_types
  ADD CONSTRAINT school_attributes_types_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.school_classes
  ADD CONSTRAINT school_classes_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.school_grades
  ADD CONSTRAINT school_grades_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.school_hours_budget
  ADD CONSTRAINT school_budget_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.school_students
  ADD CONSTRAINT school_students_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.school_tracks
  ADD CONSTRAINT school_tracks_pk
  PRIMARY KEY (id);

ALTER TABLE petel_schema.school_years
  ADD CONSTRAINT school_years_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.schools
  ADD CONSTRAINT schools_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.student_school_years
  ADD CONSTRAINT student_school_years_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.students
  ADD CONSTRAINT students_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.system_actions
  ADD CONSTRAINT system_actions_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.system_attributes
  ADD CONSTRAINT system_attributes_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.tracks
  ADD CONSTRAINT school_tracks_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.tracks_levels
  ADD CONSTRAINT school_tracks_level_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.tracks_pricing
  ADD CONSTRAINT school_tracks_pricing_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.user_roles
  ADD CONSTRAINT user_roles_pkey
  PRIMARY KEY (id);

ALTER TABLE petel_schema.users
  ADD CONSTRAINT users_pkey
  PRIMARY KEY (id);

-- ============================================
-- FOREIGN KEYS
-- ============================================


ALTER TABLE petel_schema.entities
  ADD CONSTRAINT entities_entity_type_id_fkey
  FOREIGN KEY (entity_type_id)
  REFERENCES petel_schema.entity_types (id);

ALTER TABLE petel_schema.entities
  ADD CONSTRAINT owner_fkey
  FOREIGN KEY (owner)
  REFERENCES petel_schema.entities (id);

ALTER TABLE petel_schema.persons
  ADD CONSTRAINT persons_user_id_fkey
  FOREIGN KEY (user_id)
  REFERENCES petel_schema.users (id);

ALTER TABLE petel_schema.roles_actions
  ADD CONSTRAINT action_roles_fk1
  FOREIGN KEY (role_id)
  REFERENCES petel_schema.roles (id);

ALTER TABLE petel_schema.roles_actions
  ADD CONSTRAINT action_roles_fk2
  FOREIGN KEY (action_id)
  REFERENCES petel_schema.system_actions (id);

ALTER TABLE petel_schema.school_additional_study_programs
  ADD CONSTRAINT school_tracks_class_fk
  FOREIGN KEY (class_id)
  REFERENCES petel_schema.school_classes (id);

ALTER TABLE petel_schema.school_additional_study_programs
  ADD CONSTRAINT school_additional_study_programs_school_year_fk
  FOREIGN KEY (school_year_id)
  REFERENCES petel_schema.school_years (id);

ALTER TABLE petel_schema.school_attribute_types_values
  ADD CONSTRAINT school_attribute_type_value_attribute_type_id
  FOREIGN KEY (school_attribute_id)
  REFERENCES petel_schema.school_attributes_types (id);

ALTER TABLE petel_schema.school_attributes
  ADD CONSTRAINT school_attributes_attribute_type_id
  FOREIGN KEY (school_attribute_type_id)
  REFERENCES petel_schema.school_attributes_types (id);

ALTER TABLE petel_schema.school_classes
  ADD CONSTRAINT school_classes_fk1
  FOREIGN KEY (school_year_id)
  REFERENCES petel_schema.school_years (id);

ALTER TABLE petel_schema.school_students
  ADD CONSTRAINT school_students_school_year_id_fkey
  FOREIGN KEY (school_year_id)
  REFERENCES petel_schema.school_years (id);

ALTER TABLE petel_schema.school_students
  ADD CONSTRAINT school_students_gender_fk
  FOREIGN KEY (gender)
  REFERENCES petel_schema.genders (id);

ALTER TABLE petel_schema.school_tracks
  ADD CONSTRAINT school_tracks_tracks_fk
  FOREIGN KEY (track_id)
  REFERENCES petel_schema.tracks (id);

ALTER TABLE petel_schema.school_tracks
  ADD CONSTRAINT school_tracks_class_fk
  FOREIGN KEY (class_id)
  REFERENCES petel_schema.school_classes (id);

ALTER TABLE petel_schema.school_tracks
  ADD CONSTRAINT school_tracks_level_fk
  FOREIGN KEY (track_level_id)
  REFERENCES petel_schema.tracks_levels (id);

ALTER TABLE petel_schema.school_tracks
  ADD CONSTRAINT school_tracks_school_year_fk
  FOREIGN KEY (school_year_id)
  REFERENCES petel_schema.school_years (id);

ALTER TABLE petel_schema.school_years
  ADD CONSTRAINT school_years_update_user_fkey
  FOREIGN KEY (update_user)
  REFERENCES petel_schema.users (id);

ALTER TABLE petel_schema.school_years
  ADD CONSTRAINT school_years_school_id_fkey
  FOREIGN KEY (school_id)
  REFERENCES petel_schema.entities (id);

ALTER TABLE petel_schema.schools
  ADD CONSTRAINT principal_person_fkey
  FOREIGN KEY (principal)
  REFERENCES petel_schema.persons (id);

ALTER TABLE petel_schema.schools
  ADD CONSTRAINT school_year_id_fkey
  FOREIGN KEY (school_year_id)
  REFERENCES petel_schema.school_years (id);

ALTER TABLE petel_schema.schools
  ADD CONSTRAINT owner_fkey
  FOREIGN KEY (owner)
  REFERENCES petel_schema.entities (id);

ALTER TABLE petel_schema.schools
  ADD CONSTRAINT schools_entity_id_fkey
  FOREIGN KEY (entity_id)
  REFERENCES petel_schema.entities (id);

ALTER TABLE petel_schema.schools
  ADD CONSTRAINT schools_entity_type_id_fkey
  FOREIGN KEY (entity_type_id)
  REFERENCES petel_schema.entity_types (id);

ALTER TABLE petel_schema.schools
  ADD CONSTRAINT contact_person_person_fkey
  FOREIGN KEY (contact_person)
  REFERENCES petel_schema.persons (id);

ALTER TABLE petel_schema.schools
  ADD CONSTRAINT inspector_person_fkey
  FOREIGN KEY (inspector)
  REFERENCES petel_schema.persons (id);

ALTER TABLE petel_schema.student_school_years
  ADD CONSTRAINT student_school_years_school_year_id_fkey
  FOREIGN KEY (school_year_id)
  REFERENCES petel_schema.school_years (id);

ALTER TABLE petel_schema.students
  ADD CONSTRAINT students_user_id_fkey
  FOREIGN KEY (user_id)
  REFERENCES petel_schema.users (id);

ALTER TABLE petel_schema.students
  ADD CONSTRAINT students_person_id_fkey
  FOREIGN KEY (person_id)
  REFERENCES petel_schema.persons (id);

ALTER TABLE petel_schema.tracks_levels
  ADD CONSTRAINT school_tracks_levels_school_tracks_fk
  FOREIGN KEY (school_track_id)
  REFERENCES petel_schema.tracks (id);

ALTER TABLE petel_schema.tracks_pricing
  ADD CONSTRAINT school_tracks_pricing_school_tracks_fk
  FOREIGN KEY (school_track_id)
  REFERENCES petel_schema.tracks (id);

ALTER TABLE petel_schema.tracks_pricing
  ADD CONSTRAINT school_tracks_pricing_school_tracks_levels_fk
  FOREIGN KEY (level_id)
  REFERENCES petel_schema.tracks_levels (id);

ALTER TABLE petel_schema.users
  ADD CONSTRAINT users_entity_id_id_fkey
  FOREIGN KEY (entity_id)
  REFERENCES petel_schema.entities (id);

ALTER TABLE petel_schema.users
  ADD CONSTRAINT users_update_user_fkey
  FOREIGN KEY (update_user)
  REFERENCES petel_schema.users (id);

-- ============================================
-- UNIQUE CONSTRAINTS
-- ============================================


ALTER TABLE petel_schema.roles_actions
  ADD CONSTRAINT action_roles_uq
  UNIQUE (role_id, action_id);

ALTER TABLE petel_schema.school_classes
  ADD CONSTRAINT school_classes_uq
  UNIQUE (school_year_id, name);

ALTER TABLE petel_schema.school_grades
  ADD CONSTRAINT school_grades_uq
  UNIQUE (name);

ALTER TABLE petel_schema.school_students
  ADD CONSTRAINT unique_school_student
  UNIQUE (id_number, school_year_id, version);

ALTER TABLE petel_schema.school_years
  ADD CONSTRAINT unique_school_year_per_school
  UNIQUE (school_id, hebrew_year_name);

ALTER TABLE petel_schema.schools
  ADD CONSTRAINT unique_school
  UNIQUE (entity_id, school_year_id, version);

ALTER TABLE petel_schema.student_school_years
  ADD CONSTRAINT unique_student_school_year
  UNIQUE (student_id, school_year_id);

ALTER TABLE petel_schema.tracks
  ADD CONSTRAINT School_tracks_per_year
  UNIQUE (year_id, external_code);

ALTER TABLE petel_schema.users
  ADD CONSTRAINT unique_username_per_entity_id
  UNIQUE (username, entity_id);

-- ============================================
-- CHECK CONSTRAINTS
-- ============================================


ALTER TABLE petel_schema.school_years
  ADD CONSTRAINT school_years_date_check
  CHECK ((end_date > start_date));

-- ============================================
-- INDEXES
-- ============================================


CREATE UNIQUE INDEX action_roles_uq ON petel_schema.roles_actions USING btree (role_id, action_id);

CREATE UNIQUE INDEX school_classes_uq ON petel_schema.school_classes USING btree (school_year_id, name);

CREATE UNIQUE INDEX school_grades_uq ON petel_schema.school_grades USING btree (name);

CREATE UNIQUE INDEX unique_school_student ON petel_schema.school_students USING btree (id_number, school_year_id, version);

CREATE UNIQUE INDEX unique_school_year_per_school ON petel_schema.school_years USING btree (school_id, hebrew_year_name);

CREATE UNIQUE INDEX unique_school ON petel_schema.schools USING btree (entity_id, school_year_id, version);

CREATE UNIQUE INDEX unique_student_school_year ON petel_schema.student_school_years USING btree (student_id, school_year_id);

CREATE UNIQUE INDEX School_tracks_per_year ON petel_schema.tracks USING btree (year_id, external_code);

CREATE UNIQUE INDEX unique_username_per_entity_id ON petel_schema.users USING btree (username, entity_id);

-- ============================================
-- END OF DDL EXPORT
-- ============================================