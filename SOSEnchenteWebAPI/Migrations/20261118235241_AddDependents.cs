using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SOSEnchenteWebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddDependents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dependents",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DependentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dependents", x => new { x.UserId, x.DependentId });
                    table.ForeignKey(
                        name: "FK_Dependents_Users_DependentId",
                        column: x => x.DependentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dependents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Dependents_DependentId",
                table: "Dependents",
                column: "DependentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dependents");
        }
    }
}
