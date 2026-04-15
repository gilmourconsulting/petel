using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PetelATH.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailOtpFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "petel_schema");

            migrationBuilder.CreateTable(
                name: "action_types",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alert_levels",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    description = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_levels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alert_statuses",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    description = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alert_types",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    description = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alerts",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    alert_type = table.Column<int>(type: "integer", nullable: false),
                    alert_level = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    is_event = table.Column<bool>(type: "boolean", nullable: false),
                    event_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "council_summary_vw",
                schema: "petel_schema",
                columns: table => new
                {
                    council_id = table.Column<int>(type: "integer", nullable: false),
                    council_name = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    year_id = table.Column<int>(type: "integer", nullable: false),
                    number_of_students = table.Column<long>(type: "bigint", nullable: false),
                    total_requested_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    owner_id = table.Column<int>(type: "integer", nullable: true),
                    owner_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "councils",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    council_code = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_councils", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_status_types",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_status_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_types",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    year_id = table.Column<int>(type: "integer", nullable: true),
                    object_element_check = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    object_element_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "entity_types",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hebrew_years",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    hebrew_year = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hebrew_years", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hours_budget",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_id = table.Column<string>(type: "text", nullable: false),
                    school_year = table.Column<string>(type: "text", nullable: true),
                    budget_type = table.Column<string>(type: "text", nullable: true),
                    allocated_hours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    used_hours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    remaining_hours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    department = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hours_budget", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "menu_items",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    text = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    action_id = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "persons",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    phone_number_prefix = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    position = table.Column<string>(type: "text", nullable: true),
                    id_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    id_type = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "school_attributes_types",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    year_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    hebrew_name = table.Column<string>(type: "text", nullable: true),
                    attribute_value_type = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_attributes_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "school_classes",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    school_year_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    level = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    class_number = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    end_hour = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_classes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "school_student_pricing_elements",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    school_student = table.Column<int>(type: "integer", nullable: false),
                    pricing_element = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric", nullable: false),
                    determining_factor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    hours = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_student_pricing_elements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "school_years",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    school_id = table.Column<int>(type: "integer", nullable: false),
                    hebrew_year_name = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: true),
                    year_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_years", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "special_needs_characterizations",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_special_needs_characterizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "statuses",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    @object = table.Column<string>(name: "object", type: "character varying(25)", maxLength: 25, nullable: true),
                    name = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "student_school_years_registration_summary_vw",
                schema: "petel_schema",
                columns: table => new
                {
                    school_id = table.Column<int>(type: "integer", nullable: false),
                    school_year_id = table.Column<int>(type: "integer", nullable: false),
                    school_grade = table.Column<string>(type: "text", nullable: false),
                    school_track = table.Column<string>(type: "text", nullable: false),
                    registered = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "system_attributes",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    update_user = table.Column<int>(type: "integer", nullable: true),
                    foreign_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_attributes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tracks",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    year_id = table.Column<int>(type: "integer", nullable: false),
                    external_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    available_for_classes = table.Column<string[]>(type: "text[]", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transaction_detail_types",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_user = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_detail_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transaction_types",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_credit = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_user = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "actions",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    onclick_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    action_type_id = table.Column<int>(type: "integer", nullable: false),
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_actions_action_types_action_type_id",
                        column: x => x.action_type_id,
                        principalSchema: "petel_schema",
                        principalTable: "action_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    master_document_id = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    document_type_id = table.Column<int>(type: "integer", nullable: false),
                    status_id = table.Column<int>(type: "integer", nullable: false),
                    file_blob = table.Column<byte[]>(type: "bytea", nullable: true),
                    file_encoding = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    file_name = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_last_version = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_documents_document_types_document_type_id",
                        column: x => x.document_type_id,
                        principalSchema: "petel_schema",
                        principalTable: "document_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "additional_study_programs_pricing",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    year_id = table.Column<int>(type: "integer", nullable: false),
                    students = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_additional_study_programs_pricing", x => x.id);
                    table.ForeignKey(
                        name: "FK_additional_study_programs_pricing_hebrew_years_year_id",
                        column: x => x.year_id,
                        principalSchema: "petel_schema",
                        principalTable: "hebrew_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "school_year_attributes",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    year_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_user = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_year_attributes", x => x.id);
                    table.ForeignKey(
                        name: "FK_school_year_attributes_hebrew_years_year_id",
                        column: x => x.year_id,
                        principalSchema: "petel_schema",
                        principalTable: "hebrew_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "special_needs_pricing_elements",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    year_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    calculation_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    attribute_to_check = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_special_needs_pricing_elements", x => x.id);
                    table.ForeignKey(
                        name: "FK_special_needs_pricing_elements_hebrew_years_year_id",
                        column: x => x.year_id,
                        principalSchema: "petel_schema",
                        principalTable: "hebrew_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "school_attribute_types_values",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    school_attribute_id = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_valid = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_attribute_types_values", x => x.id);
                    table.ForeignKey(
                        name: "FK_school_attribute_types_values_school_attributes_types_schoo~",
                        column: x => x.school_attribute_id,
                        principalSchema: "petel_schema",
                        principalTable: "school_attributes_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "school_additional_study_programs",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    school_year_id = table.Column<int>(type: "integer", nullable: false),
                    class_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    weekly_hours = table.Column<int>(type: "integer", nullable: false),
                    number_of_class_students = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_last_version = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    master_id = table.Column<int>(type: "integer", nullable: false),
                    cost = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    approved_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    hourly_cost = table.Column<decimal>(type: "numeric", nullable: true),
                    number_of_sessions = table.Column<int>(type: "integer", nullable: false),
                    approval_status = table.Column<int>(type: "integer", nullable: false),
                    calculate_by_hourly_cost = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_additional_study_programs", x => x.id);
                    table.ForeignKey(
                        name: "FK_school_additional_study_programs_school_additional_study_pr~",
                        column: x => x.master_id,
                        principalSchema: "petel_schema",
                        principalTable: "school_additional_study_programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_school_additional_study_programs_school_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "petel_schema",
                        principalTable: "school_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_school_additional_study_programs_school_years_school_year_id",
                        column: x => x.school_year_id,
                        principalSchema: "petel_schema",
                        principalTable: "school_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "school_attributes",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    school_year_id = table.Column<int>(type: "integer", nullable: false),
                    school_attribute_type_id = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    is_last_version = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_attributes", x => x.id);
                    table.ForeignKey(
                        name: "FK_school_attributes_school_attributes_types_school_attribute_~",
                        column: x => x.school_attribute_type_id,
                        principalSchema: "petel_schema",
                        principalTable: "school_attributes_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_school_attributes_school_years_school_year_id",
                        column: x => x.school_year_id,
                        principalSchema: "petel_schema",
                        principalTable: "school_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sign_language_translators",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    school_year_id = table.Column<int>(type: "integer", nullable: false),
                    person_id = table.Column<int>(type: "integer", nullable: false),
                    hours_employed = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sign_language_translators", x => x.id);
                    table.ForeignKey(
                        name: "FK_sign_language_translators_persons_person_id",
                        column: x => x.person_id,
                        principalSchema: "petel_schema",
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sign_language_translators_school_years_school_year_id",
                        column: x => x.school_year_id,
                        principalSchema: "petel_schema",
                        principalTable: "school_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entities",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    entity_type_id = table.Column<int>(type: "integer", nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    principal_name = table.Column<string>(type: "text", nullable: true),
                    api_connection_id = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    entity_logo = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    owner = table.Column<int>(type: "integer", nullable: true),
                    council = table.Column<int>(type: "integer", nullable: true),
                    inspector_name = table.Column<string>(type: "text", nullable: true),
                    characterization = table.Column<string>(type: "text", nullable: true),
                    contact_person = table.Column<int>(type: "integer", nullable: true),
                    education_stage = table.Column<string>(type: "text", nullable: true),
                    symbol = table.Column<string>(type: "text", nullable: true),
                    characterization_id = table.Column<int>(type: "integer", nullable: true),
                    distributor = table.Column<string>(type: "text", nullable: true),
                    tax_number = table.Column<string>(type: "text", nullable: true),
                    street = table.Column<string>(type: "text", nullable: true),
                    house_number = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    post_code = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entities", x => x.id);
                    table.ForeignKey(
                        name: "FK_entities_councils_council",
                        column: x => x.council,
                        principalSchema: "petel_schema",
                        principalTable: "councils",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_entities_entities_owner",
                        column: x => x.owner,
                        principalSchema: "petel_schema",
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_entities_entity_types_entity_type_id",
                        column: x => x.entity_type_id,
                        principalSchema: "petel_schema",
                        principalTable: "entity_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_entities_persons_contact_person",
                        column: x => x.contact_person,
                        principalSchema: "petel_schema",
                        principalTable: "persons",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_entities_special_needs_characterizations_characterization_id",
                        column: x => x.characterization_id,
                        principalSchema: "petel_schema",
                        principalTable: "special_needs_characterizations",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "school_students",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_number = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    master_student_id = table.Column<int>(type: "integer", nullable: false),
                    class_id = table.Column<int>(type: "integer", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    first_name = table.Column<string>(type: "text", nullable: true),
                    last_name = table.Column<string>(type: "text", nullable: true),
                    gender = table.Column<int>(type: "integer", nullable: true),
                    street = table.Column<string>(type: "text", nullable: true),
                    house_number = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    post_code = table.Column<string>(type: "text", nullable: true),
                    sending_council = table.Column<int>(type: "integer", nullable: true),
                    disability_category = table.Column<int>(type: "integer", nullable: true),
                    school_year_id = table.Column<int>(type: "integer", nullable: false),
                    is_last_version = table.Column<bool>(type: "boolean", nullable: false),
                    cost = table.Column<decimal>(type: "numeric", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_students", x => x.id);
                    table.ForeignKey(
                        name: "FK_school_students_statuses_status",
                        column: x => x.status,
                        principalSchema: "petel_schema",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tracks_levels",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    school_track_id = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    min_hours = table.Column<int>(type: "integer", nullable: false),
                    max_hours = table.Column<int>(type: "integer", nullable: true),
                    available_for_classes = table.Column<string[]>(type: "text[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracks_levels", x => x.id);
                    table.ForeignKey(
                        name: "FK_tracks_levels_tracks_school_track_id",
                        column: x => x.school_track_id,
                        principalSchema: "petel_schema",
                        principalTable: "tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roles_actions",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    action_id = table.Column<int>(type: "integer", nullable: false),
                    action_level = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_user = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_roles_actions_actions_action_id",
                        column: x => x.action_id,
                        principalSchema: "petel_schema",
                        principalTable: "actions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_roles_actions_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "petel_schema",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_links",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_id = table.Column<long>(type: "bigint", nullable: false),
                    school_student_id = table.Column<int>(type: "integer", nullable: true),
                    entity_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_links_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "petel_schema",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "special_needs_pricing_categories",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pricing_element = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    is_lowest_level = table.Column<bool>(type: "boolean", nullable: true),
                    price = table.Column<decimal>(type: "numeric", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_special_needs_pricing_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_special_needs_pricing_categories_special_needs_pricing_elem~",
                        column: x => x.pricing_element,
                        principalSchema: "petel_schema",
                        principalTable: "special_needs_pricing_elements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "special_needs_pricing_steps",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pricing_element = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    object_check = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    object_element_check = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    object_element_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    price = table.Column<decimal>(type: "numeric", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_special_needs_pricing_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_special_needs_pricing_steps_special_needs_pricing_elements_~",
                        column: x => x.pricing_element,
                        principalSchema: "petel_schema",
                        principalTable: "special_needs_pricing_elements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alert_links",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    alert_id = table.Column<long>(type: "bigint", nullable: false),
                    alert_status = table.Column<int>(type: "integer", nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    is_last_version = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_alert_links_alerts_alert_id",
                        column: x => x.alert_id,
                        principalSchema: "petel_schema",
                        principalTable: "alerts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alert_links_entities_entity_id",
                        column: x => x.entity_id,
                        principalSchema: "petel_schema",
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "schools",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    school_year_id = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    entity_type_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    street = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    house_number = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    city = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    post_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    council = table.Column<int>(type: "integer", nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    principal = table.Column<int>(type: "integer", nullable: true),
                    inspector = table.Column<int>(type: "integer", nullable: false),
                    contact_person = table.Column<int>(type: "integer", nullable: true),
                    api_connection_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    school_logo = table.Column<byte[]>(type: "bytea", nullable: true),
                    owner = table.Column<int>(type: "integer", nullable: true),
                    characterization_id = table.Column<int>(type: "integer", nullable: true),
                    education_stage = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    symbol = table.Column<string>(type: "character(8)", nullable: true),
                    is_last_version = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schools", x => x.id);
                    table.ForeignKey(
                        name: "FK_schools_councils_council",
                        column: x => x.council,
                        principalSchema: "petel_schema",
                        principalTable: "councils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_schools_entities_entity_id",
                        column: x => x.entity_id,
                        principalSchema: "petel_schema",
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_schools_entity_types_entity_type_id",
                        column: x => x.entity_type_id,
                        principalSchema: "petel_schema",
                        principalTable: "entity_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_schools_persons_contact_person",
                        column: x => x.contact_person,
                        principalSchema: "petel_schema",
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_schools_persons_inspector",
                        column: x => x.inspector,
                        principalSchema: "petel_schema",
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_schools_persons_principal",
                        column: x => x.principal,
                        principalSchema: "petel_schema",
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_schools_school_years_school_year_id",
                        column: x => x.school_year_id,
                        principalSchema: "petel_schema",
                        principalTable: "school_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_schools_special_needs_characterizations_characterization_id",
                        column: x => x.characterization_id,
                        principalSchema: "petel_schema",
                        principalTable: "special_needs_characterizations",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    last_login = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    update_user = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    otp_secret = table.Column<string>(type: "text", nullable: true),
                    otp_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    otp_verified = table.Column<bool>(type: "boolean", nullable: false),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false),
                    locked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    locked_by = table.Column<int>(type: "integer", nullable: true),
                    failed_password_attempts = table.Column<int>(type: "integer", nullable: false),
                    failed_otp_attempts = table.Column<int>(type: "integer", nullable: false),
                    last_failed_attempt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    password_changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    password_change_required = table.Column<bool>(type: "boolean", nullable: false),
                    email_otp_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email_otp_expiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    email_otp_attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_entities_entity_id",
                        column: x => x.entity_id,
                        principalSchema: "petel_schema",
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "action_audit_logs",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    action_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    screen_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    function_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    action_params = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_action_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "petel_schema",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "school_tracks",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    school_year_id = table.Column<int>(type: "integer", nullable: false),
                    track_id = table.Column<int>(type: "integer", nullable: false),
                    track_level_id = table.Column<int>(type: "integer", nullable: true),
                    class_id = table.Column<int>(type: "integer", nullable: false),
                    weekly_hours = table.Column<decimal>(type: "numeric", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_tracks", x => x.id);
                    table.ForeignKey(
                        name: "FK_school_tracks_school_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "petel_schema",
                        principalTable: "school_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_school_tracks_school_years_school_year_id",
                        column: x => x.school_year_id,
                        principalSchema: "petel_schema",
                        principalTable: "school_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_school_tracks_tracks_levels_track_level_id",
                        column: x => x.track_level_id,
                        principalSchema: "petel_schema",
                        principalTable: "tracks_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_school_tracks_tracks_track_id",
                        column: x => x.track_id,
                        principalSchema: "petel_schema",
                        principalTable: "tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_school_tracks_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "petel_schema",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transaction_account_types",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_user = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_account_types", x => x.id);
                    table.ForeignKey(
                        name: "FK_transaction_account_types_users_created_user",
                        column: x => x.created_user,
                        principalSchema: "petel_schema",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transaction_account_types_users_update_user",
                        column: x => x.update_user,
                        principalSchema: "petel_schema",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    update_user = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "petel_schema",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "petel_schema",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tracks_pricing",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    school_track_id = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric", nullable: true),
                    category = table.Column<int>(type: "integer", nullable: true),
                    level_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracks_pricing", x => x.id);
                    table.ForeignKey(
                        name: "FK_tracks_pricing_school_tracks_school_track_id",
                        column: x => x.school_track_id,
                        principalSchema: "petel_schema",
                        principalTable: "school_tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tracks_pricing_special_needs_pricing_categories_category",
                        column: x => x.category,
                        principalSchema: "petel_schema",
                        principalTable: "special_needs_pricing_categories",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_tracks_pricing_tracks_levels_level_id",
                        column: x => x.level_id,
                        principalSchema: "petel_schema",
                        principalTable: "tracks_levels",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "transaction_accounts",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    owner_entity_id = table.Column<int>(type: "integer", nullable: false),
                    related_entity_id = table.Column<int>(type: "integer", nullable: false),
                    account_type_id = table.Column<int>(type: "integer", nullable: false),
                    account_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    balance = table.Column<decimal>(type: "numeric", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_user = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_transaction_accounts_entities_owner_entity_id",
                        column: x => x.owner_entity_id,
                        principalSchema: "petel_schema",
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_transaction_accounts_entities_related_entity_id",
                        column: x => x.related_entity_id,
                        principalSchema: "petel_schema",
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transaction_accounts_transaction_account_types_account_type~",
                        column: x => x.account_type_id,
                        principalSchema: "petel_schema",
                        principalTable: "transaction_account_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transaction_accounts_users_created_user",
                        column: x => x.created_user,
                        principalSchema: "petel_schema",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transaction_accounts_users_update_user",
                        column: x => x.update_user,
                        principalSchema: "petel_schema",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account_id = table.Column<int>(type: "integer", nullable: false),
                    transaction_type_id = table.Column<int>(type: "integer", nullable: false),
                    transaction_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    related_transaction_id = table.Column<int>(type: "integer", nullable: true),
                    related_student_id = table.Column<int>(type: "integer", nullable: true),
                    school_year_id = table.Column<int>(type: "integer", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_user = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_transactions_hebrew_years_school_year_id",
                        column: x => x.school_year_id,
                        principalSchema: "petel_schema",
                        principalTable: "hebrew_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transactions_school_students_related_student_id",
                        column: x => x.related_student_id,
                        principalSchema: "petel_schema",
                        principalTable: "school_students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transactions_transaction_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "petel_schema",
                        principalTable: "transaction_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_transaction_types_transaction_type_id",
                        column: x => x.transaction_type_id,
                        principalSchema: "petel_schema",
                        principalTable: "transaction_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_transactions_related_transaction_id",
                        column: x => x.related_transaction_id,
                        principalSchema: "petel_schema",
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transactions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "petel_schema",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transaction_details",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    transaction_id = table.Column<int>(type: "integer", nullable: false),
                    detail_type_id = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    related_student_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_user = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_transaction_details_school_students_related_student_id",
                        column: x => x.related_student_id,
                        principalSchema: "petel_schema",
                        principalTable: "school_students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transaction_details_transaction_detail_types_detail_type_id",
                        column: x => x.detail_type_id,
                        principalSchema: "petel_schema",
                        principalTable: "transaction_detail_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transaction_details_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalSchema: "petel_schema",
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_action_audit_logs_result",
                schema: "petel_schema",
                table: "action_audit_logs",
                column: "result");

            migrationBuilder.CreateIndex(
                name: "IX_action_audit_logs_timestamp",
                schema: "petel_schema",
                table: "action_audit_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_action_audit_logs_user_id",
                schema: "petel_schema",
                table: "action_audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_action_audit_logs_user_id_timestamp",
                schema: "petel_schema",
                table: "action_audit_logs",
                columns: new[] { "user_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_action_types_name",
                schema: "petel_schema",
                table: "action_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_actions_action_type_id",
                schema: "petel_schema",
                table: "actions",
                column: "action_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_actions_is_active",
                schema: "petel_schema",
                table: "actions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_actions_name",
                schema: "petel_schema",
                table: "actions",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_actions_reference",
                schema: "petel_schema",
                table: "actions",
                column: "reference");

            migrationBuilder.CreateIndex(
                name: "IX_additional_study_programs_pricing_year_id",
                schema: "petel_schema",
                table: "additional_study_programs_pricing",
                column: "year_id");

            migrationBuilder.CreateIndex(
                name: "IX_alert_links_alert_id",
                schema: "petel_schema",
                table: "alert_links",
                column: "alert_id");

            migrationBuilder.CreateIndex(
                name: "IX_alert_links_entity_id",
                schema: "petel_schema",
                table: "alert_links",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_links_document_id_entity_id",
                schema: "petel_schema",
                table: "document_links",
                columns: new[] { "document_id", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_document_links_document_id_school_student_id",
                schema: "petel_schema",
                table: "document_links",
                columns: new[] { "document_id", "school_student_id" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_document_type_id",
                schema: "petel_schema",
                table: "documents",
                column: "document_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_entities_characterization_id",
                schema: "petel_schema",
                table: "entities",
                column: "characterization_id");

            migrationBuilder.CreateIndex(
                name: "IX_entities_contact_person",
                schema: "petel_schema",
                table: "entities",
                column: "contact_person");

            migrationBuilder.CreateIndex(
                name: "IX_entities_council",
                schema: "petel_schema",
                table: "entities",
                column: "council");

            migrationBuilder.CreateIndex(
                name: "IX_entities_entity_type_id",
                schema: "petel_schema",
                table: "entities",
                column: "entity_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_entities_owner",
                schema: "petel_schema",
                table: "entities",
                column: "owner");

            migrationBuilder.CreateIndex(
                name: "ix_hours_budget_entity_year_type",
                schema: "petel_schema",
                table: "hours_budget",
                columns: new[] { "entity_id", "school_year", "budget_type" });

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_sort_order",
                schema: "petel_schema",
                table: "menu_items",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "IX_roles_actions_action_id",
                schema: "petel_schema",
                table: "roles_actions",
                column: "action_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_actions_role_id_action_id",
                schema: "petel_schema",
                table: "roles_actions",
                columns: new[] { "role_id", "action_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_school_additional_study_programs_class_id",
                schema: "petel_schema",
                table: "school_additional_study_programs",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_school_additional_study_programs_is_last_version",
                schema: "petel_schema",
                table: "school_additional_study_programs",
                column: "is_last_version");

            migrationBuilder.CreateIndex(
                name: "IX_school_additional_study_programs_master_id",
                schema: "petel_schema",
                table: "school_additional_study_programs",
                column: "master_id");

            migrationBuilder.CreateIndex(
                name: "IX_school_additional_study_programs_school_year_id",
                schema: "petel_schema",
                table: "school_additional_study_programs",
                column: "school_year_id");

            migrationBuilder.CreateIndex(
                name: "IX_school_attribute_types_values_school_attribute_id",
                schema: "petel_schema",
                table: "school_attribute_types_values",
                column: "school_attribute_id");

            migrationBuilder.CreateIndex(
                name: "IX_school_attributes_id_school_year_id_version",
                schema: "petel_schema",
                table: "school_attributes",
                columns: new[] { "id", "school_year_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_school_attributes_school_attribute_type_id",
                schema: "petel_schema",
                table: "school_attributes",
                column: "school_attribute_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_school_attributes_school_year_id",
                schema: "petel_schema",
                table: "school_attributes",
                column: "school_year_id");

            migrationBuilder.CreateIndex(
                name: "IX_school_students_status",
                schema: "petel_schema",
                table: "school_students",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_school_tracks_class_id",
                schema: "petel_schema",
                table: "school_tracks",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_school_tracks_school_year_id",
                schema: "petel_schema",
                table: "school_tracks",
                column: "school_year_id");

            migrationBuilder.CreateIndex(
                name: "IX_school_tracks_track_id",
                schema: "petel_schema",
                table: "school_tracks",
                column: "track_id");

            migrationBuilder.CreateIndex(
                name: "IX_school_tracks_track_level_id",
                schema: "petel_schema",
                table: "school_tracks",
                column: "track_level_id");

            migrationBuilder.CreateIndex(
                name: "IX_school_tracks_user_id",
                schema: "petel_schema",
                table: "school_tracks",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_school_year_attributes_name",
                schema: "petel_schema",
                table: "school_year_attributes",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_school_year_attributes_year_id",
                schema: "petel_schema",
                table: "school_year_attributes",
                column: "year_id");

            migrationBuilder.CreateIndex(
                name: "IX_school_year_attributes_year_id_name",
                schema: "petel_schema",
                table: "school_year_attributes",
                columns: new[] { "year_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_school_years_school_id_hebrew_year_name",
                schema: "petel_schema",
                table: "school_years",
                columns: new[] { "school_id", "hebrew_year_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_schools_characterization_id",
                schema: "petel_schema",
                table: "schools",
                column: "characterization_id");

            migrationBuilder.CreateIndex(
                name: "IX_schools_contact_person",
                schema: "petel_schema",
                table: "schools",
                column: "contact_person");

            migrationBuilder.CreateIndex(
                name: "IX_schools_council",
                schema: "petel_schema",
                table: "schools",
                column: "council");

            migrationBuilder.CreateIndex(
                name: "IX_schools_entity_id",
                schema: "petel_schema",
                table: "schools",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_schools_entity_type_id",
                schema: "petel_schema",
                table: "schools",
                column: "entity_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_schools_inspector",
                schema: "petel_schema",
                table: "schools",
                column: "inspector");

            migrationBuilder.CreateIndex(
                name: "IX_schools_principal",
                schema: "petel_schema",
                table: "schools",
                column: "principal");

            migrationBuilder.CreateIndex(
                name: "IX_schools_school_year_id",
                schema: "petel_schema",
                table: "schools",
                column: "school_year_id");

            migrationBuilder.CreateIndex(
                name: "IX_sign_language_translators_person_id",
                schema: "petel_schema",
                table: "sign_language_translators",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_sign_language_translators_school_year_id",
                schema: "petel_schema",
                table: "sign_language_translators",
                column: "school_year_id");

            migrationBuilder.CreateIndex(
                name: "IX_sign_language_translators_school_year_id_person_id",
                schema: "petel_schema",
                table: "sign_language_translators",
                columns: new[] { "school_year_id", "person_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_special_needs_pricing_categories_pricing_element",
                schema: "petel_schema",
                table: "special_needs_pricing_categories",
                column: "pricing_element");

            migrationBuilder.CreateIndex(
                name: "IX_special_needs_pricing_elements_year_id",
                schema: "petel_schema",
                table: "special_needs_pricing_elements",
                column: "year_id");

            migrationBuilder.CreateIndex(
                name: "special_needs_pricing_steps_uc",
                schema: "petel_schema",
                table: "special_needs_pricing_steps",
                columns: new[] { "pricing_element", "category", "object_check", "object_element_check", "object_element_value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_attributes_description",
                schema: "petel_schema",
                table: "system_attributes",
                column: "description",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tracks_levels_school_track_id",
                schema: "petel_schema",
                table: "tracks_levels",
                column: "school_track_id");

            migrationBuilder.CreateIndex(
                name: "IX_tracks_pricing_category",
                schema: "petel_schema",
                table: "tracks_pricing",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_tracks_pricing_level_id",
                schema: "petel_schema",
                table: "tracks_pricing",
                column: "level_id");

            migrationBuilder.CreateIndex(
                name: "IX_tracks_pricing_school_track_id",
                schema: "petel_schema",
                table: "tracks_pricing",
                column: "school_track_id");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_account_types_created_user",
                schema: "petel_schema",
                table: "transaction_account_types",
                column: "created_user");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_account_types_is_active",
                schema: "petel_schema",
                table: "transaction_account_types",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_account_types_name",
                schema: "petel_schema",
                table: "transaction_account_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transaction_account_types_update_user",
                schema: "petel_schema",
                table: "transaction_account_types",
                column: "update_user");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_accounts_account_type_id",
                schema: "petel_schema",
                table: "transaction_accounts",
                column: "account_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_accounts_created_user",
                schema: "petel_schema",
                table: "transaction_accounts",
                column: "created_user");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_accounts_is_active",
                schema: "petel_schema",
                table: "transaction_accounts",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_accounts_owner_entity_id",
                schema: "petel_schema",
                table: "transaction_accounts",
                column: "owner_entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_accounts_owner_entity_id_related_entity_id_acco~",
                schema: "petel_schema",
                table: "transaction_accounts",
                columns: new[] { "owner_entity_id", "related_entity_id", "account_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transaction_accounts_related_entity_id",
                schema: "petel_schema",
                table: "transaction_accounts",
                column: "related_entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_accounts_update_user",
                schema: "petel_schema",
                table: "transaction_accounts",
                column: "update_user");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_detail_types_is_active",
                schema: "petel_schema",
                table: "transaction_detail_types",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_detail_types_name",
                schema: "petel_schema",
                table: "transaction_detail_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transaction_details_detail_type_id",
                schema: "petel_schema",
                table: "transaction_details",
                column: "detail_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_details_related_student_id",
                schema: "petel_schema",
                table: "transaction_details",
                column: "related_student_id");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_details_transaction_id",
                schema: "petel_schema",
                table: "transaction_details",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_types_is_active",
                schema: "petel_schema",
                table: "transaction_types",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_types_name",
                schema: "petel_schema",
                table: "transaction_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_account_id",
                schema: "petel_schema",
                table: "transactions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_related_student_id",
                schema: "petel_schema",
                table: "transactions",
                column: "related_student_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_related_transaction_id",
                schema: "petel_schema",
                table: "transactions",
                column: "related_transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_school_year_id",
                schema: "petel_schema",
                table: "transactions",
                column: "school_year_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_transaction_date",
                schema: "petel_schema",
                table: "transactions",
                column: "transaction_date");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_transaction_type_id",
                schema: "petel_schema",
                table: "transactions",
                column: "transaction_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_user_id",
                schema: "petel_schema",
                table: "transactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                schema: "petel_schema",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_user_id_role_id",
                schema: "petel_schema",
                table: "user_roles",
                columns: new[] { "user_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                schema: "petel_schema",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_entity_id",
                schema: "petel_schema",
                table: "users",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                schema: "petel_schema",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "action_audit_logs",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "additional_study_programs_pricing",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "alert_levels",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "alert_links",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "alert_statuses",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "alert_types",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "council_summary_vw",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "document_links",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "document_status_types",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "hours_budget",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "menu_items",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "roles_actions",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "school_additional_study_programs",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "school_attribute_types_values",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "school_attributes",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "school_student_pricing_elements",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "school_year_attributes",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "schools",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "sign_language_translators",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "special_needs_pricing_steps",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "student_school_years_registration_summary_vw",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "system_attributes",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "tracks_pricing",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "transaction_details",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "alerts",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "actions",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "school_attributes_types",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "school_tracks",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "special_needs_pricing_categories",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "transaction_detail_types",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "transactions",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "document_types",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "action_types",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "school_classes",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "school_years",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "tracks_levels",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "special_needs_pricing_elements",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "school_students",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "transaction_accounts",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "transaction_types",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "tracks",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "hebrew_years",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "statuses",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "transaction_account_types",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "users",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "entities",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "councils",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "entity_types",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "persons",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "special_needs_characterizations",
                schema: "petel_schema");
        }
    }
}
