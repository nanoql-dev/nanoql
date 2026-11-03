// SPDX-FileCopyrightText: Copyright (C) 2009 Dimitri Reiswich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/exotic/analyticcompoundoptionengine.hpp ql/pricingengines/exotic/analyticcompoundoptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for a compound option: Geske's formula, the critical spot at the mother's maturity where the
/// daughter is worth the mother's strike, and a bivariate term for the daughter.
/// </summary>
/// <remarks>
/// <para>
/// Stateless. QuantLib solves its critical spot on the volatility at a date as far from the curves' start as the
/// daughter's maturity from the mother's and on the discounts over that span, then reads the daughter's volatility at
/// its maturity and the mother's at the mother's strike, so it is exact only on flat markets; here Geske's formula
/// reads the variances to each maturity at the daughter's strike, the correlation √(V_M/V_D) and the discounts by date,
/// and the critical spot solves the daughter's forward Black value over the mother's period
/// (<c>Divergences/CompoundTermStructureTests.cs</c>, against the exact integral at the mother's maturity within
/// Brent's accuracy), with QuantLib's solver, bracket and accuracy. The Greeks are the form's: the delta and gamma with
/// the critical spot held, the vega under a parallel shift of the volatilities, and the theta with each date's zero
/// rate and volatility held as the times shorten, where QuantLib's take the vega on the daughter's volatility alone and
/// discount the mother's strike at the daughter's rate in the theta. The bivariate normal is West's, QuantLib's
/// default, where its engine reads Drezner's.
/// </para>
/// <para>
/// Where the daughter is a put whose largest value, K_D·D(T_M, T_D), doesn't exceed the mother's strike, there is no
/// critical spot and QuantLib's Brent refuses ("root not bracketed"); the mother is then never exercised (a call on the
/// put is worth 0) or always (a put on the put is worth K_M·D(T_M) less the put), and those limits are priced
/// (<c>Divergences/CompoundNoCriticalSpotTests.cs</c>). Every structure is read by date, accruing nothing before its
/// own date (<c>Divergences/ExoticMismatchedTimesTests.cs</c>); a date before the process's clock keeps QuantLib's
/// "negative time" refusal.
/// </para>
/// </remarks>
public static class AnalyticCompoundOptionEngine
{
    private static readonly CumulativeNormalDistribution Cum = new();
    private static readonly NormalDistribution Pdf = new();

