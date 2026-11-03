// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The calendar markets QuantLib deprecates by the attribute alone aren't ported: Israel's <c>Settlement</c>, 0, an
/// alias of the Tel Aviv stock exchange, and Italy's <c>Exchange</c>, 1, the old Milan stock exchange
/// (<c>israel.hpp:105-106</c>, <c>italy.hpp:78</c>). Each enum keeps QuantLib's other values and its constructor
/// QuantLib's default, and the deprecated value is refused as any value outside the enum,
/// <see cref="ArgumentOutOfRangeException"/> naming <c>market</c>, "unknown market".
/// </summary>
public class CalendarDeprecatedMarketTests
{
    [Fact]
    public void IsraelsDeprecatedSettlementIsRefused()
    {
        // QuantLib: Israel(Israel::Settlement) is the Tel Aviv stock exchange
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => new Israel((IsraelMarket)0));
        Assert.Equal("market", e.ParamName);
        Assert.StartsWith("unknown market", e.Message, StringComparison.Ordinal);

        Assert.Equal(1, (int)IsraelMarket.TASE);
        Assert.Equal(2, (int)IsraelMarket.SHIR);
        Assert.Equal(3, (int)IsraelMarket.Telbor);
        Assert.Equal("Tel Aviv stock exchange", new Israel().Name);
    }

    [Fact]
    public void ItalysDeprecatedExchangeIsRefused()
    {
        // QuantLib: Italy(Italy::Exchange) is the Milan stock exchange
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => new Italy((ItalyMarket)1));
        Assert.Equal("market", e.ParamName);
        Assert.StartsWith("unknown market", e.Message, StringComparison.Ordinal);

        Assert.Equal(0, (int)ItalyMarket.Settlement);
        Assert.Equal(new Italy(ItalyMarket.Settlement), new Italy());
    }
}
