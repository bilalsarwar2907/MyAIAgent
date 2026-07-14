using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyAIAgent.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceAlertsFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserName = table.Column<string>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    TargetPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    Direction = table.Column<string>(type: "TEXT", nullable: false),
                    IsTriggered = table.Column<bool>(type: "INTEGER", nullable: false),
                    TriggeredPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    TriggeredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceAlerts", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceAlerts");
        }
    }
}
