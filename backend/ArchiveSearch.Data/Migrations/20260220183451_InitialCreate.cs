using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArchiveSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collections",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    collection_unitid = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    repository = table.Column<string>(type: "TEXT", nullable: true),
                    date_range = table.Column<string>(type: "TEXT", nullable: true),
                    date_start = table.Column<int>(type: "INTEGER", nullable: true),
                    date_end = table.Column<int>(type: "INTEGER", nullable: true),
                    extent = table.Column<string>(type: "TEXT", nullable: true),
                    @abstract = table.Column<string>(name: "abstract", type: "TEXT", nullable: true),
                    scope_content = table.Column<string>(type: "TEXT", nullable: true),
                    biog_hist = table.Column<string>(type: "TEXT", nullable: true),
                    subjects = table.Column<string>(type: "TEXT", nullable: false),
                    persnames = table.Column<string>(type: "TEXT", nullable: false),
                    geognames = table.Column<string>(type: "TEXT", nullable: false),
                    genres = table.Column<string>(type: "TEXT", nullable: false),
                    corpnames = table.Column<string>(type: "TEXT", nullable: false),
                    series_titles = table.Column<string>(type: "TEXT", nullable: false),
                    compact_line = table.Column<string>(type: "TEXT", nullable: false),
                    source_file = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collections", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_collections_collection_unitid",
                table: "collections",
                column: "collection_unitid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collections");
        }
    }
}
