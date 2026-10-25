// SPDX-FileCopyrightText: Copyright (C) 2020 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/exponentialfittinghestonengine.hpp ql/pricingengines/vanilla/exponentialfittinghestonengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic Heston engine on an exponentially fitted Gauss-Laguerre quadrature: the integrand of
/// <see cref="AnalyticHestonEngineAPHelper"/>'s control variate summed on 64 nodes fitted to the option's moneyness.
/// </summary>
/// <remarks>
/// <para>
/// An immutable object over its <see cref="HestonModel"/>, the control variate, the scaling and the damping exponent,
/// holding QuantLib's order-1 <see cref="AnalyticHestonEngine"/> for the control variate (the S-hes plan's HD7); the
/// static <c>Calculate</c> builds one. The node table is QuantLib's, generated (HD15). The model's time runs from the
/// earliest curve date and the forward and discount are read by date (HR7 (xi)); at the model's time 0 the price is the
/// forward's discounted intrinsic value, where QuantLib's average variance is 0/0 and it throws (HR7 (xxxi)); and below
/// κt = 1e-2 the average variance's factor (1 − e^{−κt})/(κt) is −expm1(−κt)/(κt), where QuantLib's loses the
/// (v₀ − θ) term as 1 − e^{−κt} rounds to 0 (HR7 (xxv)).
/// </para>
/// <para>
/// References: D. Conte, L. Ixaru, B. Paternoster, G. Santomauro, Exponentially-fitted Gauss–Laguerre quadrature rule
/// for integrals over an unbounded interval, 2014; for the adaptation, https://hpcquantlib.wordpress.com/2020/05/17/
/// optimized-heston-model-integration-exponentially-fitted-gauss-laguerre-quadrature-rule/.
/// </para>
/// </remarks>
public sealed class ExponentialFittingHestonEngine
{
    private readonly HestonModel model;
    private readonly AnalyticHestonEngineComplexLogFormula cv;
    private readonly double? scaling;
    private readonly double alpha;
    private readonly AnalyticHestonEngine analyticEngine;

    /// <summary>Initializes a new instance of the <see cref="ExponentialFittingHestonEngine"/> class.</summary>
    /// <param name="model">The Heston model.</param>
    /// <param name="cv">The control variate: the optimal one by default; Gatheral's form and the branch correction
    /// aren't control variates.</param>
    /// <param name="scaling">The nodes' scaling, or <see langword="null"/> for QuantLib's choice.</param>
    /// <param name="alpha">The control variates' damping exponent.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cv"/> isn't a member of its enum.</exception>
    public ExponentialFittingHestonEngine(
        HestonModel model,
        AnalyticHestonEngineComplexLogFormula cv = AnalyticHestonEngineComplexLogFormula.OptimalCV,
        double? scaling = null,
        double alpha = -0.5)
    {
        ArgumentNullException.ThrowIfNull(model);
        EnumArguments.Check(cv, nameof(cv));
        this.model = model;
        this.cv = cv;
        this.scaling = scaling;
        this.alpha = alpha;
        analyticEngine = new AnalyticHestonEngine(model, 1);
    }

