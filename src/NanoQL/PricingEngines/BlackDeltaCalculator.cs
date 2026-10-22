// SPDX-FileCopyrightText: Copyright (C) 2010 Dimitri Reiswich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/blackdeltacalculator.hpp ql/pricingengines/blackdeltacalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;
using NanoQL.Quotes;

namespace NanoQL.PricingEngines;

/// <summary>
/// Black-Scholes deltas and strikes under FX markets' delta conventions: the delta of a strike, the strike of a delta,
/// and the at-the-money strike of each convention.
/// </summary>
/// <remarks>
/// Immutable: QuantLib's <c>setDeltaType</c> and <c>setOptionType</c> return copies. QuantLib's <c>cumD1</c>,
/// <c>cumD2</c>, <c>nD1</c> and <c>nD2</c>, deprecated as internal, are private; the header's two solver classes,
/// <c>BlackDeltaPremiumAdjustedSolverClass</c> and <c>BlackDeltaPremiumAdjustedMaxStrikeClass</c>, deprecated as
/// "Obsolete: do not use", aren't ported. Where a premium-adjusted put's delta
/// lies beyond the range of the non-adjusted delta QuantLib starts its solve from (the foreign discount factor for spot
/// deltas, 1 for forward ones), QuantLib refuses it; nanoql starts the solve from the forward on QuantLib's own
/// bracket, a documented divergence.
/// </remarks>
public sealed class BlackDeltaCalculator
{
    private readonly DeltaVolQuoteDeltaType dt;
    private readonly OptionType ot;
    private readonly double dDiscount;
    private readonly double fDiscount;
    private readonly double stdDev;
    private readonly double spot;
    private readonly double forward;
    private readonly int phi;
    private readonly double fExpPos;
    private readonly double fExpNeg;

    /// <summary>Initializes a new instance of the <see cref="BlackDeltaCalculator"/> class.</summary>
    /// <param name="ot">The option type.</param>
    /// <param name="dt">The delta convention.</param>
    /// <param name="spot">The spot FX rate.</param>
    /// <param name="dDiscount">The domestic discount factor.</param>
    /// <param name="fDiscount">The foreign discount factor.</param>
    /// <param name="stdDev">The standard deviation of the underlying, its volatility times the square root of the time
    /// to maturity.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ot"/> or <paramref name="dt"/> isn't a member of
    /// its type.</exception>
    /// <exception cref="ArgumentException">The spot or a discount factor isn't positive, or the standard deviation is
    /// negative.</exception>
    public BlackDeltaCalculator(
        OptionType ot, DeltaVolQuoteDeltaType dt, double spot, double dDiscount, double fDiscount, double stdDev)
    {
        EnumArguments.Check(ot, nameof(ot));
        EnumArguments.Check(dt, nameof(dt));
        this.dt = dt;
        this.ot = ot;
        this.dDiscount = dDiscount;
        this.fDiscount = fDiscount;
        this.stdDev = stdDev;
        this.spot = spot;
        forward = spot * fDiscount / dDiscount;
        phi = (int)ot;
        if (!(this.spot > 0.0))
        {
            throw new ArgumentException($"positive spot value required: {Show(this.spot)} not allowed", nameof(spot));
        }

        if (!(this.dDiscount > 0.0))
        {
            throw new ArgumentException(
                $"positive domestic discount factor required: {Show(this.dDiscount)} not allowed", nameof(dDiscount));
        }

        if (!(this.fDiscount > 0.0))
        {
            throw new ArgumentException(
                $"positive foreign discount factor required: {Show(this.fDiscount)} not allowed", nameof(fDiscount));
        }

        if (!(this.stdDev >= 0.0))
        {
            throw new ArgumentException(
                $"non-negative standard deviation required: {Show(this.stdDev)} not allowed", nameof(stdDev));
        }

        fExpPos = forward * Math.Exp(0.5 * this.stdDev * this.stdDev);
        fExpNeg = forward * Math.Exp(-0.5 * this.stdDev * this.stdDev);
    }

    /// <summary>The delta of a strike, under the calculator's delta convention.</summary>
    /// <param name="strike">The strike.</param>
    /// <returns>The delta.</returns>
    /// <exception cref="ArgumentException">The strike is negative.</exception>
    public double DeltaFromStrike(double strike)
    {
        if (!(strike >= 0.0))
        {
            throw new ArgumentException($"positive strike value required: {Show(strike)} not allowed", nameof(strike));
        }

        return dt switch
        {
            DeltaVolQuoteDeltaType.Spot => phi * fDiscount * CumD1(strike),
            DeltaVolQuoteDeltaType.Fwd => phi * CumD1(strike),
            DeltaVolQuoteDeltaType.PaSpot => phi * fDiscount * CumD2(strike) * strike / forward,
            _ => phi * CumD2(strike) * strike / forward,
        };
    }

