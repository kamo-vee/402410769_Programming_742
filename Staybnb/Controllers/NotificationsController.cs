using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Controllers;

[Authorize]
public class NotificationsController(StaybnbDbContext db, UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Index()
    {
        var id = users.GetUserId(User);
        return View(await db.Notifications.Where(n => n.UserId == id).OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = users.GetUserId(User);
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification is null) return NotFound();
        notification.IsRead = true;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var id = users.GetUserId(User);
        await db.Notifications.Where(n => n.UserId == id && !n.IsRead).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return RedirectToAction(nameof(Index));
    }
}
