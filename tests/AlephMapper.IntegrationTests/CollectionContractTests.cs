using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AlephMapper.IntegrationTests;

public class CollectionContractTests
{
    [Test]
    public async Task Skip_keeps_existing_collection_and_unmapped_state()
    {
        var oldChild = new CollectionContractChild { Value = 1 };
        var oldList = new List<CollectionContractChild> { oldChild };
        var destination = new CollectionContractDestination
        {
            Name = "old", Unmapped = "keep", Children = oldList
        };
        var source = new CollectionContractSource
        {
            Name = "new", Children = [new CollectionContractChild { Value = 2 }]
        };

        var result = CollectionContractMapper.Skip(source, destination);

        await Assert.That(result).IsSameReferenceAs(destination);
        await Assert.That(destination.Name).IsEqualTo("new");
        await Assert.That(destination.Unmapped).IsEqualTo("keep");
        await Assert.That(destination.Children).IsSameReferenceAs(oldList);
        await Assert.That(destination.Children!.Single()).IsSameReferenceAs(oldChild);
    }

    [Test]
    public async Task Update_assigns_the_rhs_collection_without_merging()
    {
        var sourceList = new List<CollectionContractChild> { new() { Value = 7 } };
        var source = new CollectionContractSource { Name = "new", Children = sourceList };
        var destination = new CollectionContractDestination
        {
            Unmapped = "keep", Children = [new CollectionContractChild { Value = 1 }]
        };

        CollectionContractMapper.ReplaceDirect(source, destination);

        await Assert.That(destination.Children).IsSameReferenceAs(sourceList);
        await Assert.That(destination.Children!.Single().Value).IsEqualTo(7);
        await Assert.That(destination.Unmapped).IsEqualTo("keep");
    }

    [Test]
    public async Task Update_mapped_children_replaces_list_and_child_instances()
    {
        var child = new CollectionContractChild { Value = 9 };
        var source = new CollectionContractSource { Children = [child] };
        var oldList = new List<CollectionContractChild> { new() { Value = 1 } };
        var destination = new CollectionContractDestination { Children = oldList };

        CollectionContractMapper.ReplaceMapped(source, destination);

        await Assert.That(ReferenceEquals(destination.Children, oldList)).IsFalse();
        await Assert.That(destination.Children!.Single().Value).IsEqualTo(9);
        await Assert.That(ReferenceEquals(destination.Children.Single(), child)).IsFalse();
    }

    [Test]
    public async Task Empty_and_null_source_collections_follow_the_initializer()
    {
        var destination = new CollectionContractDestination
        {
            Children = [new CollectionContractChild { Value = 1 }]
        };

        CollectionContractMapper.ReplaceMapped(new CollectionContractSource { Children = [] }, destination);
        await Assert.That(destination.Children).IsNotNull();
        await Assert.That(destination.Children!.Count).IsEqualTo(0);

        CollectionContractMapper.ReplaceMapped(new CollectionContractSource { Children = null }, destination);
        await Assert.That(destination.Children).IsNull();
        await Assert.That(CollectionContractMapper.ReplaceMapped(null!, destination)).IsSameReferenceAs(destination);
    }

    [Test]
    public async Task Replacing_tracked_relationship_removes_old_child_and_inserts_new_child()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ComprehensiveTestDbContext>()
            .UseSqlite(connection).Options;

        await using (var setup = new ComprehensiveTestDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var employee = await setup.Employees.SingleAsync(item => item.Id == 1);
            employee.Addresses.Add(new EmployeeAddress
            {
                Street = "Old", City = "Seattle", Country = "USA"
            });
            await setup.SaveChangesAsync();
        }

        await using (var context = new ComprehensiveTestDbContext(options))
        {
            var employee = await context.Employees.Include(item => item.Addresses)
                .SingleAsync(item => item.Id == 1);
            var oldAddress = employee.Addresses.Single();
            var oldList = employee.Addresses;
            var source = new Employee
            {
                Addresses = [new EmployeeAddress { Street = "New", City = "Oslo", Country = "Norway" }]
            };

            TrackedCollectionContractMapper.ReplaceAddresses(source, employee);
            context.ChangeTracker.DetectChanges();

            await Assert.That(ReferenceEquals(employee.Addresses, oldList)).IsFalse();
            await Assert.That(employee.Addresses.Single().Street).IsEqualTo("New");
            await Assert.That(context.Entry(oldAddress).State).IsEqualTo(EntityState.Deleted);
            await Assert.That(context.Entry(employee.Addresses.Single()).State).IsEqualTo(EntityState.Added);
            await context.SaveChangesAsync();
        }

        await using (var verify = new ComprehensiveTestDbContext(options))
        {
            var reloaded = await verify.Employees.Include(item => item.Addresses)
                .SingleAsync(item => item.Id == 1);
            await Assert.That(reloaded.Addresses.Count).IsEqualTo(1);
            await Assert.That(reloaded.Addresses.Single().Street).IsEqualTo("New");
        }
    }
}
