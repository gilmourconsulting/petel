# Entity Framework Patterns

> Canonical: docs/agents/core/backend-patterns.md. Audit column names differ per app — see docs/agents/reference/audit-fields.md


## Entity Framework Patterns

### Database Context Configuration

**CRITICAL**: Always use `HasDefaultSchema()` - never hardcode schema names in entity configurations.

```csharp
public class AppDbContext : DbContext
{
    private readonly string _schemaName;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IOptions<DatabaseSettings> dbSettings) 
        : base(options)
    {
        _schemaName = dbSettings.Value.SchemaName;
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // âœ… Set default schema ONCE
        modelBuilder.HasDefaultSchema(_schemaName);

        // âœ… Configure entities WITHOUT schema parameter
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasIndex(e => e.Username).IsUnique();
        });
    }
}
```

### Entity Class Patterns

**Standard entity attributes**:
```csharp
[Table("table_name")]  // âœ… Table name only - NO schema
public class MyEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    
    [Required]
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### Navigation Properties (CRITICAL)

**All relationships MUST include navigation properties** for proper EF Core functionality:

```csharp
// Entity with foreign key relationship
public class SchoolStudent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    
    // âœ… REQUIRED: Foreign key property
    [ForeignKey("SchoolYear")]
    [Column("school_year_id")]
    public int SchoolYearId { get; set; }
    
    // âœ… REQUIRED: Navigation property (never null)
    public virtual SchoolYear SchoolYear { get; set; } = null!;
    
    // âœ… Optional relationship
    [ForeignKey("SchoolClass")]
    [Column("class_id")]
    public int? ClassId { get; set; }
    
    // âœ… Nullable navigation property
    public virtual SchoolClass? SchoolClass { get; set; }
}

// Parent entity with collection
public class SchoolYear
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("year_name")]
    public string YearName { get; set; } = string.Empty;
    
    // âœ… REQUIRED: Collection navigation property
    public virtual ICollection<SchoolStudent> Students { get; set; } = new List<SchoolStudent>();
}
```

**Configuration in AppDbContext**:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.HasDefaultSchema(_schemaName);
    
    modelBuilder.Entity<SchoolStudent>(entity =>
    {
        entity.ToTable("school_students");
        
        // âœ… Configure required relationship
        entity.HasOne(s => s.SchoolYear)
            .WithMany(y => y.Students)
            .HasForeignKey(s => s.SchoolYearId)
            .OnDelete(DeleteBehavior.Restrict);
            
        // âœ… Configure optional relationship
        entity.HasOne(s => s.SchoolClass)
            .WithMany(c => c.Students)
            .HasForeignKey(s => s.ClassId)
            .OnDelete(DeleteBehavior.SetNull);
    });
}
```

**Benefits of Navigation Properties**:
- âœ… Enables eager loading: `.Include(s => s.SchoolYear)`
- âœ… Prevents N+1 query problems
- âœ… Provides IntelliSense for related data
- âœ… Enforces referential integrity
- âœ… Simplifies projection queries

**Loading Strategies**:

```csharp
// âœ… Eager loading (small related data)
var students = await _context.SchoolStudents
    .Include(s => s.SchoolYear)
    .Include(s => s.SchoolClass)
    .Where(s => s.SchoolYearId == yearId)
    .ToListAsync();

// âœ… Projection (large datasets, specific fields only)
var studentDtos = await _context.SchoolStudents
    .Where(s => s.SchoolYearId == yearId)
    .Select(s => new StudentDto
    {
        Id = s.Id,
        Name = s.Name,
        YearName = s.SchoolYear.YearName,
        ClassName = s.SchoolClass != null ? s.SchoolClass.ClassName : null
    })
    .ToListAsync();

// âŒ WRONG - Lazy loading causes N+1 queries
var students = await _context.SchoolStudents.ToListAsync();
foreach (var student in students)
{
    var yearName = student.SchoolYear.YearName;  // Separate query per student!
}
```

**Anti-Patterns**:
```csharp
// âŒ WRONG - Missing navigation property
public class SchoolStudent
{
    public int SchoolYearId { get; set; }
    // Missing: public virtual SchoolYear SchoolYear { get; set; }
}

// âŒ WRONG - Accessing navigation without Include
var students = await _context.SchoolStudents.ToListAsync();
var yearName = students[0].SchoolYear.YearName;  // NullReferenceException!

// âœ… CORRECT - Include navigation property
var students = await _context.SchoolStudents
    .Include(s => s.SchoolYear)
    .ToListAsync();
var yearName = students[0].SchoolYear.YearName;  // Works!
```

### Query Patterns

**Entity Scoping**: Always filter by user's EntityId
```csharp
var session = GetCurrentSession();
var entityId = int.Parse(session.EntityId);

var data = await _context.Students
    .Where(s => s.EntityId == entityId)
    .ToListAsync();
```

**Async/Await**: Always use async methods
```csharp
// âœ… CORRECT
var students = await _context.Students.ToListAsync();

// âŒ WRONG
var students = _context.Students.ToList();  // Blocks thread
```

**Projections for Performance**:
```csharp
// âœ… CORRECT - Only select needed fields
var data = await _context.Students
    .Select(s => new { s.Id, s.Name, s.ClassName })
    .ToListAsync();

// âŒ WRONG - Loading entire entity when not needed
var data = await _context.Students
    .ToListAsync()
    .Select(s => new { s.Id, s.Name, s.ClassName });
```
# Standard Database Table Structure

> See docs/agents/reference/audit-fields.md for per-app creator column naming.

## Standard Database Table Structure

**CRITICAL**: All tables in the system MUST follow standardized naming and structure patterns.

