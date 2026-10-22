using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Controllers;

[Authorize(Roles = StaybnbRoles.Guest)]
public class BookingsController(
    StaybnbDbContext db,
    UserManager<ApplicationUser> userManager,
    AuditService audit,
    NotificationService notifications) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Create(int propertyId)
    {
        var property = await db.Properties.FirstOrDefaultAsync(p => p.Id == propertyId && p.IsActive);
        if (property is null) return NotFound();
        ViewBag.Property = property;
        return View(new BookingCreateViewModel { PropertyId = propertyId, Travelers = 1 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookingCreateViewModel model, string? intent)
    {
        var property = await db.Properties.FirstOrDefaultAsync(p => p.Id == model.PropertyId && p.IsActive);
        if (property is null) return NotFound();
        ViewBag.Property = property;
        var today = DateTime.Today;
        if (model.CheckInDate.Date < today || model.CheckOutDate.Date <= model.CheckInDate.Date)
            ModelState.AddModelError("", "Choose a future check-in date and a check-out date after check-in.");
        if (model.Travelers > property.MaxGuests)
            ModelState.AddModelError(nameof(model.Travelers), $"This property allows up to {property.MaxGuests} guests.");
        var overlaps = await db.Bookings.AnyAsync(b => b.PropertyId == property.Id &&
            (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn) &&
            model.CheckInDate.Date < b.CheckOutDate && model.CheckOutDate.Date > b.CheckInDate);
        if (overlaps) ModelState.AddModelError("", "Those dates have already been booked.");
        if (!ModelState.IsValid) return View(model);
        if (intent == "preview") return View(model);

        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var nights = (model.CheckOutDate.Date - model.CheckInDate.Date).Days;
        var nightlySubtotal = property.PricePerNight * nights;
        var cleaning = property.CleaningFee;
        var service = Math.Round(nightlySubtotal * 0.10m, 2);
        var booking = new Booking
        {
            PropertyId = property.Id,
            GuestId = user.Id,
            CheckInDate = model.CheckInDate.Date,
            CheckOutDate = model.CheckOutDate.Date,
            Travelers = model.Travelers,
            CleaningFee = cleaning,
            ServiceFee = service,
            TotalPrice = nightlySubtotal + cleaning + service,
            Status = BookingStatus.Pending
        };
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        db.Payments.Add(new Payment { BookingId = booking.Id, Amount = booking.TotalPrice, Status = PaymentStatus.Pending });
        await db.SaveChangesAsync();
        await audit.RecordAsync(user.Id, $"Booking #{booking.Id} created for property #{property.Id}", ActivityType.Booking);
        await notifications.CreateAsync(property.HostId, "New booking request", $"Booking #{booking.Id} for {property.Title} is waiting for your decision.", NotificationType.Booking);
        await transaction.CommitAsync();
        TempData["Message"] = "Booking request sent to the Host.";
        return RedirectToAction(nameof(Mine));
    }

    public async Task<IActionResult> Mine()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        return View(await db.Bookings.Include(b => b.Property).ThenInclude(p => p!.Images)
            .Where(b => b.GuestId == user.Id).OrderByDescending(b => b.CreatedAt).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var user = await userManager.GetUserAsync(User);
        var booking = await db.Bookings.Include(b => b.Property).FirstOrDefaultAsync(b => b.Id == id && b.GuestId == user!.Id);
        if (user is null || booking is null || booking.Status is not (BookingStatus.Pending or BookingStatus.Approved))
            return NotFound();
        booking.Status = BookingStatus.Cancelled;
        await db.SaveChangesAsync();
        await audit.RecordAsync(user.Id, $"Booking #{id} cancelled by guest", ActivityType.Booking);
        await notifications.CreateAsync(booking.Property!.HostId, "Booking cancelled", $"The guest cancelled booking #{booking.Id}.", NotificationType.Booking);
        await transaction.CommitAsync();
        TempData["Message"] = "Booking cancelled.";
        return RedirectToAction(nameof(Mine));
    }
}
