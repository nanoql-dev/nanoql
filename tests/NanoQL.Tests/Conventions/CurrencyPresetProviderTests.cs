// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Currencies;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Every currency preset reads its data through <see cref="ConventionProvider"/>, by its code: QuantLib's with nothing
/// set, which each region's vector file checks (the vectors gate, unchanged), and a provider's otherwise, field by
/// field.
/// </summary>
public class CurrencyPresetProviderTests
{
    // The currency presets, by type name.
    private static readonly IReadOnlyDictionary<string, Func<Currency>> Presets = typeof(Currency).Assembly.GetTypes()
        .Where(t => t.IsPublic && t.IsSealed && typeof(Currency).IsAssignableFrom(t))
        .ToDictionary(t => t.Name, t => (Func<Currency>)(() => (Currency)Activator.CreateInstance(t, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DoNotWrapExceptions, null, null, null)!), StringComparer.Ordinal);

    public static TheoryData<string> PresetNames() => [.. Presets.Keys.Order(StringComparer.Ordinal)];

    [Fact]
    public void ThePresetsAreQuantLibsHundredAndElevenCurrencies() => Assert.Equal(111, Presets.Count);

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void WithNothingSetACurrencyHasTheDataItsProviderIsGiven(string preset)
    {
        // An identity provider records the data the preset passes it, QuantLib's coded values.
        Currency plain = Presets[preset]();
        var identity = new TestConventions("identity");
        Currency throughIdentity;
        using (ConventionProvider.Use(identity))
        {
            throughIdentity = Presets[preset]();
        }

        Assert.Equal(Fields(plain), Fields(throughIdentity));
        Assert.Equal("currency:" + plain.Code, identity.Resolved.First());
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void ACurrencyCarriesEveryConfigurableFieldItsProviderReturns(string preset)
    {
        string code = Presets[preset]().Code;
        var rounding = new Rounding(3, RoundingType.Up, 7);
        var provider = new TestConventions("p")
        {
            Currencies = new Dictionary<string, Func<CurrencyData, CurrencyData>>
            {
                [code] = d => new CurrencyData(
                    d.Name, d.Code, d.NumericCode, d.Symbol + "p", d.FractionSymbol + "q", d.FractionsPerUnit * 10,
                    rounding, code == "USD" ? new EURCurrency() : new USDCurrency()),
            },
        };
        Currency plain = Presets[preset]();
        Currency edited;
        using (ConventionProvider.Use(provider))
        {
            edited = Presets[preset]();
        }

        Assert.Equal(plain, edited);
        Assert.Equal(plain.Name, edited.Name);
        Assert.Equal(code, edited.Code);
        Assert.Equal(plain.NumericCode, edited.NumericCode);
        Assert.Equal(plain.Symbol + "p", edited.Symbol);
        Assert.Equal(plain.FractionSymbol + "q", edited.FractionSymbol);
        Assert.Equal(plain.FractionsPerUnit * 10, edited.FractionsPerUnit);
        Assert.Same(rounding, edited.Rounding);
        Assert.Equal(code == "USD" ? "EUR" : "USD", edited.TriangulationCurrency!.Code);
        Assert.Empty(edited.MinorUnitCodes);
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void ACurrencyRefusesAProvidersChangeOfItsNameOrNumericCode(string preset)
    {
        string code = Presets[preset]().Code;
        Func<CurrencyData, CurrencyData>[] changes =
        [
            d => d with { Name = d.Name + " (p)" },
            d => d with { NumericCode = d.NumericCode + 1 },
        ];
        foreach (Func<CurrencyData, CurrencyData> change in changes)
        {
            var provider = new TestConventions("p")
            {
                Currencies = new Dictionary<string, Func<CurrencyData, CurrencyData>> { [code] = change },
            };
            using (ConventionProvider.Use(provider))
            {
                NanoQLException e = Assert.Throws<NanoQLException>(() => Presets[preset]());
                Assert.StartsWith($"the data for the {code} currency changes its identity", e.Message);
            }
        }
    }

    [Fact]
    public void ALegacyCurrencysTriangulationCurrencyIsEuroByCodeUnderAnyProvider()
    {
        // The twelve legacy euro-area currencies build their EUR in their provider's scope: the EUR the provider
        // configures, still EUR by its code, so a triangulation through it finds the same rates.
        string[] legacy = ["ATS", "BEF", "DEM", "ESP", "FIM", "FRF", "GRD", "IEP", "ITL", "LUF", "NLG", "PTE"];
        var provider = TestConventions.MarkingCurrencies("p", "EUR");
        foreach (string code in legacy)
        {
            Func<Currency> factory = Presets[code + "Currency"];
            Currency plain = factory();
            Currency edited;
            using (ConventionProvider.Use(provider))
            {
                edited = factory();
            }

            Assert.Equal(new EURCurrency(), plain.TriangulationCurrency);
            Assert.Equal("EUR", edited.TriangulationCurrency!.Code);
            Assert.Equal("p", TestConventions.MarkOf(edited.TriangulationCurrency));
            Assert.Equal(plain, edited);
        }

        Assert.Equal(legacy.Length, provider.Resolved.Count(r => r == "currency:EUR"));
    }

    // A currency's fields, its rounding by value.
    private static object Fields(Currency c) => (
        c.Name, c.Code, c.NumericCode, c.Symbol, c.FractionSymbol, c.FractionsPerUnit,
        c.Rounding.GetType(), c.Rounding.Type, Precision(c.Rounding), c.TriangulationCurrency?.Code, c.MinorUnitCodes.Count);

    private static int? Precision(Rounding r)
    {
        try
        {
            return r.Precision();
        }
        catch (NanoQLException)
        {
            return null;
        }
    }
}
