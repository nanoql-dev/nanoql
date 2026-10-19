// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A bond helper on a bond with no cash flows, or on no bond. QuantLib's helper copies the bond it is given
/// (<c>make_shared&lt;Bond&gt;(*bond)</c>, <c>bondhelpers.cpp:33</c>), which dereferences a null one, and reads its
/// last cash flow's date (<c>bondhelpers.cpp:37</c>), past the end of an empty leg: both undefined, and the wheel ends
/// the process on the second. nanoql refuses a null bond with <see cref="ArgumentNullException"/>, and a bond with no
/// cash flows with <see cref="ArgumentException"/>, in QuantLib's text for a derived bond built with none ("bond with
/// no cashflows!", <c>fixedratebond.cpp:67</c>).
/// </summary>
public class BondHelperArgumentTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(16, Month.June, 2025) };

    [Fact]
    public void AHelperOnABondWithNoCashFlowsIsRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new BondHelper(100.0, new Bond(2, new TARGET()), Context));
        Assert.Equal("bond", e.ParamName);
        Assert.StartsWith("bond with no cashflows!", e.Message, StringComparison.Ordinal);

        // A bond with a cash flow builds, its latest date that cash flow's.
        var zero = new ZeroCouponBond(2, new TARGET(), 100.0, new Date(15, Month.June, 2030));
        Assert.Equal(new Date(17, Month.June, 2030), new BondHelper(80.0, zero, Context).LatestDate);
    }

    [Fact]
    public void AHelperOnANullBondIsRefused()
    {
        Assert.Equal("bond", Assert.Throws<ArgumentNullException>(() => new BondHelper(100.0, null!, Context)).ParamName);
    }
}
