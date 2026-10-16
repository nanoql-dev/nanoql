// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's <c>enableExtrapolation</c> and <c>disableExtrapolation</c> set a flag on the object, for every holder of
/// it. In nanoql they return a modified copy, of the receiver's own type, and leave the receiver unchanged.
/// </summary>
public class ExtrapolatorCopyTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void TheCopysFlagChangesAndTheReceiversDoesNot()
    {
        var curve = new FlatForward(Today, 0.03, new Actual365Fixed());
        Assert.False(curve.AllowsExtrapolation);

        FlatForward enabled = curve.EnableExtrapolation();
        Assert.NotSame(curve, enabled);
        Assert.True(enabled.AllowsExtrapolation);
        Assert.False(curve.AllowsExtrapolation);

        FlatForward disabled = enabled.DisableExtrapolation();
        Assert.False(disabled.AllowsExtrapolation);
        Assert.True(enabled.AllowsExtrapolation);

        // The bool argument: enabling with false disables, and disabling with false enables.
        Assert.False(curve.EnableExtrapolation(false).AllowsExtrapolation);
        Assert.True(curve.DisableExtrapolation(false).AllowsExtrapolation);
    }

    [Fact]
    public void TheCopyKeepsItsTypeAndItsValues()
    {
        var curve = new FlatForward(Today, 0.03, new Actual360(), Compounding.Compounded, Frequency.Semiannual);
        FlatForward copy = curve.EnableExtrapolation();

        Assert.IsType<FlatForward>(copy);
        Assert.Equal(curve.ReferenceDate, copy.ReferenceDate);
        Assert.Equal(curve.DayCounter, copy.DayCounter);
        Assert.Equal(curve.Compounding, copy.Compounding);
        Assert.Equal(curve.CompoundingFrequency, copy.CompoundingFrequency);
        Assert.Equal(curve.MaxDate, copy.MaxDate);
        foreach (int days in new[] { 0, 1, 90, 365, 3653 })
        {
            Assert.Equal(curve.Discount(Today + days), copy.Discount(Today + days));
        }

        // Only the flag differs: past the curve's range, the copy extrapolates and the receiver throws.
        double past = curve.MaxTime + 1.0;
        Assert.Throws<NanoQLException>(() => curve.Discount(past));
        Assert.Equal(curve.Discount(past, true), copy.Discount(past));

        // Through the base type, the copy keeps its runtime type.
        YieldTermStructure asBase = curve;
        Assert.IsType<FlatForward>(asBase.EnableExtrapolation());
    }

    [Fact]
    public void ANullReceiverThrows()
    {
        FlatForward? curve = null;
        Assert.Throws<ArgumentNullException>(() => curve!.EnableExtrapolation());
        Assert.Throws<ArgumentNullException>(() => curve!.DisableExtrapolation());
    }
}
