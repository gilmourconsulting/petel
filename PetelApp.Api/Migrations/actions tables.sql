-- SQL/actions-security-framework.sql
-- Action-Based Security Framework Migration
-- This file creates the foundation for action-based access control
-- Supports menu items, buttons, and page-level actions

-- ============================================================
-- ACTION TYPES TABLE
-- ============================================================
-- Stores the types of actions the system supports
-- Examples: 'menu_item', 'button', 'page_action', 'api_endpoint'

CREATE SEQUENCE petel_schema.action_types_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

ALTER SEQUENCE petel_schema.action_types_id_seq OWNER TO "PetelAdmin";

CREATE TABLE petel_schema.action_types (
    id smallint DEFAULT nextval('petel_schema.action_types_id_seq'::regclass) NOT NULL,
    name character varying(50) NOT NULL UNIQUE,
    description character varying(255),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    user_id integer DEFAULT 0
);

ALTER TABLE petel_schema.action_types OWNER TO "PetelAdmin";

CREATE CONSTRAINT UNIQUE (name) ON petel_schema.action_types;

-- ============================================================
-- ACTIONS TABLE
-- ============================================================
-- Stores individual actions that can be secured
-- Each action has an ID and name for identification
-- action_type_id links to the type of action (menu, button, etc.)
-- reference stores screen/page name or menu item name
-- is_active allows disabling actions without deletion

CREATE SEQUENCE petel_schema.actions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

ALTER SEQUENCE petel_schema.actions_id_seq OWNER TO "PetelAdmin";

CREATE TABLE petel_schema.actions (
    id integer DEFAULT nextval('petel_schema.actions_id_seq'::regclass) NOT NULL,
    name character varying(100) NOT NULL,
    display_name character varying(150),
    description character varying(255),
    action_type_id smallint NOT NULL,
    reference character varying(200),  -- screen name or menu item name
    sort_order integer DEFAULT 0,
    is_active boolean DEFAULT true,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    user_id integer DEFAULT 0
);

ALTER TABLE petel_schema.actions OWNER TO "PetelAdmin";

-- Create unique index on name for quick lookups
CREATE UNIQUE INDEX actions_name_uq ON petel_schema.actions(name);

-- Create index on action_type_id for filtering
CREATE INDEX actions_action_type_id_idx ON petel_schema.actions(action_type_id);

-- Create index on reference for screen-based lookups
CREATE INDEX actions_reference_idx ON petel_schema.actions(reference);

-- ============================================================
-- UPDATE ROLES_ACTIONS TABLE
-- ============================================================
-- Modify existing roles_actions table to reference the actions table
-- First, add foreign key constraint if it doesn't exist

ALTER TABLE ONLY petel_schema.roles_actions
    ADD CONSTRAINT roles_actions_action_id_fkey 
    FOREIGN KEY (action_id) REFERENCES petel_schema.actions(id)
    ON DELETE CASCADE
    ON UPDATE CASCADE;

ALTER TABLE ONLY petel_schema.roles_actions
    ADD CONSTRAINT roles_actions_role_id_fkey 
    FOREIGN KEY (role_id) REFERENCES petel_schema.roles(id)
    ON DELETE CASCADE
    ON UPDATE CASCADE;

-- ============================================================
-- FOREIGN KEY CONSTRAINTS
-- ============================================================

ALTER TABLE ONLY petel_schema.actions
    ADD CONSTRAINT actions_action_type_id_fkey 
    FOREIGN KEY (action_type_id) REFERENCES petel_schema.action_types(id)
    ON DELETE RESTRICT
    ON UPDATE CASCADE;

ALTER TABLE ONLY petel_schema.actions
    ADD CONSTRAINT actions_user_id_fkey 
    FOREIGN KEY (user_id) REFERENCES petel_schema.users(id)
    ON DELETE SET NULL
    ON UPDATE CASCADE;

