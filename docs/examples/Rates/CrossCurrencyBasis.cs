// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The cross-currency basis-swap helpers are experimental: this file opts in, and no other sample does.
#pragma warning disable NQL0001

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Rates;

/// <summary>
/// A euro curve bootstrapped from EUR/USD cross-currency basis swaps on €STR and SOFR, collateralized in dollars:
/// experimental API.
/// </summary>
public static class CrossCurrencyBasis
{
    /// <summary>The example's outputs.</summary>
    /// <returns>Each quoted basis and the basis the bootstrapped curve implies.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        var table = new Table("Tenor", ">Quoted basis", ">Implied by the curve");
        foreach (var (tenor, quote, implied) in Run())
        {
            table.Add(tenor, Table.N(quote * 1e4, 2) + " bp", Table.N(implied * 1e4, 2) + " bp");
        }

        yield return ("cross-currency-basis", table);
    }

    /// <summary>Bootstraps the euro curve from the quotes and reads each quote back.</summary>
    /// <returns>Each tenor's quoted basis and the basis the curve implies.</returns>
    public static IReadOnlyList<(string Tenor, double Quote, double Implied)> Run()
    {
        EvaluationContext context = BasisAndFx.Context;
        // <xccy>
        var estr = new Estr(BasisAndFx.EurDiscount);
        var sofr = new Sofr(BasisAndFx.UsdDiscount);
        (int Years, double Basis)[] quotes = [(1, -0.0008), (2, -0.0010), (3, -0.0012), (5, -0.0013)];
        var helpers = quotes.Select(q => new ConstNotionalCrossCurrencyBasisSwapRateHelper(
            q.Basis, new Period(q.Years, TimeUnit.Years), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
            estr, sofr, BasisAndFx.UsdDiscount, isFxBaseCurrencyCollateralCurrency: false,
            isBasisOnFxBaseCurrencyLeg: true, context, paymentFrequency: Frequency.Quarterly)).ToList();
        var eurCurve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            context.EvaluationDate, helpers, new Actual365Fixed(), context);
        // </xccy>
        return [.. helpers.Select((h, i) => ($"{quotes[i].Years}Y", h.Quote,
            h.SetTermStructure(eurCurve).ImpliedQuote(context)))];
    }
}
