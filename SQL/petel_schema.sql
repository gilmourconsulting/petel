ALTER FUNCTION petel_schema.trigger_set_timestamp() OWNER TO "PetelAdmin";

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: alert_levels; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.alert_levels (
    id smallint NOT NULL,
    name character varying(25),
    description character varying(25),
    created_at time with time zone DEFAULT now(),
    user_id integer DEFAULT 0
);


ALTER TABLE petel_schema.alert_levels OWNER TO "PetelAdmin";

--
-- Name: alert_levels_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.alert_levels_id_seq
    AS smallint
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.alert_levels_id_seq OWNER TO "PetelAdmin";

--
-- Name: alert_levels_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: PetelAdmin
--

ALTER SEQUENCE petel_schema.alert_levels_id_seq OWNED BY petel_schema.alert_levels.id;


--
-- Name: alert_links; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.alert_links (
    id bigint NOT NULL,
    alert_id bigint,
    alert_status integer,
    entity_id integer,
    created_at timestamp with time zone DEFAULT now(),
    is_last_version boolean
);


ALTER TABLE petel_schema.alert_links OWNER TO "PetelAdmin";

--
-- Name: alert_links_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.alert_links_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.alert_links_id_seq OWNER TO "PetelAdmin";

--
-- Name: alert_links_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: PetelAdmin
--

ALTER SEQUENCE petel_schema.alert_links_id_seq OWNED BY petel_schema.alert_links.id;


--
-- Name: alert_statuses_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: postgres
--

CREATE SEQUENCE petel_schema.alert_statuses_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    MAXVALUE 32767
    CACHE 1;


ALTER SEQUENCE petel_schema.alert_statuses_id_seq OWNER TO postgres;

--
-- Name: alert_statuses; Type: TABLE; Schema: petel_schema; Owner: postgres
--

CREATE TABLE petel_schema.alert_statuses (
    id smallint DEFAULT nextval('petel_schema.alert_statuses_id_seq'::regclass) NOT NULL,
    name character varying(25),
    description character varying(25),
    created_at time with time zone DEFAULT now(),
    user_id integer DEFAULT 0
);


ALTER TABLE petel_schema.alert_statuses OWNER TO postgres;

--
-- Name: alert_types; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.alert_types (
    id smallint NOT NULL,
    name character varying(25),
    description character varying(25),
    created_at time with time zone DEFAULT now(),
    user_id integer DEFAULT 0
);


ALTER TABLE petel_schema.alert_types OWNER TO "PetelAdmin";

--
-- Name: alert_types_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.alert_types_id_seq
    AS smallint
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.alert_types_id_seq OWNER TO "PetelAdmin";

--
-- Name: alert_types_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: PetelAdmin
--

ALTER SEQUENCE petel_schema.alert_types_id_seq OWNED BY petel_schema.alert_types.id;


--
-- Name: alerts; Type: TABLE; Schema: petel_schema; Owner: postgres
--

CREATE TABLE petel_schema.alerts (
    id bigint NOT NULL,
    alert_type integer,
    alert_level integer,
    description text,
    status integer,
    user_id integer DEFAULT 0,
    is_event boolean DEFAULT false,
    created_at timestamp with time zone DEFAULT now(),
    event_date timestamp without time zone
);


ALTER TABLE petel_schema.alerts OWNER TO postgres;

--
-- Name: alerts_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: postgres
--

CREATE SEQUENCE petel_schema.alerts_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.alerts_id_seq OWNER TO postgres;

--
-- Name: alerts_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: postgres
--

ALTER SEQUENCE petel_schema.alerts_id_seq OWNED BY petel_schema.alerts.id;


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

--
-- Name: budget_statuses; Type: TABLE; Schema: petel_schema; Owner: postgres
--

CREATE TABLE petel_schema.budget_statuses (
    id integer DEFAULT nextval('petel_schema.roles_seq'::regclass) NOT NULL,
    name character varying(50) NOT NULL,
    description text,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    update_user integer
);


ALTER TABLE petel_schema.budget_statuses OWNER TO postgres;

--
-- Name: councils; Type: TABLE; Schema: petel_schema; Owner: postgres
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


ALTER TABLE petel_schema.councils OWNER TO postgres;

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
-- Name: document_status_types; Type: TABLE; Schema: petel_schema; Owner: postgres
--

CREATE TABLE petel_schema.document_status_types (
    id smallint NOT NULL,
    name character varying(25),
    created_at time with time zone DEFAULT now()
);


ALTER TABLE petel_schema.document_status_types OWNER TO postgres;

--
-- Name: document_status_types_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: postgres
--

CREATE SEQUENCE petel_schema.document_status_types_id_seq
    AS smallint
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.document_status_types_id_seq OWNER TO postgres;

--
-- Name: document_status_types_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: postgres
--

ALTER SEQUENCE petel_schema.document_status_types_id_seq OWNED BY petel_schema.document_status_types.id;


--
-- Name: document_types; Type: TABLE; Schema: petel_schema; Owner: postgres
--