ALTER TABLE ONLY petel_schema.action_types
    ADD CONSTRAINT action_types_user_id_fkey 
    FOREIGN KEY (user_id) REFERENCES petel_schema.users(id)
    ON DELETE SET NULL
    ON UPDATE CASCADE;

-- ============================================================
-- SAMPLE DATA: ACTION TYPES
-- ============================================================

INSERT INTO petel_schema.action_types (name, description) VALUES
    ('menu_item', 'Navigation menu items - accessed via menu'),
    ('button', 'UI buttons on screens - screen-specific actions'),
    ('page_action', 'Page-level actions - general page functionality'),
    ('api_endpoint', 'Direct API endpoint access'),
    ('report', 'Report generation and access')
ON CONFLICT (name) DO NOTHING;

-- ============================================================
-- SAMPLE DATA: ACTIONS (Can be expanded as needed)
-- ============================================================

-- Menu item actions (reference = menu item name from database)
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, reference, sort_order, is_active) 
SELECT id, 'menu_students', 'Access students menu', id, 'students', 10, true 
FROM petel_schema.action_types WHERE name = 'menu_item' ON CONFLICT DO NOTHING;

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, reference, sort_order, is_active) 
SELECT id, 'menu_school', 'Access school menu', id, 'schoollist', 20, true 
FROM petel_schema.action_types WHERE name = 'menu_item' ON CONFLICT DO NOTHING;

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, reference, sort_order, is_active) 
SELECT id, 'menu_reports', 'Access reports menu', id, 'analytics', 30, true 
FROM petel_schema.action_types WHERE name = 'menu_item' ON CONFLICT DO NOTHING;

-- ============================================================
-- VIEWS FOR EASIER ROLE-ACTION LOOKUPS
-- ============================================================

-- View: Role Actions with Action Details
CREATE OR REPLACE VIEW petel_schema.vw_role_actions AS
SELECT 
    ra.id,
    ra.role_id,
    r.name as role_name,
    ra.action_id,
    a.name as action_name,
    a.display_name,
    a.description,
    at.name as action_type,
    a.reference,
    ra.action_level,
    ra.updated_at
FROM petel_schema.roles_actions ra
JOIN petel_schema.roles r ON ra.role_id = r.id
JOIN petel_schema.actions a ON ra.action_id = a.id
JOIN petel_schema.action_types at ON a.action_type_id = at.id
WHERE a.is_active = true;

-- View: User Actions (user -> role -> actions)
CREATE OR REPLACE VIEW petel_schema.vw_user_actions AS
SELECT DISTINCT
    ur.user_id,
    u.username,
    ur.role_id,
    r.name as role_name,
    ra.action_id,
    a.name as action_name,
    a.display_name,
    at.name as action_type,
    a.reference
FROM petel_schema.user_roles ur
JOIN petel_schema.users u ON ur.user_id = u.id
JOIN petel_schema.roles r ON ur.role_id = r.id
JOIN petel_schema.roles_actions ra ON r.id = ra.role_id
JOIN petel_schema.actions a ON ra.action_id = a.id
JOIN petel_schema.action_types at ON a.action_type_id = at.id
WHERE ur.is_active = true AND a.is_active = true;

-- ============================================================
-- INDEXES FOR PERFORMANCE
-- ============================================================

CREATE INDEX roles_actions_role_id_idx ON petel_schema.roles_actions(role_id);
CREATE INDEX roles_actions_action_id_idx ON petel_schema.roles_actions(action_id);
CREATE INDEX user_roles_user_id_idx ON petel_schema.user_roles(user_id);
CREATE INDEX user_roles_role_id_idx ON petel_schema.user_roles(role_id);

-- ============================================================
-- GRANT PERMISSIONS
-- ============================================================

GRANT SELECT ON petel_schema.action_types TO "PetelAdmin";
GRANT SELECT ON petel_schema.actions TO "PetelAdmin";
GRANT SELECT ON petel_schema.vw_role_actions TO "PetelAdmin";
GRANT SELECT ON petel_schema.vw_user_actions TO "PetelAdmin";