using Microsoft.EntityFrameworkCore;

namespace AlephMapper.ComprehensiveTests;

public class ComprehensiveTestDbContext(DbContextOptions<ComprehensiveTestDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<EmployeeProfile> EmployeeProfiles { get; set; }
    public DbSet<ContactInfo> ContactInfos { get; set; }
    public DbSet<EmployeeAddress> EmployeeAddresses { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<EmployeeProject> EmployeeProjects { get; set; }
    public DbSet<Timesheet> Timesheets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Employee entity
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Salary).HasPrecision(18, 2);

            // Self-referencing relationship (Manager)
            entity.HasOne(e => e.Manager)
                .WithMany(e => e.Subordinates)
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Department relationship
            entity.HasOne(e => e.Department)
                .WithMany(d => d.Employees)
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);

            // One-to-one relationship with EmployeeProfile
            entity.HasOne(e => e.Profile)
                .WithOne(p => p.Employee)
                .HasForeignKey<EmployeeProfile>(p => p.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            // One-to-many relationships
            entity.HasMany(e => e.Addresses)
                .WithOne(a => a.Employee)
                .HasForeignKey(a => a.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Timesheets)
                .WithOne(t => t.Employee)
                .HasForeignKey(t => t.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.EmployeeProjects)
                .WithOne(ep => ep.Employee)
                .HasForeignKey(ep => ep.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Department entity
        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Budget).HasPrecision(18, 2);

            // Manager relationship
            entity.HasOne(d => d.Manager)
                .WithMany()
                .HasForeignKey(d => d.ManagerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure EmployeeProfile entity
        modelBuilder.Entity<EmployeeProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Bio).HasMaxLength(1000);
            entity.Property(e => e.Skills).HasMaxLength(500);
            entity.Property(e => e.ProfilePictureUrl).HasMaxLength(500);

            // One-to-one relationship with ContactInfo
            entity.HasOne(p => p.ContactInfo)
                .WithOne(c => c.EmployeeProfile)
                .HasForeignKey<ContactInfo>(c => c.EmployeeProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure ContactInfo entity
        modelBuilder.Entity<ContactInfo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmergencyContactName).HasMaxLength(100);
            entity.Property(e => e.EmergencyContactPhone).HasMaxLength(20);
            entity.Property(e => e.LinkedInUrl).HasMaxLength(500);
            entity.Property(e => e.GitHubUrl).HasMaxLength(500);
        });

        // Configure EmployeeAddress entity
        modelBuilder.Entity<EmployeeAddress>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Street).HasMaxLength(200);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.State).HasMaxLength(50);
            entity.Property(e => e.Country).HasMaxLength(50);
            entity.Property(e => e.ZipCode).HasMaxLength(20);
        });

        // Configure Project entity
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Budget).HasPrecision(18, 2);

            // Many-to-many relationship with Employee through EmployeeProject
            entity.HasMany(p => p.Employees)
                .WithMany(e => e.Projects)
                .UsingEntity<EmployeeProject>(
                    l => l.HasOne<Employee>(ep => ep.Employee).WithMany(e => e.EmployeeProjects),
                    r => r.HasOne<Project>(ep => ep.Project).WithMany(p => p.EmployeeProjects),
                    j => j.HasKey(ep => ep.Id));

            entity.HasMany(p => p.Timesheets)
                .WithOne(t => t.Project)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure EmployeeProject entity
        modelBuilder.Entity<EmployeeProject>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.HourlyRate).HasPrecision(18, 2);
        });

        // Configure Timesheet entity
        modelBuilder.Entity<Timesheet>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.HoursWorked).HasPrecision(5, 2);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // Seed test data
        SeedTestData(modelBuilder);
    }

    private void SeedTestData(ModelBuilder modelBuilder)
    {
        // Seed departments
        modelBuilder.Entity<Department>().HasData(
            new Department { Id = 1, Name = "Engineering", Description = "Software Development", Budget = 1000000m, IsActive = true },
            new Department { Id = 2, Name = "Marketing", Description = "Product Marketing", Budget = 500000m, IsActive = true },
            new Department { Id = 3, Name = "Sales", Description = "Customer Sales", Budget = 750000m, IsActive = true },
            new Department { Id = 4, Name = "Human Resources", Description = "People Operations", Budget = 300000m, IsActive = true },
            new Department { Id = 5, Name = "Finance", Description = "Financial Operations", Budget = 400000m, IsActive = false }
        );

        // Seed employees
        modelBuilder.Entity<Employee>().HasData(
            new Employee { Id = 1, FirstName = "John", LastName = "Doe", Email = "john.doe@company.com", BirthDate = new DateTime(1990, 5, 15), Salary = 85000m, IsActive = true, DepartmentId = 1 },
            new Employee { Id = 2, FirstName = "Jane", LastName = "Smith", Email = "jane.smith@company.com", BirthDate = new DateTime(1985, 8, 22), Salary = 95000m, IsActive = true, DepartmentId = 1, ManagerId = 1 },
            new Employee { Id = 3, FirstName = "Bob", LastName = "Johnson", Email = "bob.johnson@company.com", BirthDate = new DateTime(1992, 12, 10), Salary = 75000m, IsActive = true, DepartmentId = 2 },
            new Employee { Id = 4, FirstName = "Alice", LastName = "Brown", Email = "alice.brown@company.com", BirthDate = null, Salary = null, IsActive = false, DepartmentId = null },
            new Employee { Id = 5, FirstName = "Charlie", LastName = "Wilson", Email = "charlie.wilson@company.com", BirthDate = new DateTime(1988, 3, 5), Salary = 110000m, IsActive = true, DepartmentId = 3 },
            new Employee { Id = 6, FirstName = "Diana", LastName = "Davis", Email = "diana.davis@company.com", BirthDate = new DateTime(1995, 7, 18), Salary = 65000m, IsActive = true, DepartmentId = 4 }
        );

        // Seed employee profiles
        modelBuilder.Entity<EmployeeProfile>().HasData(
            new EmployeeProfile { Id = 1, EmployeeId = 1, Phone = "+1-555-0101", Bio = "Senior Software Engineer with 8 years experience", Skills = "C#, .NET, SQL Server, Azure", YearsOfExperience = 8 },
            new EmployeeProfile { Id = 2, EmployeeId = 2, Phone = "+1-555-0102", Bio = "Technical Lead and Architect", Skills = "C#, .NET, Architecture, Leadership", YearsOfExperience = 12 },
            new EmployeeProfile { Id = 3, EmployeeId = 3, Phone = "+1-555-0103", Bio = "Marketing Specialist", Skills = "Digital Marketing, Analytics, SEO", YearsOfExperience = 5 },
            new EmployeeProfile { Id = 5, EmployeeId = 5, Phone = "+1-555-0105", Bio = "Sales Manager", Skills = "Sales, CRM, Negotiation", YearsOfExperience = 10 }
        );

        // Seed contact info
        modelBuilder.Entity<ContactInfo>().HasData(
            new ContactInfo { Id = 1, EmployeeProfileId = 1, EmergencyContactName = "Mary Doe", EmergencyContactPhone = "+1-555-0201", LinkedInUrl = "https://linkedin.com/in/johndoe" },
            new ContactInfo { Id = 2, EmployeeProfileId = 2, EmergencyContactName = "Jim Smith", EmergencyContactPhone = "+1-555-0202", LinkedInUrl = "https://linkedin.com/in/janesmith", GitHubUrl = "https://github.com/janesmith" },
            new ContactInfo { Id = 3, EmployeeProfileId = 3, EmergencyContactName = "Sarah Johnson", EmergencyContactPhone = "+1-555-0203" }
        );

        // Seed projects
        modelBuilder.Entity<Project>().HasData(
            new Project { Id = 1, Name = "E-commerce Platform", Description = "New online shopping platform", StartDate = new DateTime(2024, 1, 1), Budget = 500000m, Status = ProjectStatus.InProgress },
            new Project { Id = 2, Name = "Mobile App", Description = "iOS and Android mobile application", StartDate = new DateTime(2024, 2, 15), EndDate = new DateTime(2024, 8, 15), Budget = 300000m, Status = ProjectStatus.Completed },
            new Project { Id = 3, Name = "Data Analytics", Description = "Business intelligence dashboard", StartDate = new DateTime(2024, 3, 1), Budget = 200000m, Status = ProjectStatus.Planning },
            new Project { Id = 4, Name = "CRM System", Description = "Customer relationship management system", StartDate = new DateTime(2024, 4, 1), Budget = 400000m, Status = ProjectStatus.OnHold }
        );

        // Note: EmployeeProject, EmployeeAddress, and Timesheet data will be seeded in the test setup
        // to allow for more dynamic scenarios and avoid foreign key constraint issues during model creation
    }
}