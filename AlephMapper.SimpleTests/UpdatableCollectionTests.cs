namespace AlephMapper.SimpleTests;

public class UpdatableCollectionTests
{
    [Test]
    public async Task Updatable_Should_Skip_Collection_Properties()
    {
        // Arrange
        var source = new CollectionSource
        {
            Id = 42,
            Name = "New Name",
            Tags = new List<string> { "new" }
        };

        var target = new CollectionDto
        {
            Id = 1,
            Name = "Old Name",
            Tags = new List<string> { "old" }
        };

        var existingTags = target.Tags;

        // Act: use the generated Updatable overload
        var result = CollectionUpdateMapper.Map(source, target);

        // Assert: target instance is reused
        await Assert.That(result).IsSameReferenceAs(target);

        // Scalar properties update
        await Assert.That(target.Id).IsEqualTo(42);
        await Assert.That(target.Name).IsEqualTo("New Name");

        // Collection property should be skipped (reference preserved)
        await Assert.That(target.Tags).IsSameReferenceAs(existingTags);
        await Assert.That(target.Tags).Contains("old");
        // Optionally confirm it didn't adopt source contents
        await Assert.That(target.Tags).DoesNotContain("new");
    }
}

