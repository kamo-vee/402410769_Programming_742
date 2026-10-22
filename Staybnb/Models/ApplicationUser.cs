using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(80)]
    public string FirstName { get; set; } = "";

    [Required, StringLength(80)]
    public string LastName { get; set; } = "";

    [StringLength(300)]
    public string? ProfilePictureUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
