using Staybnb.Models;

namespace Staybnb.Services;

public static class CheckInRequirements
{
    public static string Describe(DocumentRequirement requirement) => requirement switch
    {
        DocumentRequirement.IdAndPassport => "Both ID and Passport",
        DocumentRequirement.IdOnly => "ID",
        DocumentRequirement.PassportOnly => "Passport",
        _ => "ID or Passport"
    };

    public static bool Allows(DocumentRequirement requirement, string documentType) => requirement switch
    {
        DocumentRequirement.IdOnly => documentType == "ID",
        DocumentRequirement.PassportOnly => documentType == "Passport",
        _ => documentType is "ID" or "Passport"
    };

    public static bool IsSatisfied(GuestCheckIn checkIn)
    {
        var verified = checkIn.Documents.Where(d => d.Status == DocumentStatus.Verified).ToList();
        var id = verified.Any(d => d.DocumentType == "ID");
        var passport = verified.Any(d => d.DocumentType == "Passport");
        return (checkIn.CheckInProcess?.DocumentRequirement ?? DocumentRequirement.IdOrPassport) switch
        {
            DocumentRequirement.IdAndPassport => id && passport,
            DocumentRequirement.IdOnly => id,
            DocumentRequirement.PassportOnly => passport,
            _ => id || passport
        };
    }
}
