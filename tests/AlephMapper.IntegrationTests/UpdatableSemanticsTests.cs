namespace AlephMapper.IntegrationTests;

public class UpdatableSemanticsTests
{
    private static UpdSource CreateSource(bool isActive = true) => new()
    {
        Name = "Ada",
        Street = "Main",
        Lat = 1.5,
        X = 3,
        Y = 4,
        Price = 1234.5m,
        IsActive = isActive,
        Tags = ["a", "b"]
    };

    [Test]
    public async Task Inherited_and_repeated_nested_members_are_created_when_missing()
    {
        var target = new UpdTarget();

        UpdatableSemanticsMapper.Map(CreateSource(), target);

        await Assert.That(target.Billing!.Geo!.Lat).IsEqualTo(1.5);
        await Assert.That(target.Shipping!.Geo!.Lat).IsEqualTo(1.5);
        await Assert.That(target.Inherited!.Street).IsEqualTo("Main");
        await Assert.That(target.Inherited.Geo!.Lat).IsEqualTo(1.5);
    }

    [Test]
    public async Task Existing_nested_instances_are_updated_in_place()
    {
        var shippingGeo = new UpdGeo { Lat = 9 };
        var inherited = new UpdAddress { Street = "Old" };
        var target = new UpdTarget { Shipping = new UpdAddress { Geo = shippingGeo }, Inherited = inherited };

        UpdatableSemanticsMapper.Map(CreateSource(), target);

        await Assert.That(target.Shipping.Geo).IsSameReferenceAs(shippingGeo);
        await Assert.That(shippingGeo.Lat).IsEqualTo(1.5);
        await Assert.That(target.Inherited).IsSameReferenceAs(inherited);
        await Assert.That(inherited.Street).IsEqualTo("Main");
    }

    [Test]
    public async Task Collections_are_skipped_by_default_including_inherited_ones()
    {
        var tags = new List<string> { "keep" };
        var baseTags = new List<string> { "keep" };
        var target = new UpdTarget { Tags = tags, BaseTags = baseTags };

        UpdatableSemanticsMapper.Map(CreateSource(), target);

        await Assert.That(target.Tags).IsSameReferenceAs(tags);
        await Assert.That(target.BaseTags).IsSameReferenceAs(baseTags);
        await Assert.That(baseTags).IsEquivalentTo(["keep"]);
    }

    [Test]
    public async Task Value_type_members_get_the_same_value_as_the_mapping()
    {
        var source = CreateSource();
        var mapped = UpdatableSemanticsMapper.Map(source);
        var target = new UpdTarget { Position = new UpdPoint { X = 100, Y = 100 } };

        UpdatableSemanticsMapper.Map(source, target);

        await Assert.That(target.Position).IsEqualTo(mapped.Position);
        await Assert.That(target.OptionalPosition).IsEqualTo(mapped.OptionalPosition);
        await Assert.That(target.OptionalPosition!.Value.X).IsEqualTo(4);

        UpdatableSemanticsMapper.Map(CreateSource(isActive: false), target);
        await Assert.That(target.OptionalPosition).IsNull();
    }

    [Test]
    public async Task Interpolated_strings_keep_format_alignment_and_escapes()
    {
        var source = CreateSource();
        var target = new UpdTarget();

        UpdatableSemanticsMapper.Map(source, target);

        await Assert.That(target.Display).IsEqualTo(UpdatableSemanticsMapper.Map(source).Display);
        await Assert.That(target.Display).IsEqualTo($"{1234.5m:0.00}|   Ada|{{literal}}");
    }

    [Test]
    public async Task Root_condition_is_respected_and_null_branch_leaves_destination_untouched()
    {
        var target = new UpdTarget { Name = "old" };

        var result = UpdatableSemanticsMapper.OnlyActive(CreateSource(isActive: false), target);

        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Name).IsEqualTo("old");

