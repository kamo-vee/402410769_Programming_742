using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Controllers;

[Authorize(Roles = StaybnbRoles.Host)]
public class HostController(
    StaybnbDbContext db,
    UserManager<ApplicationUser> userManager,
    AuditService audit,
    NotificationService notifications,
    IWebHostEnvironment environment) : Controller
{
    public async Task<IActionResult> Index()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var properties = await db.Properties.Where(p => p.HostId == user.Id).Include(p => p.Images).ToListAsync();
        ViewBag.PendingBookings = await db.Bookings.Include(b => b.Property)
            .Where(b => b.Property!.HostId == user.Id && b.Status == BookingStatus.Pending).CountAsync();
        ViewBag.ApprovedIds = await db.HostApplications.Where(a => a.ApplicationUserId == user.Id && a.Status == ApplicationStatus.Approved)
            .Select(a => a.PropertyId).ToListAsync();
        return View(properties);
    }

    public async Task<IActionResult> BookingRequests()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var bookings = await db.Bookings.Include(b => b.Property).Include(b => b.Guest)
            .Include(b => b.GuestCheckIn).ThenInclude(c => c!.Documents)
            .Include(b => b.GuestCheckIn).ThenInclude(c => c!.CheckInProcess)
            .Where(b => b.Property!.HostId == user.Id)
            .OrderByDescending(b => b.CreatedAt).ToListAsync();
        return View(bookings);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BookingDecision(int id, bool approve)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var user = await userManager.GetUserAsync(User);
        var booking = await db.Bookings.Include(b => b.Property)
            .FirstOrDefaultAsync(b => b.Id == id && b.Property!.HostId == user!.Id);
        if (user is null || booking is null || booking.Status != BookingStatus.Pending) return NotFound();
        if (approve)
        {
            var overlaps = await db.Bookings.AnyAsync(b => b.PropertyId == booking.PropertyId && b.Id != booking.Id &&
                (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn) &&
                booking.CheckInDate < b.CheckOutDate && booking.CheckOutDate > b.CheckInDate);
            if (overlaps)
            {
                TempData["Error"] = "Those dates are no longer available because another booking was approved.";
                return RedirectToAction(nameof(BookingRequests));
            }
            booking.Status = BookingStatus.Approved;
            var process = await db.CheckInProcesses.Where(c => c.PropertyId == booking.PropertyId).OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync();
            if (process is null)
            {
                process = new CheckInProcess { PropertyId = booking.PropertyId, StepsJson = "[]", RequiredDocuments = "Government-issued ID" };
                db.CheckInProcesses.Add(process);
                await db.SaveChangesAsync();
            }
            if (!await db.GuestCheckIns.AnyAsync(c => c.BookingId == booking.Id))
                db.GuestCheckIns.Add(new GuestCheckIn { BookingId = booking.Id, CheckInProcessId = process.Id });
        }
        else booking.Status = BookingStatus.Rejected;

        await db.SaveChangesAsync();
        await audit.RecordAsync(user.Id, $"Booking #{booking.Id} status changed to {booking.Status}", ActivityType.Booking);
        await notifications.CreateAsync(booking.GuestId,
            approve ? "Booking approved" : "Booking declined",
            $"Your booking for {booking.Property!.Title} is {booking.Status}.",
            NotificationType.Booking);
        await transaction.CommitAsync();
        TempData["Message"] = $"Booking {booking.Status.ToString().ToLowerInvariant()}.";
        return RedirectToAction(nameof(BookingRequests));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleListing(int id)
    {
        var user = await userManager.GetUserAsync(User);
        var property = await db.Properties.FirstOrDefaultAsync(p => p.Id == id && p.HostId == user!.Id);
        if (user is null || property is null) return NotFound();
        if (!await db.HostApplications.AnyAsync(a => a.PropertyId == id && a.Status == ApplicationStatus.Approved))
            return BadRequest("A listing must be approved by an administrator before it can be activated.");
        property.IsActive = !property.IsActive;
        await db.SaveChangesAsync();
        await audit.RecordAsync(user.Id, $"Property #{id} listing toggled {(property.IsActive ? "on" : "off")}");
        TempData["Message"] = property.IsActive ? "Listing is live." : "Listing paused.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CheckInSettings(int propertyId)
    {
        var user = await userManager.GetUserAsync(User);
        var property = await db.Properties.FirstOrDefaultAsync(p => p.Id == propertyId && p.HostId == user!.Id);
        if (user is null || property is null) return NotFound();
        var current = await db.CheckInProcesses.Where(c => c.PropertyId == propertyId)
            .OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync();
        var steps = current is null ? "" : string.Join(Environment.NewLine, JsonSerializer.Deserialize<List<string>>(current.StepsJson) ?? []);
        return View(new CheckInSettingsViewModel
        {
            PropertyId = propertyId,
            Title = current?.Title ?? "Check-in instructions",
            Steps = steps,
            RequiredDocuments = current?.RequiredDocuments ?? "Government-issued ID",
            DocumentRequirement = current?.DocumentRequirement ?? DocumentRequirement.IdOrPassport
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckInSettings(CheckInSettingsViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        var property = await db.Properties.FirstOrDefaultAsync(p => p.Id == model.PropertyId && p.HostId == user!.Id);
        if (user is null || property is null) return NotFound();
        if (!ModelState.IsValid) return View(model);
        var process = await db.CheckInProcesses.FirstOrDefaultAsync(c => c.PropertyId == property.Id);
        if (process is null)
        {
            process = new CheckInProcess { PropertyId = property.Id };
            db.CheckInProcesses.Add(process);
        }
        process.Title = model.Title.Trim();
        process.StepsJson = JsonSerializer.Serialize(model.Steps.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        process.RequiredDocuments = model.RequiredDocuments.Trim();
        process.DocumentRequirement = model.DocumentRequirement;
        // Guests with unfinished bookings must accept changed instructions again.
        var affectedCheckIns = await db.GuestCheckIns.Include(c => c.Booking).Include(c => c.Documents)
            .Where(c => c.CheckInProcessId == process.Id && c.Booking!.Status == BookingStatus.Approved)
            .ToListAsync();
        foreach (var checkIn in affectedCheckIns)
        {
            checkIn.RulesAcceptedAt = null;
            checkIn.Status = CheckInRequirements.IsSatisfied(checkIn)
                ? CheckInStatus.DocumentsVerified
                : checkIn.Documents.Count > 0 ? CheckInStatus.DocumentsSubmitted : CheckInStatus.NotStarted;
        }
        await db.SaveChangesAsync();
        await audit.RecordAsync(user.Id, $"Updated check-in process for property #{property.Id}");
        TempData["Message"] = "Check-in instructions saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewDocument(int documentId, bool approve)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var user = await userManager.GetUserAsync(User);
        var document = await db.GuestDocuments.Include(d => d.GuestCheckIn).ThenInclude(c => c!.Booking)
            .ThenInclude(b => b!.Property)
            .Include(d => d.GuestCheckIn).ThenInclude(c => c!.Documents)
            .Include(d => d.GuestCheckIn).ThenInclude(c => c!.CheckInProcess)
            .FirstOrDefaultAsync(d =>
                d.Id == documentId && d.GuestCheckIn!.Booking!.Property!.HostId == user!.Id);
        if (user is null || document is null) return NotFound();
        if (document.GuestCheckIn!.Booking!.Status != BookingStatus.Approved)
            return BadRequest("Documents can only be reviewed while the booking is approved and check-in is unfinished.");
        document.Status = approve ? DocumentStatus.Verified : DocumentStatus.Rejected;
        document.GuestCheckIn.Status = CheckInRequirements.IsSatisfied(document.GuestCheckIn)
            ? CheckInStatus.DocumentsVerified : CheckInStatus.DocumentsSubmitted;
        await db.SaveChangesAsync();
        await audit.RecordAsync(user.Id, $"Guest document #{documentId} marked {document.Status}", ActivityType.Security);
        await notifications.CreateAsync(document.GuestCheckIn!.Booking!.GuestId,
            approve ? "Check-in document verified" : "Check-in document needs attention",
            $"Your uploaded {document.DocumentType} for booking #{document.GuestCheckIn.BookingId} was marked {document.Status}.",
            NotificationType.Information);
        await transaction.CommitAsync();
        TempData["Message"] = $"Document {document.Status.ToString().ToLowerInvariant()}.";
        return RedirectToAction(nameof(BookingRequests));
    }

    [HttpGet]
    public async Task<IActionResult> Document(int id)
    {
        var user = await userManager.GetUserAsync(User);
        var document = await db.GuestDocuments.Include(d => d.GuestCheckIn).ThenInclude(c => c!.Booking)
            .ThenInclude(b => b!.Property).FirstOrDefaultAsync(d => d.Id == id);
        if (user is null || document is null || document.GuestCheckIn!.Booking!.Property!.HostId != user.Id)
            return NotFound();
        return PrivateFile(document.FileName);
    }

    private IActionResult PrivateFile(string fileName)
    {
        var path = Path.Combine(environment.ContentRootPath, "App_Data", "private-documents", fileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, "application/octet-stream", Path.GetFileName(path));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await userManager.GetUserAsync(User);
        var property = await db.Properties.FirstOrDefaultAsync(p => p.Id == id && p.HostId == user!.Id);
        if (user is null || property is null) return NotFound();
        return View(new PropertyEditViewModel
        {
            Id = property.Id, Title = property.Title, Description = property.Description,
            PricePerNight = property.PricePerNight, CleaningFee = property.CleaningFee, MaxGuests = property.MaxGuests
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PropertyEditViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        var property = await db.Properties.FirstOrDefaultAsync(p => p.Id == model.Id && p.HostId == user!.Id);
        if (user is null || property is null) return NotFound();
        if (!ModelState.IsValid) return View(model);
        property.Title = model.Title.Trim();
        property.Description = model.Description.Trim();
        property.PricePerNight = model.PricePerNight;
        property.CleaningFee = model.CleaningFee;
        property.MaxGuests = model.MaxGuests;
        await db.SaveChangesAsync();
        await audit.RecordAsync(user.Id, $"Updated property #{property.Id}");
        TempData["Message"] = "Property saved.";
        return RedirectToAction(nameof(Index));
    }
}
