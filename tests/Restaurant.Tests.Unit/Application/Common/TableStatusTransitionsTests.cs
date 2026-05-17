using Restaurant.Application.Common;
using Restaurant.Domain.Enums;

namespace Restaurant.Tests.Unit.Application.Common;

public sealed class TableStatusTransitionsTests
{
    [Theory]
    [InlineData(ETableStatus.Available, ETableStatus.Reserved, true)]
    [InlineData(ETableStatus.Available, ETableStatus.Busy, true)]
    [InlineData(ETableStatus.Reserved, ETableStatus.Busy, true)]
    [InlineData(ETableStatus.Busy, ETableStatus.Available, true)]
    [InlineData(ETableStatus.Busy, ETableStatus.Reserved, false)]
    public void CanTransition_matches_rules(ETableStatus from, ETableStatus to, bool expected) =>
        Assert.Equal(expected, TableStatusTransitions.CanTransition(from, to));
}
