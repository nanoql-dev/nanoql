// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The rate helpers' refusals, whose type, message and <see cref="ArgumentException.ParamName"/> a vector's
/// <c>{"throws": true}</c> can't assert (playbook §9.4). A refusal of the helper's own argument names its own parameter,
/// before the argument is passed on (the callee row); a refusal of a date the helper computed comes from the callee
/// that refused it.
/// </summary>
public class RateHelperErrorTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.January, 2024) };
    private static readonly DayCounter Thirty360 = new Thirty360(Thirty360Convention.BondBasis);

    [Fact]
    public void TheHelpersRefusals()
    {
        // the deposit helper's own arguments, checked before the index it builds would refuse them
        AssertRefusal<ArgumentOutOfRangeException>("fixingDays", () => new DepositRateHelper(
            0.03, new Period(6, TimeUnit.Months), -1, new TARGET(), BusinessDayConvention.Following, false,
            new Actual360(), Context));
        AssertRefusal<ArgumentNullException>("calendar", () => new DepositRateHelper(
            0.03, new Period(6, TimeUnit.Months), 2, null!, BusinessDayConvention.Following, false, new Actual360(),
            Context));
        AssertRefusal<ArgumentNullException>("dayCounter", () => new DepositRateHelper(
            0.03, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.Following, false, null!,
            Context));
        AssertRefusal<ArgumentNullException>("context", () => new DepositRateHelper(
            0.03, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.Following, false,
            new Actual360(), null!));
        AssertRefusal<ArgumentException>("fixingDate", () => new DepositRateHelper(0.03, default(Date), new Euribor6M()));

        // the swap helper's
        AssertRefusal<ArgumentOutOfRangeException>("settlementDays", () => Swap(settlementDays: -1));
        Assert.Equal(
            "unknown Pillar::Choice(7) (Parameter 'pillar')\nActual value was 7.",
            AssertRefusal<ArgumentOutOfRangeException>("pillar", () => Swap(pillar: (PillarChoice)7)).Message
                .ReplaceLineEndings("\n"));
        AssertRefusal<ArgumentOutOfRangeException>("fixedFrequency", () => Swap(fixedFrequency: Frequency.OtherFrequency));
        AssertRefusal<ArgumentOutOfRangeException>("fixedConvention", () => Swap(fixedConvention: (BusinessDayConvention)42));
        AssertRefusal<ArgumentOutOfRangeException>("floatConvention", () => Swap(floatConvention: (BusinessDayConvention)42));
        AssertRefusal<ArgumentException>("customPillarDate", () => Swap(customPillarDate: default(Date)));
        Assert.StartsWith(
            "pillar date (null date) must be later than or equal to the instrument's earliest date (January 17th, 2024)",
            AssertRefusal<ArgumentException>("customPillarDate", () => Swap(pillar: PillarChoice.CustomDate)).Message,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "pillar date (January 17th, 2034) must be before or equal to the instrument's latest relevant date (January 17th, 2029)",
            AssertRefusal<ArgumentException>(
                "customPillarDate",
                () => Swap(pillar: PillarChoice.CustomDate, customPillarDate: new Date(17, Month.January, 2034))).Message,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "fixedFrequency == Once is not supported when passing explicit startDate and endDate",
            AssertRefusal<ArgumentException>("fixedFrequency", () => new SwapRateHelper(
                0.03,
                new Date(17, Month.January, 2024),
                new Date(17, Month.January, 2029),
                new TARGET(),
                Frequency.Once,
                BusinessDayConvention.Unadjusted,
                Thirty360,
                new Euribor6M(),
                Context)).Message,
            StringComparison.Ordinal);
        AssertRefusal<ArgumentException>("startDate", () => new SwapRateHelper(
            0.03,
            default,
            new Date(17, Month.January, 2029),
            new TARGET(),
            Frequency.Annual,
            BusinessDayConvention.Unadjusted,
            Thirty360,
            new Euribor6M(),
            Context));
        AssertRefusal<ArgumentNullException>("context", () => new SwapRateHelper(
            0.03,
            new Date(17, Month.January, 2024),
            new Date(17, Month.January, 2029),
            new TARGET(),
            Frequency.Annual,
            BusinessDayConvention.Unadjusted,
            Thirty360,
            new Euribor6M(),
            null!));

        // a date the helper computes is refused by its callee, as raised
        AssertRefusal<ArgumentOutOfRangeException>("days", () => Swap(settlementDays: int.MaxValue));
    }

    private static T AssertRefusal<T>(string paramName, Func<object> build)
        where T : ArgumentException
    {
        T e = Assert.Throws<T>(build);
        Assert.Equal(paramName, e.ParamName);
        return e;
    }

    private static SwapRateHelper Swap(
        Frequency fixedFrequency = Frequency.Annual,
        BusinessDayConvention fixedConvention = BusinessDayConvention.Unadjusted,
        int? settlementDays = null,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null,
        BusinessDayConvention? floatConvention = null) =>
        new(
            0.03,
            new Period(5, TimeUnit.Years),
            new TARGET(),
            fixedFrequency,
            fixedConvention,
            Thirty360,
            new Euribor6M(),
            Context,
            settlementDays: settlementDays,
            pillar: pillar,
            customPillarDate: customPillarDate,
            floatConvention: floatConvention);
}
