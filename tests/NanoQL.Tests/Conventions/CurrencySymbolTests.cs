// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib's source writes four currency symbols as single Latin-1 bytes: USD's fraction symbol, 0xA2
/// (<c>america.cpp:127</c>), JPY's symbol, 0xA5 (<c>asia.cpp:107</c>), and the pound sign 0xA3 of GBP's and CYP's
/// symbols (<c>europe.cpp:62, 107</c>). nanoql's strings hold the characters those bytes mean in Latin-1: ¢ (U+00A2),
/// ¥ (U+00A5) and £ (U+00A3).
/// </summary>
public class CurrencySymbolTests
{
    [Fact]
    public void TheLatin1SymbolsAreTheirCharacters()
    {
        Assert.Equal("¢", new USDCurrency().FractionSymbol);
        Assert.Equal("¥", new JPYCurrency().Symbol);
        Assert.Equal("£", new GBPCurrency().Symbol);
        Assert.Equal("£C", new CYPCurrency().Symbol);
    }
}
