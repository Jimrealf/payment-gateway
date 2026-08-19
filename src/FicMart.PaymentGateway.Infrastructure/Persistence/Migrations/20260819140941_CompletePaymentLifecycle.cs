using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace FicMart.PaymentGateway.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompletePaymentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_idempotency_records_operation",
                table: "idempotency_records");

            migrationBuilder.CreateTable(
                name: "audit_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    previous_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    new_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actor = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_records_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    detail_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_records", x => x.id);
                    table.CheckConstraint("ck_reconciliation_records_operation", "operation IS NULL OR operation IN ('Authorize', 'Capture', 'Void', 'Refund')");
                    table.CheckConstraint("ck_reconciliation_records_outcome", "outcome IN ('Recovered', 'NoActionRequired', 'OperatorReviewRequired', 'AlreadyProcessing', 'StillPending')");
                    table.ForeignKey(
                        name: "FK_reconciliation_records_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refund_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    bank_refund_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refund_attempts", x => x.id);
                    table.CheckConstraint("ck_refund_attempts_bank_id", "(status = 'Succeeded' AND bank_refund_id IS NOT NULL) OR (status <> 'Succeeded' AND bank_refund_id IS NULL)");
                    table.CheckConstraint("ck_refund_attempts_status", "status IN ('Pending', 'Succeeded', 'Rejected', 'Unknown')");
                    table.ForeignKey(
                        name: "FK_refund_attempts_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "void_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    bank_void_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_void_attempts", x => x.id);
                    table.CheckConstraint("ck_void_attempts_bank_id", "(status = 'Succeeded' AND bank_void_id IS NOT NULL) OR (status <> 'Succeeded' AND bank_void_id IS NULL)");
                    table.CheckConstraint("ck_void_attempts_status", "status IN ('Pending', 'Succeeded', 'Rejected', 'Unknown')");
                    table.ForeignKey(
                        name: "FK_void_attempts_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_idempotency_records_operation",
                table: "idempotency_records",
                sql: "operation IN ('Authorize', 'Capture', 'Void', 'Refund')");

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_payment_occurred_at",
                table: "audit_records",
                columns: new[] { "payment_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_records_payment_created_at",
                table: "reconciliation_records",
                columns: new[] { "payment_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_refund_attempts_bank_idempotency_key",
                table: "refund_attempts",
                column: "bank_idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_refund_attempts_bank_refund_id",
                table: "refund_attempts",
                column: "bank_refund_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_refund_attempts_payment_id",
                table: "refund_attempts",
                column: "payment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_void_attempts_bank_idempotency_key",
                table: "void_attempts",
                column: "bank_idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_void_attempts_bank_void_id",
                table: "void_attempts",
                column: "bank_void_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_void_attempts_payment_id",
                table: "void_attempts",
                column: "payment_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_records");

            migrationBuilder.DropTable(
                name: "reconciliation_records");

            migrationBuilder.DropTable(
                name: "refund_attempts");

            migrationBuilder.DropTable(
                name: "void_attempts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_idempotency_records_operation",
                table: "idempotency_records");

            migrationBuilder.AddCheckConstraint(
                name: "ck_idempotency_records_operation",
                table: "idempotency_records",
                sql: "operation IN ('Authorize', 'Capture')");
        }
    }
}
