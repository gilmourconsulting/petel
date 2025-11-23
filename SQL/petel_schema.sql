--
-- "PetelAdmin"QL database dump
--

--\restrict pNPBouUFtjZMT9yMnyczxTUUcjFCWBYwp2fjBaVVuqkXucAbBHdvtX17Lc59dnu

-- Dumped from database version 17.6
-- Dumped by pg_dump version 17.6



--
-- Name: petel_schema; Type: SCHEMA; Schema: -; Owner: PetelAdmin
--

--CREATE SCHEMA petel_schema;


--ALTER SCHEMA petel_schema OWNER TO "PetelAdmin";

--
-- Name: trigger_set_timestamp(); Type: FUNCTION; Schema: petel_schema; Owner: PetelAdmin
--

CREATE FUNCTION petel_schema.trigger_set_timestamp() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
  NEW.updated_at = NOW();
  RETURN NEW;
END;
$$;


ALTER FUNCTION petel_schema.trigger_set_timestamp() OWNER TO "PetelAdmin";

--
-- Name: roles_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.roles_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.roles_seq OWNER TO "PetelAdmin";

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: budget_statuses; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.budget_statuses (
    id integer DEFAULT nextval('petel_schema.roles_seq'::regclass) NOT NULL,
    name character varying(50) NOT NULL,
    description text,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    update_user integer
);


ALTER TABLE petel_schema.budget_statuses OWNER TO "PetelAdmin";

--
-- Name: councils; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.councils (
    id integer NOT NULL,
    council_code integer NOT NULL,
    council_type character varying(25),
    council_short_name character varying(25),
    council_long_name character varying(50),
    council_district character varying(25),
    "council_HP_number" integer
);


ALTER TABLE petel_schema.councils OWNER TO "PetelAdmin";

--
-- Name: courses_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.courses_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.courses_seq OWNER TO "PetelAdmin";

--
-- Name: document_links; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.document_links (
    id bigint NOT NULL,
    document_id bigint NOT NULL,
    school_student_id bigint,
    entity_id bigint,
    CONSTRAINT chk_one_link_required CHECK ((((school_student_id IS NOT NULL) OR (entity_id IS NOT NULL)) AND (NOT ((school_student_id IS NOT NULL) AND (entity_id IS NOT NULL)))))
);


ALTER TABLE petel_schema.document_links OWNER TO "PetelAdmin";

--
-- Name: document_links_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.document_links_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.document_links_id_seq OWNER TO "PetelAdmin";

--
-- Name: document_links_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: PetelAdmin
--

ALTER SEQUENCE petel_schema.document_links_id_seq OWNED BY petel_schema.document_links.id;


--
-- Name: document_status_types; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.document_status_types (
    id smallint NOT NULL,
    name character varying(25),
    created_at time with time zone DEFAULT now()
);


ALTER TABLE petel_schema.document_status_types OWNER TO "PetelAdmin";

--
-- Name: document_status_types_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE SEQUENCE petel_schema.document_status_types_id_seq
    AS smallint
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.document_status_types_id_seq OWNER TO "PetelAdmin";

--
-- Name: document_status_types_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: "PetelAdmin"
--

ALTER SEQUENCE petel_schema.document_status_types_id_seq OWNED BY petel_schema.document_status_types.id;


--
-- Name: document_types; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.document_types (
    id integer NOT NULL,
    name character varying(100) NOT NULL,
    level character varying(50) NOT NULL,
    year_id integer
);


ALTER TABLE petel_schema.document_types OWNER TO "PetelAdmin";

--
-- Name: document_types_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE SEQUENCE petel_schema.document_types_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.document_types_id_seq OWNER TO "PetelAdmin";

--
-- Name: document_types_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: "PetelAdmin"
--

ALTER SEQUENCE petel_schema.document_types_id_seq OWNED BY petel_schema.document_types.id;


