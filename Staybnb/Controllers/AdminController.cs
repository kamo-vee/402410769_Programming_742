using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Controllers;

[Authorize(Roles = StaybnbRoles.Admin + "," + StaybnbRoles.SuperAdmin)]
public class AdminController(
    StaybnbDbContext db,
    UserManager<ApplicationUser> userManager,
    AuditService audit,
    NotificationService notifications) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.PendingApplications = await db.HostApplications.CountAsync(a => a.Status == ApplicationStatus.Pending);
        ViewBag.Users = await userManager.Users.CountAsync();
        ViewBag.ActiveProperties = await db.Properties.CountAsync(p => p.IsActive);
        ViewBag.RecentActivity = await db.ActivityLogs.Include(a => a.User).OrderByDescending(a => a.CreatedAt).Take(20).ToListAsync();
        ViewBag.Pending = await db.HostApplications.Include(a => a.User).Include(a => a.Property)
            .Where(a => a.Status == ApplicationStatus.Pending).OrderBy(a => a.AppliedAt).ToListAsync();
        return View();
    }

    public async Task<IActionResult> Application(int id)
    {
        var application = await db.HostApplications.Include(a => a.User)
            .Include(a => a.Property).ThenInclude(p => p!.Images).FirstOrDefaultAsync(a => a.Id == id);
        return application is null ? NotFound() : View(application);
    }

    public async Task<IActionResult> Logs(int page = 1)
    {
        page = Math.Max(1, page);
        ViewBag.Page = page;
        return View(await db.ActivityLogs.Include(a => a.User).OrderByDescending(a => a.Id)
            .Skip((page - 1) * 50).Take(50).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewApplication(int id, bool approve)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var application = await db.HostApplications.Include(a => a.Property)
            .FirstOrDefaultAsync(a => a.Id == id && a.Status == ApplicationStatus.Pending);
        if (application is null) return NotFound();
        var admin = await userManager.GetUserAsync(User);
        if (admin is null) return Challenge();

        var user = await userManager.FindByIdAsync(application.ApplicationUserId);
        if (user is null) return NotFound();
        var isGuest = await userManager.IsInRoleAsync(user, StaybnbRoles.Guest);
        var isHost = await userManager.IsInRoleAsync(user, StaybnbRoles.Host);
        if (approve && ((!isGuest && !isHost) ||
                        !await db.PropertyImages.AnyAsync(i => i.PropertyId == application.PropertyId)))
        {
            TempData["Error"] = "Approval requires a Guest/Host account and at least one property image.";
            return RedirectToAction(nameof(Index));
        }
        application.Status = approve ? ApplicationStatus.Approved : ApplicationStatus.Rejected;
        application.ReviewerId = admin.Id;
        application.ReviewedAt = DateTime.UtcNow;
        application.Property!.IsActive = approve;

        if (approve)
        {
            if (isGuest)
            {
                var addHost = await userManager.AddToRoleAsync(user, StaybnbRoles.Host);
                if (!addHost.Succeeded)
                {
                    TempData["Error"] = string.Join("; ", addHost.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Index));
                }
                var removeGuest = await userManager.RemoveFromRoleAsync(user, StaybnbRoles.Guest);
                if (!removeGuest.Succeeded)
                {
                    await userManager.RemoveFromRoleAsync(user, StaybnbRoles.Host);
                    TempData["Error"] = string.Join("; ", removeGuest.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Index));
                }
                await audit.RecordAsync(user.Id, $"Guest role removed; Host role added after application #{id} approval", ActivityType.RoleChange);
                await userManager.UpdateSecurityStampAsync(user);
            }
            if (!await db.CheckInProcesses.AnyAsync(c => c.PropertyId == application.PropertyId))
            {
                db.CheckInProcesses.Add(new CheckInProcess
                {
                    PropertyId = application.PropertyId,
                    Title = "Check-in instructions",
                    StepsJson = "[\"Contact your host for arrival details\"]",
                    RequiredDocuments = "Government-issued ID"
                });
            }
        }
        else
        {
            await audit.RecordAsync(user.Id, $"Host application #{id} rejected", ActivityType.Application);
        }

        await db.SaveChangesAsync();
        await notifications.CreateAsync(user.Id,
            approve ? "Host application approved" : "Host application update",
            approve ? $"Your property '{application.Property.Title}' is approved. Your account is now a Host."
                    : $"Your property application for '{application.Property.Title}' was not approved.",
            NotificationType.Application);
        await audit.RecordAsync(admin.Id, $"Reviewed host application #{id}: {application.Status}", ActivityType.Application);
        await transaction.CommitAsync();
        TempData["Message"] = approve ? "Application approved. Guest applicants are changed to Host." : "Application rejected.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = StaybnbRoles.SuperAdmin)]
    public async Task<IActionResult> Users(string? search)
    {
        var guestRoleId = await db.Roles.Where(r => r.Name == StaybnbRoles.Guest).Select(r => r.Id).SingleAsync();
        var users = userManager.Users.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == guestRoleId));
        if (!string.IsNullOrWhiteSpace(search))
            users = users.Where(u => u.Email!.Contains(search) || u.FirstName.Contains(search) || u.LastName.Contains(search));
        ViewBag.Search = search;
        return View(await users.OrderBy(u => u.Email).Take(100).ToListAsync());
    }

    [HttpPost, Authorize(Roles = StaybnbRoles.SuperAdmin), ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteGuest(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !await userManager.IsInRoleAsync(user, StaybnbRoles.Guest)) return NotFound();
        var admin = await userManager.GetUserAsync(User);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await userManager.AddToRoleAsync(user, StaybnbRoles.Admin);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Users));
        }
        var remove = await userManager.RemoveFromRoleAsync(user, StaybnbRoles.Guest);
        if (!remove.Succeeded) return BadRequest("Role update failed. No changes were committed.");
        await userManager.UpdateSecurityStampAsync(user);
        if (admin is not null) await audit.RecordAsync(admin.Id, $"Promoted {user.Email} from Guest to Admin", ActivityType.RoleChange);
        await audit.RecordAsync(user.Id, "Guest role removed; Admin role granted by SuperAdmin", ActivityType.RoleChange);
        await notifications.CreateAsync(user.Id, "Administrator access granted", "Your account has been assigned the Admin role.", NotificationType.Information);
        await transaction.CommitAsync();
        TempData["Message"] = $"{user.Email} is now an Admin.";
        return RedirectToAction(nameof(Users));
    }
}
