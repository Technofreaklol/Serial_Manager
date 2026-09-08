using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SerialManager.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ArticleNumber",
                table: "SerialHistories",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "RowVersion",
                table: "Articles",
                type: "timestamp(6)",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.CreateIndex(
                name: "IX_SerialHistories_ArticleNumber_SerialNumber",
                table: "SerialHistories",
                columns: new[] { "ArticleNumber", "SerialNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SerialHistories_ArticleNumber_SerialNumber",
                table: "SerialHistories");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Articles");

            migrationBuilder.AlterColumn<string>(
                name: "ArticleNumber",
                table: "SerialHistories",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SerialHistories_SerialNumber",
                table: "SerialHistories",
                column: "SerialNumber",
                unique: true);
        }
    }
}
