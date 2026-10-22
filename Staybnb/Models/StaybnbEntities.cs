using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Staybnb.Models;

public class HostProperty
{
    public int Id { get; set; }
    [Required, StringLength(120)]
    public string Title { get; set; } = "";
    [Required, StringLength(3000)]
    public string Description { get; set; } = "";
    [Range(1, 1000000), Column(TypeName = "decimal(18,2)")]
    public decimal PricePerNight { get; set; }
    [Required]
    public string HostId { get; set; } = "";
    public ApplicationUser? Host { get; set; }
    [Required, StringLength(250)]
    public string Address { get; set; } = "";
    [Required, StringLength(100)]
    public string City { get; set; } = "";
    [Required, StringLength(60)]
    public string PropertyType { get; set; } = "Apartment";
    [Range(1, 20)]
    public int MaxGuests { get; set; } = 2;
    [Column(TypeName = "decimal(18,2)")]
    public decimal CleaningFee { get; set; } = 250m;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
    public ICollection<PropertyAmenity> PropertyAmenities { get; set; } = new List<PropertyAmenity>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<WishlistItem> Wishlists { get; set; } = new List<WishlistItem>();
    public CheckInProcess? CheckInProcess { get; set; }
}

public class PropertyImage
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public HostProperty? Property { get; set; }
    [Required, StringLength(300)]
    public string ImageUrl { get; set; } = "";
}

public class Amenity
{
    public int Id { get; set; }
    [Required, StringLength(100)]
    public string Name { get; set; } = "";
    [StringLength(50)]
    public string IconClass { get; set; } = "";
    public ICollection<PropertyAmenity> PropertyAmenities { get; set; } = new List<PropertyAmenity>();
}

public class PropertyAmenity
{
    public int PropertyId { get; set; }
    public HostProperty? Property { get; set; }
    public int AmenityId { get; set; }
    public Amenity? Amenity { get; set; }
}

public class Booking
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public HostProperty? Property { get; set; }
    [Required]
    public string GuestId { get; set; } = "";
    public ApplicationUser? Guest { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    [Range(1, 20)]
    public int Travelers { get; set; } = 1;
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPrice { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal CleaningFee { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ServiceFee { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Payment? Payment { get; set; }
    public GuestCheckIn? GuestCheckIn { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Review
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public HostProperty? Property { get; set; }
    [Required]
    public string ReviewerId { get; set; } = "";
    public ApplicationUser? Reviewer { get; set; }
    [Range(1, 5)]
    public int Rating { get; set; }
    [StringLength(1000)]
    public string Comment { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WishlistItem
{
    public int Id { get; set; }
    [Required]
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    public int PropertyId { get; set; }
    public HostProperty? Property { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Message
{
    public int Id { get; set; }
    [Required]
    public string SenderId { get; set; } = "";
    public ApplicationUser? Sender { get; set; }
    [Required]
    public string ReceiverId { get; set; } = "";
    public ApplicationUser? Receiver { get; set; }
    [Required, StringLength(2000)]
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}

public class ActivityLog
{
    public int Id { get; set; }
    [Required]
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    [Required, StringLength(300)]
    public string Action { get; set; } = "";
    public ActivityType ActivityType { get; set; } = ActivityType.Information;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public int Id { get; set; }
    [Required]
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    [Required, StringLength(200)]
    public string Title { get; set; } = "";
    [Required, StringLength(1000)]
    public string Content { get; set; } = "";
    public NotificationType Type { get; set; } = NotificationType.Information;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class HostApplication
{
    public int Id { get; set; }
    [Required]
    public string ApplicationUserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    public int PropertyId { get; set; }
    public HostProperty? Property { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewerId { get; set; }
}

public class CheckInProcess
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public HostProperty? Property { get; set; }
    [Required, StringLength(200)]
    public string Title { get; set; } = "Check-in instructions";
    [Required, StringLength(3000)]
    public string StepsJson { get; set; } = "[]";
    [StringLength(1000)]
    public string RequiredDocuments { get; set; } = "Government-issued ID";
    public DocumentRequirement DocumentRequirement { get; set; } = DocumentRequirement.IdOrPassport;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class GuestCheckIn
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int CheckInProcessId { get; set; }
    public CheckInProcess? CheckInProcess { get; set; }
    public CheckInStatus Status { get; set; } = CheckInStatus.NotStarted;
    public DateTime? RulesAcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<GuestDocument> Documents { get; set; } = new List<GuestDocument>();
}

public class GuestDocument
{
    public int Id { get; set; }
    public int GuestCheckInId { get; set; }
    public GuestCheckIn? GuestCheckIn { get; set; }
    [Required, StringLength(80)]
    public string DocumentType { get; set; } = "Government ID";
    [Required, StringLength(300)]
    public string FileName { get; set; } = "";
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RegisterViewModel
{
    [Required, StringLength(80)]
    public string FirstName { get; set; } = "";
    [Required, StringLength(80)]
    public string LastName { get; set; } = "";
    [Required, EmailAddress]
    public string Email { get; set; } = "";
    [Required, DataType(DataType.Password), MinLength(8)]
    public string Password { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = "";
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public class HostApplicationViewModel
{
    [Required, StringLength(120)]
    public string Title { get; set; } = "";
    [Required, StringLength(3000)]
    public string Description { get; set; } = "";
    [Range(1, 1000000)]
    public decimal PricePerNight { get; set; }
    [Required, StringLength(250)]
    public string Address { get; set; } = "";
    [Required, StringLength(100)]
    public string City { get; set; } = "";
    [Required]
    public string PropertyType { get; set; } = "Apartment";
    [Range(1, 20)]
    public int MaxGuests { get; set; } = 2;
    public List<IFormFile> Images { get; set; } = new();
}

public class BookingCreateViewModel
{
    public int PropertyId { get; set; }
    [DataType(DataType.Date), Required]
    public DateTime CheckInDate { get; set; } = DateTime.Today.AddDays(1);
    [DataType(DataType.Date), Required]
    public DateTime CheckOutDate { get; set; } = DateTime.Today.AddDays(2);
    [Range(1, 20)]
    public int Travelers { get; set; } = 1;
}

public class CheckInSettingsViewModel
{
    public int PropertyId { get; set; }
    [Required, StringLength(200)]
    public string Title { get; set; } = "Check-in instructions";
    [Required, StringLength(3000)]
    public string Steps { get; set; } = "";
    [StringLength(1000)]
    public string RequiredDocuments { get; set; } = "Government-issued ID";
    [EnumDataType(typeof(DocumentRequirement))]
    public DocumentRequirement DocumentRequirement { get; set; } = DocumentRequirement.IdOrPassport;
}

public class MessageCreateViewModel
{
    [Required]
    public string ReceiverId { get; set; } = "";
    [Required, StringLength(2000)]
    public string Content { get; set; } = "";
}

public class ComposeViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
    [Required, StringLength(2000)]
    public string Content { get; set; } = "";
}

public class PropertyEditViewModel
{
    public int Id { get; set; }
    [Required, StringLength(120)]
    public string Title { get; set; } = "";
    [Required, StringLength(3000)]
    public string Description { get; set; } = "";
    [Range(1, 1000000)]
    public decimal PricePerNight { get; set; }
    [Range(0, 100000)]
    public decimal CleaningFee { get; set; }
    [Range(1, 20)]
    public int MaxGuests { get; set; }
}
