using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FicMart.PaymentGateway.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPaymentPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    customer_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    amount_minor_units = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character(3)", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.CheckConstraint("ck_payments_amount_positive", "amount_minor_units > 0");
                    table.CheckConstraint("ck_payments_currency_usd", "currency = 'USD'");
                    table.CheckConstraint("ck_payments_status", "status IN ('PendingAuthorization', 'Authorized', 'Declined', 'Captured', 'Voided', 'Refunded')");
                    table.CheckConstraint("ck_payments_timestamps", "updated_at >= created_at");
                });

            migrationBuilder.CreateTable(
                name: "authorization_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    bank_authorization_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authorization_attempts", x => x.id);
                    table.CheckConstraint("ck_authorization_attempts_bank_id", "(status = 'Succeeded' AND bank_authorization_id IS NOT NULL) OR (status <> 'Succeeded' AND bank_authorization_id IS NULL)");
                    table.CheckConstraint("ck_authorization_attempts_status", "status IN ('Pending', 'Succeeded', 'Rejected', 'Unknown')");
                    table.CheckConstraint("ck_authorization_attempts_timestamps", "updated_at >= created_at");
                    table.ForeignKey(
                        name: "FK_authorization_attempts_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_authorization_attempts_payment_id",
                table: "authorization_attempts",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ux_authorization_attempts_bank_authorization_id",
                table: "authorization_attempts",
                column: "bank_authorization_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_authorization_attempts_bank_idempotency_key",
                table: "authorization_attempts",
                column: "bank_idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_payments_order_id",
                table: "payments",
                column: "order_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authorization_attempts");

            migrationBuilder.DropTable(
                name: "payments");
        }
    }
}
