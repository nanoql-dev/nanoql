// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2010 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/optionlet/optionletstripper2.hpp ql/termstructures/volatility/optionlet/optionletstripper2.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.TermStructures;

/// <summary>
/// Extends an <see cref="OptionletStripper1"/>'s optionlet volatilities with those stripped from the at-the-money cap
/// and floor term volatilities of a <see cref="CapFloorTermVolCurve"/>.
/// </summary>
/// <remarks>
/// <para>
/// The strip runs in the constructor, in QuantLib's order: for each of the curve's option tenors, the at-the-money cap
/// on the index's forwarding curve and its price on the curve's volatility; then the spread over the first stripper's
/// volatilities that reprices each cap, solved by Brent's method; and the spreaded at-the-money volatility inserted
/// among each optionlet's strikes.
/// </para>
/// <para>
/// The stripper computes under its own context: built from a first stripper built under another context, it reads its
/// own for its caps and the first stripper's adapter, and takes the first stripper's dates and rates as they were
/// stripped.
/// </para>
/// </remarks>
public sealed class OptionletStripper2 : OptionletStripper
{
    private const int MaxEvaluations = 10000;
    private const double Accuracy = 1.0e-6;

    private readonly ImmutableArray<double> atmCapFloorStrikes;
    private readonly ImmutableArray<double> atmCapFloorPrices;
    private readonly ImmutableArray<double> spreadsVolImplied;

