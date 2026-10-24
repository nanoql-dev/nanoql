// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Mario Pucci
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/rangeaccrual.hpp ql/cashflows/rangeaccrual.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// The range-accrual pricer in the BGM model: each observation's range is a pair of digitals on the index's forward
/// rate, the forwards from the coupon's start S and its end T lognormal with the expiry and payment smiles'
/// volatilities and a correlation, priced without the smile, with the smile through a call spread, or with a smile
/// correction.
/// </summary>
/// <remarks>
/// <para>
/// Each observation's digitals are deflated by the discount at the payment date times the fixing at the coupon's start.
/// A trigger at or below ε/2, ε = 1e-8, prices as the deflator, as QuantLib's.
/// </para>
/// <para>
/// A coupon in progress (the S-cms plan's CMR5 (xviii), as ruled): QuantLib's call-spread route and its smile
/// correction take the variance and the drift over the before-fixing phase clamped,
/// max(S, 0)·λS² + min(U − S, U)·λT², and its drift and volatility functions carry the after-fixing branches for
/// S ≤ 0; its no-smile route alone takes S·λS² + (U − S)·λT² unclamped, high on a started coupon when λS &lt; λT and a
/// NaN refusal when λS &gt; λT, so here it is clamped as the other two. And an observation whose fixing is known is
/// counted by its fixing f, deflator × (1{f &gt; l} − 1{f &gt; u}), the two digitals QuantLib's range reads, where no
/// QuantLib route prices a past one: known as QuantLib's index decides it, its fixing date (the observation date less
/// the fixing days) before the evaluation date, or on it once its fixing is published or the context enforces it.
/// Before the coupon's start every route is QuantLib's.
/// </para>
/// </remarks>
public sealed class RangeAccrualPricerByBgm : RangeAccrualPricer
{
    private const double Eps = 1.0e-8;

    private readonly double correlation;
    private readonly bool withSmile;
    private readonly bool byCallSpread;
    private readonly SmileSection smilesOnExpiry;
    private readonly SmileSection smilesOnPayment;

