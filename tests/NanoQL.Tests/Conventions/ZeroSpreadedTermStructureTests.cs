// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The zero-spreaded curve's original is the curve itself, never an empty handle (the playbook's handle row): a null
/// original is refused with <see cref="ArgumentNullException"/>, where QuantLib builds the curve on an empty handle and
/// refuses its queries until the handle is linked, and lets a handle the curve was built on be emptied again
/// (<c>testCreateWithNullUnderlying</c> and <c>testLinkToNullUnderlying</c>, which these facts replace); its calendar
/// and settlement days are the original's, read when they are asked for. A compounded rule with no frequency, QuantLib's
/// default frequency, builds and is refused at every query that reads a zero rate, with the interest rate's own
/// refusal, as QuantLib's curve is; the discount factor at the reference date is 1 without one.
/// </summary>
public class ZeroSpreadedTermStructureTests
{
    private static readonly Date Today = new(16, Month.June, 2025);

    [Fact]
    public void TheOriginalCurveIsRequired()
    {
        Assert.Equal(
            "originalCurve",
            Assert.Throws<ArgumentNullException>(() => new ZeroSpreadedTermStructure(null!, 0.01)).ParamName);
        Assert.Equal(
            "originalCurve",
            Assert.Throws<ArgumentNullException>(
                () => new ZeroSpreadedTermStructure(null!, 0.01, Compounding.Compounded, Frequency.Annual)).ParamName);
    }

    [Fact]
    public void ACompoundedSpreadWithNoFrequencyBuildsAndIsRefusedAtEveryQuery()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        FlatForward original = FlatForward.Create(2, new TARGET(), 0.03, new Actual365Fixed(), context);
        var spreaded = new ZeroSpreadedTermStructure(original, 0.01, Compounding.Compounded);

        // it builds, with the original's conventions
        Date reference = original.ReferenceDate;
        Assert.Equal(new Date(18, Month.June, 2025), spreaded.ReferenceDate);
        Assert.Equal(original.DayCounter, spreaded.DayCounter);
        Assert.Equal(new TARGET(), spreaded.Calendar);
        Assert.Equal(2, spreaded.SettlementDays);
        Assert.Equal(original.MaxDate, spreaded.MaxDate);

        // and an original built from its reference date has neither calendar nor settlement days
        var fixedReference = new ZeroSpreadedTermStructure(new FlatForward(Today, 0.03, new Actual365Fixed()), 0.01);
        Assert.Null(fixedReference.Calendar);
        Assert.Null(fixedReference.SettlementDays);

        Action[] queries =
        [
            () => _ = spreaded.Discount(1.0),
            () => _ = spreaded.Discount(reference + 365),
            () => _ = spreaded.ZeroRate(1.0, Compounding.Continuous),
            () => _ = spreaded.ZeroRate(reference, new Actual365Fixed(), Compounding.Continuous),
            () => _ = spreaded.ForwardRate(1.0, 2.0, Compounding.Continuous),
        ];
        foreach (Action query in queries)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(query);
            Assert.Equal("freq", e.ParamName);
            Assert.StartsWith("frequency not allowed for this interest rate", e.Message, StringComparison.Ordinal);
        }

        // the reference date's discount factor needs no zero rate
        Assert.Equal(1.0, spreaded.Discount(0.0));
    }
}