    /// <summary>Prices a compound option: QuantLib's <c>calculate</c>.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The underlying's process.</param>
    /// <returns>The option's results: the value, delta, gamma, vega and theta.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms are invalid; a payoff isn't plain vanilla; a strike or the spot isn't positive; a date is before
    /// the clock's date; or the critical spot isn't bracketed.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        CompoundOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(option, process));
    }

    // QuantLib's calculate(), on discounts and variances by date.
    private static OneAssetOptionResults Body(CompoundOption option, GeneralizedBlackScholesProcess process)
    {
        PlainVanillaPayoff payoffDaughter = option.DaughterPayoff as PlainVanillaPayoff
            ?? throw new NanoQLException("non-plain payoff given");
        if (!(payoffDaughter.Strike > 0.0))
        {
            throw new NanoQLException("Daughter strike must be positive");
        }

        PlainVanillaPayoff payoffMother = option.Payoff as PlainVanillaPayoff
            ?? throw new NanoQLException("non-plain payoff given");
        if (!(payoffMother.Strike > 0.0))
        {
            throw new NanoQLException("Mother strike must be positive");
        }

        double s = process.X0;
        if (!(s > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        TermStructure clock = FdmBlackScholesOp.Clock(process);
        YieldTermStructure qTS = process.DividendYield, rTS = process.RiskFreeRate;
        BlackVolTermStructure volTS = process.BlackVolatility;
        Date maturityMother = option.Exercise.LastDate();
        Date maturityDaughter = option.DaughterExercise.LastDate();
        double strD = payoffDaughter.Strike;
        double strM = payoffMother.Strike;

        double ddM = qTS.AccruedDiscountOn(clock, maturityMother);
        double rdM = rTS.AccruedDiscountOn(clock, maturityMother);
        double ddD = qTS.AccruedDiscountOn(clock, maturityDaughter);
        double rdD = rTS.AccruedDiscountOn(clock, maturityDaughter);

        // the variances to each maturity, at the daughter's strike
        double sdM = Math.Sqrt(volTS.AccruedVariance(maturityMother, strD));
        double sdD = Math.Sqrt(volTS.AccruedVariance(maturityDaughter, strD));
        double sd12 = Math.Sqrt((sdD * sdD) - (sdM * sdM));

        // the daughter's forward discounts over the mother's period
        double dd12 = ddD / ddM;
        double rd12 = rdD / rdM;

        double phi = (double)payoffDaughter.OptionType; // -1 or 1
        double w = (double)payoffMother.OptionType; // -1 or 1

        // the rates and the volatilities' times the theta holds
        double rD = ForwardVanillaEngine.RateTo(rTS, maturityDaughter, clock);
        double dD = ForwardVanillaEngine.RateTo(qTS, maturityDaughter, clock);
        double rM = ForwardVanillaEngine.RateTo(rTS, maturityMother, clock);
        double tauM = volTS.AccrualTimeTo(maturityMother);
        double tauD = volTS.AccrualTimeTo(maturityDaughter);

        // a put whose largest value doesn't exceed the mother's strike: no critical spot
        if (payoffDaughter.OptionType == OptionType.Put && !(strM < strD * rd12))
        {
            return NoCriticalSpot(payoffMother.OptionType, s, strD, strM, ddD, rdD, rdM, sdD, rM, tauD, clock, maturityDaughter);
        }

        /* Solver Setup ***************************************************/
        double ImpliedSpot(double spot)
        {
            double forwardPrice = spot * dd12 / rd12;
            double value = BlackFormula(payoffDaughter, forwardPrice, sd12, rd12);
            return value - strM;
        }

        Brent solver = new Brent().SetMaxEvaluations(1000);
        const double accuracy = 1.0e-6;
        double sSolved = solver.Solve(ImpliedSpot, accuracy, strD, 1.0e-6, strD * 1000.0);

        // transform stock to return as in Wystup's book
        double x = Math.Log(rdM * sSolved / (s * ddM) * Math.Exp(0.5 * sdM * sdM)) / sdM;
        /* Solver Setup Finished*****************************************/

        double rho = sdM / sdD;
        var n2 = new BivariateCumulativeNormalDistributionWe04DP(w * rho);
        double xmSM = x - sdM;
        double dP = (Math.Log(s * ddD / rdD / strD) / sdD) + (0.5 * sdD);
        double dPT12 = (Math.Log(sSolved * dd12 / rd12 / strD) / sd12) + (0.5 * sd12);
        double dM = dP - sdD;
        double e = ((x * sdD) + (sdM * dM)) / sd12;

        double n2XmSM = n2.Evaluate(-phi * w * xmSM, phi * dP);
        double n2X = n2.Evaluate(-phi * w * x, phi * dM);
        double neX = Cum.Evaluate(-phi * w * e);
        double nx = Cum.Evaluate(-phi * w * x);
        double nT12 = Cum.Evaluate(phi * dPT12);
        double ndP = Pdf.Evaluate(dP);
        double nXm = Pdf.Evaluate(xmSM);

        // the form's partials in the mother's and the daughter's deviations, the critical spot held
        double dValueDsdM = ddD * s * nXm * nT12;
        double dValueDsdD = w * ddD * s * ndP * neX;

        double tempRes = (phi * w * s * ddD * n2XmSM) - (phi * w * strD * rdD * n2X) - (w * strM * rdM * nx);
        double tempDelta = phi * w * ddD * n2XmSM;
        double tempGamma = ((dValueDsdM / sdM) + (dValueDsdD / sdD)) / (s * s);
        double tempVega = (dValueDsdM * Math.Sqrt(tauM)) + (dValueDsdD * Math.Sqrt(tauD));
        double tempTheta = (phi * w * dD * s * ddD * n2XmSM) - (phi * w * rD * strD * rdD * n2X) - (w * rM * strM * rdM * nx);
        tempTheta -= 0.5 * ((dValueDsdM * sdM / tauM) + (dValueDsdD * sdD / tauD));

        return new OneAssetOptionResults
        {
            NPV = tempRes,
            Delta = tempDelta,
            Gamma = tempGamma,
            Vega = tempVega,
            Theta = tempTheta,
        };
    }

    // The limits without a critical spot: a call on the put never exercised, a put on the put always, worth the
    // mother's discounted strike less the put; its Greeks the put's, negated, and the strike's decay.
    private static OneAssetOptionResults NoCriticalSpot(
        OptionType motherType,
        double s,
        double strD,
        double strM,
        double ddD,
        double rdD,
        double rdM,
        double sdD,
        double rM,
        double tauD,
        TermStructure clock,
        Date maturityDaughter)
    {
        if (motherType == OptionType.Call)
        {
            return new OneAssetOptionResults { NPV = 0.0, Delta = 0.0, Gamma = 0.0, Vega = 0.0, Theta = 0.0 };
        }

        var put = new BlackCalculator(OptionType.Put, strD, s * ddD / rdD, sdD, rdD);
        return new OneAssetOptionResults
        {
            NPV = (strM * rdM) - put.Value,
            Delta = -put.Delta(s),
            Gamma = -put.Gamma(s),
            Vega = -put.Vega(tauD),
            Theta = (rM * strM * rdM) - put.Theta(s, clock.TimeFromReference(maturityDaughter)),
        };
    }
}
