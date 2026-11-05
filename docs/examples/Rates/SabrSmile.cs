// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/swaptionvolstructuresutilities.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Rates;

/// <summary>
/// A SABR smile: the 1Y into 10Y swaption smile of QuantLib's swaption cube tests' market data, an at-the-money vol
/// and the spreads to it at four other strikes, fitted by SABR with β fixed, and the smile section an engine reads.
/// </summary>
public static class SabrSmile
{
    /// <summary>Gets the example's evaluation context: 15 October 2026.</summary>
    public static EvaluationContext Context { get; } = new() { EvaluationDate = new Date(15, Month.October, 2026) };

    /// <summary>The smile and its fit.</summary>
    /// <param name="Forward">The forward swap rate.</param>
    /// <param name="Expiry">The time to the option's expiry, in years.</param>
    /// <param name="Strikes">The quoted strikes.</param>
    /// <param name="Quotes">The quoted Black vols.</param>
    /// <param name="Sabr">The fitted SABR interpolation.</param>
    /// <param name="Section">The smile section on the fitted parameters.</param>
    public sealed record Fit(
        double Forward,
        double Expiry,
        IReadOnlyList<double> Strikes,
        IReadOnlyList<double> Quotes,
        SABRInterpolation Sabr,
        SabrSmileSection Section);

    /// <summary>The example's outputs.</summary>
    /// <returns>The fitted parameters, and the smile against the quotes.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        Fit fit = Calibrate();
        var parameters = new Table("SABR", ">Value");
        parameters.Add("Forward", Table.Percent(fit.Forward, 4));
        parameters.Add("α", Table.N(fit.Sabr.Alpha, 6));
        parameters.Add("β (fixed)", Table.N(fit.Sabr.Beta, 1));
        parameters.Add("ν", Table.N(fit.Sabr.Nu, 6));
        parameters.Add("ρ", Table.N(fit.Sabr.Rho, 6));
        parameters.Add("Root-mean-square error", Table.Percent(fit.Sabr.RmsError, 4));
        parameters.Add("Maximum error", Table.Percent(fit.Sabr.MaxError, 4));

        var smile = new Table(">Strike against the forward", ">Strike", ">Quote", ">SABR");
        for (int bp = -200; bp <= 200; bp += 50)
        {
            double strike = fit.Forward + bp * 1e-4;
            int quoted = fit.Strikes.ToList().FindIndex(k => Math.Abs(k - strike) < 1e-12);
            smile.Add($"{bp:+0;-0;0} bp", Table.Percent(strike, 4), quoted >= 0 ? Table.Percent(fit.Quotes[quoted], 2) : "",
                Table.Percent(fit.Section.Volatility(strike), 4));
        }

        yield return ("sabr-parameters", parameters);
        yield return ("sabr-smile", smile);
    }

    /// <summary>Fits SABR to the smile.</summary>
    /// <returns>The smile, its fit and the smile section.</returns>
    public static Fit Calibrate()
    {
        EvaluationContext context = Context;
        Date today = context.EvaluationDate;
        // <smile>
        // A flat 5% curve, and the ten-year swap the option is on.
        YieldTermStructure curve = new FlatForward(today, 0.05, new Actual365Fixed());
        var swapIndex = new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), curve);
        Date optionDate = new TARGET().Advance(today, new Period(1, TimeUnit.Years), BusinessDayConvention.ModifiedFollowing);
        double expiry = new Actual365Fixed().YearFraction(today, optionDate);
        double forward = swapIndex.Fixing(optionDate, context);

        // The market's quotes: 14.70% at the money, and the spreads to it at four strikes about the forward.
        double[] strikeSpreads = [-0.020, -0.005, 0.0, 0.005, 0.020];
        double[] volSpreads = [0.0729, 0.0086, 0.0, -0.0024, 0.0098];
        double[] strikes = [.. strikeSpreads.Select(s => forward + s)];
        double[] quotes = [.. volSpreads.Select(s => 0.1470 + s)];
        // </smile>

        // <fit>
        // β fixed at 0.5; α, ν and ρ fitted from SABR's default guesses, the errors weighted by the strikes' vegas.
        var sabr = new SABRInterpolation(strikes, quotes, expiry, forward, alpha: null, beta: 0.5, nu: null, rho: null,
            alphaIsFixed: false, betaIsFixed: true, nuIsFixed: false, rhoIsFixed: false);
        // </fit>

        // <section>
        // The fitted parameters as a smile section, the smile an engine or a cube reads at any strike.
        var section = new SabrSmileSection(expiry, forward, [sabr.Alpha, sabr.Beta, sabr.Nu, sabr.Rho]);
        // </section>
        return new Fit(forward, expiry, strikes, quotes, sabr, section);
    }
}
