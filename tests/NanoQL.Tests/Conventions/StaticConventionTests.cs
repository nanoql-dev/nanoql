// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The core's registry defaults hold QuantLib's presets whatever provider is current when their types initialize:
/// <see cref="ExchangeRates.Default"/>'s currencies and <see cref="EvaluationContext"/>'s default commodity settings,
/// first read in a fresh copy of the core inside a provider renaming those currencies.
/// </summary>
public class StaticConventionTests
{
    [Fact]
    public void TheRegistryDefaultsAreBuiltUnderQuantLibsConventions()
    {
        string[] names = IsolatedCore.Run<string[]>(typeof(StaticConventionTests).FullName!, nameof(FirstReadInAScope));

        // The currencies built inside the scope are marked; the registry's and the settings' are QuantLib's.
        Assert.Equal(["p", "QuantLib", "QuantLib", "QuantLib"], names);
    }

    /// <summary>
    /// Run in a fresh copy of the core: the first read of the registry defaults, inside a provider marking EUR, DEM and
    /// USD.
    /// </summary>
    /// <returns>The provider a currency built in the scope came from, and the registry's and the settings'.</returns>
    public static string[] FirstReadInAScope()
    {
        using (ConventionProvider.Use(TestConventions.MarkingCurrencies("p", "EUR", "DEM", "USD")))
        {
            var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
            ExchangeRate rate = ExchangeRates.Default.Lookup(new EURCurrency(), new DEMCurrency(), context);
            return
            [
                TestConventions.MarkOf(new EURCurrency()),
                TestConventions.MarkOf(rate.Source!),
                TestConventions.MarkOf(rate.Target!),
                TestConventions.MarkOf(context.CommoditySettings.Currency),
            ];
        }
    }
}
