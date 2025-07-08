
-- Table: petel_schema.budget

-- DROP TABLE IF EXISTS petel_schema.budget;

CREATE TABLE IF NOT EXISTS petel_schema.budget
(
    id integer NOT NULL,
    school_year_id integer,
    version smallint,
    status character varying(50)[] COLLATE pg_catalog."default",
    created_at time with time zone DEFAULT now(),
    updated_at time with time zone DEFAULT now(),
    budget_name character varying(50)[] COLLATE pg_catalog."default",
    CONSTRAINT budget_pkey PRIMARY KEY (id)
	
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS petel_schema.budget
    OWNER to PetelAdmin;


-- Students Table
CREATE TABLE petel_schema.students (
    id INTEGER PRIMARY KEY DEFAULT nextval('petel_schema.students_seq'),
    internal_id VARCHAR(50),
    national_id VARCHAR(50) NOT NULL,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    gender CHAR(1) CHECK (gender IN ('M', 'F', 'O')),
    date_of_birth DATE,
    school_id INTEGER NOT NULL REFERENCES petel_schema.schools(id),
    user_id INTEGER REFERENCES petel_schema.users(id),
    additional_details JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT unique_national_id_per_school UNIQUE (national_id, school_id)
);

CREATE TRIGGER set_timestamp_students
BEFORE UPDATE ON petel_schema.students
FOR EACH ROW
EXECUTE PROCEDURE trigger_set_timestamp();

-- Teachers Table
CREATE TABLE petel_schema.teachers (
    id INTEGER PRIMARY KEY DEFAULT nextval('petel_schema.teachers_seq'),
    internal_id VARCHAR(50),
    national_id VARCHAR(50) NOT NULL,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    gender CHAR(1) CHECK (gender IN ('M', 'F', 'O')),
    date_of_birth DATE,
    school_id INTEGER NOT NULL REFERENCES petel_schema.schools(id),
    user_id INTEGER REFERENCES petel_schema.users(id),
    contact_info JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT unique_national_id_per_school UNIQUE (national_id, school_id)
);

CREATE TRIGGER set_timestamp_teachers
BEFORE UPDATE ON petel_schema.teachers
FOR EACH ROW
EXECUTE PROCEDURE trigger_set_timestamp();

-- Courses Table
CREATE TABLE petel_schema.courses (
    id INTEGER PRIMARY KEY DEFAULT nextval('petel_schema.courses_seq'),
    school_id INTEGER NOT NULL REFERENCES petel_schema.schools(id),
    code VARCHAR(50) NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    subject_area VARCHAR(100),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT unique_course_code_per_school UNIQUE (code, school_id)
);

CREATE TRIGGER set_timestamp_courses
BEFORE UPDATE ON petel_schema.courses
FOR EACH ROW
EXECUTE PROCEDURE trigger_set_timestamp();

-- Student-School Year Many-to-Many Relationship
CREATE TABLE petel_schema.student_school_years (
    id SERIAL PRIMARY KEY,
    student_id INTEGER NOT NULL REFERENCES petel_schema.students(id),
    school_year_id INTEGER NOT NULL REFERENCES petel_schema.school_years(id),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT unique_student_school_year UNIQUE (student_id, school_year_id)
);

CREATE TRIGGER set_timestamp_student_school_years
BEFORE UPDATE ON petel_schema.student_school_years
FOR EACH ROW
EXECUTE PROCEDURE trigger_set_timestamp();

-- Teacher-School Year Many-to-Many Relationship
CREATE TABLE petel_schema.teacher_school_years (
    id SERIAL PRIMARY KEY,
    teacher_id INTEGER NOT NULL REFERENCES petel_schema.teachers(id),
    school_year_id INTEGER NOT NULL REFERENCES petel_schema.school_years(id),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT unique_teacher_school_year UNIQUE (teacher_id, school_year_id)
);

CREATE TRIGGER set_timestamp_teacher_school_years
BEFORE UPDATE ON petel_schema.teacher_school_years
FOR EACH ROW
EXECUTE PROCEDURE trigger_set_timestamp();

-- Historical Student Grades (Time-stamped)
CREATE TABLE petel_schema.student_grades (
    id SERIAL PRIMARY KEY,
    student_id INTEGER NOT NULL REFERENCES petel_schema.students(id),
    school_year_id INTEGER NOT NULL REFERENCES petel_schema.school_years(id),
    grade_level VARCHAR(50) NOT NULL,
    class_name VARCHAR(50),
    homeroom_teacher_id INTEGER REFERENCES petel_schema.teachers(id),
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TRIGGER set_timestamp_student_grades
BEFORE UPDATE ON petel_schema.student_grades
FOR EACH ROW
EXECUTE PROCEDURE trigger_set_timestamp();

-- Historical Student Courses (Time-stamped)
CREATE TABLE petel_schema.student_courses (
    id SERIAL PRIMARY KEY,
    student_id INTEGER NOT NULL REFERENCES petel_schema.students(id),
    school_year_id INTEGER NOT NULL REFERENCES petel_schema.school_years(id),
    course_id INTEGER NOT NULL REFERENCES petel_schema.courses(id),
    level VARCHAR(50),
    start_date DATE NOT NULL,
    end_date DATE,
    status VARCHAR(50) CHECK (status IN ('active', 'completed', 'withdrawn')),
    final_grade NUMERIC(5,2),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TRIGGER set_timestamp_student_courses
BEFORE UPDATE ON petel_schema.student_courses
FOR EACH ROW
EXECUTE PROCEDURE trigger_set_timestamp();

-- Historical Student Special Needs (Time-stamped)
CREATE TABLE petel_schema.student_special_needs (
    id SERIAL PRIMARY KEY,
    student_id INTEGER NOT NULL REFERENCES petel_schema.students(id),
    school_year_id INTEGER NOT NULL REFERENCES petel_schema.school_years(id),
    need_type VARCHAR(100) NOT NULL,
    description TEXT,
    documentation_reference VARCHAR(255),
    accommodations_required TEXT,
    valid_from_date DATE NOT NULL,
    valid_to_date DATE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TRIGGER set_timestamp_student_special_needs
BEFORE UPDATE ON petel_schema.student_special_needs
FOR EACH ROW
EXECUTE PROCEDURE trigger_set_timestamp();

-- Historical Teacher Specialties (Time-stamped)
CREATE TABLE petel_schema.teacher_specialties (
    id SERIAL PRIMARY KEY,
    teacher_id INTEGER NOT NULL REFERENCES petel_schema.teachers(id),
    school_year_id INTEGER NOT NULL REFERENCES petel_schema.school_years(id),
    specialty_name VARCHAR(100) NOT NULL,
    certification_info VARCHAR(255),
    certification_date DATE,
    is_primary BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TRIGGER set_timestamp_teacher_specialties
BEFORE UPDATE ON petel_schema.teacher_specialties
FOR EACH ROW
EXECUTE PROCEDURE trigger_set_timestamp();

-- Historical Teacher Available Hours (Time-stamped)
CREATE TABLE petel_schema.teacher_available_hours (
    id SERIAL PRIMARY KEY,
    teacher_id INTEGER NOT NULL REFERENCES petel_schema.teachers(id),
    school_year_id INTEGER NOT NULL REFERENCES petel_schema.school_years(id),
    day_of_week INTEGER NOT NULL CHECK (day_of_week BETWEEN 1 AND 7),
    start_time TIME NOT NULL,
    end_time TIME NOT NULL,
    recurrence_type VARCHAR(50) DEFAULT 'weekly',
    recurrence_end_date DATE,
    is_available BOOLEAN DEFAULT TRUE,
    reason VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT check_end_after_start CHECK (end_time > start_time)
);

CREATE TRIGGER set_timestamp_teacher_available_hours
BEFORE UPDATE ON petel_schema.teacher_available_hours
FOR EACH ROW
EXECUTE PROCEDURE trigger_set_timestamp();

-- Row-Level Security Policies
-- Function to check if the current user belongs to a school
CREATE OR REPLACE FUNCTION petel_schema.user_belongs_to_school(school_id INTEGER)
RETURNS BOOLEAN AS $$
BEGIN
    RETURN (
        SELECT EXISTS (
            SELECT 1
            FROM petel_schema.users
            WHERE 
                users.school_id = school_id
                AND users.username = current_user
        )
    );
END;
$$ LANGUAGE plpgsql;

-- Row-Level Security for Schools table
ALTER TABLE petel_schema.schools ENABLE ROW LEVEL SECURITY;
CREATE POLICY school_isolation_policy ON petel_schema.schools
    USING (petel_schema.user_belongs_to_school(id));

-- Row-Level Security for other tables based on school_id
ALTER TABLE petel_schema.school_years ENABLE ROW LEVEL SECURITY;
CREATE POLICY school_years_isolation_policy ON petel_schema.school_years
    USING (petel_schema.user_belongs_to_school(school_id));

ALTER TABLE petel_schema.users ENABLE ROW LEVEL SECURITY;
CREATE POLICY users_isolation_policy ON petel_schema.users
    USING (petel_schema.user_belongs_to_school(school_id));

ALTER TABLE petel_schema.students ENABLE ROW LEVEL SECURITY;
CREATE POLICY students_isolation_policy ON petel_schema.students
    USING (petel_schema.user_belongs_to_school(school_id));

ALTER TABLE petel_schema.teachers ENABLE ROW LEVEL SECURITY;
CREATE POLICY teachers_isolation_policy ON petel_schema.teachers
    USING (petel_schema.user_belongs_to_school(school_id));

ALTER TABLE petel_schema.courses ENABLE ROW LEVEL SECURITY;
CREATE POLICY courses_isolation_policy ON petel_schema.courses
    USING (petel_schema.user_belongs_to_school(school_id));

-- Insert default roles
INSERT INTO petel_schema.roles (name, description) VALUES 
('system_admin', 'System administrator with full access'),
('school_admin', 'School administrator with full access to school data'),
('teacher', 'Teacher with access to assigned classes and students'),
('student', 'Student with limited access to their own data'),
('parent', 'Parent with limited access to their children''s data');

-- Index Creation for Performance Optimization
CREATE INDEX idx_school_years_school_id ON petel_schema.school_years(school_id);
CREATE INDEX idx_users_school_id ON petel_schema.users(school_id);
CREATE INDEX idx_users_role_id ON petel_schema.users(role_id);
CREATE INDEX idx_students_school_id ON petel_schema.students(school_id);
CREATE INDEX idx_students_national_id ON petel_schema.students(national_id);
CREATE INDEX idx_teachers_school_id ON petel_schema.teachers(school_id);
CREATE INDEX idx_teachers_national_id ON petel_schema.teachers(national_id);
CREATE INDEX idx_student_school_years_student_id ON petel_schema.student_school_years(student_id);
CREATE INDEX idx_student_school_years_school_year_id ON petel_schema.student_school_years(school_year_id);
CREATE INDEX idx_teacher_school_years_teacher_id ON petel_schema.teacher_school_years(teacher_id);
CREATE INDEX idx_teacher_school_years_school_year_id ON petel_schema.teacher_school_years(school_year_id);
CREATE INDEX idx_student_grades_student_id ON petel_schema.student_grades(student_id);
CREATE INDEX idx_student_grades_school_year_id ON petel_schema.student_grades(school_year_id);
CREATE INDEX idx_student_courses_student_id ON petel_schema.student_courses(student_id);
CREATE INDEX idx_student_courses_school_year_id ON petel_schema.student_courses(school_year_id);
CREATE INDEX idx_student_special_needs_student_id ON petel_schema.student_special_needs(student_id);
CREATE INDEX idx_teacher_specialties_teacher_id ON petel_schema.teacher_specialties(teacher_id);
CREATE INDEX idx_teacher_available_hours_teacher_id ON petel_schema.teacher_available_hours(teacher_id);

-- Create a view to help with current school year queries
CREATE OR REPLACE VIEW petel_schema.current_school_years AS
SELECT 
    sy.*,
    s.name as school_name
FROM 
    petel_schema.school_years sy
    JOIN petel_schema.schools s ON sy.school_id = s.id
WHERE 
    sy.is_current = true;

-- Function to get all students in current school year for a school
CREATE OR REPLACE FUNCTION petel_schema.get_current_students(p_school_id INTEGER)
RETURNS TABLE (
    student_id INTEGER,
    internal_id VARCHAR(50),
    national_id VARCHAR(50),
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    grade_level VARCHAR(50),
    class_name VARCHAR(50)
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        s.id as student_id,
        s.internal_id,
        s.national_id,
        s.first_name,
        s.last_name,
        sg.grade_level,
        sg.class_name
    FROM 
        petel_schema.students s
        JOIN petel_schema.student_school_years ssy ON s.id = ssy.student_id
        JOIN petel_schema.school_years sy ON ssy.school_year_id = sy.id
        LEFT JOIN petel_schema.student_grades sg ON s.id = sg.student_id AND sg.school_year_id = sy.id
    WHERE 
        s.school_id = p_school_id
        AND sy.is_current = true;
END;
$$ LANGUAGE plpgsql;