    /// <summary>Initializes a new instance of the <see cref="OptionletStripper2"/> class, stripping the curve.</summary>
    /// <param name="optionletStripper1">The first stripper, whose surface, index, type, displacement, frequency and
    /// payment lag the stripper takes.</param>
    /// <param name="atmCapFloorTermVolCurve">The at-the-money term-volatility curve, on the surface's day
    /// counter.</param>
    /// <param name="context">The evaluation context: its evaluation date starts the Ibor caps and sets the adapter's
    /// reference date, and its fixings and forecasts give the caps' prices.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The curve's day counter isn't the surface's, an overnight cap of the curve
    /// is longer than the first stripper's optionlets, or the first stripper has no settlement days for its
    /// adapter.</exception>
    /// <exception cref="NanoQLException">
    /// The index has no forwarding curve, a cap can't be priced, or no spread reprices an at-the-money cap.
    /// </exception>
    public OptionletStripper2(
        OptionletStripper1 optionletStripper1,
        CapFloorTermVolCurve atmCapFloorTermVolCurve,
        EvaluationContext context)
        : base(
            Checked(optionletStripper1).TermVolSurface,
            optionletStripper1.IborIndex,
            null,
            optionletStripper1.VolatilityType,
            optionletStripper1.Displacement,
            optionletStripper1.OptionletFrequency,
            optionletStripper1.PaymentLag)
    {
        ArgumentNullException.ThrowIfNull(atmCapFloorTermVolCurve);
        ArgumentNullException.ThrowIfNull(context);
        DayCounter dc = optionletStripper1.TermVolSurface.DayCounter;
        int nOptionExpiries = atmCapFloorTermVolCurve.OptionTenors.Length;
        if (!(dc == atmCapFloorTermVolCurve.DayCounter))
        {
            throw new ArgumentException("different day counters provided", nameof(atmCapFloorTermVolCurve));
        }

        // optionletStripper data
        ImmutableArray<Date> optionletDates = optionletStripper1.OptionletFixingDates;
        ImmutableArray<double> optionletTimes = optionletStripper1.OptionletFixingTimes;
        var optionletStrikes = new List<double>[optionletTimes.Length];
        var optionletVolatilities = new List<double>[optionletTimes.Length];
        for (int i = 0; i < optionletTimes.Length; ++i)
        {
            optionletStrikes[i] = [.. optionletStripper1.OptionletStrikes(i)];
            optionletVolatilities[i] = [.. optionletStripper1.OptionletVolatilities(i)];
        }

        // atmCapFloorTermVolCurve data
        ImmutableArray<Period> optionExpiriesTenors = atmCapFloorTermVolCurve.OptionTenors;
        ImmutableArray<double> optionExpiriesTimes = atmCapFloorTermVolCurve.OptionTimes;

        // For each ATM cap/floor expiry j, capOptionletCounts[j] is the number of optionlets whose volatilities that
        // cap adjusts; the optionlets are then 0 ... count-1, and the adjustment is applied at the end of this method.
        //
        // Note: if the first coupon fixes before the first optionlet date, we extrapolate its volatility from the
        // first two optionlet nodes.
        var capOptionletCounts = new int[nOptionExpiries];
        var strikes = new double[nOptionExpiries];
        var prices = new double[nOptionExpiries];
        var caps = new CapFloor[nOptionExpiries];
        YieldTermStructure forwarding = IborIndex.ForwardingTermStructure
            ?? throw new NanoQLException("empty Handle cannot be dereferenced");

        for (int j = 0; j < nOptionExpiries; ++j)
        {
            double atmOptionVol = atmCapFloorTermVolCurve.Volatility(optionExpiriesTimes[j], 33.3333); // dummy strike
            Func<CapFloor, EvaluationContext, InstrumentResults> engine =
                MakeCapFloorPricingEngine(forwarding, atmOptionVol);

            ImmutableArray<CashFlow> leg = MakeCapFloorLeg(optionExpiriesTenors[j], context);
            if (IsOvernightIndex)
            {
                if (!(leg.Length <= optionletVolatilities.Length))
                {
                    throw new ArgumentException(
                        $"ATM cap/floor for expiry {optionExpiriesTenors[j]} extends beyond the source optionlet surface",
                        nameof(atmCapFloorTermVolCurve));
                }
            }

            capOptionletCounts[j] = Math.Min(leg.Length, optionletVolatilities.Length);
            var firstCoupon = (FloatingRateCoupon)leg[0];
            if (firstCoupon.FixingDate() < optionletDates[0])
            {
                if (!(optionletVolatilities.Length >= 2))
                {
                    throw new NanoQLException(
                        "cannot extrapolate an ATM cap/floor fixing before the first optionlet date from fewer than two "
                        + "optionlet nodes");
                }

                capOptionletCounts[j] = Math.Max(capOptionletCounts[j], 2);
            }

            strikes[j] = CashFlows.AtmRate(leg, forwarding, context, false, forwarding.ReferenceDate);
            caps[j] = new CapFloor(CapFloorType.Cap, leg, [strikes[j]]);
            prices[j] = engine(caps[j], context).NPV!.Value;
        }

        atmCapFloorStrikes = [.. strikes];
        atmCapFloorPrices = [.. prices];

        StrippedOptionletAdapter adapter = StrippedOptionletAdapter.Create(optionletStripper1, context).EnableExtrapolation();
        spreadsVolImplied = SpreadsVolImplied(adapter, forwarding, caps, atmCapFloorTermVolCurve, context);

        for (int j = 0; j < nOptionExpiries; ++j)
        {
            for (int i = 0; i < capOptionletCounts[j]; ++i)
            {
                double unadjustedVol = adapter.Volatility(optionletTimes[i], atmCapFloorStrikes[j]);
                double adjustedVol = unadjustedVol + spreadsVolImplied[j];

                // insert adjusted volatility
                int insertIndex = LowerBound(optionletStrikes[i], atmCapFloorStrikes[j]);
                optionletStrikes[i].Insert(insertIndex, atmCapFloorStrikes[j]);
                optionletVolatilities[i].Insert(insertIndex, adjustedVol);
            }
        }

        StrippedFixingDates = optionletDates;
        StrippedFixingTimes = optionletTimes;
        StrippedPaymentDates = optionletStripper1.OptionletPaymentDates;
        StrippedAccrualPeriods = optionletStripper1.OptionletAccrualPeriods;
        StrippedAtmRates = optionletStripper1.AtmOptionletRates(context);
        StrippedStrikes = [.. optionletStrikes.Select(s => s.ToImmutableArray())];
        StrippedVolatilities = [.. optionletVolatilities.Select(v => v.ToImmutableArray())];
    }

