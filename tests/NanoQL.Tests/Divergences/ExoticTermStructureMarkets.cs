// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The markets and the independent check of the S-exo plan's ER4 facts: a flat market, a rising one (zero rates 1% to
/// 6% and dividends 0 to 3% from three months to a year, volatilities 0.15, 0.30, 0.30 at three months, a year and two)
/// and a falling one (the rates reversed, volatilities 0.30, 0.25, 0.25), the planner's P4b and EP31 markets; and the
/// exact value of an option at its first date under deterministic rates, dividends and volatility, its payoff there
/// integrated over the lognormal spot by Gauss-Lobatto at an absolute accuracy of 1e-13, split at the payoff's kinks.
/// </summary>
internal static class ExoticTermStructureMarkets
{
    internal static readonly Date Today = new(15, Month.October, 2026);
    internal static readonly DayCounter A365 = new Actual365Fixed();
    internal static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    internal static GeneralizedBlackScholesProcess Flat(Real s, Rate r = 0.03, Rate q = 0.0, Volatility v = 0.25) =>
        new BlackScholesMertonProcess(
            s, new FlatForward(Today, q, A365), new FlatForward(Today, r, A365), new BlackConstantVol(Today, new NullCalendar(), v, A365));

    internal static GeneralizedBlackScholesProcess Rising(Real s) => OnNodes(s, Today, [0.01, 0.06], [0.0, 0.03], [0.15, 0.30, 0.30]);

    internal static GeneralizedBlackScholesProcess Falling(Real s) => OnNodes(s, Today, [0.06, 0.01], [0.03, 0.0], [0.30, 0.25, 0.25]);

    // The rising or falling market from another evaluation date, each node's zero rate and volatility held (the bump
    // the compound theta reads), its volatilities shifted in parallel (the vega's).
    internal static GeneralizedBlackScholesProcess Rising(Real s, Date reference, double volShift = 0.0) =>
        OnNodes(s, reference, [0.01, 0.06], [0.0, 0.03], [0.15 + volShift, 0.30 + volShift, 0.30 + volShift]);

    internal static GeneralizedBlackScholesProcess Falling(Real s, Date reference, double volShift = 0.0) =>
        OnNodes(s, reference, [0.06, 0.01], [0.03, 0.0], [0.30 + volShift, 0.25 + volShift, 0.25 + volShift]);

    // The value at a first date t of a payoff g(S_t), D_r(t)·E[g(S_t)], S_t lognormal with the forward and the total
    // variance to t; g split at its kinks (spot levels where it isn't smooth).
    internal static double Exact(GeneralizedBlackScholesProcess p, Date t, Func<double, double> g, params double[] kinks)
    {
        double dr = p.RiskFreeRate.Discount(t);
        double forward = p.X0 * p.DividendYield.Discount(t) / dr;
        double sd = Math.Sqrt(p.BlackVolatility.BlackVariance(t, p.X0));
        double Spot(double z) => forward * Math.Exp((-0.5 * sd * sd) + (sd * z));
        double Integrand(double z) => Math.Exp(-0.5 * z * z) / Math.Sqrt(2.0 * Math.PI) * g(Spot(z));

        var cuts = new List<double> { -12.0, 12.0 };
        foreach (double k in kinks)
        {
            if (k > 0.0)
            {
                double z = (Math.Log(k / forward) + (0.5 * sd * sd)) / sd;
                if (z > -12.0 && z < 12.0)
                {
                    cuts.Add(z);
                }
            }
        }

        cuts.Sort();
        var integrator = new GaussLobattoIntegral(1000000, 1e-13);
        double sum = 0.0;
        for (int i = 1; i < cuts.Count; ++i)
        {
            sum += integrator.Integrate(Integrand, cuts[i - 1], cuts[i]);
        }

        return dr * sum;
    }

    // The Black value at a first date t1 of a vanilla to t2 on the spot then: the forward discounts and variance over
    // (t1, t2), read by date at the strike.
    internal static double ForwardBlack(
        GeneralizedBlackScholesProcess p, OptionType type, double strike, Date t1, Date t2, double spot)
    {
        double dr = p.RiskFreeRate.Discount(t2) / p.RiskFreeRate.Discount(t1);
        double dq = p.DividendYield.Discount(t2) / p.DividendYield.Discount(t1);
        double variance = p.BlackVolatility.BlackVariance(t2, strike) - p.BlackVolatility.BlackVariance(t1, strike);
        return BlackFormula(type, strike, spot * dq / dr, Math.Sqrt(variance), dr);
    }

    // A spot level in (lower, upper) where f changes sign, by bisection to the last bit: where a payoff's branches meet.
    internal static double Kink(Func<double, double> f, double lower, double upper)
    {
        double fl = f(lower);
        for (int i = 0; i < 200; ++i)
        {
            double mid = 0.5 * (lower + upper);
            double fm = f(mid);
            if (Math.Sign(fm) == Math.Sign(fl))
            {
                lower = mid;
                fl = fm;
            }
            else
            {
                upper = mid;
            }
        }

        return 0.5 * (lower + upper);
    }

    private static GeneralizedBlackScholesProcess OnNodes(
        Real s, Date reference, double[] rates, double[] dividends, double[] vols)
    {
        Date[] curveDates = [reference, Today + 90, Today + 365, Today + 3650];
        Date[] volDates = [Today + 90, Today + 365, Today + 730];
        var r = new ZeroCurve(curveDates, [rates[0], rates[0], rates[1], rates[1]], A365);
        var q = new ZeroCurve(curveDates, [dividends[0], dividends[0], dividends[1], dividends[1]], A365);
        var v = new BlackVarianceCurve(reference, volDates, vols, A365);
        return new BlackScholesMertonProcess(s, q, r, v);
    }
}
