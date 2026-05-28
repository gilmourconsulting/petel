using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PetelATH.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCouncilTypeAndDistrict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create council_types lookup table
            migrationBuilder.CreateTable(
                name: "council_types",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_user = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_council_types", x => x.id);
                    table.UniqueConstraint("uk_council_types_name", x => x.name);
                });

            migrationBuilder.CreateIndex(
                name: "idx_council_types_name",
                schema: "petel_schema",
                table: "council_types",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_council_types_sort_order",
                schema: "petel_schema",
                table: "council_types",
                column: "sort_order");

            // Create districts lookup table
            migrationBuilder.CreateTable(
                name: "districts",
                schema: "petel_schema",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_user = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_districts", x => x.id);
                    table.UniqueConstraint("uk_districts_name", x => x.name);
                });

            migrationBuilder.CreateIndex(
                name: "idx_districts_name",
                schema: "petel_schema",
                table: "districts",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_districts_sort_order",
                schema: "petel_schema",
                table: "districts",
                column: "sort_order");

            // Add new columns to councils table
            migrationBuilder.AddColumn<string>(
                name: "long_name",
                schema: "petel_schema",
                table: "councils",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "council_type_id",
                schema: "petel_schema",
                table: "councils",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "district_id",
                schema: "petel_schema",
                table: "councils",
                type: "integer",
                nullable: true);

            // Add FK constraints
            migrationBuilder.AddForeignKey(
                name: "FK_councils_council_types_council_type_id",
                schema: "petel_schema",
                table: "councils",
                column: "council_type_id",
                principalSchema: "petel_schema",
                principalTable: "council_types",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_councils_districts_district_id",
                schema: "petel_schema",
                table: "councils",
                column: "district_id",
                principalSchema: "petel_schema",
                principalTable: "districts",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateIndex(
                name: "idx_councils_council_type_id",
                schema: "petel_schema",
                table: "councils",
                column: "council_type_id");

            migrationBuilder.CreateIndex(
                name: "idx_councils_district_id",
                schema: "petel_schema",
                table: "councils",
                column: "district_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_councils_council_types_council_type_id",
                schema: "petel_schema",
                table: "councils");

            migrationBuilder.DropForeignKey(
                name: "FK_councils_districts_district_id",
                schema: "petel_schema",
                table: "councils");

            migrationBuilder.DropIndex(
                name: "idx_councils_council_type_id",
                schema: "petel_schema",
                table: "councils");

            migrationBuilder.DropIndex(
                name: "idx_councils_district_id",
                schema: "petel_schema",
                table: "councils");

            migrationBuilder.DropColumn(
                name: "long_name",
                schema: "petel_schema",
                table: "councils");

            migrationBuilder.DropColumn(
                name: "council_type_id",
                schema: "petel_schema",
                table: "councils");

            migrationBuilder.DropColumn(
                name: "district_id",
                schema: "petel_schema",
                table: "councils");

            migrationBuilder.DropTable(
                name: "council_types",
                schema: "petel_schema");

            migrationBuilder.DropTable(
                name: "districts",
                schema: "petel_schema");
        }
    }
}