    /// <summary>Initializes a new instance of the <see cref="RangeAccrualPricerByBgm"/> class.</summary>
    /// <param name="correlation">The correlation of the forwards from S and from T.</param>
    /// <param name="smilesOnExpiry">The smile on the forward from S.</param>
    /// <param name="smilesOnPayment">The smile on the forward from T.</param>
    /// <param name="withSmile">Whether the digitals take the smile.</param>
    /// <param name="byCallSpread">Whether, with the smile, a digital is priced as a call spread, else with a smile
    /// correction.</param>
    /// <exception cref="ArgumentNullException">A smile is null.</exception>
    public RangeAccrualPricerByBgm(
        double correlation, SmileSection smilesOnExpiry, SmileSection smilesOnPayment, bool withSmile, bool byCallSpread)
    {
        ArgumentNullException.ThrowIfNull(smilesOnExpiry);
        ArgumentNullException.ThrowIfNull(smilesOnPayment);
        this.correlation = correlation;
        this.withSmile = withSmile;
        this.byCallSpread = byCallSpread;
        this.smilesOnExpiry = smilesOnExpiry;
        this.smilesOnPayment = smilesOnPayment;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The coupon has no observation dates, where QuantLib's price is 0/0 (the S-cms
    /// plan's CMR5 (xiii)); or a digital's price is out of its bounds, with QuantLib's texts.</exception>
    protected override double SwapletPrice(State state, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        if (state.ObservationsNo == 0)
        {
            throw new NanoQLException("no observation dates: the coupon's observation schedule has its start and end alone");
        }

        RangeAccrualFloatersCoupon coupon = state.Coupon;
        var index = (IborIndex)coupon.Index;
        double result = 0.0;
        double deflator = state.Discount * state.InitialValues[0];
        for (int i = 0; i < state.ObservationsNo; i++)
        {
            double initialValue = state.InitialValues[i + 1];
            double digitalFloater = IsKnown(index, coupon.ObservationSchedule.Dates[i + 1], coupon.FixingDays, context)
                ? KnownRangePrice(state.LowerTrigger, state.UpperTrigger, initialValue, deflator)
                : DigitalRangePrice(state, state.LowerTrigger, state.UpperTrigger, initialValue, state.ObservationTimes[i], deflator);
            result += digitalFloater;
        }

        return (state.Gearing * (result * state.AccrualFactor / state.ObservationsNo)) + state.SpreadLegValue;
    }

    // Whether an observation's fixing is known, as QuantLib's InterestRateIndex::fixing decides it.
    private static bool IsKnown(IborIndex index, Date observationDate, int fixingDays, EvaluationContext context)
    {
        Date fixingDate = index.FixingCalendar.Advance(observationDate, -fixingDays, TimeUnit.Days);
        Date today = context.EvaluationDate;
        return fixingDate < today
            || (fixingDate == today && (context.EnforcesTodaysHistoricFixings || index.HasHistoricalFixing(fixingDate, context)));
    }

    // A known observation's range: the two digitals QuantLib's range reads, on the fixing, a trigger at or below ε/2
    // counting as QuantLib's 1.
    private static double KnownRangePrice(double lowerTrigger, double upperTrigger, double fixing, double deflator)
    {
        double lower = lowerTrigger > Eps / 2 ? (fixing > lowerTrigger ? deflator : 0.0) : deflator;
        double upper = upperTrigger > Eps / 2 ? (fixing > upperTrigger ? deflator : 0.0) : deflator;
        return lower - upper;
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    private double[] DriftsOverPeriod(State s, double u, double lambdaS, double lambdaT, double rho)
    {
        double p = (u - s.StartTime) / s.AccrualFactor;
        double q = (s.EndTime - u) / s.AccrualFactor;
        double l0T = s.InitialValues[^1];

        double driftBeforeFixing =
            (p * s.AccrualFactor * l0T / (1.0 + (l0T * s.AccrualFactor)) * ((p * lambdaT * lambdaT) + (q * lambdaS * lambdaT * rho)))
            + (q * lambdaS * lambdaS) + (p * lambdaS * lambdaT * rho)
            - (0.5 * Lambda(s, u, lambdaS, lambdaT) * Lambda(s, u, lambdaS, lambdaT));
        double driftAfterFixing = ((p * s.AccrualFactor * l0T / (1.0 + (l0T * s.AccrualFactor))) - 0.5) * lambdaT * lambdaT;
        return [driftBeforeFixing, driftAfterFixing];
    }

    private static double[] LambdasOverPeriod(State s, double u, double lambdaS, double lambdaT)
    {
        double p = (u - s.StartTime) / s.AccrualFactor;
        double q = (s.EndTime - u) / s.AccrualFactor;
        double lambdaBeforeFixing = (q * lambdaS) + (p * lambdaT);
        double lambdaAfterFixing = lambdaT;
        return [lambdaBeforeFixing, lambdaAfterFixing];
    }

    private static double Lambda(State s, double u, double lambdaS, double lambdaT)
    {
        double p = (u - s.StartTime) / s.AccrualFactor;
        double q = (s.EndTime - u) / s.AccrualFactor;
        return s.StartTime > 0 ? (q * lambdaS) + (p * lambdaT) : lambdaT;
    }

    private static double DerLambdaDerLambdaS(State s, double u) =>
        s.StartTime > 0 ? (s.EndTime - u) / s.AccrualFactor : 0.0;

    private static double DerLambdaDerLambdaT(State s, double u) =>
        s.StartTime > 0 ? (u - s.StartTime) / s.AccrualFactor : 0.0;

    private double DigitalRangePrice(State s, double lowerTrigger, double upperTrigger, double initialValue, double expiry, double deflator)
    {
        double lowerPrice = DigitalPrice(s, lowerTrigger, initialValue, expiry, deflator);
        double upperPrice = DigitalPrice(s, upperTrigger, initialValue, expiry, deflator);
        double result = lowerPrice - upperPrice;
        if (!(result >= 0.0))
        {
            throw new NanoQLException(
                $"RangeAccrualPricerByBgm::digitalRangePrice:\n digitalPrice({Show(upperTrigger)}): {Show(upperPrice)} >  "
                + $"digitalPrice({Show(lowerTrigger)}): {Show(lowerPrice)}");
        }

        return result;
    }

    private double DigitalPrice(State s, double strike, double initialValue, double expiry, double deflator)
    {
        double result = deflator;
        if (strike > Eps / 2)
        {
            result = withSmile
                ? DigitalPriceWithSmile(s, strike, initialValue, expiry, deflator)
                : DigitalPriceWithoutSmile(s, strike, initialValue, expiry, deflator);
        }

        return result;
    }

    private double DigitalPriceWithoutSmile(State s, double strike, double initialValue, double expiry, double deflator)
    {
        double lambdaS = smilesOnExpiry.Volatility(strike);
        double lambdaT = smilesOnPayment.Volatility(strike);

        // the before-fixing phase clamped, as the other two routes clamp it (CMR5 (xviii)); QuantLib's bits before the
        // coupon's start, where the clamps take S and U − S
        double[] lambdaU = LambdasOverPeriod(s, expiry, lambdaS, lambdaT);
        double variance = (Math.Max(s.StartTime, 0.0) * lambdaU[0] * lambdaU[0])
            + (Math.Min(expiry - s.StartTime, expiry) * lambdaU[1] * lambdaU[1]);

        double lambdaSATM = smilesOnExpiry.Volatility(initialValue);
        double lambdaTATM = smilesOnPayment.Volatility(initialValue);

        // drift of Lognormal process (of Libor) "a_U()" nel paper
        double[] muU = DriftsOverPeriod(s, expiry, lambdaSATM, lambdaTATM, correlation);
        double adjustment = (Math.Max(s.StartTime, 0.0) * muU[0]) + (Math.Min(expiry - s.StartTime, expiry) * muU[1]);

        double d2 = (Math.Log(initialValue / strike) + adjustment - (0.5 * variance)) / Math.Sqrt(variance);
        var phi = new CumulativeNormalDistribution();
        double result = deflator * phi.Evaluate(d2);

        if (!(result > 0.0))
        {
            throw new NanoQLException($"RangeAccrualPricerByBgm::digitalPriceWithoutSmile: result< 0. Result:{Show(result)}");
        }

        if (!(result / deflator <= 1.0))
        {
            throw new NanoQLException(
                $"RangeAccrualPricerByBgm::digitalPriceWithoutSmile: result/deflator > 1. Ratio: {Show(result / deflator)} "
                + $"result: {Show(result)} deflator: {Show(deflator)}");
        }

        return result;
    }

    private double DigitalPriceWithSmile(State s, double strike, double initialValue, double expiry, double deflator)
    {
        double result;
        if (byCallSpread)
        {
            // Previous strike
            double previousStrike = strike - (Eps / 2);
            double lambdaS = smilesOnExpiry.Volatility(previousStrike);
            double lambdaT = smilesOnPayment.Volatility(previousStrike);

            // drift of Lognormal process (of Libor) "a_U()" nel paper
            double[] lambdaU = LambdasOverPeriod(s, expiry, lambdaS, lambdaT);
            double previousVariance = (Math.Max(s.StartTime, 0.0) * lambdaU[0] * lambdaU[0])
                + (Math.Min(expiry - s.StartTime, expiry) * lambdaU[1] * lambdaU[1]);

            double lambdaSATM = smilesOnExpiry.Volatility(initialValue);
            double lambdaTATM = smilesOnPayment.Volatility(initialValue);
            double[] muU = DriftsOverPeriod(s, expiry, lambdaSATM, lambdaTATM, correlation);
            double previousAdjustment = Math.Exp((Math.Max(s.StartTime, 0.0) * muU[0]) + (Math.Min(expiry - s.StartTime, expiry) * muU[1]));
            double previousForward = initialValue * previousAdjustment;

            // Next strike
            double nextStrike = strike + (Eps / 2);
            lambdaS = smilesOnExpiry.Volatility(nextStrike);
            lambdaT = smilesOnPayment.Volatility(nextStrike);

            lambdaU = LambdasOverPeriod(s, expiry, lambdaS, lambdaT);
            double nextVariance = (Math.Max(s.StartTime, 0.0) * lambdaU[0] * lambdaU[0])
                + (Math.Min(expiry - s.StartTime, expiry) * lambdaU[1] * lambdaU[1]);

            // drift of Lognormal process (of Libor) "a_U()" nel paper
            muU = DriftsOverPeriod(s, expiry, lambdaSATM, lambdaTATM, correlation);
            double nextAdjustment = Math.Exp((Math.Max(s.StartTime, 0.0) * muU[0]) + (Math.Min(expiry - s.StartTime, expiry) * muU[1]));
            double nextForward = initialValue * nextAdjustment;

            result = CallSpreadPrice(previousForward, nextForward, previousStrike, nextStrike, deflator, previousVariance, nextVariance);
        }
        else
        {
            result = DigitalPriceWithoutSmile(s, strike, initialValue, expiry, deflator)
                + SmileCorrection(s, strike, initialValue, expiry, deflator);
        }

        if (!(result > -Math.Pow(Eps, 0.5)))
        {
            throw new NanoQLException($"RangeAccrualPricerByBgm::digitalPriceWithSmile: result< 0 Result:{Show(result)}");
        }

        if (!(result / deflator <= 1.0 + Math.Pow(Eps, 0.2)))
        {
            throw new NanoQLException(
                $"RangeAccrualPricerByBgm::digitalPriceWithSmile: result/deflator > 1. Ratio: {Show(result / deflator)} "
                + $"result: {Show(result)} deflator: {Show(deflator)}");
        }

        return result;
    }

    private double SmileCorrection(State s, double strike, double forward, double expiry, double deflator)
    {
        double previousStrike = strike - (Eps / 2);
        double nextStrike = strike + (Eps / 2);

        double derSmileS = (smilesOnExpiry.Volatility(nextStrike) - smilesOnExpiry.Volatility(previousStrike)) / Eps;
        double derSmileT = (smilesOnPayment.Volatility(nextStrike) - smilesOnPayment.Volatility(previousStrike)) / Eps;

        double lambdaS = smilesOnExpiry.Volatility(strike);
        double lambdaT = smilesOnPayment.Volatility(strike);

        double derLambdaDerK = (DerLambdaDerLambdaS(s, expiry) * derSmileS) + (DerLambdaDerLambdaT(s, expiry) * derSmileT);

        double lambdaSATM = smilesOnExpiry.Volatility(forward);
        double lambdaTATM = smilesOnPayment.Volatility(forward);
        double[] lambdasOverPeriodU = LambdasOverPeriod(s, expiry, lambdaS, lambdaT);

        // drift of Lognormal process (of Libor) "a_U()" nel paper
        double[] muU = DriftsOverPeriod(s, expiry, lambdaSATM, lambdaTATM, correlation);

        double variance = (Math.Max(s.StartTime, 0.0) * lambdasOverPeriodU[0] * lambdasOverPeriodU[0])
            + (Math.Min(expiry - s.StartTime, expiry) * lambdasOverPeriodU[1] * lambdasOverPeriodU[1]);

        double forwardAdjustment = Math.Exp((Math.Max(s.StartTime, 0.0) * muU[0]) + (Math.Min(expiry - s.StartTime, expiry) * muU[1]));
        double forwardAdjusted = forward * forwardAdjustment;

        double d1 = (Math.Log(forwardAdjusted / strike) + (0.5 * variance)) / Math.Sqrt(variance);

        double sqrtOfTimeToExpiry = ((Math.Max(s.StartTime, 0.0) * lambdasOverPeriodU[0])
                + (Math.Min(expiry - s.StartTime, expiry) * lambdasOverPeriodU[1]))
            * (1.0 / Math.Sqrt(variance));

        var psi = new NormalDistribution();
        double result = -forwardAdjusted * psi.Evaluate(d1) * sqrtOfTimeToExpiry * derLambdaDerK;

        result *= deflator;

        if (!(Math.Abs(result / deflator) <= 1.0 + Math.Pow(Eps, 0.2)))
        {
            throw new NanoQLException(
                $"RangeAccrualPricerByBgm::smileCorrection: abs(result/deflator) > 1. Ratio: {Show(result / deflator)} "
                + $"result: {Show(result)} deflator: {Show(deflator)}");
        }

        return result;
    }

    private static double CallSpreadPrice(
        double previousForward,
        double nextForward,
        double previousStrike,
        double nextStrike,
        double deflator,
        double previousVariance,
        double nextVariance)
    {
        double nextCall = BlackFormulaFunctions.BlackFormula(OptionType.Call, nextStrike, nextForward, Math.Sqrt(nextVariance), deflator);
        double previousCall =
            BlackFormulaFunctions.BlackFormula(OptionType.Call, previousStrike, previousForward, Math.Sqrt(previousVariance), deflator);

        if (!(nextCall < previousCall))
        {
            throw new NanoQLException(
                "RangeAccrualPricerByBgm::callSpreadPrice: nextCall > previousCall"
                + $"\n nextCall: strike :{Show(nextStrike)}; variance: {Show(nextVariance)} adjusted initial value {Show(nextForward)}"
                + $"\n previousCall: strike :{Show(previousStrike)}; variance: {Show(previousVariance)} adjusted initial value "
                + $"{Show(previousForward)}");
        }

        return (previousCall - nextCall) / (nextStrike - previousStrike);
    }
}
