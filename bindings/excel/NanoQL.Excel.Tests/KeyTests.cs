// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>
/// The key: a call's function, the provider, the date system and each argument (numbers by their bits, a missing
/// argument as itself, a handle by its own key); equal calls one object in any cell; the handle's 16 hex digits, 32 on
/// a collision; the RTD topic keyed by the store's key.
/// </summary>
[Collection(TestSettings.Name)]
public sealed class KeyTests
{
    private const double Date = 42262;  // DATE(2015,9,15)

    /// <summary>Initializes a new instance of the <see cref="KeyTests"/> class, on a clean runtime.</summary>
    public KeyTests() => Reset();

    /// <summary>Equal calls, in any cell, give one handle and one object.</summary>
    [Fact]
    public void EqualCallsGiveOneHandle()
    {
        string dc = Dc();
        string a = H(Call("nqlFlatForward", Date, 0.03, dc));
        Host.Cell = TestHost.At(40, 9);
        string b = H(Call("nqlFlatForward", Date, 0.03, dc));
        Assert.Equal(a, b);
        Assert.Matches("^FlatForward#[0-9A-F]{16}$", a);
        Assert.True(X.TryEntry(a, out XlEntry ea));
        Assert.True(X.TryEntry(b, out XlEntry eb));
        Assert.Same(ea.Value, eb.Value);
    }

    /// <summary>Numbers by their bits: 0.1 and the next double are two objects.</summary>
    [Fact]
    public void NumbersByTheirBits()
    {
        string dc = Dc();
        Assert.NotEqual(H(Call("nqlFlatForward", Date, 0.1, dc)), H(Call("nqlFlatForward", Date, Math.BitIncrement(0.1), dc)));
    }

    /// <summary>The effective provider is in the key: two providers, two objects.</summary>
    [Fact]
    public void TwoProvidersGiveTwoHandles()
    {
        string dc = Dc();
        string a = H(Call("nqlFlatForward", Date, 0.03, dc));
        X.ProviderId = "file:0123456789ABCDEF";
        try
        {
            Assert.NotEqual(a, H(Call("nqlFlatForward", Date, 0.03, dc)));
        }
        finally
        {
            X.ProviderId = "QuantLib";
        }
    }

    /// <summary>The same raw call from a 1900 and a 1904 workbook: two objects, two RTD topics.</summary>
    [Fact]
    public void TwoDateSystemsGiveTwoHandlesAndTwoTopics()
    {
        string dc = Dc();
        string a = H(Call("nqlFlatForward", Date, 0.03, dc));
        Host.Shift = 1462;
        string b = H(Call("nqlFlatForward", Date, 0.03, dc));
        Assert.NotEqual(a, b);
        Assert.True(X.TryEntry(a, out XlEntry ea));
        Assert.True(X.TryEntry(b, out XlEntry eb));
        Assert.NotEqual(ea.Key, eb.Key);
        Assert.Contains(ea.Key, Host.Topics.Keys);
        Assert.Contains(eb.Key, Host.Topics.Keys);
    }

    /// <summary>A missing argument and its explicit default are two calls, two objects.</summary>
    [Fact]
    public void AMissingArgumentAgainstItsDefault()
    {
        string dc = Dc();
        Assert.NotEqual(H(Call("nqlFlatForward", Date, 0.03, dc)), H(Call("nqlFlatForward", Date, 0.03, dc, "Continuous", "Annual")));
    }

    /// <summary>A collision of the shown 16 digits, forced: the second object shows 32.</summary>
    [Fact]
    public void ACollisionShowsThirtyTwoDigits()
    {
        string dc = Dc();
        X.Hash = b =>
        {
            byte[] h = SHA256.HashData(b);
            Array.Clear(h, 0, 8);
            return h;
        };
        try
        {
            string a = H(Call("nqlFlatForward", Date, 0.03, dc));
            string b = H(Call("nqlFlatForward", Date, 0.04, dc));
            Assert.Matches("^FlatForward#0{16}$", a);
            Assert.Matches("^FlatForward#0{16}[0-9A-F]{16}$", b);
            Assert.NotEqual(D(Call("nqlYieldTermStructureDiscount2", a, 1.0)), D(Call("nqlYieldTermStructureDiscount2", b, 1.0)));
        }
        finally
        {
            X.Hash = SHA256.HashData;
        }
    }

    private static string Dc() => H(Call("nqlConventionSetDayCounter", null, "Actual365Fixed"));
}
