// SPDX-FileCopyrightText: Copyright (C) 2021 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/currency.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class CurrencyTests
{
    [Fact]
    public void TestBespokeConstructor()
    {
        // Testing bespoke currency constructor...

        string name = "Some Currency";
        string code = "CCY";
        string symbol = "#";

        Currency? customCcy = new Currency(name, code, 100, symbol, string.Empty, 100, new Rounding());

        // QuantLib's empty() is a null currency.
        if (customCcy is null)
        {
            Assert.Fail("Failed to create bespoke currency.");
        }

        if (customCcy.Name != name)
        {
            Assert.Fail(
                "incorrect currency name\n"
                + $"    actual:    {customCcy.Name}\n"
                + $"    expected:    {name}\n");
        }

        if (customCcy.Code != code)
        {
            Assert.Fail(
                "incorrect currency code\n"
                + $"    actual:    {customCcy.Code}\n"
                + $"    expected:    {code}\n");
        }

        if (customCcy.Symbol != symbol)
        {
            Assert.Fail(
                "incorrect currency symbol\n"
                + $"    actual:    {customCcy.Symbol}\n"
                + $"    expected:    {symbol}\n");
        }
    }
}
