// SPDX-FileCopyrightText: Copyright (C) 2014 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/analyticpartialtimebarrieroptionengine.hpp ql/pricingengines/barrier/analyticpartialtimebarrieroptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for partial-time barrier options: Haug's formulas ("Option Pricing Formulas"), a put priced by
/// the put-call symmetry from the call.
/// </summary>
/// <remarks>
/// <para>
/// The value alone, as QuantLib's. The bivariate normal is QuantLib's default,
/// <see cref="BivariateCumulativeNormalDistributionWe04DP"/>, where QuantLib's engine reads Drezner's 1978 algorithm,
/// to six decimal places (the S-bar plan's BR4 (e), the S-exo plan's ER7); on QuantLib's 40 cases the values move by at
/// most 3.3e-5, within QuantLib's tolerance. The put's symmetric call runs on a process whose curves QuantLib's
/// constructor call binds in each other's places, the standard put-call symmetry P(S, K, r, q) = (K/S)·C(S, S²/K, q, r),
/// and prices a start-window knock-in's European on that call's payoff and process, upstream's fix after v1.44 (#2876,
/// <c>Divergences/BarrierUpstreamFixesTests.cs</c>).
/// </para>
/// <para>
/// Documented divergences (BR5). Each rate is read on the volatility's clock, the curve's zero rate times its own time
/// over the volatility's, and each time is the volatility's, as <see cref="AnalyticBarrierEngine"/> reads them, where
/// QuantLib reads every structure at the risk-free curve's times (BR5 (i),
/// <c>Divergences/BarrierClosedFormMismatchedTimesTests.cs</c>). A start-window knock-out's rebate, paid at the hit
/// before the cover event, is priced as the analytic barrier engine prices a knock-out's rebate to the cover event,
/// where QuantLib's engine ignores the rebate; no formula covers the end windows' or the knock-ins' rebates, which are
/// refused (BR5 (xii)). A cover event on the evaluation date takes the formulas' limit: the start window's, the vanilla
/// for a knock-out and 0 for a knock-in; the end windows', the standard barrier option through the analytic barrier
/// engine, where QuantLib's formulas take infinite arguments (BR5 (xiii)). A start-window option whose spot lies beyond
/// the barrier is refused ("barrier touched"), as the family's other engines refuse it, where QuantLib's prices it
/// (BR5 (xiv)). The end-window knock-ins, which QuantLib refuses, are priced by in-out parity on QuantLib's European
/// and end-window knock-out, the parity QuantLib applies to the start window's (the S-bar plan's BR8 (a)). And a
/// non-European exercise is refused, as the family's European engines refuse it, where QuantLib prices it to its last
/// date and its start-window knock-in crashes (<c>Divergences/PartialTimeExerciseTests.cs</c>).
/// </para>
/// <para>
/// The formulas are first-passage closed forms in equivalent-constant rates and volatilities: no exact closed form
/// exists under general deterministic term structures, and on shaped markets they miss an independent Monte Carlo by
/// up to +3.9% (BR4 (d)).
/// </para>
/// </remarks>
public static class AnalyticPartialTimeBarrierOptionEngine
{
    /// <summary>Prices a partial-time barrier option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results: the value; the expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European; the payoff isn't plain vanilla or its strike isn't positive; the spot isn't
    /// positive; the cover event is before the evaluation date ("negative time"); a start-window spot lies beyond the
    /// barrier ("barrier touched"); a rebate is given for an end window or a knock-in; an end-window B2 option is
    /// struck beyond its barrier, as QuantLib refuses it.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        PartialTimeBarrierOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new OneAssetOptionResults { NPV = Value(option, process) });
    }

    // QuantLib's calculate(), with BR5 (xii)–(xiv) and BR8 (a)
    private static double Value(PartialTimeBarrierOption option, GeneralizedBlackScholesProcess process)
    {
        // QuantLib prices any exercise to its last date, its start-window knock-in crashing on an American one: the
        // formulas price a European exercise only (the S-bar code review's B1-3)
        if (option.Exercise.Type != ExerciseType.European)
        {
            throw new NanoQLException("only european style option are supported");
        }

        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        if (!(payoff.Strike > 0.0))
        {
            throw new NanoQLException("strike must be positive");
        }

        double spot = process.X0;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        BarrierType barrierType = option.BarrierType;
        PartialBarrierRange range = option.BarrierRange;
        bool knockIn = barrierType is BarrierType.DownIn or BarrierType.UpIn;
        bool down = barrierType is BarrierType.DownIn or BarrierType.DownOut;
        double barrier = option.Barrier;

        // no formula reads a rebate but a start-window knock-out's (BR5 (xii))
        bool startKnockOut = range == PartialBarrierRange.Start && !knockIn;
        if (option.Rebate != 0.0 && !startKnockOut)
        {
            throw new NanoQLException("rebate not supported for this partial-time barrier");
        }

        // a cover event before the evaluation date is refused as QuantLib's read of its volatility refuses it
        BlackVolTermStructure volatility = process.BlackVolatility;
        _ = volatility.BlackVol(option.CoverEventDate, payoff.Strike);

        // a start-window spot beyond the barrier is already touched (BR5 (xiv))
        if (range == PartialBarrierRange.Start && (down ? spot < barrier : spot > barrier))
        {
            throw new NanoQLException("barrier touched");
        }

        double value = volatility.TimeFromReference(option.CoverEventDate) == 0.0
            ? CoverEventToday(option, process, payoff, spot)
            : payoff.OptionType == OptionType.Put ? PutBySymmetry(option, process, payoff, spot)
            : new Formulas(option, payoff, process, process).Value(barrierType, range, barrier, payoff.Strike);

        if (option.Rebate != 0.0)
        {
            // a start-window knock-out's rebate, paid at the hit before the cover event, as the analytic barrier engine
            // prices a knock-out's rebate to the cover event (BR5 (xii))
            var toCover = new BarrierOption(
                down ? BarrierType.DownOut : BarrierType.UpOut,
                barrier,
                option.Rebate,
                payoff,
                new EuropeanExercise(option.CoverEventDate));
            value += AnalyticBarrierEngine.RebateValue(toCover, process, payoff.Strike);
        }

        return value;
    }

    // QuantLib's put through the symmetric call: P(S, K, r, q) = (K/S)·C(S, S²/K, q, r), on a process whose curves
    // QuantLib's constructor call binds in each other's places
    private static double PutBySymmetry(
        PartialTimeBarrierOption option, GeneralizedBlackScholesProcess process, PlainVanillaPayoff payoff, double spot)
    {
        double spotSq = spot * spot;
        double callStrike = spotSq / payoff.Strike;
        var callPayoff = new PlainVanillaPayoff(OptionType.Call, callStrike);
        BarrierType symmetricType = option.BarrierType switch
        {
            BarrierType.UpIn => BarrierType.DownIn,
            BarrierType.DownIn => BarrierType.UpIn,
            BarrierType.UpOut => BarrierType.DownOut,
            _ => BarrierType.UpOut,
        };
        var callProcess = new GeneralizedBlackScholesProcess(
            process.X0, process.RiskFreeRate, process.DividendYield, process.BlackVolatility);
        double call = new Formulas(option, callPayoff, process, callProcess)
            .Value(symmetricType, option.BarrierRange, spotSq / option.Barrier, callStrike);
        return payoff.Strike / spot * call;
    }

    // The limit at a cover event on the evaluation date (BR5 (xiii)): the start window monitors only the spot, so a
    // knock-out is the vanilla and a knock-in 0 (the spot beyond the barrier refused before, BR5 (xiv)); the end
    // windows monitor the barrier to expiry, so B1 is the standard knock-out on the spot's side of the barrier and B2 the
    // standard knock-out on its own side, out at once with the spot beyond it; the end-window knock-ins by parity
    private static double CoverEventToday(
        PartialTimeBarrierOption option, GeneralizedBlackScholesProcess process, PlainVanillaPayoff payoff, double spot)
    {
        double barrier = option.Barrier;
        bool knockIn = option.BarrierType is BarrierType.DownIn or BarrierType.UpIn;
        bool down = option.BarrierType is BarrierType.DownIn or BarrierType.DownOut;
        double european = European(payoff, option.Exercise, process);
        double knockOut;
        if (option.BarrierRange == PartialBarrierRange.Start)
        {
            // the spot on the barrier is a hit now
            knockOut = spot == barrier ? 0.0 : european;
        }
        else
        {
            BarrierType? standard;
            if (option.BarrierRange == PartialBarrierRange.EndB1)
            {
                standard = spot > barrier ? BarrierType.DownOut : spot < barrier ? BarrierType.UpOut : null;
            }
            else
            {
                standard = down
                    ? (spot < barrier ? null : BarrierType.DownOut)
                    : (spot > barrier ? null : BarrierType.UpOut);
            }

            knockOut = standard is BarrierType type
                ? AnalyticBarrierEngine.Body(new BarrierOption(type, barrier, 0.0, payoff, option.Exercise), process).NPV!.Value
                : 0.0;
        }

        return knockIn ? european - knockOut : knockOut;
    }

    // A European vanilla on a process: QuantLib's AnalyticEuropeanEngine on its risk-free curve
    private static double European(PlainVanillaPayoff payoff, Exercise exercise, GeneralizedBlackScholesProcess process) =>
        AnalyticEuropeanEngine.Body(new VanillaOption(payoff, exercise), process, process.RiskFreeRate).NPV!.Value;

    // QuantLib's members on the volatility's clock: each time the volatility's, each volatility read at its date, each
    // rate re-expressed on it (BR5 (i)). The call's process gives the rates (the put's symmetric call's, its curves in
    // each other's places), the option's process the spot, the times and the volatilities, as in QuantLib.
    private sealed class Formulas
    {
        private readonly PartialTimeBarrierOption option;
        private readonly GeneralizedBlackScholesProcess process;
        private readonly GeneralizedBlackScholesProcess callProcess;
        private readonly PlainVanillaPayoff callPayoff;
        private readonly BlackVolTermStructure volatility;
        private readonly double s;
        private readonly double t1;
        private readonly double t2;

        internal Formulas(
            PartialTimeBarrierOption option,
            PlainVanillaPayoff callPayoff,
            GeneralizedBlackScholesProcess process,
            GeneralizedBlackScholesProcess callProcess)
        {
            this.option = option;
            this.process = process;
            this.callProcess = callProcess;
            this.callPayoff = callPayoff;
            volatility = process.BlackVolatility;
            s = process.X0;
            t1 = volatility.TimeFromReference(option.CoverEventDate);
            t2 = volatility.TimeFromReference(option.Exercise.LastDate());
        }

        // QuantLib's calculate(arguments, payoff, process), with the end-window knock-ins by parity (BR8 (a))
        internal double Value(BarrierType barrierType, PartialBarrierRange barrierRange, double barrier, double strike)
        {
            Date lastDate = option.Exercise.LastDate();
            double r = AnalyticBarrierEngine.RateOnVolatilityClock(callProcess.RiskFreeRate, volatility, lastDate);
            double q = AnalyticBarrierEngine.RateOnVolatilityClock(callProcess.DividendYield, volatility, lastDate);

            return barrierType switch
            {
                BarrierType.DownOut => barrierRange switch
                {
                    PartialBarrierRange.Start => CA(1, barrier, strike, r, q),
                    PartialBarrierRange.EndB1 => CoB1(barrier, strike, r, q),
                    _ => CoB2(BarrierType.DownOut, barrier, strike, r, q),
                },
                BarrierType.DownIn => barrierRange switch
                {
                    PartialBarrierRange.Start => CIA(1, barrier, strike, r, q),
                    PartialBarrierRange.EndB1 => CallEuropean() - CoB1(barrier, strike, r, q),
                    _ => CallEuropean() - CoB2(BarrierType.DownOut, barrier, strike, r, q),
                },
                BarrierType.UpOut => barrierRange switch
                {
                    PartialBarrierRange.Start => CA(-1, barrier, strike, r, q),
                    PartialBarrierRange.EndB1 => CoB1(barrier, strike, r, q),
                    _ => CoB2(BarrierType.UpOut, barrier, strike, r, q),
                },
                _ => barrierRange switch
                {
                    PartialBarrierRange.Start => CIA(-1, barrier, strike, r, q),
                    PartialBarrierRange.EndB1 => CallEuropean() - CoB1(barrier, strike, r, q),
                    _ => CallEuropean() - CoB2(BarrierType.UpOut, barrier, strike, r, q),
                },
            };
        }

        // the call's European on the call's payoff and process (#2876)
        private double CallEuropean() => European(callPayoff, option.Exercise, callProcess);

        private double CoB2(BarrierType barrierType, double barrier, double strike, double r, double q)
        {
            double result;
            double b = r - q;
            double bigT = t2;
            double mu = Mu(strike, b);
            double g1 = G1(barrier, strike, b);
            double g2 = G2(barrier, strike, b);
            double g3 = G3(barrier, strike, b);
            double g4 = G4(barrier, strike, b);
            double e1 = E1(barrier, strike, b);
            double e2 = E2(barrier, strike, b);
            double e3 = E3(barrier, strike, b);
            double e4 = E4(barrier, strike, b);
            double rho = Rho();
            double hsMu = HS(s, barrier, 2 * mu);
            double hsMu1 = HS(s, barrier, 2 * (mu + 1));
            double x1 = strike * Math.Exp(-r * bigT);

            if (strike < barrier)
            {
                switch (barrierType)
                {
                    case BarrierType.DownOut:
                        result = s * Math.Exp((b - r) * bigT);
                        result *= M(g1, e1, rho) - (hsMu1 * M(g3, -e3, -rho));
                        result -= x1 * (M(g2, e2, rho) - (hsMu * M(g4, -e4, -rho)));
                        return result;

                    case BarrierType.UpOut:
                        result = s * Math.Exp((b - r) * bigT);
                        result *= M(-g1, -e1, rho) - (hsMu1 * M(-g3, e3, -rho));
                        result -= x1 * (M(-g2, -e2, rho) - (hsMu * M(-g4, e4, -rho)));
                        result -= s * Math.Exp((b - r) * bigT)
                            * (M(-D1(strike, b), -e1, rho) - (hsMu1 * M(e3, -F1(barrier, strike, b), -rho)));
                        result += x1 * (M(-D2(strike, b), -e2, rho) - (hsMu * M(e4, -F2(barrier, strike, b), -rho)));
                        return result;

                    default:
                        throw new NanoQLException("invalid barrier type");
                }
            }

            throw new NanoQLException("case of strike>barrier is not implemented for OutEnd B2 type");
        }

        private double CoB1(double barrier, double strike, double r, double q)
        {
            double result;
            double b = r - q;
            double bigT = t2;
            double mu = Mu(strike, b);
            double g1 = G1(barrier, strike, b);
            double g2 = G2(barrier, strike, b);
            double g3 = G3(barrier, strike, b);
            double g4 = G4(barrier, strike, b);
            double e1 = E1(barrier, strike, b);
            double e2 = E2(barrier, strike, b);
            double e3 = E3(barrier, strike, b);
            double e4 = E4(barrier, strike, b);
            double rho = Rho();
            double hsMu = HS(s, barrier, 2 * mu);
            double hsMu1 = HS(s, barrier, 2 * (mu + 1));
            double x1 = strike * Math.Exp(-r * bigT);

            if (strike > barrier)
            {
                result = s * Math.Exp((b - r) * bigT);
                result *= M(D1(strike, b), e1, rho) - (hsMu1 * M(F1(barrier, strike, b), -e3, -rho));
                result -= x1 * (M(D2(strike, b), e2, rho) - (hsMu * M(F2(barrier, strike, b), -e4, -rho)));
                return result;
            }

            double s1 = s * Math.Exp((b - r) * bigT);
            result = s1;
            result *= M(-g1, -e1, rho) - (hsMu1 * M(-g3, e3, -rho));
            result -= x1 * (M(-g2, -e2, rho) - (hsMu * M(-g4, e4, -rho)));
            result -= s1 * (M(-D1(strike, b), -e1, rho) - (hsMu1 * M(-F1(barrier, strike, b), e3, -rho)));
            result += x1 * (M(-D2(strike, b), -e2, rho) - (hsMu * M(-F2(barrier, strike, b), e4, -rho)));
            result += s1 * (M(g1, e1, rho) - (hsMu1 * M(g3, -e3, -rho)));
            result -= x1 * (M(g2, e2, rho) - (hsMu * M(g4, -e4, -rho)));
            return result;
        }

        // eta = -1: Up-and-In Call
        // eta =  1: Down-and-In Call
        private double CIA(int eta, double barrier, double strike, double r, double q) =>
            CallEuropean() - CA(eta, barrier, strike, r, q);

        private double CA(int eta, double barrier, double strike, double r, double q)
        {
            // Partial-Time-Start- OUT  Call Option calculation
            double b = r - q;
            double rho = Rho();
            double bigT = t2;
            double mu = Mu(strike, b);
            double e1 = E1(barrier, strike, b);
            double e2 = E2(barrier, strike, b);
            double e3 = E3(barrier, strike, b);
            double e4 = E4(barrier, strike, b);
            double hsMu = HS(s, barrier, 2 * mu);
            double hsMu1 = HS(s, barrier, 2 * (mu + 1));

            double result = s * Math.Exp((b - r) * bigT);
            result *= M(D1(strike, b), eta * e1, eta * rho)
                - (hsMu1 * M(F1(barrier, strike, b), eta * e3, eta * rho));
            result -= strike * Math.Exp(-r * bigT)
                * (M(D2(strike, b), eta * e2, eta * rho) - (hsMu * M(F2(barrier, strike, b), eta * e4, eta * rho)));
            return result;
        }

        // QuantLib's volatility(coverEventTime(), strike) and volatility(residualTime(), strike), each read at its date
        private double VolatilityAtCover(double strike) => volatility.BlackVol(option.CoverEventDate, strike);

        private double VolatilityAtExpiry(double strike) => volatility.BlackVol(option.Exercise.LastDate(), strike);

        private double F1(double barrier, double strike, double b)
        {
            double sigma = VolatilityAtExpiry(strike);
            return (Math.Log(s / strike) + (2 * Math.Log(barrier / s)) + ((b + (Math.Pow(sigma, 2) / 2)) * t2))
                / (sigma * Math.Sqrt(t2));
        }

        private double F2(double barrier, double strike, double b) =>
            F1(barrier, strike, b) - (VolatilityAtExpiry(strike) * Math.Sqrt(t2));

        // QuantLib's M, on We04DP where QuantLib's reads Dr78 (BR4 (e))
        private static double M(double a, double b, double rho) =>
            new BivariateCumulativeNormalDistributionWe04DP(rho).Evaluate(a, b);

        private double Rho() => Math.Sqrt(t1 / t2);

        private double Mu(double strike, double b)
        {
            double vol = VolatilityAtCover(strike);
            return (b - (vol * vol / 2)) / (vol * vol);
        }

        private double D1(double strike, double b)
        {
            double vol = VolatilityAtExpiry(strike);
            return (Math.Log(s / strike) + ((b + (vol * vol / 2)) * t2)) / (Math.Sqrt(t2) * vol);
        }

        private double D2(double strike, double b) => D1(strike, b) - (VolatilityAtExpiry(strike) * Math.Sqrt(t2));

        private double E1(double barrier, double strike, double b)
        {
            double vol = VolatilityAtCover(strike);
            return (Math.Log(s / barrier) + ((b + (vol * vol / 2)) * t1)) / (Math.Sqrt(t1) * vol);
        }

        private double E2(double barrier, double strike, double b) =>
            E1(barrier, strike, b) - (VolatilityAtCover(strike) * Math.Sqrt(t1));

        private double E3(double barrier, double strike, double b)
        {
            double vol = VolatilityAtCover(strike);
            return E1(barrier, strike, b) + (2 * Math.Log(barrier / s) / (vol * Math.Sqrt(t1)));
        }

        private double E4(double barrier, double strike, double b) =>
            E3(barrier, strike, b) - (VolatilityAtCover(strike) * Math.Sqrt(t1));

        private double G1(double barrier, double strike, double b)
        {
            double vol = VolatilityAtExpiry(strike);
            return (Math.Log(s / barrier) + ((b + (vol * vol / 2)) * t2)) / (Math.Sqrt(t2) * vol);
        }

        private double G2(double barrier, double strike, double b) =>
            G1(barrier, strike, b) - (VolatilityAtExpiry(strike) * Math.Sqrt(t2));

        private double G3(double barrier, double strike, double b)
        {
            double vol = VolatilityAtExpiry(strike);
            return G1(barrier, strike, b) + (2 * Math.Log(barrier / s) / (vol * Math.Sqrt(t2)));
        }

        private double G4(double barrier, double strike, double b) =>
            G3(barrier, strike, b) - (VolatilityAtExpiry(strike) * Math.Sqrt(t2));

        private static double HS(double s, double h, double power) => Math.Pow(h / s, power);
    }
}
