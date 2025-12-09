-- Create action_audit_logs table for security auditing
-- Records all action authorization attempts (granted/denied)

CREATE SEQUENCE petel_schema.action_audit_logs_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

ALTER SEQUENCE petel_schema.action_audit_logs_id_seq OWNER TO "PetelAdmin";

CREATE TABLE petel_schema.action_audit_logs (
    id bigint DEFAULT nextval('petel_schema.action_audit_logs_id_seq'::regclass) NOT NULL PRIMARY KEY,
    user_id integer NOT NULL,
    action_name character varying(200) NOT NULL,
    screen_name character varying(100) NOT NULL,
    function_name character varying(100) NOT NULL,
    event_type character varying(50) NOT NULL,
    result character varying(20) NOT NULL,
    timestamp timestamp with time zone DEFAULT now() NOT NULL,
    ip_address character varying(45),
    user_agent character varying(500),
    CONSTRAINT action_audit_logs_user_id_fkey FOREIGN KEY (user_id)
        REFERENCES petel_schema.users(id) ON DELETE RESTRICT
);

ALTER TABLE petel_schema.action_audit_logs OWNER TO "PetelAdmin";

-- Create indexes for performance
CREATE INDEX action_audit_logs_user_id_idx ON petel_schema.action_audit_logs(user_id);
CREATE INDEX action_audit_logs_timestamp_idx ON petel_schema.action_audit_logs(timestamp);
CREATE INDEX action_audit_logs_result_idx ON petel_schema.action_audit_logs(result);
CREATE INDEX action_audit_logs_user_timestamp_idx ON petel_schema.action_audit_logs(user_id, timestamp);
CREATE INDEX action_audit_logs_action_name_idx ON petel_schema.action_audit_logs(action_name);

-- Grant permissions
GRANT SELECT, INSERT ON petel_schema.action_audit_logs TO "PetelAdmin";
GRANT USAGE ON SEQUENCE petel_schema.action_audit_logs_id_seq TO "PetelAdmin";

COMMENT ON TABLE petel_schema.action_audit_logs IS 'Audit log for all action authorization attempts - tracks granted and denied access';
COMMENT ON COLUMN petel_schema.action_audit_logs.user_id IS 'User who attempted the action';
COMMENT ON COLUMN petel_schema.action_audit_logs.action_name IS 'Action identifier (format: screenname_functionname)';
COMMENT ON COLUMN petel_schema.action_audit_logs.result IS 'Authorization result: GRANTED or DENIED';