--
-- Name: documents; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.documents (
    id bigint NOT NULL,
    master_document_id bigint,
    description character varying(50),
    document_type_id integer NOT NULL,
    status_id integer NOT NULL,
    file_blob bytea,
    file_encoding character varying(20),
    version integer NOT NULL,
    is_last_version boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP,
    file_name character varying(100)
);


ALTER TABLE petel_schema.documents OWNER TO "PetelAdmin";

--
-- Name: documents_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.documents_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.documents_id_seq OWNER TO "PetelAdmin";

--
-- Name: documents_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: PetelAdmin
--

ALTER SEQUENCE petel_schema.documents_id_seq OWNED BY petel_schema.documents.id;


--
-- Name: documents_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.documents_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.documents_seq OWNER TO "PetelAdmin";

--
-- Name: entities_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.entities_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.entities_seq OWNER TO "PetelAdmin";

--
-- Name: entities; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.entities (
    id integer DEFAULT nextval('petel_schema.entities_seq'::regclass) NOT NULL,
    entity_type_id integer NOT NULL,
    name character varying(255) NOT NULL,
    address text,
    phone character varying(50),
    email character varying(255),
    principal_name character varying(255),
    api_connection_id character varying(255),
    is_active boolean DEFAULT true,
    entity_logo bytea,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    owner integer,
    council integer,
    inspector_name character varying(50),
    characterization character varying(24),
    contact_person character varying(50),
    education_stage character varying(25),
    symbol character(8),
    characterization_id integer,
    tax_number character varying(20)
);


ALTER TABLE petel_schema.entities OWNER TO "PetelAdmin";

--
-- Name: entity_types_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.entity_types_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.entity_types_seq OWNER TO "PetelAdmin";

