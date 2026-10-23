// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Theo Boafo
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/bond/binomialconvertibleengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The binomial Tsiveriotis-Fernandes engine for convertible bonds: the bond rolled back on a Tsiveriotis-Fernandes
/// lattice over a binomial tree, built on the process's curves and volatility flattened at the last exercise date, the
/// dividends' present values taken from the spot.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's template on the tree (D12): <see cref="Calculate{TTree}"/> takes the tree as its type argument; the seven
/// named engines, as QuantLib-Python names them, fix it. The credit spread is a number, where QuantLib observes a quote.
/// </para>
/// <para>
/// <b>Its clock</b> (the S-sr plan's SSR8, ruled by the user on 2026-10-22, (a)): the tree runs from the bond's
/// settlement date over the risk-free curve's year fraction to the last exercise date, and the NPV is the bond's value
/// there, the settlement value equal to it, as QuantLib's are: a named departure from the playbook's lattice clock, since
/// the engine prices the bond at its settlement date and QuantLib's convertible cases assert that definition. The
/// structures are flattened at the last exercise date, dated at the risk-free curve's reference date, as QuantLib's are;
/// where the dividend curve or the volatility has another day counter or reference date than the risk-free curve, its
/// rate or volatility is the one that gives its discount or variance from the risk-free curve's reference date to that
/// date, counted on the risk-free day counter, each structure accruing nothing before its own date, where QuantLib reads
/// it on its own day counter and the tree reads it as the risk-free curve's (<c>Divergences/ConvertibleClockTests.cs</c>).
/// Where every structure shares the risk-free curve's day counter and reference date, the tree is QuantLib's.
/// </para>
/// <para>
/// The time grid and the lattice take their step count from the tree, its columns less one, where QuantLib's take the
/// count given: the Leisen-Reimer and Joshi trees build an even count on the next odd one, as S-eq's binomial engine
/// handles them (the S-eq plan's SEQR7 (x)). Every odd count and every other tree is QuantLib's.
/// </para>
/// </remarks>
public static class BinomialConvertibleEngine
{
    /// <summary>Prices a convertible bond on a binomial tree.</summary>
    /// <typeparam name="TTree">The binomial tree.</typeparam>
    /// <param name="bond">The convertible bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process of the underlying.</param>
    /// <param name="timeSteps">The number of time steps, positive.</param>
    /// <param name="creditSpread">The credit spread.</param>
    /// <param name="dividends">The underlying's dividends; none by default.</param>
    /// <returns>The NPV and the settlement value, both the bond's value at its settlement date.</returns>
    /// <exception cref="ArgumentNullException">The bond, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeSteps"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeSteps"/> is 0, or a dividend is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The bond's arguments are inconsistent, the underlying isn't positive before or after the dividends, the tree
    /// can't be built, or the value overflows.
    /// </exception>
    public static BondResults Calculate<TTree>(
        ConvertibleBond bond,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int timeSteps,
        double creditSpread,
        ImmutableArray<Dividend> dividends = default)
        where TTree : Tree, IBinomialTree<TTree>
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        LatticeShortRateModelEngine.CheckTimeSteps(timeSteps);
        ImmutableArray<Dividend> schedule = FdBlackScholesBarrierEngine.Dividends(dividends, nameof(dividends));
        if (bond.IsExpired(context))
        {
            return bond.SetupExpired();
        }

        ConvertibleBondArguments arguments = bond.SetupArguments(context);
        arguments.Validate();
        return bond.FetchResults(Body<TTree>(arguments, context, process, timeSteps, creditSpread, schedule));
    }

    // QuantLib's calculate().
    private static BondResults Body<TTree>(
        ConvertibleBondArguments arguments,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int timeSteps,
        double creditSpread,
        ImmutableArray<Dividend> dividends)
        where TTree : Tree, IBinomialTree<TTree>
    {
        DayCounter rfdc = process.RiskFreeRate.DayCounter;
        DayCounter divdc = process.DividendYield.DayCounter;
        DayCounter voldc = process.BlackVolatility.DayCounter;
        Calendar? volcal = process.BlackVolatility.Calendar;

        double s0 = process.StateVariable;
        if (!(s0 > 0.0))
        {
            throw new NanoQLException("negative or null underlying");
        }

        Date maturityDate = arguments.Exercise.LastDate();
        Date referenceDate = process.RiskFreeRate.ReferenceDate;

        // SSR8: a structure on another day counter or reference date is flattened on the risk-free curve's clock, from its
        // discount or variance between the risk-free curve's reference date and the last exercise date; QuantLib's reads
        // where it shares that clock
        bool dividendsFlattened = divdc != rfdc || process.DividendYield.ReferenceDate != referenceDate;
        bool volatilityFlattened = voldc != rfdc || process.BlackVolatility.ReferenceDate != referenceDate;
        double tau = rfdc.YearFraction(referenceDate, maturityDate);

        double v = volatilityFlattened
            ? Math.Sqrt((process.BlackVolatility.AccruedVariance(maturityDate, s0)
                - process.BlackVolatility.AccruedVariance(referenceDate, s0)) / tau)
            : process.BlackVolatility.BlackVol(maturityDate, s0);
        double riskFreeRate = process.RiskFreeRate.ZeroRate(maturityDate, rfdc, Compounding.Continuous, Frequency.NoFrequency).Rate;
        double q = dividendsFlattened
            ? Math.Log(process.DividendYield.AccruedDiscount(referenceDate) / process.DividendYield.AccruedDiscount(maturityDate)) / tau
            : process.DividendYield.ZeroRate(maturityDate, divdc, Compounding.Continuous, Frequency.NoFrequency).Rate;

        // subtract dividends
        for (int i = 0; i < dividends.Length; i++)
        {
            if (dividends[i].Date >= referenceDate)
            {
                s0 -= dividends[i].Amount(context) * process.RiskFreeRate.Discount(dividends[i].Date);
            }
        }

        if (!(s0 > 0.0))
        {
            throw new NanoQLException("negative value after subtracting dividends");
        }

        if (dividendsFlattened)
        {
            divdc = rfdc;
        }

        if (volatilityFlattened)
        {
            voldc = rfdc;
        }

        // binomial trees with constant coefficient
        var flatRiskFree = new FlatForward(referenceDate, riskFreeRate, rfdc);
        var flatDividends = new FlatForward(referenceDate, q, divdc);
        var flatVol = new BlackConstantVol(referenceDate, volcal, v, voldc);

        double maturity = rfdc.YearFraction(arguments.SettlementDate, maturityDate);

        double strike = arguments.Redemption / arguments.ConversionRatio;
        var bs = new GeneralizedBlackScholesProcess(s0, flatDividends, flatRiskFree, flatVol);
        TTree tree = TTree.Create(bs, maturity, timeSteps, strike);

        // SEQR7 (x): the grid and the lattice on the steps the tree was built on
        int steps = tree.Columns - 1;
        var lattice = new TsiveriotisFernandesLattice<TTree>(tree, riskFreeRate, maturity, steps, creditSpread, v, q);

        var convertible = new DiscretizedConvertible(arguments, bs, dividends, creditSpread, context, new TimeGrid(maturity, steps));

        convertible.Initialize(lattice, maturity);
        convertible.Rollback(0.0);
        double value = convertible.PresentValue();
        if (!(value < double.MaxValue))
        {
            throw new NanoQLException("floating-point overflow on tree grid");
        }

        return new BondResults { NPV = value, SettlementValue = value };
    }
}
