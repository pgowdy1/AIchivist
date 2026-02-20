using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArchiveSearch.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFts5SearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE VIRTUAL TABLE IF NOT EXISTS collections_fts USING fts5(
                    collection_unitid UNINDEXED,
                    title,
                    abstract_subjects,
                    scope_biog_detail,
                    tokenize='porter unicode61'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS collections_fts;");
        }
    }
}