--
-- Name: entity_types; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.entity_types (
    id integer DEFAULT nextval('petel_schema.entity_types_seq'::regclass) NOT NULL,
    name character varying(255) NOT NULL,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


ALTER TABLE petel_schema.entity_types OWNER TO "PetelAdmin";

--
-- Name: genders; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.genders (
    id integer NOT NULL,
    description character varying(255) NOT NULL,
    external_code character varying(10),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


ALTER TABLE petel_schema.genders OWNER TO "PetelAdmin";

--
-- Name: hebrew_years; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.hebrew_years (
    id integer NOT NULL,
    hebrew_year character varying NOT NULL
);


ALTER TABLE petel_schema.hebrew_years OWNER TO "PetelAdmin";

--
-- Name: persons_seq; Type: SEQUENCE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE SEQUENCE petel_schema.persons_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.persons_seq OWNER TO "PetelAdmin";

--
-- Name: persons; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.persons (
    id integer DEFAULT nextval('petel_schema.persons_seq'::regclass) NOT NULL,
    id_number character varying(50) DEFAULT 0 NOT NULL,
    id_type integer DEFAULT 0 NOT NULL,
    first_name character varying(100) NOT NULL,
    last_name character varying(100) NOT NULL,
    gender integer DEFAULT 0,
    date_of_birth date,
    user_id integer,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    phone_number_prefix character varying(7),
    phone_number character varying(10),
    email character varying(50),
    office_number_prefix character varying(3),
    office_number character varying(10),
    "position" character varying(25)
);


ALTER TABLE petel_schema.persons OWNER TO "PetelAdmin";

--
-- Name: roles; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.roles (
    id integer DEFAULT nextval('petel_schema.roles_seq'::regclass) NOT NULL,
    name character varying(50) NOT NULL,
    description text,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    update_user integer
);


ALTER TABLE petel_schema.roles OWNER TO "PetelAdmin";

--
-- Name: roles_actions; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.roles_actions (
    id integer NOT NULL,
    role_id integer NOT NULL,
    action_id integer NOT NULL,
    action_level integer DEFAULT 0 NOT NULL,
    updated_at timestamp with time zone,
    update_user integer
);


ALTER TABLE petel_schema.roles_actions OWNER TO "PetelAdmin";

--
-- Name: school_additional_study_programs_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.school_additional_study_programs_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    MAXVALUE 2147483647
    CACHE 1;


ALTER SEQUENCE petel_schema.school_additional_study_programs_id_seq OWNER TO "PetelAdmin";

--
-- Name: school_additional_study_programs; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.school_additional_study_programs (
    id integer DEFAULT nextval('petel_schema.school_additional_study_programs_id_seq'::regclass) NOT NULL,
    school_year_id integer NOT NULL,
    name character varying(255) NOT NULL,
    class_id integer NOT NULL,
    weekly_hours integer NOT NULL,
    number_of_class_students integer NOT NULL,
    created_at time with time zone DEFAULT now() NOT NULL,
    updated_at time with time zone DEFAULT now() NOT NULL,
    user_id integer DEFAULT 0 NOT NULL
);


ALTER TABLE petel_schema.school_additional_study_programs OWNER TO "PetelAdmin";

--
-- Name: school_attribute_types_values_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.school_attribute_types_values_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.school_attribute_types_values_seq OWNER TO "PetelAdmin";

--
-- Name: school_attribute_types_values; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.school_attribute_types_values (
    id integer DEFAULT nextval('petel_schema.school_attribute_types_values_seq'::regclass) NOT NULL,
    school_attribute_id integer NOT NULL,
    value character varying(50),
    created_at time with time zone,
    is_valid boolean DEFAULT true,
    sort_order integer DEFAULT 10
);


ALTER TABLE petel_schema.school_attribute_types_values OWNER TO "PetelAdmin";

--
-- Name: school_attributes_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.school_attributes_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.school_attributes_seq OWNER TO "PetelAdmin";

--
-- Name: school_attributes; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.school_attributes (
    id integer DEFAULT nextval('petel_schema.school_attributes_seq'::regclass) NOT NULL,
    school_year_id integer NOT NULL,
    school_attribute_type_id integer NOT NULL,
    version integer DEFAULT 0 NOT NULL,
    value character varying(50),
    created_at time with time zone DEFAULT now(),
    updated_at time with time zone DEFAULT now(),
    user_id integer NOT NULL,
    is_last_version boolean DEFAULT true
);


ALTER TABLE petel_schema.school_attributes OWNER TO "PetelAdmin";

--
-- Name: school_attributes_types; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.school_attributes_types (
    id integer NOT NULL,
    name character varying(25),
    created_at time with time zone,
    attribute_value_type character varying(25),
    hebrew_name character varying,
    year_id integer
);


ALTER TABLE petel_schema.school_attributes_types OWNER TO "PetelAdmin";

--
-- Name: school_classes; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.school_classes (
    id integer NOT NULL,
    school_year_id integer NOT NULL,
    name character varying(6) NOT NULL,
    level character varying(3) NOT NULL,
    class_number character varying(3) NOT NULL,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


ALTER TABLE petel_schema.school_classes OWNER TO "PetelAdmin";

--
-- Name: school_classes_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE petel_schema.school_classes ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME petel_schema.school_classes_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: school_grades; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.school_grades (
    id integer NOT NULL,
    name character varying(3) NOT NULL,
    external_code character varying(10),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


ALTER TABLE petel_schema.school_grades OWNER TO "PetelAdmin";

--
-- Name: school_hours_budget; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.school_hours_budget (
    id integer NOT NULL,
    school_year integer,
    version character(10),
    status integer,
    name character varying(50),
    description character varying(255),
    is_main_budget boolean,
    created_at timestamp with time zone,
    update_at timestamp with time zone,
    update_user integer
);


ALTER TABLE petel_schema.school_hours_budget OWNER TO "PetelAdmin";

--
-- Name: school_student_pricing_elements; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.school_student_pricing_elements (
    id integer NOT NULL,
    school_student integer NOT NULL,
    pricing_element integer NOT NULL,
    price numeric(7,2),
    determinig_factor character varying(30)
);


ALTER TABLE petel_schema.school_student_pricing_elements OWNER TO "PetelAdmin";

--
-- Name: school_student_pricing_elements_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE SEQUENCE petel_schema.school_student_pricing_elements_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.school_student_pricing_elements_id_seq OWNER TO "PetelAdmin";

--
-- Name: school_student_pricing_elements_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: "PetelAdmin"
--

ALTER SEQUENCE petel_schema.school_student_pricing_elements_id_seq OWNED BY petel_schema.school_student_pricing_elements.id;


--
-- Name: school_students_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE SEQUENCE petel_schema.school_students_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.school_students_id_seq OWNER TO "PetelAdmin";

--
-- Name: school_students; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.school_students (
    id integer DEFAULT nextval('petel_schema.school_students_id_seq'::regclass) NOT NULL,
    id_number character varying(15) NOT NULL,
    school_year_id integer NOT NULL,
    version integer DEFAULT 1 NOT NULL,
    first_name character varying(50) NOT NULL,
    last_name character varying(50) NOT NULL,
    gender integer DEFAULT 99 NOT NULL,
    class_id integer DEFAULT 0 NOT NULL,
    start_date date,
    end_date date NOT NULL,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    school_grade_id integer,
    disability_category integer,
    street character varying(50),
    house_number character varying(6),
    city character varying(50),
    post_code character varying(10),
    sending_council integer,
    is_last_version boolean DEFAULT true,
    cost numeric(7,2) DEFAULT 0
);


ALTER TABLE petel_schema.school_students OWNER TO "PetelAdmin";

--
-- Name: school_tracks; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.school_tracks (
    id integer NOT NULL,
    school_year_id integer NOT NULL,
    track_id integer NOT NULL,
    track_level_id integer DEFAULT 0 NOT NULL,
    class_id integer NOT NULL,
    weekly_hours integer NOT NULL,
    user_id integer NOT NULL,
    created_at time with time zone DEFAULT now() NOT NULL
);


ALTER TABLE petel_schema.school_tracks OWNER TO "PetelAdmin";

--
-- Name: school_tracks_seq; Type: SEQUENCE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE SEQUENCE petel_schema.school_tracks_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    MAXVALUE 9999999
    CACHE 1;


ALTER SEQUENCE petel_schema.school_tracks_seq OWNER TO "PetelAdmin";

--
-- Name: school_tracks_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: "PetelAdmin"
--

ALTER SEQUENCE petel_schema.school_tracks_seq OWNED BY petel_schema.school_tracks.id;


--
-- Name: school_years_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.school_years_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.school_years_seq OWNER TO "PetelAdmin";

--
-- Name: school_years; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.school_years (
    id integer DEFAULT nextval('petel_schema.school_years_seq'::regclass) NOT NULL,
    school_id integer NOT NULL,
    hebrew_year_name character varying(50) NOT NULL,
    start_date date NOT NULL,
    end_date date NOT NULL,
    is_current boolean DEFAULT false,
    update_user integer,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    status integer,
    year_id integer,
    CONSTRAINT school_years_date_check CHECK ((end_date > start_date))
);


ALTER TABLE petel_schema.school_years OWNER TO "PetelAdmin";

--
-- Name: schools_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.schools_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.schools_seq OWNER TO "PetelAdmin";

--
-- Name: schools; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.schools (
    id integer DEFAULT nextval('petel_schema.schools_seq'::regclass) NOT NULL,
    entity_id integer NOT NULL,
    school_year_id integer NOT NULL,
    version integer DEFAULT 0 NOT NULL,
    entity_type_id integer NOT NULL,
    name character varying(255) NOT NULL,
    street character varying(50),
    house_number character varying(6),
    city character varying(50),
    post_code character varying(10),
    council integer DEFAULT 0,
    phone character varying(50),
    email character varying(255),
    principal integer DEFAULT 0,
    api_connection_id character varying(255),
    is_active boolean DEFAULT true,
    school_logo bytea,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    owner integer DEFAULT 0 NOT NULL,
    inspector integer DEFAULT 0,
    contact_person integer DEFAULT 0,
    education_stage character varying(25),
    symbol character(8),
    is_last_version boolean DEFAULT true,
    characterization_id integer
);


ALTER TABLE petel_schema.schools OWNER TO "PetelAdmin";

--
-- Name: special_needs_characterizations; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.special_needs_characterizations (
    id integer NOT NULL,
    name character varying(50),
    foreign_id integer,
    user_id integer DEFAULT 0
);


ALTER TABLE petel_schema.special_needs_characterizations OWNER TO "PetelAdmin";

--
-- Name: special_needs_pricing_categories; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.special_needs_pricing_categories (
    id integer NOT NULL,
    pricing_element integer NOT NULL,
    category integer NOT NULL,
    is_lowest_level boolean,
    price numeric(10,2),
    user_id integer
);


ALTER TABLE petel_schema.special_needs_pricing_categories OWNER TO "PetelAdmin";

--
-- Name: special_needs_pricing_categories_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.special_needs_pricing_categories_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.special_needs_pricing_categories_id_seq OWNER TO "PetelAdmin";

--
-- Name: special_needs_pricing_categories_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: PetelAdmin
--

ALTER SEQUENCE petel_schema.special_needs_pricing_categories_id_seq OWNED BY petel_schema.special_needs_pricing_categories.id;


--
-- Name: special_needs_pricing_elements; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.special_needs_pricing_elements (
    id integer NOT NULL,
    year_id integer NOT NULL,
    name character varying(50) NOT NULL,
    title character varying(25) NOT NULL,
    description text,
    user_id integer,
    created_at timestamp with time zone DEFAULT now()
);


ALTER TABLE petel_schema.special_needs_pricing_elements OWNER TO "PetelAdmin";

--
-- Name: special_needs_pricing_elements_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.special_needs_pricing_elements_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.special_needs_pricing_elements_id_seq OWNER TO "PetelAdmin";

--
-- Name: special_needs_pricing_elements_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: PetelAdmin
--

ALTER SEQUENCE petel_schema.special_needs_pricing_elements_id_seq OWNED BY petel_schema.special_needs_pricing_elements.id;


--
-- Name: student_school_years_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE SEQUENCE petel_schema.student_school_years_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.student_school_years_id_seq OWNER TO "PetelAdmin";

--
-- Name: student_school_years; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.student_school_years (
    id integer DEFAULT nextval('petel_schema.student_school_years_id_seq'::regclass) NOT NULL,
    student_id integer NOT NULL,
    school_year_id integer NOT NULL,
    track_id integer DEFAULT 0 NOT NULL,
    status integer DEFAULT 0 NOT NULL,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    school_grade_id integer
);


ALTER TABLE petel_schema.student_school_years OWNER TO "PetelAdmin";

--
-- Name: tracks_seq; Type: SEQUENCE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE SEQUENCE petel_schema.tracks_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.tracks_seq OWNER TO "PetelAdmin";

--
-- Name: tracks; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.tracks (
    id integer DEFAULT nextval('petel_schema.tracks_seq'::regclass) NOT NULL,
    name character varying(255) NOT NULL,
    year_id integer NOT NULL,
    external_code character varying(10),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    available_for_classes character varying(3)[]
);


ALTER TABLE petel_schema.tracks OWNER TO "PetelAdmin";

--
-- Name: student_school_years_registration_summary_vw; Type: VIEW; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE VIEW petel_schema.student_school_years_registration_summary_vw AS
 SELECT y.hebrew_year_name,
    y.school_id,
    sg.name AS school_grade,
    st.name AS school_track,
    sy.school_year_id,
    count(*) AS registered
   FROM (((petel_schema.school_years y
     JOIN petel_schema.student_school_years sy ON ((y.id = sy.school_year_id)))
     JOIN petel_schema.tracks st ON ((sy.track_id = st.id)))
     JOIN petel_schema.school_grades sg ON ((sg.id = sy.school_grade_id)))
  GROUP BY y.hebrew_year_name, y.school_id, sg.name, st.name, sy.school_year_id;


ALTER VIEW petel_schema.student_school_years_registration_summary_vw OWNER TO "PetelAdmin";

--
-- Name: students_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.students_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.students_seq OWNER TO "PetelAdmin";

--
-- Name: students; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.students (
    id integer DEFAULT nextval('petel_schema.students_seq'::regclass) NOT NULL,
    person_id integer NOT NULL,
    user_id integer,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


ALTER TABLE petel_schema.students OWNER TO "PetelAdmin";

--
-- Name: student_school_years_registration_vw; Type: VIEW; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE VIEW petel_schema.student_school_years_registration_vw AS
 SELECT y.hebrew_year_name,
    y.school_id,
    p.first_name,
    p.last_name,
    p.id_type,
    p.id_number AS official_id,
    p.date_of_birth,
    sg.name AS school_grade,
    st.name AS school_track,
    sy.school_year_id
   FROM (((((petel_schema.school_years y
     JOIN petel_schema.student_school_years sy ON ((y.id = sy.school_year_id)))
     JOIN petel_schema.students s ON ((sy.student_id = s.id)))
     JOIN petel_schema.persons p ON ((s.person_id = p.id)))
     JOIN petel_schema.tracks st ON ((sy.track_id = st.id)))
     JOIN petel_schema.school_grades sg ON ((sg.id = sy.school_grade_id)));


ALTER VIEW petel_schema.student_school_years_registration_vw OWNER TO "PetelAdmin";

--
-- Name: system_actions; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.system_actions (
    id integer NOT NULL,
    name character varying(50) NOT NULL,
    action_type character varying(50) NOT NULL,
    description text,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    update_user integer
);


ALTER TABLE petel_schema.system_actions OWNER TO "PetelAdmin";

--
-- Name: system_attributes; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.system_attributes (
    id integer NOT NULL,
    description character varying(50) NOT NULL,
    value character varying(25) NOT NULL,
    value_type character varying(25),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    name character varying(50),
    update_user integer,
    foreign_id integer
);


ALTER TABLE petel_schema.system_attributes OWNER TO "PetelAdmin";

--
-- Name: teachers_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.teachers_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.teachers_seq OWNER TO "PetelAdmin";

--
-- Name: tracks_level_seq; Type: SEQUENCE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE SEQUENCE petel_schema.tracks_level_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    MAXVALUE 99999999
    CACHE 1;


ALTER SEQUENCE petel_schema.tracks_level_seq OWNER TO "PetelAdmin";

--
-- Name: tracks_levels; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.tracks_levels (
    id integer DEFAULT nextval('petel_schema.tracks_level_seq'::regclass) NOT NULL,
    school_track_id integer NOT NULL,
    level character varying(15),
    min_hours integer NOT NULL,
    max_hours integer,
    available_for_classes character varying(3)[]
);


ALTER TABLE petel_schema.tracks_levels OWNER TO "PetelAdmin";

--
-- Name: tracks_pricing; Type: TABLE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE TABLE petel_schema.tracks_pricing (
    id integer NOT NULL,
    school_track_id integer NOT NULL,
    price numeric(10,2),
    category integer,
    level_id integer
);


ALTER TABLE petel_schema.tracks_pricing OWNER TO "PetelAdmin";

--
-- Name: tracks_pricing_seq; Type: SEQUENCE; Schema: petel_schema; Owner: "PetelAdmin"
--

CREATE SEQUENCE petel_schema.tracks_pricing_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    MAXVALUE 10000000
    CACHE 1;


ALTER SEQUENCE petel_schema.tracks_pricing_seq OWNER TO "PetelAdmin";

--
-- Name: tracks_pricing_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: "PetelAdmin"
--

ALTER SEQUENCE petel_schema.tracks_pricing_seq OWNED BY petel_schema.tracks_pricing.id;


--
-- Name: user_roles_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.user_roles_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.user_roles_seq OWNER TO "PetelAdmin";

--
-- Name: user_roles; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.user_roles (
    id integer DEFAULT nextval('petel_schema.user_roles_seq'::regclass) NOT NULL,
    user_id integer NOT NULL,
    role_id integer NOT NULL,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    update_user integer,
    is_active boolean
);


ALTER TABLE petel_schema.user_roles OWNER TO "PetelAdmin";

--
-- Name: users_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.users_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.users_seq OWNER TO "PetelAdmin";

--
-- Name: users; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.users (
    id integer DEFAULT nextval('petel_schema.users_seq'::regclass) NOT NULL,
    entity_id integer NOT NULL,
    username character varying(50) NOT NULL,
    password_hash character varying(255) NOT NULL,
    email character varying(255),
    phone character varying(50),
    first_name character varying(100),
    last_name character varying(100),
    last_login timestamp with time zone,
    is_active boolean DEFAULT true,
    update_user integer,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


ALTER TABLE petel_schema.users OWNER TO "PetelAdmin";

--
-- Name: document_links id; Type: DEFAULT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.document_links ALTER COLUMN id SET DEFAULT nextval('petel_schema.document_links_id_seq'::regclass);


--
-- Name: document_status_types id; Type: DEFAULT; Schema: petel_schema; Owner: "PetelAdmin"
--

ALTER TABLE ONLY petel_schema.document_status_types ALTER COLUMN id SET DEFAULT nextval('petel_schema.document_status_types_id_seq'::regclass);


--
-- Name: document_types id; Type: DEFAULT; Schema: petel_schema; Owner: "PetelAdmin"
--

ALTER TABLE ONLY petel_schema.document_types ALTER COLUMN id SET DEFAULT nextval('petel_schema.document_types_id_seq'::regclass);


--
-- Name: documents id; Type: DEFAULT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.documents ALTER COLUMN id SET DEFAULT nextval('petel_schema.documents_id_seq'::regclass);


--
-- Name: school_student_pricing_elements id; Type: DEFAULT; Schema: petel_schema; Owner: "PetelAdmin"
--

ALTER TABLE ONLY petel_schema.school_student_pricing_elements ALTER COLUMN id SET DEFAULT nextval('petel_schema.school_student_pricing_elements_id_seq'::regclass);


--
-- Name: school_tracks id; Type: DEFAULT; Schema: petel_schema; Owner: "PetelAdmin"
--

ALTER TABLE ONLY petel_schema.school_tracks ALTER COLUMN id SET DEFAULT nextval('petel_schema.school_tracks_seq'::regclass);


--
-- Name: special_needs_pricing_categories id; Type: DEFAULT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.special_needs_pricing_categories ALTER COLUMN id SET DEFAULT nextval('petel_schema.special_needs_pricing_categories_id_seq'::regclass);


--
-- Name: special_needs_pricing_elements id; Type: DEFAULT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.special_needs_pricing_elements ALTER COLUMN id SET DEFAULT nextval('petel_schema.special_needs_pricing_elements_id_seq'::regclass);


--
-- Name: tracks_pricing id; Type: DEFAULT; Schema: petel_schema; Owner: "PetelAdmin"
--

ALTER TABLE ONLY petel_schema.tracks_pricing ALTER COLUMN id SET DEFAULT nextval('petel_schema.tracks_pricing_seq'::regclass);


--
-- Data for Name: budget_statuses; Type: TABLE DATA; Schema: petel_schema; Owner: "PetelAdmin"
--
