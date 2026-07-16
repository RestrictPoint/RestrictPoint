using FluentAssertions;
using RestrictPoint.Api.Marketplace.Domain;

namespace RestrictPoint.Tests.Marketplace.Domain;

public sealed class ListingStateMachineTests
{
    [Theory]
    [InlineData(ListingStatus.Draft, ListingStatus.Published, true)]
    [InlineData(ListingStatus.Draft, ListingStatus.Removed, true)]
    [InlineData(ListingStatus.Draft, ListingStatus.Suspended, false)]
    [InlineData(ListingStatus.Published, ListingStatus.Suspended, true)]
    [InlineData(ListingStatus.Published, ListingStatus.Deprecated, true)]
    [InlineData(ListingStatus.Published, ListingStatus.Removed, true)]
    [InlineData(ListingStatus.Published, ListingStatus.Draft, false)]
    [InlineData(ListingStatus.Suspended, ListingStatus.Published, true)]
    [InlineData(ListingStatus.Suspended, ListingStatus.Removed, true)]
    [InlineData(ListingStatus.Suspended, ListingStatus.Draft, false)]
    [InlineData(ListingStatus.Deprecated, ListingStatus.Removed, true)]
    [InlineData(ListingStatus.Deprecated, ListingStatus.Published, false)]
    [InlineData(ListingStatus.Removed, ListingStatus.Draft, false)]
    [InlineData(ListingStatus.Removed, ListingStatus.Published, false)]
    public void CanTransition_ReturnsExpectedResult(ListingStatus from, ListingStatus to, bool expected)
    {
        var canTransition = ListingStateMachine.CanTransition(from, to);

        canTransition.Should().Be(expected);
    }
}
