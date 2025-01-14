using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EstateKit.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Initial database migration that creates a secure and compliant schema for the EstateKit encryption service.
    /// Implements GDPR and SOC 2 requirements with comprehensive audit logging capabilities.
    /// </summary>
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create dedicated schema with restricted permissions
            migrationBuilder.EnsureSchema(
                name: "estatekit",
                schema: @"
                    REVOKE ALL ON ALL TABLES IN SCHEMA estatekit FROM PUBLIC;
                    GRANT USAGE ON SCHEMA estatekit TO estatekit_app;
                    ALTER DEFAULT PRIVILEGES IN SCHEMA estatekit 
                    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO estatekit_app;"
            );

            // Enable required PostgreSQL extensions
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_stat_statements;");

            // Create UserKeys table with encryption and audit capabilities
            migrationBuilder.CreateTable(
                name: "user_keys",
                schema: "estatekit",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "varchar(2048)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    rotation_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    created_by = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    updated_by = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_keys", x => x.user_id);
                    table.CheckConstraint("CK_user_keys_key_format", "key ~ '^[A-Za-z0-9+/=\\-_]+$'");
                    table.CheckConstraint("CK_user_keys_key_length", "length(key) >= 2048");
                }
            );

            // Create UserKeyHistory table with encryption and audit trail
            migrationBuilder.CreateTable(
                name: "user_key_history",
                schema: "estatekit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn)
                        .Annotation("Npgsql:IdentityStartValue", 1000),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    key_value = table.Column<string>(type: "varchar(2048)", nullable: false),
                    rotation_date = table.Column<DateTime>(type: "timestamp with time zone", precision: 6, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    rotation_reason = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    created_by = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    system_version = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_key_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_key_history_user_keys_user_id",
                        column: x => x.user_id,
                        principalSchema: "estatekit",
                        principalTable: "user_keys",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.CheckConstraint("CK_user_key_history_key_format", "key_value ~ '^[A-Za-z0-9+/=\\-_]+$'");
                }
            );

            // Create optimized indexes
            migrationBuilder.CreateIndex(
                name: "IX_user_keys_is_active_user_id",
                schema: "estatekit",
                table: "user_keys",
                columns: new[] { "is_active", "user_id" },
                filter: "is_active = true"
            );

            migrationBuilder.CreateIndex(
                name: "IX_user_key_history_user_rotation",
                schema: "estatekit",
                table: "user_key_history",
                columns: new[] { "user_id", "rotation_date" },
                filter: "rotation_date >= CURRENT_DATE - INTERVAL '7 years'"
            );

            // Enable row-level security
            migrationBuilder.Sql(@"
                ALTER TABLE estatekit.user_keys ENABLE ROW LEVEL SECURITY;
                ALTER TABLE estatekit.user_key_history ENABLE ROW LEVEL SECURITY;
                
                CREATE POLICY user_keys_tenant_isolation_policy ON estatekit.user_keys
                    USING (created_by = current_user);
                    
                CREATE POLICY user_key_history_tenant_isolation_policy ON estatekit.user_key_history
                    USING (created_by = current_user);
            ");

            // Create audit triggers
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION estatekit.audit_trigger_function()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        NEW.updated_at = CURRENT_TIMESTAMP;
                        NEW.updated_by = current_user;
                    ELSIF TG_OP = 'INSERT' THEN
                        NEW.created_at = CURRENT_TIMESTAMP;
                        NEW.created_by = current_user;
                        NEW.updated_at = CURRENT_TIMESTAMP;
                        NEW.updated_by = current_user;
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER user_keys_audit_trigger
                    BEFORE INSERT OR UPDATE ON estatekit.user_keys
                    FOR EACH ROW EXECUTE FUNCTION estatekit.audit_trigger_function();

                CREATE TRIGGER user_key_history_audit_trigger
                    BEFORE INSERT OR UPDATE ON estatekit.user_key_history
                    FOR EACH ROW EXECUTE FUNCTION estatekit.audit_trigger_function();
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop audit triggers first
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS user_key_history_audit_trigger ON estatekit.user_key_history;
                DROP TRIGGER IF EXISTS user_keys_audit_trigger ON estatekit.user_keys;
                DROP FUNCTION IF EXISTS estatekit.audit_trigger_function();
            ");

            // Drop tables
            migrationBuilder.DropTable(
                name: "user_key_history",
                schema: "estatekit");

            migrationBuilder.DropTable(
                name: "user_keys",
                schema: "estatekit");

            // Disable row level security
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS user_key_history_tenant_isolation_policy ON estatekit.user_key_history;
                DROP POLICY IF EXISTS user_keys_tenant_isolation_policy ON estatekit.user_keys;
            ");

            // Drop schema
            migrationBuilder.DropSchema(
                name: "estatekit");

            // Drop extensions if no other schemas need them
            migrationBuilder.Sql(@"
                DROP EXTENSION IF EXISTS pg_stat_statements;
                DROP EXTENSION IF EXISTS pgcrypto;
            ");
        }
    }
}