    /// <summary>Gets the at-the-money caps' strikes, one per option tenor of the curve.</summary>
    public ImmutableArray<double> AtmCapFloorStrikes => atmCapFloorStrikes;

    /// <summary>Gets the at-the-money caps' prices on the curve's volatilities, one per option tenor.</summary>
    public ImmutableArray<double> AtmCapFloorPrices => atmCapFloorPrices;

    /// <summary>Gets the spreads that reprice the at-the-money caps, one per option tenor.</summary>
    public ImmutableArray<double> SpreadsVol => spreadsVolImplied;

    private static OptionletStripper1 Checked(OptionletStripper1 optionletStripper1)
    {
        ArgumentNullException.ThrowIfNull(optionletStripper1);
        return optionletStripper1;
    }

    // std::lower_bound: the first position whose strike isn't less than the value.
    private static int LowerBound(List<double> strikes, double value)
    {
        int i = 0;
        while (i < strikes.Count && strikes[i] < value)
        {
            ++i;
        }

        return i;
    }

    private ImmutableArray<double> SpreadsVolImplied(
        OptionletVolatilityStructure baseVolatility,
        YieldTermStructure forwarding,
        CapFloor[] caps,
        CapFloorTermVolCurve atmCapFloorTermVolCurve,
        EvaluationContext context)
    {
        Brent solver = new Brent().SetMaxEvaluations(MaxEvaluations);
        var result = new double[caps.Length];
        const double maxSpread = 0.1;
        for (int j = 0; j < caps.Length; ++j)
        {
            double minBaseVol = NumericLimits.QL_MAX_REAL;
            foreach (CashFlow cashflow in caps[j].FloatingLeg)
            {
                var coupon = (FloatingRateCoupon)cashflow;
                minBaseVol = Math.Min(
                    minBaseVol, baseVolatility.Volatility(coupon.FixingDate(), atmCapFloorStrikes[j], true));
            }

            Period tenor = atmCapFloorTermVolCurve.OptionTenors[j];
            if (!(minBaseVol != NumericLimits.QL_MAX_REAL))
            {
                throw new NanoQLException($"empty cap/floor leg for option expiry {tenor}");
            }

            if (!(minBaseVol >= 0.0))
            {
                throw new NanoQLException(
                    $"negative optionlet volatility at ATM strike {Show(atmCapFloorStrikes[j])}");
            }

            CapFloor cap = caps[j];
            double target = atmCapFloorPrices[j];
            double F(double s)
            {
                var spreadedVolatility = new SpreadedOptionletVolatility(baseVolatility, s);
                Func<CapFloor, EvaluationContext, InstrumentResults> engine =
                    MakeCapFloorPricingEngine(forwarding, spreadedVolatility);
                return engine(cap, context).NPV!.Value - target;
            }

            double valueAtZero = F(0.0);
            if (ComparisonFunctions.Close(valueAtZero, 0.0))
            {
                result[j] = 0.0;
            }
            else if (valueAtZero > 0.0)
            {
                if (!(minBaseVol > 0.0))
                {
                    throw new NanoQLException(
                        $"ATM cap/floor price for option expiry {tenor} is below the minimum attainable price");
                }

                double minSpread = -minBaseVol;
                double valueAtMinSpread = F(minSpread);
                if (!(valueAtMinSpread <= 0.0))
                {
                    throw new NanoQLException(
                        $"ATM cap/floor price for option expiry {tenor} is below the minimum attainable price");
                }

                result[j] = solver.Solve(F, Accuracy, 0.5 * minSpread, minSpread, 0.0);
            }
            else
            {
                double valueAtMaxSpread = F(maxSpread);
                if (!(valueAtMaxSpread >= 0.0))
                {
                    throw new NanoQLException(
                        $"ATM cap/floor price for option expiry {tenor} requires a volatility spread greater than "
                        + Show(maxSpread));
                }

                result[j] = solver.Solve(F, Accuracy, 0.5 * maxSpread, 0.0, maxSpread);
            }
        }

        return [.. result];
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}
