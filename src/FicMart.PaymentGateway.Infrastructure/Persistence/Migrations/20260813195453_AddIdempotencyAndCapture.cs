using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FicMart.PaymentGateway.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyAndCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "capture_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    bank_capture_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capture_attempts", x => x.id);
                    table.CheckConstraint("ck_capture_attempts_bank_id", "(status = 'Succeeded' AND bank_capture_id IS NOT NULL) OR (status <> 'Succeeded' AND bank_capture_id IS NULL)");
                    table.CheckConstraint("ck_capture_attempts_status", "status IN ('Pending', 'Succeeded', 'Rejected', 'Unknown')");
                    table.CheckConstraint("ck_capture_attempts_timestamps", "updated_at >= created_at");
                    table.ForeignKey(
                        name: "FK_capture_attempts_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    request_fingerprint = table.Column<string>(type: "character(64)", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => new { x.operation, x.key });
                    table.CheckConstraint("ck_idempotency_records_fingerprint", "char_length(request_fingerprint) = 64");
                    table.CheckConstraint("ck_idempotency_records_operation", "operation IN ('Authorize', 'Capture')");
                    table.CheckConstraint("ck_idempotency_records_state", "state IN ('Processing', 'Retryable', 'Completed')");
                    table.CheckConstraint("ck_idempotency_records_timestamps", "updated_at >= created_at");
                    table.ForeignKey(
                        name: "FK_idempotency_records_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_capture_attempts_payment_id",
                table: "capture_attempts",
                column: "payment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_capture_attempts_bank_capture_id",
                table: "capture_attempts",
                column: "bank_capture_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_capture_attempts_bank_idempotency_key",
                table: "capture_attempts",
                column: "bank_idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_payment_id",
                table: "idempotency_records",
                column: "payment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "capture_attempts");

            migrationBuilder.DropTable(
                name: "idempotency_records");
        }
    }
}