CREATE TABLE petel_schema.document_types (
    id integer NOT NULL,
    name character varying(100) NOT NULL,
    level character varying(50) NOT NULL,
    year_id integer
);


ALTER TABLE petel_schema.document_types OWNER TO postgres;

--
-- Name: document_types_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: postgres
--

CREATE SEQUENCE petel_schema.document_types_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.document_types_id_seq OWNER TO postgres;

--
-- Name: document_types_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: postgres
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
    contact_person_old character varying(50),
    education_stage character varying(25),
    symbol character(8),
    characterization_id integer,
    tax_number character varying(20),
    street character varying(50),
    house_number character varying(6),
    city character varying(50),
    post_code character varying(10),
    contact_person integer
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
-- Name: genders; Type: TABLE; Schema: petel_schema; Owner: postgres
--

CREATE TABLE petel_schema.genders (
    id integer NOT NULL,
    description character varying(255) NOT NULL,
    external_code character varying(10),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


ALTER TABLE petel_schema.genders OWNER TO postgres;

--
-- Name: hebrew_years; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.hebrew_years (
    id integer NOT NULL,
    hebrew_year character varying NOT NULL
);


ALTER TABLE petel_schema.hebrew_years OWNER TO "PetelAdmin";

--
-- Name: persons_seq; Type: SEQUENCE; Schema: petel_schema; Owner: postgres
--

CREATE SEQUENCE petel_schema.persons_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.persons_seq OWNER TO postgres;

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
-- Name: school_additional_study_programs; Type: TABLE; Schema: petel_schema; Owner: postgres
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
    user_id integer DEFAULT 0 NOT NULL,
    version integer DEFAULT 1 NOT NULL,
    is_last_version boolean DEFAULT true NOT NULL,
    master_id integer NOT NULL,
    cost numeric(10,2),
    approved_amount numeric(10,2)
);


ALTER TABLE petel_schema.school_additional_study_programs OWNER TO postgres;

--
-- Name: COLUMN school_additional_study_programs.version; Type: COMMENT; Schema: petel_schema; Owner: postgres
--

COMMENT ON COLUMN petel_schema.school_additional_study_programs.version IS 'Version number for this record (1 = first version, increments on update)';


--
-- Name: COLUMN school_additional_study_programs.is_last_version; Type: COMMENT; Schema: petel_schema; Owner: postgres
--

COMMENT ON COLUMN petel_schema.school_additional_study_programs.is_last_version IS 'Flag indicating if this is the most recent version of the record';


--
-- Name: COLUMN school_additional_study_programs.master_id; Type: COMMENT; Schema: petel_schema; Owner: postgres
--

COMMENT ON COLUMN petel_schema.school_additional_study_programs.master_id IS 'Reference to the original (first version) record ID for version history tracking';


--
-- Name: COLUMN school_additional_study_programs.cost; Type: COMMENT; Schema: petel_schema; Owner: postgres
--

COMMENT ON COLUMN petel_schema.school_additional_study_programs.cost IS 'Estimated or budgeted cost for the program';


--
-- Name: COLUMN school_additional_study_programs.approved_amount; Type: COMMENT; Schema: petel_schema; Owner: postgres
--

COMMENT ON COLUMN petel_schema.school_additional_study_programs.approved_amount IS 'Approved budget amount for the program';


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
-- Name: school_attribute_types_values; Type: TABLE; Schema: petel_schema; Owner: postgres
--

CREATE TABLE petel_schema.school_attribute_types_values (
    id integer DEFAULT nextval('petel_schema.school_attribute_types_values_seq'::regclass) NOT NULL,
    school_attribute_id integer NOT NULL,
    value character varying(50),
    created_at time with time zone,
    is_valid boolean DEFAULT true,
    sort_order integer DEFAULT 10
);


ALTER TABLE petel_schema.school_attribute_types_values OWNER TO postgres;

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
-- Name: school_attributes; Type: TABLE; Schema: petel_schema; Owner: postgres
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


ALTER TABLE petel_schema.school_attributes OWNER TO postgres;

--
-- Name: school_attributes_types; Type: TABLE; Schema: petel_schema; Owner: postgres
--

CREATE TABLE petel_schema.school_attributes_types (
    id integer NOT NULL,
    name character varying(25),
    created_at time with time zone,
    attribute_value_type character varying(25),
    hebrew_name character varying,
    year_id integer
);


ALTER TABLE petel_schema.school_attributes_types OWNER TO postgres;

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
    updated_at timestamp with time zone DEFAULT now(),
    end_hour time without time zone
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
-- Name: school_student_pricing_elements; Type: TABLE; Schema: petel_schema; Owner: postgres
--

CREATE TABLE petel_schema.school_student_pricing_elements (
    id integer NOT NULL,
    school_student integer NOT NULL,
    pricing_element integer NOT NULL,
    price numeric(7,2),
    determinig_factor character varying(30)
);


ALTER TABLE petel_schema.school_student_pricing_elements OWNER TO postgres;

