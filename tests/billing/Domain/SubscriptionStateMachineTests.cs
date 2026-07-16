using RestrictPoint.Api.Billing.Domain;
using Xunit;

namespace RestrictPoint.Api.Billing.Tests.Domain;

public sealed class SubscriptionStateMachineTests
{
    [Theory]
    // docs/12 state machine — legal transitions:
    [InlineData(SubscriptionStatus.Trialing, SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.Trialing, SubscriptionStatus.Canceled, true)]
    [InlineData(SubscriptionStatus.Trialing, SubscriptionStatus.Expired, true)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.PastDue, true)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Canceled, true)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Paused, true)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Refunded, true)]
    [InlineData(SubscriptionStatus.PastDue, SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.PastDue, SubscriptionStatus.Canceled, true)]
    [InlineData(SubscriptionStatus.Canceled, SubscriptionStatus.Expired, true)]
    // Illegal transitions:
    [InlineData(SubscriptionStatus.Canceled, SubscriptionStatus.Active, false)]
    [InlineData(SubscriptionStatus.Expired, SubscriptionStatus.Active, false)]
    [InlineData(SubscriptionStatus.Refunded, SubscriptionStatus.Active, false)]
    [InlineData(SubscriptionStatus.Trialing, SubscriptionStatus.PastDue, false)]
    [InlineData(SubscriptionStatus.Expired, SubscriptionStatus.Canceled, false)]
    public void Transitions_follow_the_documented_state_machine(
        SubscriptionStatus from, SubscriptionStatus to, bool expected)
    {
        Assert.Equal(expected, SubscriptionStateMachine.CanTransition(from, to));
    }

    [Fact]
    public void Self_transitions_are_always_allowed()
    {
        foreach (var status in Enum.GetValues<SubscriptionStatus>())
        {
            Assert.True(SubscriptionStateMachine.CanTransition(status, status));
        }
    }

    [Fact]
    public void Terminal_states_have_no_exits()
    {
        foreach (var target in Enum.GetValues<SubscriptionStatus>())
        {
            if (target != SubscriptionStatus.Expired)
            {
                Assert.False(SubscriptionStateMachine.CanTransition(SubscriptionStatus.Expired, target));
            }

            if (target != SubscriptionStatus.Refunded)
            {
                Assert.False(SubscriptionStateMachine.CanTransition(SubscriptionStatus.Refunded, target));
            }
        }
    }
}
