using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Controllers;

[Authorize]
public class InboxController(
    StaybnbDbContext db,
    UserManager<ApplicationUser> users,
    NotificationService notifications) : Controller
{
    public async Task<IActionResult> Index()
    {
        var id = users.GetUserId(User);
        return View(await db.Messages.Include(m => m.Sender).Include(m => m.Receiver)
            .Where(m => m.SenderId == id || m.ReceiverId == id)
            .OrderByDescending(m => m.Timestamp).Take(200).ToListAsync());
    }

    public async Task<IActionResult> Thread(string otherId)
    {
        var id = users.GetUserId(User);
        var other = await users.FindByIdAsync(otherId);
        if (other is null || otherId == id) return NotFound();
        var messages = await db.Messages.Where(m =>
            (m.SenderId == id && m.ReceiverId == otherId) || (m.SenderId == otherId && m.ReceiverId == id))
            .OrderBy(m => m.Timestamp).ToListAsync();
        foreach (var message in messages.Where(m => m.ReceiverId == id)) message.IsRead = true;
        await db.SaveChangesAsync();
        ViewBag.OtherUser = other;
        return View(messages);
    }

    public IActionResult Compose() => View(new ComposeViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Compose(ComposeViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var receiver = await users.FindByEmailAsync(model.Email.Trim());
        if (receiver is null || receiver.Id == users.GetUserId(User))
        {
            ModelState.AddModelError("", "Enter another registered user's email address.");
            return View(model);
        }
        await SendAsync(receiver.Id, model.Content);
        return RedirectToAction(nameof(Thread), new { otherId = receiver.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(MessageCreateViewModel model)
    {
        if (!ModelState.IsValid || model.ReceiverId == users.GetUserId(User) || await users.FindByIdAsync(model.ReceiverId) is null)
            return BadRequest("A valid recipient and a message of 1–2000 characters are required.");
        await SendAsync(model.ReceiverId, model.Content);
        return RedirectToAction(nameof(Thread), new { otherId = model.ReceiverId });
    }

    private async Task SendAsync(string receiverId, string content)
    {
        var sender = await users.GetUserAsync(User);
        if (sender is null) return;
        db.Messages.Add(new Message { SenderId = sender.Id, ReceiverId = receiverId, Content = content.Trim() });
        await db.SaveChangesAsync();
        await notifications.CreateAsync(receiverId, "New message", $"{sender.FirstName} sent you a message.", NotificationType.Message);
    }
}
