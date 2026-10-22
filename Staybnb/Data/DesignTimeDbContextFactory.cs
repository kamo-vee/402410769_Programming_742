

// Lets EF tooling generate migrations without starting the web app or opening a database connection.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<StaybnbDbContext>
{
    public StaybnbDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StaybnbDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=StaybnbDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
            .Options;
        return new StaybnbDbContext(options);
    }
}
