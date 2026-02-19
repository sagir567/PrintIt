using System;
using FluentAssertions;
using PrintIt.Domain.Entities;
using PrintIt.Domain.DomainLogic;


using Xunit;

namespace PrintIt.Tests.DomainLogic;

// Pure unit tests: no DB, no containers, just business rules.
public class SpoolConsumptionTests
{
    [Fact]
    public void CanConsume_should_return_false_when_remaining_is_zero()
    {
        // Arrange
        var spool = new FilamentSpool
        {
            RemainingGrams = 0,
            Status = "Opened",
            CreatedAtUtc = DateTime.UtcNow
        };

        // Act
        var ok = SpoolConsumption.CanConsume(spool, gramsUsed: 50, toleranceGrams: 10);

        // Assert
        ok.Should().BeFalse();
    }

    [Fact]
    public void CanConsume_should_return_true_when_remaining_plus_tolerance_covers_usage()
    {
        // Arrange
        var spool = new FilamentSpool
        {
            RemainingGrams = 45,
            Status = "Opened",
            CreatedAtUtc = DateTime.UtcNow
        };

        // 45 + 10 >= 50  => true

        // Act
        var ok = SpoolConsumption.CanConsume(spool, gramsUsed: 50, toleranceGrams: 10);

        // Assert
        ok.Should().BeTrue();
    }

    [Fact]
    public void Apply_should_decrease_remaining_and_update_status_and_last_used()
    {
        // Arrange
        var before = DateTime.UtcNow.AddMinutes(-5);

        var spool = new FilamentSpool
        {
            InitialGrams = 1000,
            RemainingGrams = 100,
            Status = "Opened",
            CreatedAtUtc = before,
            LastUsedAtUtc = null
        };

        // Act
        SpoolConsumption.Apply(spool, gramsUsed: 40);

        // Assert
        spool.RemainingGrams.Should().Be(60);
        spool.Status.Should().Be("Opened");
        spool.LastUsedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Apply_should_mark_empty_when_remaining_becomes_zero()
    {
        // Arrange
        var spool = new FilamentSpool
        {
            InitialGrams = 1000,
            RemainingGrams = 30,
            Status = "Opened",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        // Act
        SpoolConsumption.Apply(spool, gramsUsed: 30);

        // Assert
        spool.RemainingGrams.Should().Be(0);
        spool.Status.Should().Be("Empty");
        spool.LastUsedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Apply_should_throw_when_grams_used_is_not_positive()
    {
        // Arrange
        var spool = new FilamentSpool
        {
            RemainingGrams = 100,
            Status = "Opened",
            CreatedAtUtc = DateTime.UtcNow
        };

        // Act
        Action act = () => SpoolConsumption.Apply(spool, gramsUsed: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
