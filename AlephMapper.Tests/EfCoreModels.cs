using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AlephMapper.Tests;

// EF Core entity models for integration testing
public class Person
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = "";
    
    [Required]
    public string Email { get; set; } = "";
    
    public int? BirthInfoId { get; set; }
    public PersonBirthInfo? BirthInfo { get; set; }
    
    public List<Address> Addresses { get; set; } = new();
    public List<Order> Orders { get; set; } = new();
}

public class PersonBirthInfo
{
    public int Id { get; set; }
    
    public int Age { get; set; }
    
    [Required]
    public string BirthPlace { get; set; } = "";
    
    [Required]
    public string Address { get; set; } = "";
    
    public DateTime? BirthDate { get; set; }
    
    // Navigation property
    public List<Person> Persons { get; set; } = new();
}

public class Address
{
    public int Id { get; set; }
    
    [Required]
    public string Street { get; set; } = "";
    
    [Required] 
    public string City { get; set; } = "";
    
    public string? State { get; set; }
    
    [Required]
    public string Country { get; set; } = "";
    
    public string? ZipCode { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Foreign key
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
}

public class Order
{
    public int Id { get; set; }
    
    [Required]
    public string OrderNumber { get; set; } = "";
    
    public decimal Amount { get; set; }
    
    public DateTime OrderDate { get; set; }
    
    public bool IsCompleted { get; set; }
    
    public string? Notes { get; set; }
    
    // Foreign key
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    
    [Required]
    public string ProductName { get; set; } = "";
    
    public int Quantity { get; set; }
    
    public decimal UnitPrice { get; set; }
    
    public decimal TotalPrice => Quantity * UnitPrice;
    
    // Foreign key
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
}

// DbContext for integration tests
public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    public DbSet<Person> Persons { get; set; }
    public DbSet<PersonBirthInfo> PersonBirthInfos { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Person entity
        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(255);
            
            // One-to-one relationship with PersonBirthInfo
            entity.HasOne(e => e.BirthInfo)
                .WithMany(bi => bi.Persons)
                .HasForeignKey(e => e.BirthInfoId)
                .OnDelete(DeleteBehavior.SetNull);
                
            // One-to-many relationship with Address
            entity.HasMany(e => e.Addresses)
                .WithOne(a => a.Person)
                .HasForeignKey(a => a.PersonId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // One-to-many relationship with Order
            entity.HasMany(e => e.Orders)
                .WithOne(o => o.Person)
                .HasForeignKey(o => o.PersonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure PersonBirthInfo entity
        modelBuilder.Entity<PersonBirthInfo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BirthPlace).HasMaxLength(100);
            entity.Property(e => e.Address).HasMaxLength(200);
        });

        // Configure Address entity
        modelBuilder.Entity<Address>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Street).HasMaxLength(200);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.State).HasMaxLength(50);
            entity.Property(e => e.Country).HasMaxLength(50);
            entity.Property(e => e.ZipCode).HasMaxLength(20);
        });

        // Configure Order entity
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNumber).HasMaxLength(50);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Notes).HasMaxLength(500);
            
            // One-to-many relationship with OrderItem
            entity.HasMany(e => e.Items)
                .WithOne(oi => oi.Order)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure OrderItem entity
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProductName).HasMaxLength(100);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Ignore(e => e.TotalPrice); // Computed property
        });

        // Seed some test data
        modelBuilder.Entity<PersonBirthInfo>().HasData(
            new PersonBirthInfo { Id = 1, Age = 30, BirthPlace = "Kyiv", Address = "Kyiv, Ukraine", BirthDate = new DateTime(1993, 5, 15) },
            new PersonBirthInfo { Id = 2, Age = 25, BirthPlace = "Lviv", Address = "Lviv, Ukraine", BirthDate = new DateTime(1998, 8, 22) },
            new PersonBirthInfo { Id = 3, Age = 40, BirthPlace = "New York", Address = "New York, USA", BirthDate = new DateTime(1983, 12, 10) }
        );

        modelBuilder.Entity<Person>().HasData(
            new Person { Id = 1, Name = "John Doe", Email = "john.doe@example.com", BirthInfoId = 1 },
            new Person { Id = 2, Name = "Jane Smith", Email = "jane.smith@example.com", BirthInfoId = 2 },
            new Person { Id = 3, Name = "Bob Johnson", Email = "bob.johnson@example.com", BirthInfoId = 3 },
            new Person { Id = 4, Name = "Alice Brown", Email = "alice.brown@example.com", BirthInfoId = null }
        );
    }
}