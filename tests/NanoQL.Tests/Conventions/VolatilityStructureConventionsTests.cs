// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A constant swaption volatility structure answers the conventions it was built with: its calendar, business-day
/// convention and day counter, in both forms; its settlement days are the days given to <c>Create</c>, and
/// <see langword="null"/> for a fixed reference date, where QuantLib's <c>settlementDays()</c> refuses
/// (<c>termstructure.hpp:127-129</c>), as the term structures' <see cref="TermStructure.SettlementDays"/> is.
/// </summary>
public class VolatilityStructureConventionsTests
{
    [Fact]
    public void AConstantSwaptionStructureAnswersTheConventionsItWasBuiltWith()
    {
        var reference = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = reference };
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        DayCounter dayCounter = new Actual360();

        var fixedForm = new ConstantSwaptionVolatility(
            reference, calendar, BusinessDayConvention.ModifiedFollowing, 0.2, dayCounter);
        Assert.Null(fixedForm.SettlementDays);
        Assert.Equal(calendar, fixedForm.Calendar);
        Assert.Equal(BusinessDayConvention.ModifiedFollowing, fixedForm.BusinessDayConvention);
        Assert.Equal(dayCounter, fixedForm.DayCounter);

        var moving = ConstantSwaptionVolatility.Create(
            3, new TARGET(), BusinessDayConvention.Preceding, 0.2, new Thirty360(Thirty360Convention.BondBasis), context);
        Assert.Equal(3, moving.SettlementDays);
        Assert.Equal(new TARGET(), moving.Calendar);
        Assert.Equal(BusinessDayConvention.Preceding, moving.BusinessDayConvention);
        Assert.Equal(new Thirty360(Thirty360Convention.BondBasis), moving.DayCounter);
    }
}
