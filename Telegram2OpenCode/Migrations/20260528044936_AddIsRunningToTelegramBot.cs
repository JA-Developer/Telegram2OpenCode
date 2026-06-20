using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telegram2OpenCode.Migrations
{
    /// <inheritdoc />
    public partial class AddIsRunningToTelegramBot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRunning",
                table: "TelegramBots",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRunning",
                table: "TelegramBots");
        }
    }
}