--
-- Name: school_student_pricing_elements_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: postgres
--

CREATE SEQUENCE petel_schema.school_student_pricing_elements_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.school_student_pricing_elements_id_seq OWNER TO postgres;

--
-- Name: school_student_pricing_elements_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: postgres
--

ALTER SEQUENCE petel_schema.school_student_pricing_elements_id_seq OWNED BY petel_schema.school_student_pricing_elements.id;


--
-- Name: school_students_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: postgres
--

CREATE SEQUENCE petel_schema.school_students_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.school_students_id_seq OWNER TO postgres;

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
-- Name: school_tracks; Type: TABLE; Schema: petel_schema; Owner: postgres
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


ALTER TABLE petel_schema.school_tracks OWNER TO postgres;

--
-- Name: school_tracks_seq; Type: SEQUENCE; Schema: petel_schema; Owner: postgres
--

CREATE SEQUENCE petel_schema.school_tracks_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    MAXVALUE 9999999
    CACHE 1;


ALTER SEQUENCE petel_schema.school_tracks_seq OWNER TO postgres;

--
-- Name: school_tracks_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: postgres
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
-- Name: special_needs_characterizations; Type: TABLE; Schema: petel_schema; Owner: postgres
--

CREATE TABLE petel_schema.special_needs_characterizations (
    id integer NOT NULL,
    name character varying(50),
    foreign_id integer,
    user_id integer DEFAULT 0
);


ALTER TABLE petel_schema.special_needs_characterizations OWNER TO postgres;

