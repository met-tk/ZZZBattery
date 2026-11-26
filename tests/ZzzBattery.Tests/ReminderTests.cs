using System.Text.Json;
using Xunit;
using ZzzBattery.Core;

namespace ZzzBattery.Tests;

public class ReminderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);
    private static EnergySnapshot Snapshot(int current = 179) =>
        new("account", "role", current, 240, (240 - current) * 360, Now, []);
    private static EnergyProjection Projection(int current) => new(current, 240, (240 - current) * 360, true, false, false);

    [Fact]
    public void CrossesTargetEvenWhenRefreshSkipsExactValueAndRearmsAfterConsumption()
    {
        var state = new ReminderState(); var snapshot = Snapshot();
        Assert.Null(state.EvaluateEnergy(snapshot, Projection(179), true, 180));
        Assert.Contains("182/240", state.EvaluateEnergy(snapshot, Projection(182), true, 180));
        Assert.Null(state.EvaluateEnergy(snapshot, Projection(182), true, 180));
        Assert.Null(state.EvaluateEnergy(snapshot, Projection(220), true, 180));
        Assert.Null(state.EvaluateEnergy(Snapshot(160), Projection(160), true, 180));
        Assert.NotNull(state.EvaluateEnergy(Snapshot(160), Projection(180), true, 180));
    }

    [Fact]
    public void SmallServerCorrectionBelowTargetDoesNotCreateAnotherConsumptionCycle()
    {
        var state = new ReminderState(); var snapshot = Snapshot();
        state.EvaluateEnergy(snapshot, EnergyEstimator.Project(snapshot, TimeSpan.Zero), true, 180);
        Assert.NotNull(state.EvaluateEnergy(snapshot, EnergyEstimator.Project(snapshot, TimeSpan.FromSeconds(360)), true, 180));
        var corrected = snapshot with { ReceivedAt = Now.AddSeconds(360), RestoreSeconds = 21601 };
        Assert.Null(state.EvaluateEnergy(corrected, EnergyEstimator.Project(corrected, TimeSpan.Zero), true, 180));
        Assert.False(state.EnergyArmed);
        Assert.Null(state.EvaluateEnergy(corrected, EnergyEstimator.Project(corrected, TimeSpan.FromSeconds(1)), true, 180));
        var consumed = corrected with { ReceivedAt = Now.AddSeconds(361), RestoreSeconds = 21960 };
        Assert.Null(state.EvaluateEnergy(consumed, EnergyEstimator.Project(consumed, TimeSpan.Zero), true, 180));
        Assert.True(state.EnergyArmed);
        Assert.NotNull(state.EvaluateEnergy(consumed, EnergyEstimator.Project(consumed, TimeSpan.FromSeconds(360)), true, 180));
    }

    [Fact]
    public void RestartPreservesBothArmingAndDelivery()
    {
        var state = new ReminderState(); var snapshot = Snapshot();
        state.EvaluateEnergy(snapshot, Projection(179), true, 180);
        state = JsonSerializer.Deserialize<ReminderState>(JsonSerializer.Serialize(state))!;
        Assert.NotNull(state.EvaluateEnergy(snapshot, Projection(180), true, 180));
        state = JsonSerializer.Deserialize<ReminderState>(JsonSerializer.Serialize(state))!;
        Assert.Null(state.EvaluateEnergy(snapshot, Projection(180), true, 180));
    }

    [Theory]
    [InlineData(180)]
    [InlineData(240)]
    [InlineData(250)]
    public void EnablingAtOrAboveTargetDoesNotNotify(int current)
    {
        var state = new ReminderState();
        Assert.Null(state.EvaluateEnergy(Snapshot(current), Projection(current), true, 180));
        Assert.Null(state.EvaluateEnergy(Snapshot(current), Projection(current), true, 180));
    }

    [Fact]
    public void UnreliableDataNeitherNotifiesNorRearms()
    {
        var state = new ReminderState(); var snapshot = Snapshot();
        state.EvaluateEnergy(snapshot, Projection(179), true, 180);
        var before = state with { };
        Assert.Null(state.EvaluateEnergy(snapshot, Projection(180), false, 180));
        Assert.Null(state.EvaluateEnergy(snapshot, Projection(180) with { Stale = true }, true, 180));
        Assert.Null(state.EvaluateEnergy(snapshot, Projection(180) with { Inconsistent = true }, true, 180));
        Assert.Equal(before, state);
        Assert.NotNull(state.EvaluateEnergy(snapshot, Projection(180), true, 180));
        Assert.Null(state.EvaluateEnergy(snapshot, Projection(100), false, 180));
        Assert.Null(state.EvaluateEnergy(snapshot, Projection(181), true, 180));
    }

    [Fact]
    public void ChangedTargetDisabledRuleAndDifferentAccountOrRoleResetArming()
    {
        var state = new ReminderState(); var snapshot = Snapshot();
        state.EvaluateEnergy(snapshot, Projection(179), true, 180);
        Assert.Null(state.EvaluateEnergy(snapshot, Projection(179), true, 170));
        Assert.Null(state.EvaluateEnergy(snapshot, Projection(180), true, 170));
        state.EvaluateEnergy(snapshot, Projection(160), true, 170);
        Assert.Null(state.EvaluateEnergy(snapshot, Projection(160), true, null));
        Assert.Null(state.EvaluateEnergy(snapshot, Projection(180), true, 170));
        foreach (var other in new[] { snapshot with { AccountKey = "other" }, snapshot with { RoleKey = "other" } })
        {
            state.EvaluateEnergy(snapshot, Projection(160), true, 170);
            Assert.Null(state.EvaluateEnergy(other, Projection(180), true, 170));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(241)]
    public void InvalidTargetDoesNotNotify(int target)
    {
        var state = new ReminderState();
        Assert.Null(state.EvaluateEnergy(Snapshot(), Projection(100), true, target));
        Assert.Null(state.EvaluateEnergy(Snapshot(), Projection(240), true, target));
        Assert.False(state.EnergyArmed);
    }

    [Fact]
    public void NaturalRecoveryAt240AndFullReminderCanBothBeConsumedOnce()
    {
        var state = new ReminderState(); var snapshot = Snapshot(239);
        state.EvaluateEnergy(snapshot, Projection(239), true, 240);
        state.Evaluate(snapshot, Projection(239), Now, true, 5);
        var full = EnergyEstimator.Project(snapshot, TimeSpan.FromMinutes(6));
        Assert.NotNull(state.EvaluateEnergy(snapshot, full, true, 240));
        Assert.NotNull(state.Evaluate(snapshot, full, Now.AddMinutes(6), true, 5));
        Assert.Null(state.EvaluateEnergy(snapshot, full, true, 240));
        Assert.Null(state.Evaluate(snapshot, full, Now.AddMinutes(6), true, 5));
    }

    [Fact]
    public void OldProfilesKeepTheirSettingsAndNewRemindersDefaultOff()
    {
        var settings = JsonSerializer.Deserialize<WidgetSettings>("""{"ReminderEnabled":true,"ReminderMinutes":15}""")!;
        Assert.True(settings.ReminderEnabled);
        Assert.Equal(15, settings.ReminderMinutes);
        Assert.False(settings.EnergyReminderEnabled);
        Assert.Equal(180, settings.ReminderEnergy);
        Assert.Equal("System", settings.ReminderSound);
        var state = JsonSerializer.Deserialize<ReminderState>("""{"Key":"account:role","FullSent":true}""")!;
        Assert.True(state.FullSent);
        Assert.Null(state.EvaluateEnergy(Snapshot(), Projection(180), true, 180));
    }
}
