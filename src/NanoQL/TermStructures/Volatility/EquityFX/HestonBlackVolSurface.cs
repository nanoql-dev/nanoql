// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/hestonblackvolsurface.hpp ql/termstructures/volatility/equityfx/hestonblackvolsurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The Black volatility surface of a Heston model: at each time and strike, the volatility whose Black price is the
/// model's price of the out-of-the-money option, by <see cref="AnalyticHestonEngine"/>.
/// </summary>
/// <remarks>
/// <para>
/// Immutable; it builds an analytic engine per query, as QuantLib's does. Its reference date is the model's origin, the
/// earlier of its curves' reference dates, and its times are on the risk-free curve's day counter from there, each
/// curve read at the instant a time denotes, where QuantLib's runs from the risk-free curve's reference date and reads
/// the dividend curve at the risk-free curve's times; a market whose dividend curve shares the risk-free curve's
/// reference date and day counter keeps QuantLib's values. Where the model's price isn't positive the volatility is √θ,
/// QuantLib's guard.
/// </para>
/// </remarks>
public sealed class HestonBlackVolSurface : BlackVolTermStructure
{
    private readonly HestonModel hestonModel;
    private readonly AnalyticHestonEngineComplexLogFormula cpxLogFormula;
    private readonly FourierIntegration integration;

    /// <summary>Initializes a new instance of the <see cref="HestonBlackVolSurface"/> class.</summary>
    /// <param name="hestonModel">The Heston model.</param>
    /// <param name="cpxLogFormula">The analytic engine's formulation.</param>
    /// <param name="integration">The analytic engine's integration, or <see langword="null"/> for QuantLib's default,
    /// Gauss-Laguerre of order 160.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hestonModel"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cpxLogFormula"/> isn't a member of its
    /// enum.</exception>
    public HestonBlackVolSurface(
        HestonModel hestonModel,
        AnalyticHestonEngineComplexLogFormula cpxLogFormula = AnalyticHestonEngineComplexLogFormula.AngledContour,
        FourierIntegration? integration = null)
        : base(Origin(hestonModel), new NullCalendar(), BusinessDayConvention.Following, hestonModel.Process.RiskFreeRate.DayCounter)
    {
        EnumArguments.Check(cpxLogFormula, nameof(cpxLogFormula));
        this.hestonModel = hestonModel;
        this.cpxLogFormula = cpxLogFormula;
        this.integration = integration ?? FourierIntegration.GaussLaguerre(160);
    }

    /// <inheritdoc/>
    public override Date MaxDate => Date.MaxDate;

    /// <inheritdoc/>
    public override double MinStrike() => 0.0;

    /// <inheritdoc/>
    public override double MaxStrike() => double.MaxValue;

    /// <inheritdoc/>
    /// <remarks>The forward at the instant <paramref name="t"/> denotes, each curve extrapolated.</remarks>
    public override double? AtmLevel(double t)
    {
        HestonProcess process = hestonModel.Process;
        return process.S0 * process.DividendYield.DiscountAt(this, t, true) / process.RiskFreeRate.DiscountAt(this, t, true);
    }

    /// <inheritdoc/>
    protected override double BlackVarianceImpl(double t, double strike)
    {
        double vol = BlackVolImpl(t, strike);
        return vol * vol * t;
    }

    /// <inheritdoc/>
    protected override double BlackVolImpl(double t, double strike)
    {
        var hestonEngine = new AnalyticHestonEngine(hestonModel, cpxLogFormula, integration);

        HestonProcess process = hestonModel.Process;
        double df = process.RiskFreeRate.DiscountAt(this, t, true);
        double fwd = process.S0 * process.DividendYield.DiscountAt(this, t, true) / df;

        var payoff = new PlainVanillaPayoff(fwd > strike ? OptionType.Put : OptionType.Call, strike);
        double npv = hestonEngine.PriceVanillaPayoffWithStatistics(payoff, this, t).Price;

        double theta = hestonModel.Theta;
        if (npv <= 0.0)
        {
            return Math.Sqrt(theta);
        }

        Brent solver = new Brent().SetMaxEvaluations(10000);
        double guess = Math.Sqrt(theta);
        const double Accuracy = NumericLimits.QL_EPSILON;

        return solver.Solve(v => BlackValue(payoff.OptionType, strike, fwd, t, v, df, npv), Accuracy, guess, 0.01);
    }

    // the Black price at a volatility, floored at 0, less the model's price
    private static double BlackValue(
        OptionType optionType, double strike, double forward, double maturity, double vol, double discount, double npv) =>
        BlackFormulaFunctions.BlackFormula(optionType, strike, forward, (0.0 < vol ? vol : 0.0) * Math.Sqrt(maturity), discount)
        - npv;

    private static Date Origin(HestonModel hestonModel)
    {
        ArgumentNullException.ThrowIfNull(hestonModel);
        return hestonModel.Process.EarliestReferenceDate();
    }
}
