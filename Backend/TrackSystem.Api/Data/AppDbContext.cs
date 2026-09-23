// brings in EF Core tools (DbContext, DbContextOptions)
using Microsoft.EntityFrameworkCore;

// Lets this file see the Employee class from the Models folder
using TrackSystem.Api.Models;

// the "address" of this class, so Program.cs can find it with: using TrackSystem.Api.Data;
namespace TrackSystem.Api.Data;

// our bridge to the DB. ": DbContext" means it inherits all of EF Core's DB abilities
public class AppDbContext : DbContext
{
    // input: options (the connection string + "use SQL Server"), set in Program.cs
    // output: a ready AppDbContext connected to FinalProject
    // ": base(options)" passes the options to EF Core's DbContext so it knows where to connect
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // dbo.Employees table -> collection of Employee objects we can query
    public DbSet<Employee> Employees => Set<Employee>();
}