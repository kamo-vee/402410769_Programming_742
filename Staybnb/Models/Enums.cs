namespace Staybnb.Models;

public enum ApplicationStatus { Pending, Approved, Rejected }
public enum BookingStatus { Pending, Approved, Rejected, CheckedIn, Completed, Cancelled }
public enum PaymentStatus { Pending, Paid, Refunded }
public enum DocumentStatus { Pending, Verified, Rejected }
public enum CheckInStatus { NotStarted, DocumentsSubmitted, CheckedIn, DocumentsVerified }
public enum DocumentRequirement { IdOrPassport, IdAndPassport, IdOnly, PassportOnly }
public enum ActivityType { Information, Security, Booking, RoleChange, Application }
public enum NotificationType { Information, Booking, Message, Application }
