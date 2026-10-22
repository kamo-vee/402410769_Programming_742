using Microsoft.AspNetCore.Identity;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Services;

public static class StaybnbRoles
{
    public const string Guest = "Guest";
    public const string Host = "Host";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";
}

public class AuditService(StaybnbDbContext db)
{
    public async Task RecordAsync(string? userId, string action, ActivityType type = ActivityType.Information)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        db.ActivityLogs.Add(new ActivityLog { UserId = userId, Action = action, ActivityType = type });
        await db.SaveChangesAsync();
    }
}

public class NotificationService(StaybnbDbContext db)
{
    public async Task CreateAsync(string userId, string title, string content, NotificationType type)
    {
        db.Notifications.Add(new Notification { UserId = userId, Title = title, Content = content, Type = type });
        await db.SaveChangesAsync();
    }
}

public static class RoleSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<StaybnbDbContext>();
        var email = configuration["AdminSeed:Email"];
        var password = configuration["AdminSeed:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "SuperAdmin setup is required. Set AdminSeed__Email and AdminSeed__Password in environment variables, then restart. See START_HERE.cs.");

        await using var transaction = await db.Database.BeginTransactionAsync();

        foreach (var roleName in new[] { StaybnbRoles.Guest, StaybnbRoles.Host, StaybnbRoles.Admin, StaybnbRoles.SuperAdmin })
        {
            if (!await roles.RoleExistsAsync(roleName))
            {
                var result = await roles.CreateAsync(new IdentityRole(roleName));
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Could not seed role {roleName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        var admin = await users.FindByEmailAsync(email);
        var changed = false;
        if (admin is not null && !await users.IsInRoleAsync(admin, StaybnbRoles.SuperAdmin))
            throw new InvalidOperationException("AdminSeed:Email belongs to an existing non-SuperAdmin account. Choose an unused email for the seeded SuperAdmin.");
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Staybnb",
                LastName = "SuperAdmin"
            };
            var create = await users.CreateAsync(admin, password);
            if (!create.Succeeded)
                throw new InvalidOperationException($"Could not seed SuperAdmin: {string.Join(", ", create.Errors.Select(e => e.Description))}");
            changed = true;
        }

        foreach (var adminRole in new[] { StaybnbRoles.Admin, StaybnbRoles.SuperAdmin })
            if (!await users.IsInRoleAsync(admin, adminRole))
            {
                var result = await users.AddToRoleAsync(admin, adminRole);
                if (!result.Succeeded) throw new InvalidOperationException("SuperAdmin role seeding failed.");
                changed = true;
            }
        if (await users.IsInRoleAsync(admin, StaybnbRoles.Guest))
        {
            await users.RemoveFromRoleAsync(admin, StaybnbRoles.Guest);
            changed = true;
        }
        if (changed)
        {
            var audit = scope.ServiceProvider.GetRequiredService<AuditService>();
            await audit.RecordAsync(admin.Id, "Startup seeding created/updated SuperAdmin and Admin roles", ActivityType.RoleChange);
        }
        await transaction.CommitAsync();
    }
}