        UpdatableSemanticsMapper.OnlyActive(CreateSource(isActive: true), target);
        await Assert.That(target.Name).IsEqualTo("Ada");
    }

    [Test]
    public async Task Negated_root_condition_with_default_branch_is_respected()
    {
        var target = new UpdTarget { Name = "old" };

        await Assert.That(UpdatableSemanticsMapper.OnlyInactive(CreateSource(isActive: true), target)).IsSameReferenceAs(target);
        await Assert.That(target.Name).IsEqualTo("old");
        await Assert.That(UpdatableSemanticsMapper.OnlyInactive(null, target)).IsSameReferenceAs(target);

        UpdatableSemanticsMapper.OnlyInactive(CreateSource(isActive: false), target);
        await Assert.That(target.Name).IsEqualTo("Ada");
    }

    [Test]
    public async Task Required_members_are_supported_for_new_and_existing_destinations()
    {
        var created = UpdatableSemanticsMapper.MapRequired(CreateSource(), null!);
        await Assert.That(created.Name).IsEqualTo("Ada");
        await Assert.That(created.Child!.Value).IsEqualTo("Main");

        var child = new UpdRequiredChild { Value = "old" };
        var existing = new UpdRequiredTarget { Name = "old", Child = child };
        UpdatableSemanticsMapper.MapRequired(CreateSource(), existing);

        await Assert.That(existing.Name).IsEqualTo("Ada");
        await Assert.That(existing.Child).IsSameReferenceAs(child);
        await Assert.That(child.Value).IsEqualTo("Main");
    }

    [Test]
    public async Task Nested_objects_with_construction_only_state_are_replaced_with_mapped_values()
    {
        var target = new UpdNestedHolder { InitChild = new UpdInitChild { Value = "old" }, CtorChild = new UpdCtorChild("old") };
        var holderFixed = target.Fixed;

        UpdatableSemanticsMapper.MapNested(CreateSource(), target);

        await Assert.That(target.InitChild!.Value).IsEqualTo("Main");
        await Assert.That(target.CtorChild!.Value).IsEqualTo("Main");
        await Assert.That(target.CtorChild.Count).IsEqualTo(3);
        await Assert.That(target.Fixed).IsSameReferenceAs(holderFixed);
        await Assert.That(target.Fixed.Street).IsEqualTo("Main");
    }

    [Test]
    public async Task Collection_update_policy_still_creates_nested_objects_and_replaces_collections()
    {
        var target = new UpdTarget { Tags = ["old"], BaseTags = ["old"] };

        UpdatableCollectionUpdateMapper.Map(CreateSource(), target);

        await Assert.That(target.Billing!.Geo!.Lat).IsEqualTo(1.5);
        await Assert.That(target.Inherited!.Street).IsEqualTo("Main");
        await Assert.That(target.Tags).IsEquivalentTo(["a", "b"]);
        await Assert.That(target.BaseTags).IsEquivalentTo(["a", "b"]);
    }

    [Test]
    public async Task Verbatim_keyword_member_names_are_kept()
    {
        var target = new UpdKeyword();

        UpdatableReviewMapper.MapKeyword(CreateSource(), target);

        await Assert.That(target.@event).IsEqualTo("Ada");
    }

    [Test]
    public async Task Getter_only_override_still_uses_the_inherited_setter()
    {
        var target = new UpdGetterOverride();

        var result = UpdatableReviewMapper.MapOverride(CreateSource(), target);

        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Name).IsEqualTo("Ada");
    }

    [Test]
    public async Task Derived_creations_produce_the_same_type_as_the_mapping()
    {
        var zoo = new UpdZoo { Pet = new UpdAnimal { Name = "old" } };
        UpdatableReviewMapper.MapZoo(CreateSource(), zoo);

        await Assert.That(zoo.Pet).IsTypeOf<UpdDog>();
        await Assert.That(zoo.Pet!.Name).IsEqualTo("Ada");

        var created = UpdatableReviewMapper.MapAnimal(CreateSource(), null!);
        await Assert.That(created).IsTypeOf<UpdDog>();
        await Assert.That(created.Name).IsEqualTo("Ada");

        var existing = new UpdAnimal();
        await Assert.That(UpdatableReviewMapper.MapAnimal(CreateSource(), existing)).IsSameReferenceAs(existing);
        await Assert.That(existing.Name).IsEqualTo("Ada");
    }

    [Test]
    public async Task Conditional_members_keep_the_natural_type_of_the_conditional()
    {
        var source = CreateSource(isActive: false);
        var target = new UpdZoo { Age = 42 };

        UpdatableReviewMapper.MapZoo(source, target);

        await Assert.That(target.Age).IsEqualTo(UpdatableReviewMapper.MapZoo(source).Age);
        await Assert.That(target.Age).IsEqualTo(0);
    }

    [Test]
    public async Task Generic_members_match_the_mapping_for_classes_and_structs()
    {
        var classTarget = new UpdWrapper<UpdNamedClass>();
        UpdatableReviewMapper.MapGeneric(CreateSource(), classTarget);
        await Assert.That(classTarget.Value!.Name).IsEqualTo("Ada");

        var structTarget = new UpdWrapper<UpdNamedStruct>();
        UpdatableReviewMapper.MapGeneric(CreateSource(), structTarget);
        await Assert.That(structTarget.Value.Name).IsEqualTo("Ada");
    }

    [Test]
    public async Task Root_throw_guard_on_the_source_is_preserved()
    {
        await Assert.That(() => UpdatableReviewMapper.MapRequiredSource(null, new UpdAnimal()))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Nested_collection_initializer_inside_a_creation_replaces_the_nested_object()
    {
        var oldBag = new UpdBag();
        oldBag.Items.Add("old");
        var target = new UpdZoo { Bag = oldBag };

        UpdatableReviewCollectionMapper.MapBag(CreateSource(), target);

        await Assert.That(target.Bag!.Items).IsEquivalentTo(["Ada"]);
        await Assert.That(oldBag.Items).IsEquivalentTo(["old"]);
    }

    [Test]
    public async Task Skipped_init_only_collections_do_not_force_replacing_their_owner()
    {
        var rootTags = new List<string> { "root" };
        var childTags = new List<string> { "child" };
        var child = new UpdInitCollectionChild { Name = "old", Tags = childTags };
        var target = new UpdInitCollectionRoot { Name = "old", Tags = rootTags, Child = child };

        var result = UpdatableSkippedCollectionMapper.Map(CreateSource(), target);

        await Assert.That(result).IsSameReferenceAs(target);
        await Assert.That(target.Name).IsEqualTo("Ada");
        await Assert.That(target.Tags).IsSameReferenceAs(rootTags);
        await Assert.That(target.Child).IsSameReferenceAs(child);
        await Assert.That(child.Name).IsEqualTo("Ada");
        await Assert.That(child.Tags).IsSameReferenceAs(childTags);
        await Assert.That(childTags).IsEquivalentTo(["child"]);
    }
}
