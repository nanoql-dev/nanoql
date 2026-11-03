// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Quotes;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The ATM quote's delta. QuantLib's ATM constructor leaves <c>delta_</c> uninitialized
/// (<c>deltavolquote.cpp:32-37</c>), so its <c>delta()</c> reads whatever the memory held: on the wheel, after a
/// 25-delta quote, 0.25 (QuantLib-Python 1.44). Here an ATM quote has no delta, <see langword="null"/>; the
/// delta-and-volatility constructor's is the one given, as QuantLib's. QuantLib's check: that constructor, unchanged.
/// </summary>
public class DeltaVolQuoteAtmDeltaTests
{
    [Fact]
    public void AnAtmQuoteHasNoDelta()
    {
        var atm = new DeltaVolQuote(0.1, DeltaVolQuoteDeltaType.Fwd, 1.0, DeltaVolQuoteAtmType.AtmDeltaNeutral);
        Assert.Null(atm.Delta);
        Assert.Equal(0.1, atm.Value);
        Assert.Equal(DeltaVolQuoteAtmType.AtmDeltaNeutral, atm.AtmType);

        var quoted = new DeltaVolQuote(-0.25, 0.11, 1.0, DeltaVolQuoteDeltaType.Spot);
        Assert.Equal(-0.25, quoted.Delta);
        Assert.Equal(DeltaVolQuoteAtmType.AtmNull, quoted.AtmType);
    }
}