--
-- Name: special_needs_pricing_categories; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.special_needs_pricing_categories (
    id integer NOT NULL,
    pricing_element integer NOT NULL,
    category integer NOT NULL,
    is_lowest_level boolean,
    price numeric(10,2),
    user_id integer,
    calculation_level CHARACTER VARYING(50)
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
    created_at timestamp with time zone DEFAULT now(),
    calculation_level character varying(25) COLLATE pg_catalog."default",
    sort_order integer NOT NULL DEFAULT 10,
    attribute_to_check character varying(50) COLLATE pg_catalog."default";
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


CREATE SEQUENCE IF NOT EXISTS petel_schema.special_needs_pricing_steps_id_seq
    INCREMENT 1
    START 1
    MINVALUE 1
    MAXVALUE 2147483647
    CACHE 1;

ALTER SEQUENCE petel_schema.special_needs_pricing_steps_id_seq
    OWNED BY petel_schema.special_needs_pricing_steps.id;

ALTER SEQUENCE petel_schema.special_needs_pricing_steps_id_seq
    OWNER TO "PetelAdmin";

CREATE TABLE IF NOT EXISTS petel_schema.special_needs_pricing_steps
(
    id integer NOT NULL DEFAULT nextval('petel_schema.special_needs_pricing_steps_id_seq'::regclass),
    pricing_element integer NOT NULL,
    category integer NOT NULL,
    object_check character varying(50) COLLATE pg_catalog."default" NOT NULL,
    object_element_check character varying(50) COLLATE pg_catalog."default" NOT NULL,
    object_element_value character varying(50) COLLATE pg_catalog."default" NOT NULL,
    price numeric(10,2),
    user_id integer,
    CONSTRAINT special_needs_pricing_steps_pk PRIMARY KEY (id),
    CONSTRAINT special_needs_pricing_steps_uc UNIQUE (pricing_element, category, object_check, object_element_check, object_element_value),
    CONSTRAINT special_needs_pricing_steps_pricing_element_fk FOREIGN KEY (pricing_element)
        REFERENCES petel_schema.special_needs_pricing_elements (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
)

--
-- Name: student_school_years_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: postgres
--

CREATE SEQUENCE petel_schema.student_school_years_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.student_school_years_id_seq OWNER TO postgres;

--
-- Name: student_school_years; Type: TABLE; Schema: petel_schema; Owner: postgres
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


ALTER TABLE petel_schema.student_school_years OWNER TO postgres;

--
-- Name: tracks_seq; Type: SEQUENCE; Schema: petel_schema; Owner: postgres
--

CREATE SEQUENCE petel_schema.tracks_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.tracks_seq OWNER TO postgres;

--
-- Name: tracks; Type: TABLE; Schema: petel_schema; Owner: postgres
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


ALTER TABLE petel_schema.tracks OWNER TO postgres;

--
-- Name: student_school_years_registration_summary_vw; Type: VIEW; Schema: petel_schema; Owner: postgres
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


ALTER VIEW petel_schema.student_school_years_registration_summary_vw OWNER TO postgres;

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
-- Name: students; Type: TABLE; Schema: petel_schema; Owner: postgres
--

CREATE TABLE petel_schema.students (
    id integer DEFAULT nextval('petel_schema.students_seq'::regclass) NOT NULL,
    person_id integer NOT NULL,
    user_id integer,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


ALTER TABLE petel_schema.students OWNER TO postgres;

--
-- Name: student_school_years_registration_vw; Type: VIEW; Schema: petel_schema; Owner: postgres
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


ALTER VIEW petel_schema.student_school_years_registration_vw OWNER TO postgres;

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
-- Name: tracks_level_seq; Type: SEQUENCE; Schema: petel_schema; Owner: postgres
--

CREATE SEQUENCE petel_schema.tracks_level_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    MAXVALUE 99999999
    CACHE 1;


ALTER SEQUENCE petel_schema.tracks_level_seq OWNER TO postgres;

--
-- Name: tracks_levels; Type: TABLE; Schema: petel_schema; Owner: postgres
--

CREATE TABLE petel_schema.tracks_levels (
    id integer DEFAULT nextval('petel_schema.tracks_level_seq'::regclass) NOT NULL,
    school_track_id integer NOT NULL,
    level character varying(15),
    min_hours integer NOT NULL,
    max_hours integer,
    available_for_classes character varying(3)[]
);


ALTER TABLE petel_schema.tracks_levels OWNER TO postgres;

--
-- Name: tracks_pricing; Type: TABLE; Schema: petel_schema; Owner: postgres
--

CREATE TABLE petel_schema.tracks_pricing (
    id integer NOT NULL,
    school_track_id integer NOT NULL,
    price numeric(10,2),
    category integer,
    level_id integer
);


ALTER TABLE petel_schema.tracks_pricing OWNER TO postgres;

--
-- Name: tracks_pricing_seq; Type: SEQUENCE; Schema: petel_schema; Owner: postgres
--

CREATE SEQUENCE petel_schema.tracks_pricing_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    MAXVALUE 10000000
    CACHE 1;


ALTER SEQUENCE petel_schema.tracks_pricing_seq OWNER TO postgres;

--
-- Name: tracks_pricing_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: postgres
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
-- Name: alert_levels id; Type: DEFAULT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.alert_levels ALTER COLUMN id SET DEFAULT nextval('petel_schema.alert_levels_id_seq'::regclass);


--
-- Name: alert_links id; Type: DEFAULT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.alert_links ALTER COLUMN id SET DEFAULT nextval('petel_schema.alert_links_id_seq'::regclass);


--
-- Name: alert_types id; Type: DEFAULT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.alert_types ALTER COLUMN id SET DEFAULT nextval('petel_schema.alert_types_id_seq'::regclass);


--
-- Name: alerts id; Type: DEFAULT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.alerts ALTER COLUMN id SET DEFAULT nextval('petel_schema.alerts_id_seq'::regclass);


--
-- Name: document_links id; Type: DEFAULT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.document_links ALTER COLUMN id SET DEFAULT nextval('petel_schema.document_links_id_seq'::regclass);


--
-- Name: document_status_types id; Type: DEFAULT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.document_status_types ALTER COLUMN id SET DEFAULT nextval('petel_schema.document_status_types_id_seq'::regclass);


--
-- Name: document_types id; Type: DEFAULT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.document_types ALTER COLUMN id SET DEFAULT nextval('petel_schema.document_types_id_seq'::regclass);


--
-- Name: documents id; Type: DEFAULT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.documents ALTER COLUMN id SET DEFAULT nextval('petel_schema.documents_id_seq'::regclass);


--
-- Name: school_student_pricing_elements id; Type: DEFAULT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_student_pricing_elements ALTER COLUMN id SET DEFAULT nextval('petel_schema.school_student_pricing_elements_id_seq'::regclass);


--
-- Name: school_tracks id; Type: DEFAULT; Schema: petel_schema; Owner: postgres
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
-- Name: tracks_pricing id; Type: DEFAULT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.tracks_pricing ALTER COLUMN id SET DEFAULT nextval('petel_schema.tracks_pricing_seq'::regclass);


--
-- Name: tracks School_tracks_per_year; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.tracks
    ADD CONSTRAINT "School_tracks_per_year" UNIQUE (year_id, external_code);


--
-- Name: roles_actions action_roles_PK; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.roles_actions
    ADD CONSTRAINT "action_roles_PK" PRIMARY KEY (id);


--
-- Name: roles_actions action_roles_uq; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.roles_actions
    ADD CONSTRAINT action_roles_uq UNIQUE (role_id, action_id);


--
-- Name: alert_levels alert_levels_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.alert_levels
    ADD CONSTRAINT alert_levels_pkey PRIMARY KEY (id);


--
-- Name: alert_links alert_links_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.alert_links
    ADD CONSTRAINT alert_links_pkey PRIMARY KEY (id);


--
-- Name: alert_statuses alert_statuses_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.alert_statuses
    ADD CONSTRAINT alert_statuses_pkey PRIMARY KEY (id);


--
-- Name: alert_types alert_types_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.alert_types
    ADD CONSTRAINT alert_types_pkey PRIMARY KEY (id);


--
-- Name: alerts alerts_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.alerts
    ADD CONSTRAINT alerts_pkey PRIMARY KEY (id);


--
-- Name: budget_statuses budget_statuses_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.budget_statuses
    ADD CONSTRAINT budget_statuses_pkey PRIMARY KEY (id);


--
-- Name: councils councils_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.councils
    ADD CONSTRAINT councils_pkey PRIMARY KEY (id);


--
-- Name: document_links document_links_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.document_links
    ADD CONSTRAINT document_links_pkey PRIMARY KEY (id);


--
-- Name: document_status_types document_status_types_pk; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.document_status_types
    ADD CONSTRAINT document_status_types_pk PRIMARY KEY (id);


--
-- Name: document_types document_types_name_key; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.document_types
    ADD CONSTRAINT document_types_name_key UNIQUE (name);


--
-- Name: document_types document_types_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.document_types
    ADD CONSTRAINT document_types_pkey PRIMARY KEY (id);


--
-- Name: documents documents_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.documents
    ADD CONSTRAINT documents_pkey PRIMARY KEY (id);


--
-- Name: entities entities_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.entities
    ADD CONSTRAINT entities_pkey PRIMARY KEY (id);


--
-- Name: entity_types entity_types_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.entity_types
    ADD CONSTRAINT entity_types_pkey PRIMARY KEY (id);


--
-- Name: genders genders_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.genders
    ADD CONSTRAINT genders_pkey PRIMARY KEY (id);


--
-- Name: hebrew_years hebrew_years_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.hebrew_years
    ADD CONSTRAINT hebrew_years_pkey PRIMARY KEY (id);


--
-- Name: persons persons_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.persons
    ADD CONSTRAINT persons_pkey PRIMARY KEY (id);


--
-- Name: special_needs_pricing_categories pricing_element_category_uc; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.special_needs_pricing_categories
    ADD CONSTRAINT pricing_element_category_uc UNIQUE (pricing_element, category);


--
-- Name: roles roles_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.roles
    ADD CONSTRAINT roles_pkey PRIMARY KEY (id);


--
-- Name: school_additional_study_programs school_additional_study_programs_pk; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_additional_study_programs
    ADD CONSTRAINT school_additional_study_programs_pk PRIMARY KEY (id);


--
-- Name: school_attribute_types_values school_attribute_types_values_pk; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_attribute_types_values
    ADD CONSTRAINT school_attribute_types_values_pk PRIMARY KEY (id);


--
-- Name: school_attributes school_attributes_pk; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_attributes
    ADD CONSTRAINT school_attributes_pk PRIMARY KEY (id);


--
-- Name: school_attributes_types school_attributes_types_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_attributes_types
    ADD CONSTRAINT school_attributes_types_pkey PRIMARY KEY (id);


--
-- Name: school_hours_budget school_budget_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.school_hours_budget
    ADD CONSTRAINT school_budget_pkey PRIMARY KEY (id);


--
-- Name: school_classes school_classes_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.school_classes
    ADD CONSTRAINT school_classes_pkey PRIMARY KEY (id);


--
-- Name: school_classes school_classes_uq; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.school_classes
    ADD CONSTRAINT school_classes_uq UNIQUE (school_year_id, name);


--
-- Name: school_grades school_grades_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.school_grades
    ADD CONSTRAINT school_grades_pkey PRIMARY KEY (id);


--
-- Name: school_grades school_grades_uq; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.school_grades
    ADD CONSTRAINT school_grades_uq UNIQUE (name);


--
-- Name: school_student_pricing_elements school_student_pricing_elements_pk; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_student_pricing_elements
    ADD CONSTRAINT school_student_pricing_elements_pk PRIMARY KEY (id);


--
-- Name: school_students school_students_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.school_students
    ADD CONSTRAINT school_students_pkey PRIMARY KEY (id);


--
-- Name: tracks_levels school_tracks_level_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.tracks_levels
    ADD CONSTRAINT school_tracks_level_pkey PRIMARY KEY (id);


--
-- Name: school_tracks school_tracks_pk; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_tracks
    ADD CONSTRAINT school_tracks_pk PRIMARY KEY (id);


--
-- Name: tracks school_tracks_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.tracks
    ADD CONSTRAINT school_tracks_pkey PRIMARY KEY (id);


--
-- Name: tracks_pricing school_tracks_pricing_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.tracks_pricing
    ADD CONSTRAINT school_tracks_pricing_pkey PRIMARY KEY (id);


--
-- Name: school_years school_years_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.school_years
    ADD CONSTRAINT school_years_pkey PRIMARY KEY (id);


--
-- Name: schools schools_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.schools
    ADD CONSTRAINT schools_pkey PRIMARY KEY (id);


--
-- Name: special_needs_characterizations special_needs_characterizations_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.special_needs_characterizations
    ADD CONSTRAINT special_needs_characterizations_pkey PRIMARY KEY (id);


--
-- Name: special_needs_pricing_categories special_needs_pricing_categories_pk; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.special_needs_pricing_categories
    ADD CONSTRAINT special_needs_pricing_categories_pk PRIMARY KEY (id);


--
-- Name: special_needs_pricing_elements special_needs_pricing_elements_pk; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.special_needs_pricing_elements
    ADD CONSTRAINT special_needs_pricing_elements_pk PRIMARY KEY (id);


--
-- Name: student_school_years student_school_years_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.student_school_years
    ADD CONSTRAINT student_school_years_pkey PRIMARY KEY (id);


--
-- Name: students students_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.students
    ADD CONSTRAINT students_pkey PRIMARY KEY (id);


--
-- Name: system_actions system_actions_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.system_actions
    ADD CONSTRAINT system_actions_pkey PRIMARY KEY (id);


--
-- Name: system_attributes system_attributes_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.system_attributes
    ADD CONSTRAINT system_attributes_pkey PRIMARY KEY (id);


--
-- Name: special_needs_pricing_elements unique_name_per_year_uc; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.special_needs_pricing_elements
    ADD CONSTRAINT unique_name_per_year_uc UNIQUE (year_id, name);


--
-- Name: schools unique_school; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.schools
    ADD CONSTRAINT unique_school UNIQUE (entity_id, school_year_id, version);


--
-- Name: school_students unique_school_student; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.school_students
    ADD CONSTRAINT unique_school_student UNIQUE (id_number, school_year_id, version);


--
-- Name: school_years unique_school_year_per_school; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.school_years
    ADD CONSTRAINT unique_school_year_per_school UNIQUE (school_id, hebrew_year_name);


--
-- Name: student_school_years unique_student_school_year; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.student_school_years
    ADD CONSTRAINT unique_student_school_year UNIQUE (student_id, school_year_id);


--
-- Name: users unique_username_per_entity_id; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.users
    ADD CONSTRAINT unique_username_per_entity_id UNIQUE (username, entity_id);


--
-- Name: user_roles user_roles_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.user_roles
    ADD CONSTRAINT user_roles_pkey PRIMARY KEY (id);


--
-- Name: users users_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);


--
-- Name: idx_additional_study_is_last_version; Type: INDEX; Schema: petel_schema; Owner: postgres
--

CREATE INDEX idx_additional_study_is_last_version ON petel_schema.school_additional_study_programs USING btree (is_last_version);


--
-- Name: idx_additional_study_master_id; Type: INDEX; Schema: petel_schema; Owner: postgres
--

CREATE INDEX idx_additional_study_master_id ON petel_schema.school_additional_study_programs USING btree (master_id);


--
-- Name: idx_unique_document_link_entity; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE UNIQUE INDEX idx_unique_document_link_entity ON petel_schema.document_links USING btree (document_id, entity_id) WHERE (entity_id IS NOT NULL);


--
-- Name: idx_unique_document_link_student; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE UNIQUE INDEX idx_unique_document_link_student ON petel_schema.document_links USING btree (document_id, school_student_id) WHERE (school_student_id IS NOT NULL);


--
-- Name: idx_unique_document_version; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE UNIQUE INDEX idx_unique_document_version ON petel_schema.documents USING btree (master_document_id, version);


--
-- Name: entities set_timestamp_entities; Type: TRIGGER; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TRIGGER set_timestamp_entities BEFORE UPDATE ON petel_schema.entities FOR EACH ROW EXECUTE FUNCTION petel_schema.trigger_set_timestamp();


--
-- Name: entity_types set_timestamp_entity_types; Type: TRIGGER; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TRIGGER set_timestamp_entity_types BEFORE UPDATE ON petel_schema.entity_types FOR EACH ROW EXECUTE FUNCTION petel_schema.trigger_set_timestamp();


--
-- Name: roles set_timestamp_roles; Type: TRIGGER; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TRIGGER set_timestamp_roles BEFORE UPDATE ON petel_schema.roles FOR EACH ROW EXECUTE FUNCTION petel_schema.trigger_set_timestamp();


--
-- Name: school_years set_timestamp_school_years; Type: TRIGGER; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TRIGGER set_timestamp_school_years BEFORE UPDATE ON petel_schema.school_years FOR EACH ROW EXECUTE FUNCTION petel_schema.trigger_set_timestamp();


--
-- Name: schools set_timestamp_schools; Type: TRIGGER; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TRIGGER set_timestamp_schools BEFORE UPDATE ON petel_schema.schools FOR EACH ROW EXECUTE FUNCTION petel_schema.trigger_set_timestamp();


--
-- Name: system_actions set_timestamp_system_actions; Type: TRIGGER; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TRIGGER set_timestamp_system_actions BEFORE UPDATE ON petel_schema.system_actions FOR EACH ROW EXECUTE FUNCTION petel_schema.trigger_set_timestamp();


--
-- Name: user_roles set_timestamp_user_roles; Type: TRIGGER; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TRIGGER set_timestamp_user_roles BEFORE UPDATE ON petel_schema.user_roles FOR EACH ROW EXECUTE FUNCTION petel_schema.trigger_set_timestamp();


--
-- Name: users set_timestamp_users; Type: TRIGGER; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TRIGGER set_timestamp_users BEFORE UPDATE ON petel_schema.users FOR EACH ROW EXECUTE FUNCTION petel_schema.trigger_set_timestamp();


--
-- Name: roles_actions action_roles_fk1; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.roles_actions
    ADD CONSTRAINT action_roles_fk1 FOREIGN KEY (role_id) REFERENCES petel_schema.roles(id);


--
-- Name: roles_actions action_roles_fk2; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.roles_actions
    ADD CONSTRAINT action_roles_fk2 FOREIGN KEY (action_id) REFERENCES petel_schema.system_actions(id);


--
-- Name: schools characterizations_id_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.schools
    ADD CONSTRAINT characterizations_id_fk FOREIGN KEY (characterization_id) REFERENCES petel_schema.special_needs_characterizations(id) NOT VALID;


--
-- Name: schools contact_person_person_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.schools
    ADD CONSTRAINT contact_person_person_fkey FOREIGN KEY (contact_person) REFERENCES petel_schema.persons(id);


--
-- Name: document_links document_links_document_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.document_links
    ADD CONSTRAINT document_links_document_id_fkey FOREIGN KEY (document_id) REFERENCES petel_schema.documents(id);


--
-- Name: document_links document_links_entity_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.document_links
    ADD CONSTRAINT document_links_entity_id_fkey FOREIGN KEY (entity_id) REFERENCES petel_schema.entities(id);


--
-- Name: document_links document_links_school_student_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.document_links
    ADD CONSTRAINT document_links_school_student_id_fkey FOREIGN KEY (school_student_id) REFERENCES petel_schema.school_students(id);


--
-- Name: documents documents_document_type_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.documents
    ADD CONSTRAINT documents_document_type_id_fkey FOREIGN KEY (document_type_id) REFERENCES petel_schema.document_types(id);


--
-- Name: entities entites_contact_person_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.entities
    ADD CONSTRAINT entites_contact_person_fk FOREIGN KEY (contact_person) REFERENCES petel_schema.persons(id) NOT VALID;


--
-- Name: entities entities_entity_type_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.entities
    ADD CONSTRAINT entities_entity_type_id_fkey FOREIGN KEY (entity_type_id) REFERENCES petel_schema.entity_types(id);


--
-- Name: school_additional_study_programs fk_additional_study_master; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_additional_study_programs
    ADD CONSTRAINT fk_additional_study_master FOREIGN KEY (master_id) REFERENCES petel_schema.school_additional_study_programs(id);


--
-- Name: documents fk_master_document; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.documents
    ADD CONSTRAINT fk_master_document FOREIGN KEY (master_document_id) REFERENCES petel_schema.documents(id);


--
-- Name: schools inspector_person_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.schools
    ADD CONSTRAINT inspector_person_fkey FOREIGN KEY (inspector) REFERENCES petel_schema.persons(id);


--
-- Name: entities owner_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.entities
    ADD CONSTRAINT owner_fkey FOREIGN KEY (owner) REFERENCES petel_schema.entities(id) NOT VALID;


--
-- Name: schools owner_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.schools
    ADD CONSTRAINT owner_fkey FOREIGN KEY (owner) REFERENCES petel_schema.entities(id);


--
-- Name: persons persons_user_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.persons
    ADD CONSTRAINT persons_user_id_fkey FOREIGN KEY (user_id) REFERENCES petel_schema.users(id);


--
-- Name: schools principal_person_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.schools
    ADD CONSTRAINT principal_person_fkey FOREIGN KEY (principal) REFERENCES petel_schema.persons(id);


--
-- Name: school_additional_study_programs school_additional_study_programs_school_year_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_additional_study_programs
    ADD CONSTRAINT school_additional_study_programs_school_year_fk FOREIGN KEY (school_year_id) REFERENCES petel_schema.school_years(id);


--
-- Name: school_attribute_types_values school_attribute_type_value_attribute_type_id; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_attribute_types_values
    ADD CONSTRAINT school_attribute_type_value_attribute_type_id FOREIGN KEY (school_attribute_id) REFERENCES petel_schema.school_attributes_types(id);


--
-- Name: school_attributes school_attributes_attribute_type_id; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_attributes
    ADD CONSTRAINT school_attributes_attribute_type_id FOREIGN KEY (school_attribute_type_id) REFERENCES petel_schema.school_attributes_types(id);


--
-- Name: school_classes school_classes_fk1; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.school_classes
    ADD CONSTRAINT school_classes_fk1 FOREIGN KEY (school_year_id) REFERENCES petel_schema.school_years(id) NOT VALID;


--
-- Name: school_student_pricing_elements school_student_pricing_elements_pricing_element; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_student_pricing_elements
    ADD CONSTRAINT school_student_pricing_elements_pricing_element FOREIGN KEY (pricing_element) REFERENCES petel_schema.special_needs_pricing_elements(id);


--
-- Name: school_student_pricing_elements school_student_pricing_elements_school_student; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_student_pricing_elements
    ADD CONSTRAINT school_student_pricing_elements_school_student FOREIGN KEY (school_student) REFERENCES petel_schema.school_students(id);


--
-- Name: school_students school_students_gender_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.school_students
    ADD CONSTRAINT school_students_gender_fk FOREIGN KEY (gender) REFERENCES petel_schema.genders(id);


--
-- Name: school_students school_students_school_year_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.school_students
    ADD CONSTRAINT school_students_school_year_id_fkey FOREIGN KEY (school_year_id) REFERENCES petel_schema.school_years(id);


--
-- Name: school_tracks school_tracks_class_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_tracks
    ADD CONSTRAINT school_tracks_class_fk FOREIGN KEY (class_id) REFERENCES petel_schema.school_classes(id);


--
-- Name: school_additional_study_programs school_tracks_class_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_additional_study_programs
    ADD CONSTRAINT school_tracks_class_fk FOREIGN KEY (class_id) REFERENCES petel_schema.school_classes(id);


--
-- Name: school_tracks school_tracks_level_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_tracks
    ADD CONSTRAINT school_tracks_level_fk FOREIGN KEY (track_level_id) REFERENCES petel_schema.tracks_levels(id);


--
-- Name: tracks_levels school_tracks_levels_school_tracks_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.tracks_levels
    ADD CONSTRAINT school_tracks_levels_school_tracks_fk FOREIGN KEY (school_track_id) REFERENCES petel_schema.tracks(id) NOT VALID;


--
-- Name: tracks_pricing school_tracks_pricing_school_tracks_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.tracks_pricing
    ADD CONSTRAINT school_tracks_pricing_school_tracks_fk FOREIGN KEY (school_track_id) REFERENCES petel_schema.tracks(id) NOT VALID;


--
-- Name: tracks_pricing school_tracks_pricing_school_tracks_levels_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.tracks_pricing
    ADD CONSTRAINT school_tracks_pricing_school_tracks_levels_fk FOREIGN KEY (level_id) REFERENCES petel_schema.tracks_levels(id) NOT VALID;


--
-- Name: school_tracks school_tracks_school_year_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_tracks
    ADD CONSTRAINT school_tracks_school_year_fk FOREIGN KEY (school_year_id) REFERENCES petel_schema.school_years(id);


--
-- Name: school_tracks school_tracks_tracks_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.school_tracks
    ADD CONSTRAINT school_tracks_tracks_fk FOREIGN KEY (track_id) REFERENCES petel_schema.tracks(id);


--
-- Name: schools school_year_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.schools
    ADD CONSTRAINT school_year_id_fkey FOREIGN KEY (school_year_id) REFERENCES petel_schema.school_years(id);


--
-- Name: school_years school_years_school_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.school_years
    ADD CONSTRAINT school_years_school_id_fkey FOREIGN KEY (school_id) REFERENCES petel_schema.entities(id);


--
-- Name: school_years school_years_update_user_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.school_years
    ADD CONSTRAINT school_years_update_user_fkey FOREIGN KEY (update_user) REFERENCES petel_schema.users(id);


--
-- Name: schools schools_entity_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.schools
    ADD CONSTRAINT schools_entity_id_fkey FOREIGN KEY (entity_id) REFERENCES petel_schema.entities(id);


--
-- Name: schools schools_entity_type_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.schools
    ADD CONSTRAINT schools_entity_type_id_fkey FOREIGN KEY (entity_type_id) REFERENCES petel_schema.entity_types(id);


--
-- Name: special_needs_pricing_categories special_needs_pricing_categories_pricing_element_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.special_needs_pricing_categories
    ADD CONSTRAINT special_needs_pricing_categories_pricing_element_fk FOREIGN KEY (pricing_element) REFERENCES petel_schema.special_needs_pricing_elements(id);


--
-- Name: special_needs_pricing_elements special_needs_pricing_elements_year_id_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.special_needs_pricing_elements
    ADD CONSTRAINT special_needs_pricing_elements_year_id_fk FOREIGN KEY (year_id) REFERENCES petel_schema.hebrew_years(id);


--
-- Name: student_school_years student_school_years_school_year_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.student_school_years
    ADD CONSTRAINT student_school_years_school_year_id_fkey FOREIGN KEY (school_year_id) REFERENCES petel_schema.school_years(id);


--
-- Name: students students_person_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.students
    ADD CONSTRAINT students_person_id_fkey FOREIGN KEY (person_id) REFERENCES petel_schema.persons(id);


--
-- Name: students students_user_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.students
    ADD CONSTRAINT students_user_id_fkey FOREIGN KEY (user_id) REFERENCES petel_schema.users(id);


--
-- Name: users users_entity_id_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.users
    ADD CONSTRAINT users_entity_id_id_fkey FOREIGN KEY (entity_id) REFERENCES petel_schema.entities(id);


--
-- Name: users users_update_user_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.users
    ADD CONSTRAINT users_update_user_fkey FOREIGN KEY (update_user) REFERENCES petel_schema.users(id);


--

