using System.Collections.Generic;
using R3;
using R3.DynamicData.Cache;
using R3.DynamicData.Kernel;
#pragma warning disable SA1516, SA1503, SA1513, SA1107, SA1502, SA1515

namespace R3Ext.Tests;

public class TransformSafeCacheTests
{
    private sealed record Person(int Id, string Name, int Age);
    private sealed record PersonWithGender(Person Person, string Gender)
    {
        public override string ToString() => $"{Person.Name} ({Gender})";
    }

    [Fact]
    public void TransformSafe_NoErrors()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var errors = new List<Error<Person, int>>();
        var results = new List<PersonWithGender>();

        using var sub = cache.Connect()
            .TransformSafe(
                p => new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F"),
                error => errors.Add(error))
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ChangeReason.Add)
                    {
                        results.Add(change.Current);
                    }
                }
            });

        cache.AddOrUpdate(new Person(1, "Alice", 30));
        cache.AddOrUpdate(new Person(2, "Bob", 25));

        Assert.Empty(errors);
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Person.Name == "Alice" && r.Gender == "M");
        Assert.Contains(results, r => r.Person.Name == "Bob" && r.Gender == "F");
    }

    [Fact]
    public void TransformSafe_HandlesErrorsGracefully()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var errors = new List<Error<Person, int>>();
        var results = new List<PersonWithGender>();

        using var sub = cache.Connect()
            .TransformSafe(
                p =>
                {
                    if (p.Age % 3 == 0)
                        throw new InvalidOperationException($"Cannot transform {p.Name}");
                    return new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");
                },
                error => errors.Add(error))
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ChangeReason.Add)
                    {
                        results.Add(change.Current);
                    }
                }
            });

        cache.AddOrUpdate(new Person(1, "Alice", 30)); // Age 30, divisible by 3 - will error
        cache.AddOrUpdate(new Person(2, "Bob", 25));   // Age 25, will succeed
        cache.AddOrUpdate(new Person(3, "Charlie", 33)); // Age 33, divisible by 3 - will error

        Assert.Equal(2, errors.Count);
        Assert.All(errors, e => Assert.IsType<InvalidOperationException>(e.Exception));
        Assert.Contains(errors, e => e.Value.Name == "Alice");
        Assert.Contains(errors, e => e.Value.Name == "Charlie");

        Assert.Single(results);
        Assert.Contains(results, r => r.Person.Name == "Bob");
    }

    [Fact]
    public void TransformSafe_UpdateAfterError()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var errors = new List<Error<Person, int>>();
        var results = new List<PersonWithGender>();

        using var sub = cache.Connect()
            .TransformSafe(
                p =>
                {
                    if (p.Age % 3 == 0)
                        throw new InvalidOperationException($"Cannot transform {p.Name}");
                    return new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");
                },
                error => errors.Add(error))
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ChangeReason.Add || change.Reason == ChangeReason.Update)
                    {
                        results.Add(change.Current);
                    }
                }
            });

        cache.AddOrUpdate(new Person(1, "Alice", 30)); // Age 30, will error
        Assert.Single(errors);
        Assert.Empty(results);

        cache.AddOrUpdate(new Person(1, "Alice", 31)); // Age 31, will succeed
        Assert.Single(errors); // No new errors
        Assert.Single(results);
        Assert.Equal("Alice", results[0].Person.Name);
    }

    [Fact]
    public void TransformSafe_RemoveItemAfterError()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var errors = new List<Error<Person, int>>();
        var removals = new List<PersonWithGender>();

        using var sub = cache.Connect()
            .TransformSafe(
                p =>
                {
                    if (p.Age % 3 == 0)
                        throw new InvalidOperationException($"Cannot transform {p.Name}");
                    return new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");
                },
                error => errors.Add(error))
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ChangeReason.Remove)
                    {
                        removals.Add(change.Current);
                    }
                }
            });

        cache.AddOrUpdate(new Person(1, "Alice", 30)); // Age 30, will error
        Assert.Single(errors);

        cache.Remove(1); // Remove the item that errored
        Assert.Empty(removals); // No removal emitted because transform failed
    }

    [Fact]
    public void TransformSafe_WithKeyTransformFactory()
    {
        var cache = new SourceCache<Person, int>(p => p.Id);
        var errors = new List<Error<Person, int>>();
        var results = new List<PersonWithGender>();

        using var sub = cache.Connect()
            .TransformSafe(
                (p, key) =>
                {
                    if (key > 2)
                        throw new InvalidOperationException($"Cannot transform key {key}");
                    return new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");
                },
                error => errors.Add(error))
            .Subscribe(changeSet =>
            {
                foreach (var change in changeSet)
                {
                    if (change.Reason == ChangeReason.Add)
                    {
                        results.Add(change.Current);
                    }
                }
            });

        cache.AddOrUpdate(new Person(1, "Alice", 30));
        cache.AddOrUpdate(new Person(2, "Bob", 25));
        cache.AddOrUpdate(new Person(3, "Charlie", 35)); // Key 3, will error

        Assert.Single(errors);
        Assert.Equal(3, errors[0].Key);
        Assert.Equal(2, results.Count);
    }
}
