using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;

var builder = WebApplication.CreateBuilder(args);
// Settings come from C# defaults and Windows environment variables, not JSON files.
builder.Configuration.Sources.Clear();
builder.Configuration.AddEnvironmentVariables();
builder.WebHost.UseUrls("https://localhost:7270", "http://localhost:5159");
// This source-only package has no static files initially. Create its upload root.
var publicRoot = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(publicRoot);
builder.Environment.WebRootPath = publicRoot;
builder.Environment.WebRootFileProvider =
    new Microsoft.Extensions.FileProviders.PhysicalFileProvider(publicRoot);

var connectionString = builder.Configuration.GetConnectionString("Staybnb")
    ?? @"Server=(localdb)\MSSQLLocalDB;Database=StaybnbDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

builder.Services.AddDbContext<StaybnbDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<StaybnbDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
});
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);

builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// InitialCreate migration is included with the source. Apply migrations and roles at startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StaybnbDbContext>();
    if (app.Environment.IsEnvironment("Testing"))
        await db.Database.EnsureCreatedAsync();
    else
        await db.Database.MigrateAsync();
}
await RoleSeeder.SeedAsync(app.Services, app.Configuration);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