    /// <summary>The strike of a delta, under the calculator's delta convention.</summary>
    /// <param name="delta">The delta.</param>
    /// <returns>The strike; for a premium-adjusted call, the strike to the right of the delta's maximum.</returns>
    /// <exception cref="ArgumentException">The delta's sign isn't the option type's, or a spot or forward delta lies
    /// beyond its range (the foreign discount factor, or 1).</exception>
    /// <exception cref="NanoQLException">The solve for a premium-adjusted strike fails.</exception>
    public double StrikeFromDelta(double delta) => StrikeFromDelta(delta, dt);

    /// <summary>The at-the-money strike of a convention.</summary>
    /// <param name="atmT">The at-the-money convention.</param>
    /// <returns>The strike.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="atmT"/> is
    /// <see cref="DeltaVolQuoteAtmType.AtmNull"/> or isn't a member of its type, with QuantLib's "invalid atm
    /// type".</exception>
    /// <exception cref="ArgumentException"><paramref name="atmT"/> is
    /// <see cref="DeltaVolQuoteAtmType.AtmPutCall50"/> and the calculator's deltas aren't forward deltas.</exception>
    public double AtmStrike(DeltaVolQuoteAtmType atmT)
    {
        switch (atmT)
        {
            case DeltaVolQuoteAtmType.AtmSpot:
                return spot;
            case DeltaVolQuoteAtmType.AtmDeltaNeutral:
                return dt is DeltaVolQuoteDeltaType.Spot or DeltaVolQuoteDeltaType.Fwd ? fExpPos : fExpNeg;
            case DeltaVolQuoteAtmType.AtmFwd:
                return forward;
            case DeltaVolQuoteAtmType.AtmGammaMax:
            case DeltaVolQuoteAtmType.AtmVegaMax:
                return fExpPos;
            case DeltaVolQuoteAtmType.AtmPutCall50:
                if (!(dt == DeltaVolQuoteDeltaType.Fwd))
                {
                    throw new ArgumentException(
                        "|PutDelta|=CallDelta=0.50 only possible for forward delta.", nameof(atmT));
                }

                return fExpPos;
            default:
                throw new ArgumentOutOfRangeException(nameof(atmT), atmT, "invalid atm type");
        }
    }

    /// <summary>A copy of the calculator with another delta convention: QuantLib's <c>setDeltaType</c>.</summary>
    /// <param name="deltaType">The delta convention.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaType"/> isn't a member of its
    /// type.</exception>
    public BlackDeltaCalculator SetDeltaType(DeltaVolQuoteDeltaType deltaType) =>
        new(ot, deltaType, spot, dDiscount, fDiscount, stdDev);

