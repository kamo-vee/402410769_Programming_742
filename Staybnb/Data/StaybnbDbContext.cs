using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;

namespace Staybnb.Data;

public class StaybnbDbContext(DbContextOptions<StaybnbDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<HostProperty> Properties => Set<HostProperty>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<PropertyAmenity> PropertyAmenities => Set<PropertyAmenity>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<HostApplication> HostApplications => Set<HostApplication>();
    public DbSet<CheckInProcess> CheckInProcesses => Set<CheckInProcess>();
    public DbSet<GuestCheckIn> GuestCheckIns => Set<GuestCheckIn>();
    public DbSet<GuestDocument> GuestDocuments => Set<GuestDocument>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PropertyAmenity>().HasKey(x => new { x.PropertyId, x.AmenityId });
        builder.Entity<PropertyAmenity>().HasOne(x => x.Property).WithMany(x => x.PropertyAmenities)
            .HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<PropertyAmenity>().HasOne(x => x.Amenity).WithMany(x => x.PropertyAmenities)
            .HasForeignKey(x => x.AmenityId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Booking>().HasOne(x => x.Guest).WithMany()
            .HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<HostProperty>().HasOne(x => x.Host).WithMany()
            .HasForeignKey(x => x.HostId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Review>().HasOne(x => x.Reviewer).WithMany()
            .HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<WishlistItem>().HasIndex(x => new { x.UserId, x.PropertyId }).IsUnique();
        builder.Entity<GuestCheckIn>().HasIndex(x => x.BookingId).IsUnique();
        builder.Entity<Payment>().HasIndex(x => x.BookingId).IsUnique();
        builder.Entity<CheckInProcess>().HasOne(x => x.Property).WithOne(x => x.CheckInProcess)
            .HasForeignKey<CheckInProcess>(x => x.PropertyId);
        builder.Entity<GuestCheckIn>().HasOne(x => x.CheckInProcess).WithMany()
            .HasForeignKey(x => x.CheckInProcessId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Message>().HasOne(x => x.Sender).WithMany()
            .HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Message>().HasOne(x => x.Receiver).WithMany()
            .HasForeignKey(x => x.ReceiverId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ActivityLog>().HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
