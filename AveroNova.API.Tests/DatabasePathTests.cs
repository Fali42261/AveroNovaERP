using AveroNova.Shared.Helpers;
using Xunit;

namespace AveroNova.API.Tests;

public sealed class DatabasePathTests
{
    [Fact]
    public void DevelopmentDatabase_IsCreatedUnderApiDatabaseFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"averonova-api-{Guid.NewGuid():N}");
        try
        {
            var path = DatabasePath.GetDatabasePath(root);

            Assert.Equal(
                Path.Combine(root, "Database", "AveroNovaDev.db"),
                path);
            Assert.True(Directory.Exists(Path.Combine(root, "Database")));
            Assert.False(Directory.Exists(Path.Combine(root, "Data")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