    /// <summary>Prices a European plain-vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="cv">The control variate.</param>
    /// <param name="scaling">The nodes' scaling, or <see langword="null"/> for QuantLib's choice.</param>
    /// <param name="alpha">The control variates' damping exponent.</param>
    /// <returns>The option's results.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cv"/> isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the underlying isn't positive, or the control variate
    /// is Gatheral's form or the branch correction.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        AnalyticHestonEngineComplexLogFormula cv = AnalyticHestonEngineComplexLogFormula.OptimalCV,
        double? scaling = null,
        double alpha = -0.5) =>
        new ExponentialFittingHestonEngine(model, cv, scaling, alpha).Calculate(option, context);

    /// <summary>Prices a European plain-vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The option's results.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the underlying isn't positive, or the control variate
    /// is Gatheral's form or the branch correction.
    /// </exception>
    public OneAssetOptionResults Calculate(VanillaOption option, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new OneAssetOptionResults { NPV = Value(option) });
    }

    // std::max and std::min, which return their first argument where neither is less than the other
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;

    // QuantLib's calculate, the model's time from the earliest curve date and the curves read by date (HR7 (xi))
    private double Value(VanillaOption option)
    {
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European option");
        }

        Date maturityDate = option.Exercise.LastDate();

        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non plain vanilla payoff given");
        }

        double strike = payoff.Strike;

        HestonProcess process = model.Process;

        double t = process.ModelTime(maturityDate);

        double rd = process.RiskFreeRate.AccruedDiscount(maturityDate);
        double dd = process.DividendYield.AccruedDiscount(maturityDate);

        double spot = process.S0;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        double freq = Math.Log(spot) - Math.Log(rd / dd) - Math.Log(strike);

        double fwd = spot * dd / rd;

        double v0 = model.V0;
        double kappa = model.Kappa;
        double theta = model.Theta;
        double sigma = model.Sigma;
        double rho = model.Rho;

        if (!(cv != AnalyticHestonEngineComplexLogFormula.Gatheral && cv != AnalyticHestonEngineComplexLogFormula.BranchCorrection))
        {
            throw new NanoQLException("Gatheral and Branch-Correction are not supported as control-variate");
        }

        if (t == 0.0)
        {
            // no variance has accrued: the forward's discounted intrinsic value (HR7 (xxxi))
            return rd * payoff.Evaluate(fwd);
        }

        AnalyticHestonEngineComplexLogFormula analyticCV = (cv == AnalyticHestonEngineComplexLogFormula.OptimalCV)
            ? AnalyticHestonEngine.OptimalControlVariate(t, v0, kappa, theta, sigma, rho)
            : cv;

        var helper = new AnalyticHestonEngineAPHelper(t, fwd, strike, analyticCV, analyticEngine, alpha);

        // below kappa t = 1e-2, (1 - e^{-kappa t}) by -expm1(-kappa t), which keeps the (v0 - theta) term (HR7 (xxv))
        double kt = kappa * t;
        double vAvg = kt < 1e-2
            ? (-Expm1Functions.StdExpm1(-kt) * (v0 - theta) / kt) + theta
            : ((1 - Math.Exp(-kappa * t)) * (v0 - theta) / (kappa * t)) + theta;

        double scalingFactor = scaling ?? ((analyticCV != AnalyticHestonEngineComplexLogFormula.AsymptoticChF)
            ? Max(0.25, Min(1000.0, 0.25 / Math.Sqrt(0.5 * vAvg * t)))
            : 1.0);

        double[] moneyness = ExponentialFittingHestonEngineValues.Moneyness;
        int n;
        double u;
        if (Math.Abs(freq) < 0.1)
        {
            n = 0;
            u = scalingFactor;
        }
        else
        {
            double lookup = Math.Abs(scalingFactor * freq);
            n = Math.Min(moneyness.Length - 1, LowerBound(moneyness, lookup));

            if (n > 0 && Math.Abs(lookup - moneyness[n]) > Math.Abs(lookup - moneyness[n - 1]))
            {
                --n;
            }

            double omega = moneyness[n];
            u = Math.Abs(omega / freq);
        }

        const int Order = (ExponentialFittingHestonEngineValues.Columns - 1) / 2;
        double[] row = ExponentialFittingHestonEngineValues.Values[n];

        double s = 0.0;
        for (int i = 0; i < Order; ++i)
        {
            double xI = row[i + 1];
            double wI = row[Order + 1 + i];

            s += wI * u * helper.Evaluate(u * xI);
        }

        double hCv = s * fwd / Math.PI;
        double cvValue = helper.ControlVariateValue();

        return payoff.OptionType == OptionType.Call
            ? (cvValue + hCv) * rd
            : (cvValue + hCv - (fwd - strike)) * rd;
    }

    // std::lower_bound: the first index whose value isn't less than x
    private static int LowerBound(double[] values, double x)
    {
        int first = 0, count = values.Length;
        while (count > 0)
        {
            int step = count / 2;
            if (values[first + step] < x)
            {
                first += step + 1;
                count -= step + 1;
            }
            else
            {
                count = step;
            }
        }

        return first;
    }
}
