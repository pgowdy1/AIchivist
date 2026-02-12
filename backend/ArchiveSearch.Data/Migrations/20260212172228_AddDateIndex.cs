using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArchiveSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_collections_date_start_date_end",
                table: "collections",
                columns: new[] { "date_start", "date_end" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_collections_date_start_date_end",
                table: "collections");
        }
    }
}
