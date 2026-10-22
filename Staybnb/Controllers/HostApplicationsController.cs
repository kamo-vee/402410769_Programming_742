using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Controllers;

[Authorize(Roles = StaybnbRoles.Guest + "," + StaybnbRoles.Host)]
public class HostApplicationsController(
    StaybnbDbContext db,
    IWebHostEnvironment environment,
    AuditService audit,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet, Authorize(Roles = StaybnbRoles.Guest)]
    public IActionResult Create() => View(new HostApplicationViewModel());

    [HttpPost, Authorize(Roles = StaybnbRoles.Guest), ValidateAntiForgeryToken, RequestSizeLimit(25 * 1024 * 1024)]
    public Task<IActionResult> Create(HostApplicationViewModel model) => SubmitAsync(model);

    [HttpGet, Authorize(Roles = StaybnbRoles.Host)]
    public IActionResult CreateAdditional() => View("Create", new HostApplicationViewModel());

    [HttpPost, Authorize(Roles = StaybnbRoles.Host), ValidateAntiForgeryToken, RequestSizeLimit(25 * 1024 * 1024)]
    public Task<IActionResult> CreateAdditional(HostApplicationViewModel model) => SubmitAsync(model);

    private async Task<IActionResult> SubmitAsync(HostApplicationViewModel model)
    {
        if (model.Images is null || model.Images.Count == 0)
            ModelState.AddModelError(nameof(model.Images), "Upload at least one property image before submitting.");
        if (!ModelState.IsValid) return View("Create", model);

        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        if (await db.HostApplications.AnyAsync(a => a.ApplicationUserId == user.Id && a.Status == ApplicationStatus.Pending))
        {
            ModelState.AddModelError("", "You already have an application awaiting review.");
            return View("Create", model);
        }
        foreach (var image in model.Images!)
        {
            if (image.Length == 0 || image.Length > 5 * 1024 * 1024 ||
                !new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(Path.GetExtension(image.FileName).ToLowerInvariant()))
                ModelState.AddModelError(nameof(model.Images), "Images must be JPG, PNG, or WEBP and no larger than 5 MB each.");
        }
        if (model.Images.Count > 8) ModelState.AddModelError(nameof(model.Images), "Upload no more than eight images.");
        if (model.Images.Sum(i => i.Length) > 20 * 1024 * 1024)
            ModelState.AddModelError(nameof(model.Images), "The combined size of property images must not exceed 20 MB.");
        if (!ModelState.IsValid) return View("Create", model);
        var property = new HostProperty
        {
            Title = model.Title.Trim(),
            Description = model.Description.Trim(),
            PricePerNight = model.PricePerNight,
            Address = model.Address.Trim(),
            City = model.City.Trim(),
            PropertyType = model.PropertyType,
            MaxGuests = model.MaxGuests,
            HostId = user.Id,
            IsActive = false
        };

        var imageDirectory = Path.Combine(environment.WebRootPath, "uploads", "properties");
        Directory.CreateDirectory(imageDirectory);
        foreach (var image in model.Images)
        {
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            await using var stream = System.IO.File.Create(Path.Combine(imageDirectory, fileName));
            await image.CopyToAsync(stream);
            property.Images.Add(new PropertyImage { ImageUrl = $"/uploads/properties/{fileName}" });
        }
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        db.HostApplications.Add(new HostApplication { ApplicationUserId = user.Id, PropertyId = property.Id });
        await db.SaveChangesAsync();
        await audit.RecordAsync(user.Id, $"Submitted host application for property #{property.Id}", ActivityType.Application);
        TempData["Message"] = "Your host application has been submitted for review.";
        return RedirectToAction("Index", User.IsInRole(StaybnbRoles.Host) ? "Host" : "Home");
    }

    public async Task<IActionResult> Status()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        return View(await db.HostApplications.Include(a => a.Property).Where(a => a.ApplicationUserId == user.Id)
            .OrderByDescending(a => a.AppliedAt).ToListAsync());
    }
}
