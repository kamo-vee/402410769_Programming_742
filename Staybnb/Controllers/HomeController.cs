using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;

namespace Staybnb.Controllers;

public class HomeController(StaybnbDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? city, string? propertyType)
    {
        var properties = db.Properties.Where(p => p.IsActive).Include(p => p.Images).AsQueryable();
        if (!string.IsNullOrWhiteSpace(city)) properties = properties.Where(p => p.City.Contains(city));
        if (!string.IsNullOrWhiteSpace(propertyType)) properties = properties.Where(p => p.PropertyType == propertyType);
        ViewBag.City = city;
        ViewBag.PropertyType = propertyType;
        ViewBag.Cities = await db.Properties.Where(p => p.IsActive).Select(p => p.City).Distinct().OrderBy(c => c).ToListAsync();
        ViewBag.PropertyTypes = await db.Properties.Where(p => p.IsActive).Select(p => p.PropertyType).Distinct().OrderBy(t => t).ToListAsync();
        return View(await properties.OrderByDescending(p => p.CreatedAt).ToListAsync());
    }

    public IActionResult Error() => View();
}
