--
-- PostgreSQL database dump
--

SET row_security = off;

--
-- Name: petel_schema; Type: SCHEMA; Schema: -; Owner: PetelAdmin
--

CREATE SCHEMA petel_schema;


ALTER SCHEMA petel_schema OWNER TO "PetelAdmin";

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
-- Name: action_audit_logs_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.action_audit_logs_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.action_audit_logs_id_seq OWNER TO "PetelAdmin";

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: action_audit_logs; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.action_audit_logs (
    id bigint DEFAULT nextval('petel_schema.action_audit_logs_id_seq'::regclass) NOT NULL,
    user_id integer NOT NULL,
    action_name character varying(200) NOT NULL,
    screen_name character varying(100) NOT NULL,
    function_name character varying(100) NOT NULL,
    event_type character varying(50) NOT NULL,
    result character varying(20) NOT NULL,
    "timestamp" timestamp with time zone DEFAULT now() NOT NULL,
    ip_address character varying(45),
    action_params character varying(500),
    description character varying(1000)
);


ALTER TABLE petel_schema.action_audit_logs OWNER TO "PetelAdmin";

--
-- Name: TABLE action_audit_logs; Type: COMMENT; Schema: petel_schema; Owner: PetelAdmin
--

COMMENT ON TABLE petel_schema.action_audit_logs IS 'Audit log for all action authorization attempts - tracks granted and denied access';


--
-- Name: COLUMN action_audit_logs.user_id; Type: COMMENT; Schema: petel_schema; Owner: PetelAdmin
--

COMMENT ON COLUMN petel_schema.action_audit_logs.user_id IS 'User who attempted the action';


--
-- Name: COLUMN action_audit_logs.action_name; Type: COMMENT; Schema: petel_schema; Owner: PetelAdmin
--

COMMENT ON COLUMN petel_schema.action_audit_logs.action_name IS 'Action identifier (format: screenname_functionname)';


--
-- Name: COLUMN action_audit_logs.event_type; Type: COMMENT; Schema: petel_schema; Owner: PetelAdmin
--

COMMENT ON COLUMN petel_schema.action_audit_logs.event_type IS 'Authorization type: ONCLICK_BUTTON, MENU_NAVIGATION, API_CALL, FILE_UPLOAD, etc.';


--
-- Name: COLUMN action_audit_logs.result; Type: COMMENT; Schema: petel_schema; Owner: PetelAdmin
--

COMMENT ON COLUMN petel_schema.action_audit_logs.result IS 'Authorization result: GRANTED or DENIED';


--
-- Name: COLUMN action_audit_logs.action_params; Type: COMMENT; Schema: petel_schema; Owner: PetelAdmin
--

COMMENT ON COLUMN petel_schema.action_audit_logs.action_params IS 'Parameters passed to action (e.g., yearId, schoolId, file name)';


--
-- Name: COLUMN action_audit_logs.description; Type: COMMENT; Schema: petel_schema; Owner: PetelAdmin
--

COMMENT ON COLUMN petel_schema.action_audit_logs.description IS 'Optional human-readable description of the action';


--
-- Name: action_types_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.action_types_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.action_types_id_seq OWNER TO "PetelAdmin";

--
-- Name: action_types; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.action_types (
    id smallint DEFAULT nextval('petel_schema.action_types_id_seq'::regclass) NOT NULL,
    name character varying(50) NOT NULL,
    description character varying(255),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    user_id integer DEFAULT 1
);


ALTER TABLE petel_schema.action_types OWNER TO "PetelAdmin";

--
-- Name: actions_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.actions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.actions_id_seq OWNER TO "PetelAdmin";

--
-- Name: actions; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.actions (
    id integer DEFAULT nextval('petel_schema.actions_id_seq'::regclass) NOT NULL,
    name character varying(100) NOT NULL,
    display_name character varying(150),
    description character varying(255),
    action_type_id smallint NOT NULL,
    reference character varying(200),
    sort_order integer DEFAULT 0,
    is_active boolean DEFAULT true,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    user_id integer DEFAULT 1,
    onclick_name character varying(100)
);


