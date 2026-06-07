using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArchiveSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collection_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    requester_name = table.Column<string>(type: "TEXT", nullable: false),
                    requester_email = table.Column<string>(type: "TEXT", nullable: false),
                    requester_phone = table.Column<string>(type: "TEXT", nullable: true),
                    affiliation = table.Column<string>(type: "TEXT", nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    collections_json = table.Column<string>(type: "TEXT", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    email_sent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_requests", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collection_requests");
        }
    }
}
