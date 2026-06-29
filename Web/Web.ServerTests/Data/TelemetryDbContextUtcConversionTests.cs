using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;

namespace Web.ServerTests.Data;

[TestClass]
public class TelemetryDbContextUtcConversionTests
{
    [TestMethod]
    public async Task UserActivityTimestamps_AreStoredAndLoadedAsUtc()
    {
        var options = new DbContextOptionsBuilder<TelemetryDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var context = new TelemetryDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        var lastActiveLocal = DateTime.SpecifyKind(new DateTime(2026, 6, 29, 23, 15, 0), DateTimeKind.Local);
        var lastLoginUnspecified = DateTime.SpecifyKind(new DateTime(2026, 6, 29, 20, 5, 0), DateTimeKind.Unspecified);

        var user = new User
        {
            FirstName = "Test",
            LastName = "User",
            Email = "utc-test@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastUpdate = DateTime.UtcNow,
            LastActive = lastActiveLocal,
            LastLogin = lastLoginUnspecified,
            UserRoles = new List<UserRole>()
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var connection = context.Database.GetDbConnection();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT LastActive, LastLogin FROM Users WHERE ID = $id";
            var idParameter = command.CreateParameter();
            idParameter.ParameterName = "$id";
            idParameter.Value = user.ID;
            command.Parameters.Add(idParameter);

            await using var reader = await command.ExecuteReaderAsync();
            Assert.IsTrue(await reader.ReadAsync());

            var rawLastActive = reader.GetString(0);
            var rawLastLogin = reader.GetString(1);

            StringAssert.EndsWith(rawLastActive, "Z");
            StringAssert.EndsWith(rawLastLogin, "Z");
        }

        context.ChangeTracker.Clear();
        var savedUser = await context.Users.SingleAsync(u => u.ID == user.ID);

        Assert.IsTrue(savedUser.LastActive.HasValue);
        Assert.IsTrue(savedUser.LastLogin.HasValue);
        Assert.AreEqual(DateTimeKind.Utc, savedUser.LastActive.Value.Kind);
        Assert.AreEqual(DateTimeKind.Utc, savedUser.LastLogin.Value.Kind);
    }
}
