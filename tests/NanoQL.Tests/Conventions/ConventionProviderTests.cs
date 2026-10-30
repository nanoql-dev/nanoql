// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="ConventionProvider"/>'s scopes: <see cref="ConventionProvider.Current"/> is the innermost scope's provider
/// on the asynchronous flow, <see cref="ConventionProvider.Default"/> outside any, and a disposed scope restores the one
/// before it. None of these facts sets <see cref="ConventionProvider.Default"/> (<see cref="DefaultProviderCollection"/>'s
/// do), so it is QuantLib's here.
/// </summary>
public class ConventionProviderTests
{
    [Fact]
    public void QuantLibsProviderReturnsEveryReference()
    {
        ConventionProvider quantLib = ConventionProvider.QuantLib;
        var edits = new CalendarEdits([new Date(24, Month.December, 2026)], []);
        var data = new CurrencyData("Test", "TST", 999, "T", "t", 100, new Numerics.Rounding());
        var calendar = new TARGET();

        Assert.Same(edits, quantLib.ResolveCalendarEdits("TARGET", edits));
        Assert.Same(data, quantLib.ResolveCurrency("TST", () => data));
        Assert.Same(calendar, quantLib.ResolveBusiness252Calendar(() => calendar));
        Assert.Same(ConventionProvider.QuantLib, ConventionProvider.QuantLib);
    }

    [Fact]
    public void OutsideAnyScopeCurrentIsTheDefaultQuantLibs()
    {
        Assert.Same(ConventionProvider.QuantLib, ConventionProvider.Default);
        Assert.Same(ConventionProvider.Default, ConventionProvider.Current);
    }

    [Fact]
    public void AScopeIsCurrentUntilDisposedAndScopesNest()
    {
        var outer = new TestConventions("outer");
        var inner = new TestConventions("inner");

        using (ConventionProvider.Use(outer))
        {
            Assert.Same(outer, ConventionProvider.Current);
            using (ConventionProvider.Use(inner))
            {
                Assert.Same(inner, ConventionProvider.Current);

                // Opening the scope that is already current changes nothing, and disposing it restores nothing.
                using (ConventionProvider.Use(inner))
                {
                    Assert.Same(inner, ConventionProvider.Current);
                }

                Assert.Same(inner, ConventionProvider.Current);
            }

            Assert.Same(outer, ConventionProvider.Current);
        }

        Assert.Same(ConventionProvider.QuantLib, ConventionProvider.Current);
    }

    [Fact]
    public void DisposingAScopeTwiceRestoresOnce()
    {
        var outer = new TestConventions("outer");
        var inner = new TestConventions("inner");

        using (ConventionProvider.Use(outer))
        {
            IDisposable scope = ConventionProvider.Use(inner);
            scope.Dispose();
            Assert.Same(outer, ConventionProvider.Current);

            using (ConventionProvider.Use(new TestConventions("other")))
            {
                // A second dispose of the first scope doesn't restore over the scope opened since.
                scope.Dispose();
                Assert.Equal("other", ((TestConventions)ConventionProvider.Current).Name);
            }

            Assert.Same(outer, ConventionProvider.Current);
        }
    }

    [Fact]
    public void ANullProviderIsRefused()
    {
        ArgumentNullException e = Assert.Throws<ArgumentNullException>(() => ConventionProvider.Use(null!));
        Assert.Equal("provider", e.ParamName);
    }

    [Fact]
    public async Task TwoConcurrentScopesEachReadTheirOwnAfterAnAwait()
    {
        var a = new TestConventions("a");
        var b = new TestConventions("b");
        var caller = new TestConventions("caller");
        using var gate = new SemaphoreSlim(0);

        async Task<(ConventionProvider Before, ConventionProvider After)> In(ConventionProvider provider)
        {
            using (ConventionProvider.Use(provider))
            {
                ConventionProvider before = ConventionProvider.Current;
                await gate.WaitAsync();
                return (before, ConventionProvider.Current);
            }
        }

        using (ConventionProvider.Use(caller))
        {
            Task<(ConventionProvider Before, ConventionProvider After)> first = Task.Run(() => In(a));
            Task<(ConventionProvider Before, ConventionProvider After)> second = Task.Run(() => In(b));
            gate.Release(2);
            var results = await Task.WhenAll(first, second);

            Assert.Equal((a, a), (results[0].Before, results[0].After));
            Assert.Equal((b, b), (results[1].Before, results[1].After));
            Assert.Same(caller, ConventionProvider.Current);
        }
    }

    [Fact]
    public async Task AScopeFlowsIntoWorkStartedInsideItAndNotIntoWorkStartedOutside()
    {
        var provider = new TestConventions("scoped");
        Task<ConventionProvider> outside;
        using var started = new SemaphoreSlim(0);
        outside = Task.Run(async () =>
        {
            await started.WaitAsync();
            return ConventionProvider.Current;
        });

        Task<ConventionProvider> inside;
        using (ConventionProvider.Use(provider))
        {
            inside = Task.Run(() => ConventionProvider.Current);
            started.Release();
            Assert.Same(provider, await inside);
        }

        Assert.Same(ConventionProvider.QuantLib, await outside);
    }

    [Fact]
    public void APresetsNestedPresetsComeFromItsProvider()
    {
        // The legacy currency's triangulation currency is built inside the provider's scope, even when the provider is
        // read outside any scope.
        var provider = TestConventions.MarkingCurrencies("p", "ATS", "EUR");
        ATSCurrency ats;
        using (ConventionProvider.Use(provider))
        {
            ats = new ATSCurrency();
        }

        Assert.Equal("p", TestConventions.MarkOf(ats));
        Assert.Equal("p", TestConventions.MarkOf(ats.TriangulationCurrency!));
        Assert.Equal(["currency:ATS", "currency:EUR"], provider.Resolved);
    }

    [Theory]
    [InlineData("code", "EUX, 978, \"European Euro\"")]
    [InlineData("numeric code", "EUR, 979, \"European Euro\"")]
    [InlineData("name", "EUR, 978, \"Euro\"")]
    public void AProvidersCurrencyMustKeepItsIdentity(string field, string given)
    {
        // The codes and the name are what a currency's equality and the exchange-rate registries read.
        Func<CurrencyData, CurrencyData> change = field switch
        {
            "code" => d => d with { Code = "EUX" },
            "numeric code" => d => d with { NumericCode = 979 },
            _ => d => d with { Name = "Euro" },
        };
        var provider = new TestConventions("p")
        {
            Currencies = new Dictionary<string, Func<CurrencyData, CurrencyData>> { ["EUR"] = change },
        };

        using (ConventionProvider.Use(provider))
        {
            NanoQLException e = Assert.Throws<NanoQLException>(() => new EURCurrency());
            Assert.Equal(
                $"the data for the EUR currency changes its identity: {given} for QuantLib's EUR, 978, \"European Euro\"",
                e.Message);
        }
    }
}
