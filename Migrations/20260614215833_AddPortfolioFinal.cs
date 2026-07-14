using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyAIAgent.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortfolioItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserName = table.Column<string>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    Shares = table.Column<decimal>(type: "TEXT", nullable: false),
                    BuyPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false),
                    BoughtAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioItems", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioItems");
        }
    }
}
