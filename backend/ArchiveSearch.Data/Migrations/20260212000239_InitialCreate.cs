using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_unitid = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    repository = table.Column<string>(type: "text", nullable: true),
                    date_range = table.Column<string>(type: "text", nullable: true),
                    date_start = table.Column<int>(type: "integer", nullable: true),
                    date_end = table.Column<int>(type: "integer", nullable: true),
                    extent = table.Column<string>(type: "text", nullable: true),
                    @abstract = table.Column<string>(name: "abstract", type: "text", nullable: true),
                    scope_content = table.Column<string>(type: "text", nullable: true),
                    biog_hist = table.Column<string>(type: "text", nullable: true),
                    subjects = table.Column<string[]>(type: "text[]", nullable: false),
                    persnames = table.Column<string[]>(type: "text[]", nullable: false),
                    geognames = table.Column<string[]>(type: "text[]", nullable: false),
                    genres = table.Column<string[]>(type: "text[]", nullable: false),
                    corpnames = table.Column<string[]>(type: "text[]", nullable: false),
                    series_titles = table.Column<string[]>(type: "text[]", nullable: false),
                    compact_line = table.Column<string>(type: "text", nullable: false),
                    source_file = table.Column<string>(type: "text", nullable: true)
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