### Required Audit Fields

**Every table must include these audit fields**:

```sql
created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
created_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
update_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL
```

**Entity Model Pattern**:
```csharp
[Column("created_at")]
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

[Column("created_user")]
public int? CreatedUser { get; set; }

[Column("updated_at")]
public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

[Column("update_user")]
public int? UpdateUser { get; set; }
```

### Field Naming Conventions

**âœ… CORRECT Patterns**:
- Use concise names: `name`, `value`, `description` (NOT `attribute_name`, `attribute_value`)
- Table name provides context, so field names should be minimal
- Use underscores for multi-word fields: `school_year_id`, `created_at`
- Hebrew descriptions: Always include a `description` field for Hebrew UI labels

**âŒ WRONG Patterns**:
```sql
-- NO! Redundant prefix from table name
CREATE TABLE school_year_attributes (
    attribute_name VARCHAR(100),  -- Should be just "name"
    attribute_value VARCHAR(500)  -- Should be just "value"
);

-- NO! Missing audit fields
CREATE TABLE my_table (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100)
    -- Missing: created_at, created_user, updated_at, update_user
);
```

### Standard Table Template

**Use this template for all new tables**:

```sql
CREATE TABLE petel_schema.table_name (
    id SERIAL PRIMARY KEY,
    
    -- Foreign keys (if applicable)
    parent_id INTEGER NOT NULL REFERENCES petel_schema.parent_table(id) ON DELETE CASCADE,
    
    -- Business fields
    name VARCHAR(100) NOT NULL,
    description VARCHAR(200) NULL,  -- Hebrew description for UI
    value VARCHAR(500) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    
    -- Audit fields (REQUIRED)
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    update_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
    
    -- Constraints
    CONSTRAINT uk_table_unique UNIQUE (parent_id, name)
);

-- Indexes
CREATE INDEX idx_table_name_parent_id ON petel_schema.table_name(parent_id);
CREATE INDEX idx_table_name_name ON petel_schema.table_name(name);
CREATE INDEX idx_table_name_created_user ON petel_schema.table_name(created_user);
CREATE INDEX idx_table_name_update_user ON petel_schema.table_name(update_user);
```

### Controller Pattern for User Tracking

**Always populate user audit fields**:

```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateRequest request)
{
    var session = GetCurrentSession();
    int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

    var entity = new MyEntity
    {
        Name = request.Name,
        Description = request.Description,
        Value = request.Value,
        CreatedAt = DateTime.UtcNow,
        CreatedUser = userId,  // âœ… Track who created
        UpdatedAt = DateTime.UtcNow,
        UpdateUser = userId
    };

    _context.MyEntities.Add(entity);
    await _context.SaveChangesAsync();
    return Ok(new { success = true });
}

[HttpPut("{id}")]
public async Task<IActionResult> Update(int id, [FromBody] UpdateRequest request)
{
    var session = GetCurrentSession();
    int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

    var entity = await _context.MyEntities.FindAsync(id);
    
    entity.Name = request.Name;
    entity.Value = request.Value;
    entity.UpdatedAt = DateTime.UtcNow;
    entity.UpdateUser = userId;  // âœ… Track who updated

    await _context.SaveChangesAsync();
    return Ok(new { success = true });
}
```

### Migration Script Pattern

**Use idempotent migrations with proper checks**:

```sql
-- Check if table exists
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema'
        AND tablename = 'my_table'
    ) THEN
        CREATE TABLE petel_schema.my_table (
            -- Table definition here
        );
        
        -- Create indexes
        CREATE INDEX idx_my_table_field ON petel_schema.my_table(field);
        
        RAISE NOTICE 'Table my_table created successfully';
    ELSE
        RAISE NOTICE 'Table my_table already exists';
    END IF;
END
$$;

-- Insert seed data
INSERT INTO petel_schema.my_table (field1, field2, description, value)
VALUES 
    (1, 'key1', '×ª×™××•×¨ ×‘×¢×‘×¨×™×ª', 'value1'),
    (2, 'key2', '×ª×™××•×¨ ××—×¨', 'value2')
ON CONFLICT (unique_field) DO NOTHING;
```

### Benefits of Standard Structure

âœ… **Full audit trail** - Know who created/modified every record
âœ… **Consistent patterns** - Easy to learn and maintain
âœ… **Hebrew support** - Description field for UI localization
âœ… **Referential integrity** - Proper foreign key constraints
âœ… **Performance** - Standard indexes on common query fields
âœ… **Idempotent migrations** - Safe to run multiple times

### Common Mistakes to Avoid

```sql
-- âŒ WRONG - Redundant field names
CREATE TABLE school_attributes (
    attribute_name VARCHAR(100),
    attribute_value VARCHAR(500)
);

-- âœ… CORRECT - Concise field names
CREATE TABLE school_attributes (
    name VARCHAR(100),
    value VARCHAR(500),
    description VARCHAR(200)
);

-- âŒ WRONG - Missing audit fields
CREATE TABLE my_table (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100)
);

-- âœ… CORRECT - Complete audit fields
CREATE TABLE my_table (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_user INTEGER NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    update_user INTEGER NULL
);

-- âŒ WRONG - No Hebrew description
CREATE TABLE options (
    id SERIAL PRIMARY KEY,
    code VARCHAR(50),
    value VARCHAR(100)
);

-- âœ… CORRECT - Include Hebrew description
CREATE TABLE options (
    id SERIAL PRIMARY KEY,
    code VARCHAR(50),
    description VARCHAR(200),  -- For Hebrew UI label
    value VARCHAR(100)
);
```
