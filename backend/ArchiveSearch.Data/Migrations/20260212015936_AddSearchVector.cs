using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace ArchiveSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add column as nullable first so existing rows aren't rejected
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "collections",
                type: "tsvector",
                nullable: true);

            // Populate all existing rows with weighted tsvector
            migrationBuilder.Sql("""
                UPDATE collections SET search_vector =
                    setweight(to_tsvector('english', coalesce(title, '')), 'A') ||
                    setweight(to_tsvector('english',
                        coalesce(abstract, '') || ' ' ||
                        coalesce(array_to_string(subjects, ' '), '') || ' ' ||
                        coalesce(array_to_string(persnames, ' '), '') || ' ' ||
                        coalesce(array_to_string(geognames, ' '), '')
                    ), 'B') ||
                    setweight(to_tsvector('english',
                        coalesce(scope_content, '') || ' ' ||
                        coalesce(biog_hist, '') || ' ' ||
                        coalesce(array_to_string(corpnames, ' '), '') || ' ' ||
                        coalesce(array_to_string(genres, ' '), '') || ' ' ||
                        coalesce(array_to_string(series_titles, ' '), '')
                    ), 'C');
                """);

            // Now make it NOT NULL with empty tsvector default for future inserts
            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "collections",
                type: "tsvector",
                nullable: false,
                defaultValueSql: "''::tsvector");

            // GIN index for fast full-text search
            migrationBuilder.CreateIndex(
                name: "IX_collections_search_vector",
                table: "collections",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_collections_search_vector",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "collections");
        }
    }
}