    /// <summary>A copy of the calculator with another option type: QuantLib's <c>setOptionType</c>.</summary>
    /// <param name="optionType">The option type.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its
    /// type.</exception>
    public BlackDeltaCalculator SetOptionType(OptionType optionType) =>
        new(optionType, dt, spot, dDiscount, fDiscount, stdDev);

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    // alternative delta type
    private double StrikeFromDelta(double delta, DeltaVolQuoteDeltaType deltaType)
    {
        var invNorm = new InverseCumulativeNormal();
        if (!(delta * phi >= 0.0))
        {
            throw new ArgumentException("Option type and delta are incoherent.", nameof(delta));
        }

        double arg;
        switch (deltaType)
        {
            case DeltaVolQuoteDeltaType.Spot:
                if (!(Math.Abs(delta) <= fDiscount))
                {
                    throw new ArgumentException("Spot delta out of range.", nameof(delta));
                }

                arg = (-phi * invNorm.Evaluate(phi * delta / fDiscount) * stdDev) + (0.5 * stdDev * stdDev);
                return forward * Math.Exp(arg);
            case DeltaVolQuoteDeltaType.Fwd:
                if (!(Math.Abs(delta) <= 1.0))
                {
                    throw new ArgumentException("Forward delta out of range.", nameof(delta));
                }

                arg = (-phi * invNorm.Evaluate(phi * delta) * stdDev) + (0.5 * stdDev * stdDev);
                return forward * Math.Exp(arg);
            default:
                {
                    // This has to be solved numerically. One of the
                    // problems is that the premium adjusted call delta is
                    // not monotonic in strike, such that two solutions
                    // might occur. The one right to the max of the delta is
                    // considered to be the correct strike.  Some proper
                    // interval bounds for the strike need to be chosen, the
                    // numerics can otherwise be very unreliable and
                    // unstable.  I've chosen Brent over Newton, since the
                    // interval can be specified explicitly and we can not
                    // run into the area on the left of the maximum.  The
                    // put delta doesn't have this property and can be
                    // solved without any problems, but also numerically.
                    double F(double strike) => DeltaFromStrike(strike) - delta;
                    Brent solver = new Brent().SetMaxEvaluations(1000);
                    const double Accuracy = 1.0e-10;

                    // Strike of not premium adjusted is always to the right of premium adjusted
                    DeltaVolQuoteDeltaType unadjusted = deltaType == DeltaVolQuoteDeltaType.PaSpot
                        ? DeltaVolQuoteDeltaType.Spot
                        : DeltaVolQuoteDeltaType.Fwd;

                    if (phi < 0)
                    {
                        // if put: a delta beyond the unadjusted delta's range, which QuantLib's guess refuses, is
                        // solved from the forward on QuantLib's bracket
                        double guess = Math.Abs(delta) <= (unadjusted == DeltaVolQuoteDeltaType.Spot ? fDiscount : 1.0)
                            ? StrikeFromDelta(delta, unadjusted)
                            : forward;
                        return solver.Solve(F, Accuracy, guess, 0.0, spot * 100.0);
                    }

                    double rightLimit = StrikeFromDelta(delta, unadjusted);

                    // find out the left limit which is the strike
                    // corresponding to the value where premium adjusted
                    // deltas have their maximum
                    double G(double strike) => (CumD2(strike) * stdDev) - ND2(strike);
                    double leftLimit = solver.Solve(G, Accuracy, rightLimit * 0.5, 0.0, rightLimit);
                    double guessCall = leftLimit + ((rightLimit - leftLimit) * 0.5);
                    return solver.Solve(F, Accuracy, guessCall, leftLimit, rightLimit);
                }
        }
    }

    // N(d1) or N(-d1)
    private double CumD1(double strike)
    {
        double d1;
        double cumD1Pos = 1.0; // N(d1)
        double cumD1Neg = 0.0; // N(-d1)
        var f = new CumulativeNormalDistribution();
        if (stdDev >= NumericLimits.QL_EPSILON)
        {
            if (strike > 0)
            {
                d1 = (Math.Log(forward / strike) / stdDev) + (0.5 * stdDev);
                return f.Evaluate(phi * d1);
            }
        }
        else
        {
            if (forward < strike)
            {
                cumD1Pos = 0.0;
                cumD1Neg = 1.0;
            }
            else if (forward == strike)
            {
                d1 = 0.5 * stdDev;
                return f.Evaluate(phi * d1);
            }
        }

        return phi > 0 ? cumD1Pos : cumD1Neg;
    }

    // N(d2) or N(-d2)
    private double CumD2(double strike)
    {
        double d2;
        double cumD2Pos = 1.0; // N(d2)
        double cumD2Neg = 0.0; // N(-d2)
        var f = new CumulativeNormalDistribution();
        if (stdDev >= NumericLimits.QL_EPSILON)
        {
            if (strike > 0)
            {
                d2 = (Math.Log(forward / strike) / stdDev) - (0.5 * stdDev);
                return f.Evaluate(phi * d2);
            }
        }
        else
        {
            if (forward < strike)
            {
                cumD2Pos = 0.0;
                cumD2Neg = 1.0;
            }
            else if (forward == strike)
            {
                d2 = -0.5 * stdDev;
                return f.Evaluate(phi * d2);
            }
        }

        return phi > 0 ? cumD2Pos : cumD2Neg;
    }

    // n(d2)
    private double ND2(double strike)
    {
        double nD2 = 0.0;
        if (stdDev >= NumericLimits.QL_EPSILON)
        {
            if (strike > 0)
            {
                double d2 = (Math.Log(forward / strike) / stdDev) - (0.5 * stdDev);
                var f = new CumulativeNormalDistribution();
                nD2 = f.Derivative(d2);
            }
        }

        return nD2;
    }
}
