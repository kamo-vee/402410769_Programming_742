using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;

namespace Staybnb.Controllers;

public class PropertiesController(StaybnbDbContext db) : Controller
{
    public async Task<IActionResult> Details(int id)
    {
        var property = await db.Properties
            .Include(p => p.Images)
            .Include(p => p.PropertyAmenities).ThenInclude(pa => pa.Amenity)
            .Include(p => p.Reviews).ThenInclude(r => r.Reviewer)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (property is null) return NotFound();
        return View(property);
    }
}
