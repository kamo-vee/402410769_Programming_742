using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Controllers;

[Authorize(Roles = StaybnbRoles.Guest)]
public class CheckInController(
    StaybnbDbContext db,
    UserManager<ApplicationUser> userManager,
    IWebHostEnvironment environment,
    AuditService audit,
    NotificationService notifications) : Controller
{
    public async Task<IActionResult> Index(int bookingId)
    {
        var user = await userManager.GetUserAsync(User);
        var checkIn = await db.GuestCheckIns.Include(c => c.CheckInProcess)
            .Include(c => c.Documents).Include(c => c.Booking).ThenInclude(b => b!.Property)
            .FirstOrDefaultAsync(c => c.BookingId == bookingId && c.Booking!.GuestId == user!.Id);
        if (user is null || checkIn is null) return NotFound();
        if (checkIn.Booking!.Status is not (BookingStatus.Approved or BookingStatus.CheckedIn)) return Forbid();
        return View(checkIn);
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int bookingId, string documentType, IFormFile file)
    {
        var user = await userManager.GetUserAsync(User);
        var checkIn = await db.GuestCheckIns.Include(c => c.Booking).ThenInclude(b => b!.Property)
            .Include(c => c.CheckInProcess)
            .FirstOrDefaultAsync(c => c.BookingId == bookingId && c.Booking!.GuestId == user!.Id);
        if (user is null || checkIn is null || checkIn.Booking!.Status != BookingStatus.Approved)
            return NotFound();
        if (checkIn.RulesAcceptedAt is null)
        {
            TempData["Error"] = "Accept the property check-in rules first.";
            return RedirectToAction(nameof(Index), new { bookingId });
        }
        var requirement = checkIn.CheckInProcess?.DocumentRequirement ?? DocumentRequirement.IdOrPassport;
        if (!CheckInRequirements.Allows(requirement, documentType))
        {
            TempData["Error"] = $"Choose a document required by the Host: {CheckInRequirements.Describe(requirement)}.";
            return RedirectToAction(nameof(Index), new { bookingId });
        }
        if (file is null || file.Length == 0 || file.Length > 10 * 1024 * 1024 ||
            !new[] { ".pdf", ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
        {
            TempData["Error"] = "Upload a PDF, JPG, or PNG document up to 10 MB.";
            return RedirectToAction(nameof(Index), new { bookingId });
        }

        var folder = Path.Combine(environment.ContentRootPath, "App_Data", "private-documents");
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using (var stream = System.IO.File.Create(Path.Combine(folder, fileName)))
            await file.CopyToAsync(stream);

        db.GuestDocuments.Add(new GuestDocument
        {
            GuestCheckInId = checkIn.Id,
            DocumentType = documentType,
            FileName = fileName
        });
        checkIn.Status = CheckInStatus.DocumentsSubmitted;
        await db.SaveChangesAsync();
        await audit.RecordAsync(user.Id, $"Uploaded guest check-in document for booking #{bookingId}", ActivityType.Security);
        await notifications.CreateAsync(checkIn.Booking!.Property!.HostId, "Check-in document awaiting verification",
            $"A {documentType} was uploaded for booking #{bookingId}.", NotificationType.Booking);
        TempData["Message"] = "Document uploaded securely for Host verification.";
        return RedirectToAction(nameof(Index), new { bookingId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int bookingId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var user = await userManager.GetUserAsync(User);
        var booking = await db.Bookings.Include(b => b.GuestCheckIn).ThenInclude(c => c!.Documents)
            .Include(b => b.GuestCheckIn).ThenInclude(c => c!.CheckInProcess).Include(b => b.Property)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.GuestId == user!.Id);
        if (user is null || booking is null || booking.Status != BookingStatus.Approved) return NotFound();
        if (booking.GuestCheckIn?.RulesAcceptedAt is null || !CheckInRequirements.IsSatisfied(booking.GuestCheckIn))
        {
            TempData["Error"] = "Accept the rules and wait for your Host to verify all required documents before completing check-in.";
            return RedirectToAction(nameof(Index), new { bookingId });
        }
        booking.Status = BookingStatus.CheckedIn;
        booking.GuestCheckIn.Status = CheckInStatus.CheckedIn;
        await db.SaveChangesAsync();
        await audit.RecordAsync(user.Id, $"Booking #{bookingId} checked in", ActivityType.Booking);
        await notifications.CreateAsync(booking.Property!.HostId, "Guest checked in",
            $"The guest completed check-in for booking #{bookingId}.", NotificationType.Booking);
        await transaction.CommitAsync();
        TempData["Message"] = "Check-in complete. Enjoy your stay.";
        return RedirectToAction("Mine", "Bookings");
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var user = await userManager.GetUserAsync(User);
        var document = await db.GuestDocuments.Include(d => d.GuestCheckIn).ThenInclude(c => c!.Booking)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (user is null || document is null || document.GuestCheckIn!.Booking!.GuestId != user.Id)
            return NotFound();
        var path = Path.Combine(environment.ContentRootPath, "App_Data", "private-documents", document.FileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, "application/octet-stream", document.FileName);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptRules(int bookingId, bool accepted)
    {
        var user = await userManager.GetUserAsync(User);
        var checkIn = await db.GuestCheckIns.Include(c => c.Booking)
            .FirstOrDefaultAsync(c => c.BookingId == bookingId && c.Booking!.GuestId == user!.Id);
        if (user is null || checkIn is null || checkIn.Booking!.Status != BookingStatus.Approved) return NotFound();
        if (accepted)
        {
            checkIn.RulesAcceptedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.RecordAsync(user.Id, $"Accepted check-in rules for booking #{bookingId}", ActivityType.Booking);
        }
        else TempData["Error"] = "Tick the checkbox to accept the rules.";
        return RedirectToAction(nameof(Index), new { bookingId });
    }
}