ALTER TABLE petel_schema.actions OWNER TO "PetelAdmin";

--
-- Name: additional_study_programs_pricing_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.additional_study_programs_pricing_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    MAXVALUE 2147483647
    CACHE 1;


ALTER SEQUENCE petel_schema.additional_study_programs_pricing_id_seq OWNER TO "PetelAdmin";

--
-- Name: additional_study_programs_pricing; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.additional_study_programs_pricing (
    id integer DEFAULT nextval('petel_schema.additional_study_programs_pricing_id_seq'::regclass) NOT NULL,
    year_id integer NOT NULL,
    students integer NOT NULL,
    price numeric(10,2),
    user_id integer,
    created_at timestamp with time zone DEFAULT now()
);


ALTER TABLE petel_schema.additional_study_programs_pricing OWNER TO "PetelAdmin";

--
-- Name: alert_levels; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.alert_levels (
    id smallint NOT NULL,
    name character varying(25),
    description character varying(25),
    user_id integer DEFAULT 0,
    created_at timestamp with time zone DEFAULT now()
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
    user_id integer DEFAULT 0,
    created_at timestamp with time zone DEFAULT now()
);


ALTER TABLE petel_schema.alert_statuses OWNER TO postgres;

--
-- Name: alert_types; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.alert_types (
    id smallint NOT NULL,
    name character varying(25),
    description character varying(25),
    user_id integer DEFAULT 0,
    created_at timestamp with time zone DEFAULT now()
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

CREATE TABLE IF NOT EXISTS petel_schema.councils
(
    id integer NOT NULL,
    council_code integer NOT NULL,
    name character varying(25) COLLATE pg_catalog."default",
    created_at timestamp with time zone,
    user_id integer DEFAULT 0,
    CONSTRAINT councils_pkey PRIMARY KEY (id)
)

ALTER TABLE petel_schema.councils OWNER TO postgres;

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
-- Name: council_summary_vw; Type: VIEW; Schema: petel_schema; Owner: PetelAdmin
--

CREATE VIEW petel_schema.council_summary_vw AS
 SELECT c.id AS council_id,
    c.council_short_name,
    c.council_long_name,
    sy.year_id,
    count(DISTINCT ss.id) AS number_of_students,
    COALESCE(sum(ss.cost), (0)::numeric) AS total_requested_amount
   FROM ((petel_schema.councils c
     LEFT JOIN petel_schema.school_students ss ON (((c.id = ss.sending_council) AND (ss.is_last_version = true))))
     LEFT JOIN petel_schema.school_years sy ON ((ss.school_year_id = sy.id)))
  WHERE (sy.year_id IS NOT NULL)
  GROUP BY c.id, c.council_short_name, c.council_long_name, sy.year_id;


ALTER VIEW petel_schema.council_summary_vw OWNER TO "PetelAdmin";

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
    created_at timestamp with time zone DEFAULT now()
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
-- Name: menu_items; Type: TABLE; Schema: petel_schema; Owner: postgres
--

CREATE TABLE petel_schema.menu_items (
    id integer NOT NULL,
    name character varying(50) NOT NULL,
    reference character varying(100) NOT NULL,
    text character varying(100) NOT NULL,
    action_id integer,
    sort_order integer DEFAULT 0 NOT NULL,
    is_active boolean DEFAULT true NOT NULL
);


ALTER TABLE petel_schema.menu_items OWNER TO postgres;

--
-- Name: menu_items_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: postgres
--

CREATE SEQUENCE petel_schema.menu_items_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.menu_items_id_seq OWNER TO postgres;

--
-- Name: menu_items_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: postgres
--

ALTER SEQUENCE petel_schema.menu_items_id_seq OWNED BY petel_schema.menu_items.id;


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
-- Name: roles_actions_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.roles_actions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.roles_actions_id_seq OWNER TO "PetelAdmin";

--
-- Name: roles_actions_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: PetelAdmin
--

ALTER SEQUENCE petel_schema.roles_actions_id_seq OWNED BY petel_schema.roles_actions.id;


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
    user_id integer DEFAULT 0 NOT NULL,
    version integer DEFAULT 1 NOT NULL,
    is_last_version boolean DEFAULT true NOT NULL,
    master_id integer,
    cost numeric(10,2),
    approved_amount numeric(10,2),
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    hourly_cost numeric(10,2)
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
    is_valid boolean DEFAULT true,
    sort_order integer DEFAULT 10,
    created_at timestamp with time zone
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
    user_id integer NOT NULL,
    is_last_version boolean DEFAULT true,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


ALTER TABLE petel_schema.school_attributes OWNER TO postgres;

--
-- Name: school_attributes_types; Type: TABLE; Schema: petel_schema; Owner: postgres
--

CREATE TABLE petel_schema.school_attributes_types (
    id integer NOT NULL,
    name character varying(25),
    attribute_value_type character varying(25),
    hebrew_name character varying,
    year_id integer,
    created_at timestamp with time zone
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
    determining_factor character varying(100),
    hours smallint
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
    created_at timestamp with time zone DEFAULT now() NOT NULL
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
-- Name: sign_language_translators_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.sign_language_translators_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE petel_schema.sign_language_translators_seq OWNER TO "PetelAdmin";

--
-- Name: sign_language_translators; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.sign_language_translators (
    id integer DEFAULT nextval('petel_schema.sign_language_translators_seq'::regclass) NOT NULL,
    school_year_id integer NOT NULL,
    person_id integer NOT NULL,
    hours_employed numeric(6,2) NOT NULL,
    created_at timestamp without time zone DEFAULT now(),
    updated_at timestamp without time zone DEFAULT now(),
    user_id integer NOT NULL
);


ALTER TABLE petel_schema.sign_language_translators OWNER TO "PetelAdmin";

--
-- Name: TABLE sign_language_translators; Type: COMMENT; Schema: petel_schema; Owner: PetelAdmin
--

COMMENT ON TABLE petel_schema.sign_language_translators IS 'Stores sign language translators employed by schools per school year';


--
-- Name: COLUMN sign_language_translators.hours_employed; Type: COMMENT; Schema: petel_schema; Owner: PetelAdmin
--

COMMENT ON COLUMN petel_schema.sign_language_translators.hours_employed IS 'Number of hours employed for the school year';


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
    next_level character varying(20)
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
    created_at timestamp with time zone DEFAULT now(),
    calculation_level character varying(25),
    sort_order integer DEFAULT 10 NOT NULL,
    attribute_to_check character varying(50)
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
-- Name: special_needs_pricing_steps; Type: TABLE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE TABLE petel_schema.special_needs_pricing_steps (
    id integer NOT NULL,
    pricing_element integer NOT NULL,
    category integer NOT NULL,
    object_check character varying(50) NOT NULL,
    object_element_check character varying(50) NOT NULL,
    object_element_value character varying(50) NOT NULL,
    price numeric(10,2),
    user_id integer
);


ALTER TABLE petel_schema.special_needs_pricing_steps OWNER TO "PetelAdmin";

--
-- Name: special_needs_pricing_steps_id_seq; Type: SEQUENCE; Schema: petel_schema; Owner: PetelAdmin
--

CREATE SEQUENCE petel_schema.special_needs_pricing_steps_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    MAXVALUE 2147483647
    CACHE 1;


ALTER SEQUENCE petel_schema.special_needs_pricing_steps_id_seq OWNER TO "PetelAdmin";

--
-- Name: special_needs_pricing_steps_id_seq; Type: SEQUENCE OWNED BY; Schema: petel_schema; Owner: PetelAdmin
--

ALTER SEQUENCE petel_schema.special_needs_pricing_steps_id_seq OWNED BY petel_schema.special_needs_pricing_steps.id;


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
    updated_at timestamp with time zone DEFAULT now(),
    otp_secret character varying(255),
    otp_enabled boolean DEFAULT false,
    otp_verified boolean DEFAULT false
);


ALTER TABLE petel_schema.users OWNER TO "PetelAdmin";

--
-- Name: vw_role_actions; Type: VIEW; Schema: petel_schema; Owner: postgres
--

CREATE VIEW petel_schema.vw_role_actions AS
 SELECT ra.id,
    ra.role_id,
    r.name AS role_name,
    ra.action_id,
    a.name AS action_name,
    a.display_name,
    a.description,
    at.name AS action_type,
    a.reference,
    ra.action_level,
    ra.updated_at
   FROM (((petel_schema.roles_actions ra
     JOIN petel_schema.roles r ON ((ra.role_id = r.id)))
     JOIN petel_schema.actions a ON ((ra.action_id = a.id)))
     JOIN petel_schema.action_types at ON ((a.action_type_id = at.id)))
  WHERE (a.is_active = true);


ALTER VIEW petel_schema.vw_role_actions OWNER TO postgres;

--
-- Name: vw_user_actions; Type: VIEW; Schema: petel_schema; Owner: postgres
--

CREATE VIEW petel_schema.vw_user_actions AS
 SELECT DISTINCT ur.user_id,
    u.username,
    ur.role_id,
    r.name AS role_name,
    ra.action_id,
    a.name AS action_name,
    a.display_name,
    at.name AS action_type,
    a.reference
   FROM (((((petel_schema.user_roles ur
     JOIN petel_schema.users u ON ((ur.user_id = u.id)))
     JOIN petel_schema.roles r ON ((ur.role_id = r.id)))
     JOIN petel_schema.roles_actions ra ON ((r.id = ra.role_id)))
     JOIN petel_schema.actions a ON ((ra.action_id = a.id)))
     JOIN petel_schema.action_types at ON ((a.action_type_id = at.id)))
  WHERE ((ur.is_active = true) AND (a.is_active = true));


ALTER VIEW petel_schema.vw_user_actions OWNER TO postgres;

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
-- Name: menu_items id; Type: DEFAULT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.menu_items ALTER COLUMN id SET DEFAULT nextval('petel_schema.menu_items_id_seq'::regclass);


--
-- Name: roles_actions id; Type: DEFAULT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.roles_actions ALTER COLUMN id SET DEFAULT nextval('petel_schema.roles_actions_id_seq'::regclass);


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
-- Name: special_needs_pricing_steps id; Type: DEFAULT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.special_needs_pricing_steps ALTER COLUMN id SET DEFAULT nextval('petel_schema.special_needs_pricing_steps_id_seq'::regclass);


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
-- Name: action_audit_logs action_audit_logs_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.action_audit_logs
    ADD CONSTRAINT action_audit_logs_pkey PRIMARY KEY (id);


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
-- Name: action_types action_types_name_key; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.action_types
    ADD CONSTRAINT action_types_name_key UNIQUE (name);


--
-- Name: action_types action_types_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.action_types
    ADD CONSTRAINT action_types_pkey PRIMARY KEY (id);


--
-- Name: actions actions_pk; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.actions
    ADD CONSTRAINT actions_pk PRIMARY KEY (id);


--
-- Name: actions actions_unique_pk; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.actions
    ADD CONSTRAINT actions_unique_pk UNIQUE (name);


--
-- Name: additional_study_programs_pricing additional_study_programs_pricing_pk; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.additional_study_programs_pricing
    ADD CONSTRAINT additional_study_programs_pricing_pk PRIMARY KEY (id);


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
-- Name: menu_items menu_items_pkey; Type: CONSTRAINT; Schema: petel_schema; Owner: postgres
--

ALTER TABLE ONLY petel_schema.menu_items
    ADD CONSTRAINT menu_items_pkey PRIMARY KEY (id);


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
-- Name: sign_language_translators sign_language_translators_pk; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.sign_language_translators
    ADD CONSTRAINT sign_language_translators_pk PRIMARY KEY (id);


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
-- Name: special_needs_pricing_steps special_needs_pricing_steps_pk; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.special_needs_pricing_steps
    ADD CONSTRAINT special_needs_pricing_steps_pk PRIMARY KEY (id);


--
-- Name: special_needs_pricing_steps special_needs_pricing_steps_uc; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.special_needs_pricing_steps
    ADD CONSTRAINT special_needs_pricing_steps_uc UNIQUE (pricing_element, category, object_check, object_element_check, object_element_value);


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
-- Name: sign_language_translators unique_translator_per_year; Type: CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.sign_language_translators
    ADD CONSTRAINT unique_translator_per_year UNIQUE (school_year_id, person_id);


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
-- Name: action_audit_logs_action_name_idx; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX action_audit_logs_action_name_idx ON petel_schema.action_audit_logs USING btree (action_name);


--
-- Name: action_audit_logs_result_idx; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX action_audit_logs_result_idx ON petel_schema.action_audit_logs USING btree (result);


--
-- Name: action_audit_logs_timestamp_idx; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX action_audit_logs_timestamp_idx ON petel_schema.action_audit_logs USING btree ("timestamp");


--
-- Name: action_audit_logs_user_id_idx; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX action_audit_logs_user_id_idx ON petel_schema.action_audit_logs USING btree (user_id);


--
-- Name: action_audit_logs_user_timestamp_idx; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX action_audit_logs_user_timestamp_idx ON petel_schema.action_audit_logs USING btree (user_id, "timestamp");


--
-- Name: actions_action_type_id_idx; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX actions_action_type_id_idx ON petel_schema.actions USING btree (action_type_id);


--
-- Name: actions_action_type_id_is_active_idx; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX actions_action_type_id_is_active_idx ON petel_schema.actions USING btree (action_type_id, is_active);


--
-- Name: actions_is_active_idx; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX actions_is_active_idx ON petel_schema.actions USING btree (is_active);


--
-- Name: actions_name_uq; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE UNIQUE INDEX actions_name_uq ON petel_schema.actions USING btree (name);


--
-- Name: actions_reference_idx; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX actions_reference_idx ON petel_schema.actions USING btree (reference);


--
-- Name: idx_action_audit_logs_event_type; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX idx_action_audit_logs_event_type ON petel_schema.action_audit_logs USING btree (event_type);


--
-- Name: idx_action_audit_logs_user_result; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX idx_action_audit_logs_user_result ON petel_schema.action_audit_logs USING btree (user_id, result, "timestamp" DESC);


--
-- Name: idx_additional_study_is_last_version; Type: INDEX; Schema: petel_schema; Owner: postgres
--

CREATE INDEX idx_additional_study_is_last_version ON petel_schema.school_additional_study_programs USING btree (is_last_version);


--
-- Name: idx_additional_study_master_id; Type: INDEX; Schema: petel_schema; Owner: postgres
--

CREATE INDEX idx_additional_study_master_id ON petel_schema.school_additional_study_programs USING btree (master_id);


--
-- Name: idx_menu_items_action_id; Type: INDEX; Schema: petel_schema; Owner: postgres
--

CREATE INDEX idx_menu_items_action_id ON petel_schema.menu_items USING btree (action_id);


--
-- Name: idx_menu_items_sort_order; Type: INDEX; Schema: petel_schema; Owner: postgres
--

CREATE INDEX idx_menu_items_sort_order ON petel_schema.menu_items USING btree (sort_order);


--
-- Name: idx_sign_language_translators_person; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX idx_sign_language_translators_person ON petel_schema.sign_language_translators USING btree (person_id);


--
-- Name: idx_sign_language_translators_school_year; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX idx_sign_language_translators_school_year ON petel_schema.sign_language_translators USING btree (school_year_id);


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
-- Name: idx_users_otp_enabled; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX idx_users_otp_enabled ON petel_schema.users USING btree (otp_enabled) WHERE (otp_enabled = true);


--
-- Name: roles_actions_action_id_idx; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX roles_actions_action_id_idx ON petel_schema.roles_actions USING btree (action_id);


--
-- Name: roles_actions_role_id_idx; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX roles_actions_role_id_idx ON petel_schema.roles_actions USING btree (role_id);


--
-- Name: user_roles_role_id_idx; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX user_roles_role_id_idx ON petel_schema.user_roles USING btree (role_id);


--
-- Name: user_roles_user_id_idx; Type: INDEX; Schema: petel_schema; Owner: PetelAdmin
--

CREATE INDEX user_roles_user_id_idx ON petel_schema.user_roles USING btree (user_id);


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
-- Name: action_audit_logs action_audit_logs_user_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.action_audit_logs
    ADD CONSTRAINT action_audit_logs_user_id_fkey FOREIGN KEY (user_id) REFERENCES petel_schema.users(id) ON DELETE RESTRICT;


--
-- Name: action_types action_types_user_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.action_types
    ADD CONSTRAINT action_types_user_id_fkey FOREIGN KEY (user_id) REFERENCES petel_schema.users(id) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- Name: actions actions_action_type_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.actions
    ADD CONSTRAINT actions_action_type_id_fkey FOREIGN KEY (action_type_id) REFERENCES petel_schema.action_types(id) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- Name: actions actions_user_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.actions
    ADD CONSTRAINT actions_user_id_fkey FOREIGN KEY (user_id) REFERENCES petel_schema.users(id) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- Name: additional_study_programs_pricing additional_study_programs_pricing_year_id_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.additional_study_programs_pricing
    ADD CONSTRAINT additional_study_programs_pricing_year_id_fk FOREIGN KEY (year_id) REFERENCES petel_schema.hebrew_years(id);


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
-- Name: sign_language_translators fk_sign_language_translators_person; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.sign_language_translators
    ADD CONSTRAINT fk_sign_language_translators_person FOREIGN KEY (person_id) REFERENCES petel_schema.persons(id) ON DELETE RESTRICT;


--
-- Name: sign_language_translators fk_sign_language_translators_school_year; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.sign_language_translators
    ADD CONSTRAINT fk_sign_language_translators_school_year FOREIGN KEY (school_year_id) REFERENCES petel_schema.school_years(id) ON DELETE CASCADE;


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
-- Name: roles_actions roles_actions_action_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.roles_actions
    ADD CONSTRAINT roles_actions_action_id_fkey FOREIGN KEY (action_id) REFERENCES petel_schema.actions(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: roles_actions roles_actions_role_id_fkey; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.roles_actions
    ADD CONSTRAINT roles_actions_role_id_fkey FOREIGN KEY (role_id) REFERENCES petel_schema.roles(id) ON UPDATE CASCADE ON DELETE CASCADE;


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
-- Name: special_needs_pricing_steps special_needs_pricing_steps_pricing_element_fk; Type: FK CONSTRAINT; Schema: petel_schema; Owner: PetelAdmin
--

ALTER TABLE ONLY petel_schema.special_needs_pricing_steps
    ADD CONSTRAINT special_needs_pricing_steps_pricing_element_fk FOREIGN KEY (pricing_element) REFERENCES petel_schema.special_needs_pricing_elements(id);


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
-- Name: TABLE vw_role_actions; Type: ACL; Schema: petel_schema; Owner: postgres
--

GRANT SELECT ON TABLE petel_schema.vw_role_actions TO "PetelAdmin";


--
-- Name: TABLE vw_user_actions; Type: ACL; Schema: petel_schema; Owner: postgres
--

GRANT SELECT ON TABLE petel_schema.vw_user_actions TO "PetelAdmin";


--
-- PostgreSQL database dump complete
--



