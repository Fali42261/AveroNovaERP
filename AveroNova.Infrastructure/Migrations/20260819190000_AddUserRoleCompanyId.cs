using System;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AveroNova.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260819190000_AddUserRoleCompanyId")]
    public partial class AddUserRoleCompanyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserRoles_UserId_RoleId",
                table: "UserRoles");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "UserRoles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_CompanyId",
                table: "UserRoles",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_CompanyId_RoleId",
                table: "UserRoles",
                columns: new[] { "UserId", "CompanyId", "RoleId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Companies_CompanyId",
                table: "UserRoles",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Companies_CompanyId",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_UserId_CompanyId_RoleId",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_CompanyId",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "UserRoles");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_RoleId",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId" },
                unique: true);
        }
    }
}
