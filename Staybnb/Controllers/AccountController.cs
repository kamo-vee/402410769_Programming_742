using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Controllers;

public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    AuditService audit) : Controller
{
    [AllowAnonymous]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home") : View();

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim()
        };
        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View(model);
        }

        var roleResult = await userManager.AddToRoleAsync(user, StaybnbRoles.Guest);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            foreach (var error in roleResult.Errors) ModelState.AddModelError("", error.Description);
            return View(model);
        }

        await audit.RecordAsync(user.Id, "Registered account and assigned Guest role", ActivityType.RoleChange);
        await signInManager.SignInAsync(user, isPersistent: false);
        await audit.RecordAsync(user.Id, "Successful login after registration", ActivityType.Security);
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home") : View();
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(model);
        var result = await signInManager.PasswordSignInAsync(model.Email.Trim(), model.Password, false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            var user = await userManager.FindByEmailAsync(model.Email.Trim());
            if (user is not null) await audit.RecordAsync(user.Id, "Successful login", ActivityType.Security);
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }
        var failedUser = await userManager.FindByEmailAsync(model.Email.Trim());
        if (failedUser is not null) await audit.RecordAsync(failedUser.Id, "Unsuccessful login attempt", ActivityType.Security);
        ModelState.AddModelError("", result.IsLockedOut ? "This account is temporarily locked. Try again later." : "Invalid email or password.");
        return View(model);
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
