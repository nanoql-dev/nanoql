// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The zero-spreaded curve's compounding and frequency are checked when it is built (the playbook's enum row): a value
/// outside its enum is refused with <see cref="ArgumentOutOfRangeException"/> naming the parameter, "unknown compounding
/// convention" or "unknown frequency (99)". QuantLib stores both and computes with them at every query
/// (<c>zerospreadedtermstructure.hpp:90-98</c>): an unknown compounding builds and each query that reads a zero rate
/// fails, "null interest rate", since <c>impliedRate</c> leaves its rate null for it (<c>interestrate.cpp:111-132</c>)
/// and the rate's compound factor refuses that (<c>:47</c>); an unknown frequency builds and computes, a compounded
/// spread as one compounded 99 times a year (a discount factor of 0.9607928351209212 at one year on a flat 3%
/// Actual/365 (Fixed) curve with a 1% spread), a continuous one as if it had none.
/// </summary>
public class ZeroSpreadedCompoundingTests
{
    [Fact]
    public void AnUnknownCompoundingOrFrequencyIsRefusedWhenTheCurveIsBuilt()
    {
        var original = new FlatForward(new Date(16, Month.June, 2025), 0.03, new Actual365Fixed());

        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ZeroSpreadedTermStructure(original, 0.01, (Compounding)9, Frequency.Annual));
        Assert.Equal("comp", e.ParamName);
        Assert.StartsWith("unknown compounding convention", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ZeroSpreadedTermStructure(original, 0.01, Compounding.Compounded, (Frequency)99));
        Assert.Equal("freq", e.ParamName);
        Assert.StartsWith("unknown frequency (99)", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ZeroSpreadedTermStructure(original, 0.01, Compounding.Continuous, (Frequency)99));
        Assert.Equal("freq", e.ParamName);

        // the members compute as QuantLib's: OtherFrequency, compounded 999 times a year, among them
        Assert.Equal(
            0.9607897757576962,
            new ZeroSpreadedTermStructure(original, 0.01, Compounding.Compounded, Frequency.OtherFrequency).Discount(1.0),
            1e-15);
    }
}
