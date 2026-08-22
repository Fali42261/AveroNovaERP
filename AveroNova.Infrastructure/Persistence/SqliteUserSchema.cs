using System.Data;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Persistence
{
    /// <summary>
    /// Adds User.MobileNumber on existing SQLite files created before the column existed.
    /// New databases receive the column from the EF model via EnsureCreated.
    /// </summary>
    public static class SqliteUserSchema
    {
        public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
                await connection.OpenAsync(cancellationToken);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Users') WHERE name = 'MobileNumber'";
                var result = await command.ExecuteScalarAsync(cancellationToken);
                if (Convert.ToInt32(result ?? 0) > 0)
                    return;
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }

            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "Users" ADD COLUMN "MobileNumber" TEXT NOT NULL DEFAULT '';""",
                cancellationToken);
        }
    }
}
