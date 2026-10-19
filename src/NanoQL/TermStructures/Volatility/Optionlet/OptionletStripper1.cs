// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2015 Michael von den Driesch
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/optionlet/optionletstripper1.hpp ql/termstructures/volatility/optionlet/optionletstripper1.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: capletVols(), a matrix QuantLib never writes, not ported

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Time;
using NanoQL.Utilities;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.TermStructures;

/// <summary>
/// Strips optionlet (caplet and floorlet) volatilities, also called forward-forward volatilities, from the cap and
/// floor term volatilities of a <see cref="CapFloorTermVolSurface"/>.
/// </summary>
/// <remarks>
/// <para>
/// The strip runs in the constructor, in QuantLib's order: the optionlets' dates and at-the-money rates, the switch
/// strike, then per strike the caps' or floors' prices on the term volatilities, the optionlets' prices by difference
/// and their implied volatilities. The strip reads the context it is given, its evaluation date and fixings, and is
/// done once: a new evaluation date or market is a new stripper.
/// </para>
/// <para>
/// Each shifted lognormal implied standard deviation is solved from QuantLib's first guess, 0.14, where QuantLib's
/// recalculation starts from the previous calculation's result; a normal one takes no guess. A failure it was told not
/// to throw leaves 0. Out-of-the-money options are used: floors below the switch strike and caps from it.
/// </para>
/// </remarks>
public sealed class OptionletStripper1 : OptionletStripper
{
    // QuantLib's guess for the implied standard deviations, used only for shifted lognormal volatilities.
    private const double FirstGuess = 0.14;

    private readonly Matrix capFloorPrices;
    private readonly Matrix optionletPrices;
    private readonly Matrix capFloorVols;

    /// <summary>Initializes a new instance of the <see cref="OptionletStripper1"/> class, stripping the surface.</summary>
    /// <param name="termVolSurface">The cap and floor term-volatility surface.</param>
    /// <param name="index">The index of the caps' and floors' coupons.</param>
    /// <param name="context">The evaluation context: its evaluation date starts the Ibor caps, and its fixings and
    /// forecasts give the at-the-money rates and the caps' prices.</param>
    /// <param name="switchStrike">The strike below which floors are used, or <see langword="null"/>, QuantLib's
    /// <c>Null&lt;Rate&gt;()</c>, for the average of the at-the-money rates.</param>
    /// <param name="accuracy">The implied volatilities' accuracy.</param>
    /// <param name="maxIter">The implied volatilities' maximum number of evaluations.</param>
    /// <param name="discount">The discount curve, or <see langword="null"/>, QuantLib's empty handle, for the index's
    /// forwarding curve.</param>
    /// <param name="type">The type of the stripped volatilities.</param>
    /// <param name="displacement">The displacement of shifted lognormal volatilities.</param>
    /// <param name="dontThrow">Whether an optionlet whose volatility can't be implied gets 0, instead of the strip
    /// throwing.</param>
    /// <param name="optionletFrequency">The optionlets' frequency, or <see langword="null"/> for the index's tenor;
    /// required for an overnight index.</param>
    /// <param name="paymentLag">The coupons' payment lag, in business days.</param>
    /// <exception cref="ArgumentNullException"><paramref name="termVolSurface"/>, <paramref name="index"/> or
    /// <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type, or
    /// <paramref name="maxIter"/> or <paramref name="paymentLag"/> is negative.</exception>
    /// <exception cref="ArgumentException">The base refuses its arguments (<see cref="OptionletStripper"/>).</exception>
    /// <exception cref="NanoQLException">
    /// No discount curve is given and the index has no forwarding curve, a cap or floor can't be priced, or, without
    /// <paramref name="dontThrow"/>, an optionlet's volatility can't be implied.
    /// </exception>
    public OptionletStripper1(
        CapFloorTermVolSurface termVolSurface,
        IborIndex index,
        EvaluationContext context,
        double? switchStrike = null,
        double accuracy = 1.0e-6,
        int maxIter = 100,
        YieldTermStructure? discount = null,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double displacement = 0.0,
        bool dontThrow = false,
        Period? optionletFrequency = null,
        int paymentLag = 0)
        : base(termVolSurface, index, discount, type, displacement, optionletFrequency, paymentLag)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(maxIter);

        int nOptionletTenors = OptionletMaturities;
        int nStrikes = NStrikes;
        capFloorPrices = new Matrix(nOptionletTenors, nStrikes);
        optionletPrices = new Matrix(nOptionletTenors, nStrikes);
        capFloorVols = new Matrix(nOptionletTenors, nStrikes);
        var optionletStDevs = new Matrix(nOptionletTenors, nStrikes, FirstGuess);

        // update dates
        Date referenceDate = termVolSurface.ReferenceDate;
        DayCounter dc = termVolSurface.DayCounter;
        ImmutableArray<CashFlow> fullOvernightLeg = IsOvernightIndex
            ? MakeCapFloorLeg(CapFloorLengths[^1], context)
            : [];

        var optionletDates = new Date[nOptionletTenors];
        var optionletPaymentDates = new Date[nOptionletTenors];
        var optionletAccrualPeriods = new double[nOptionletTenors];
        var optionletTimes = new double[nOptionletTenors];
        var atmOptionletRate = new double[nOptionletTenors];

        // QuantLib prices these on a dummy engine it never calls: only their last coupons are read.
        for (int i = 0; i < nOptionletTenors; ++i)
        {
            CapFloor temp = MakeCapFloor(CapFloorType.Cap, i, 0.04, fullOvernightLeg, context);
            FloatingRateCoupon lFRC = temp.LastFloatingRateCoupon()!;
            optionletDates[i] = lFRC.FixingDate();
            optionletPaymentDates[i] = lFRC.Date;
            optionletAccrualPeriods[i] = lFRC.AccrualPeriod();
            optionletTimes[i] = dc.YearFraction(referenceDate, optionletDates[i]);
            atmOptionletRate[i] = IsOvernightIndex ? lFRC.AdjustedFixing(context) : lFRC.IndexFixing(context);
        }

        if (switchStrike is double given)
        {
            SwitchStrike = given;
        }
        else
        {
            double averageAtmOptionletRate = 0.0;
            for (int i = 0; i < nOptionletTenors; ++i)
            {
                averageAtmOptionletRate += atmOptionletRate[i];
            }

            SwitchStrike = averageAtmOptionletRate / nOptionletTenors;
        }

        YieldTermStructure discountCurve = discount
            ?? index.ForwardingTermStructure
            ?? throw new NanoQLException("empty Handle cannot be dereferenced");

        ImmutableArray<double> strikes = termVolSurface.Strikes;
        var optionletVolatilities = new double[nOptionletTenors][];
        for (int i = 0; i < nOptionletTenors; ++i)
        {
            optionletVolatilities[i] = new double[nStrikes];
        }

        for (int j = 0; j < nStrikes; ++j)
        {
            // using out-of-the-money options
            CapFloorType capFloorType = strikes[j] < SwitchStrike ? CapFloorType.Floor : CapFloorType.Cap;
            OptionType optionletType = strikes[j] < SwitchStrike ? OptionType.Put : OptionType.Call;

            double previousCapFloorPrice = 0.0;
            for (int i = 0; i < nOptionletTenors; ++i)
            {
                capFloorVols[i, j] = termVolSurface.Volatility(CapFloorLengths[i], strikes[j], true);
                Func<CapFloor, EvaluationContext, InstrumentResults> capFloorEngine =
                    MakeCapFloorPricingEngine(discountCurve, capFloorVols[i, j]);
                CapFloor capFloor = MakeCapFloor(capFloorType, i, strikes[j], fullOvernightLeg, context);
                capFloorPrices[i, j] = capFloorEngine(capFloor, context).NPV!.Value;
                optionletPrices[i, j] = capFloorPrices[i, j] - previousCapFloorPrice;
                previousCapFloorPrice = capFloorPrices[i, j];
                double d = discountCurve.Discount(optionletPaymentDates[i]);
                double optionletAnnuity = optionletAccrualPeriods[i] * d;
                try
                {
                    if (VolatilityType == VolatilityType.ShiftedLognormal)
                    {
                        optionletStDevs[i, j] = BlackFormulaImpliedStdDev(
                            optionletType, strikes[j], atmOptionletRate[i], optionletPrices[i, j], optionletAnnuity,
                            Displacement, optionletStDevs[i, j], accuracy, maxIter);
                    }
                    else
                    {
                        optionletStDevs[i, j] = Math.Sqrt(optionletTimes[i])
                            * BachelierBlackFormulaImpliedVol(
                                optionletType, strikes[j], atmOptionletRate[i], optionletTimes[i],
                                optionletPrices[i, j], optionletAnnuity);
                    }
                }
                catch (Exception e) when (e is ArgumentException or NanoQLException)
                {
                    if (dontThrow)
                    {
                        optionletStDevs[i, j] = 0.0;
                    }
                    else
                    {
                        throw new NanoQLException(
                            "could not bootstrap optionlet:"
                            + $"\n type:    {optionletType}"
                            + $"\n strike:  {DataFormattersFunctions.Rate(strikes[j])}"
                            + $"\n atm:     {DataFormattersFunctions.Rate(atmOptionletRate[i])}"
                            + $"\n price:   {Show(optionletPrices[i, j])}"
                            + $"\n annuity: {Show(optionletAnnuity)}"
                            + $"\n expiry:  {optionletDates[i]}"
                            + $"\n error:   {DiscountingSwapEngine.QuantLibText(e)}",
                            e);
                    }
                }

                optionletVolatilities[i][j] = optionletStDevs[i, j] / Math.Sqrt(optionletTimes[i]);
            }
        }

        StrippedFixingDates = [.. optionletDates];
        StrippedFixingTimes = [.. optionletTimes];
        StrippedPaymentDates = [.. optionletPaymentDates];
        StrippedAccrualPeriods = [.. optionletAccrualPeriods];
        StrippedAtmRates = [.. atmOptionletRate];
        StrippedStrikes = [.. Enumerable.Repeat(strikes, nOptionletTenors)];
        StrippedVolatilities = [.. optionletVolatilities.Select(v => ImmutableArray.Create(v))];
    }

    /// <summary>Gets the switch strike: the given one, or the average of the at-the-money rates.</summary>
    public double SwitchStrike { get; }

    /// <summary>The caps' and floors' prices on the term volatilities.</summary>
    /// <returns>A copy of the prices: a row per optionlet tenor and a column per strike.</returns>
    public Matrix CapFloorPrices() => capFloorPrices.Clone();

    /// <summary>The term volatilities the caps and floors were priced on.</summary>
    /// <returns>A copy of the volatilities: a row per optionlet tenor and a column per strike.</returns>
    public Matrix CapFloorVolatilities() => capFloorVols.Clone();

    /// <summary>The optionlets' prices: each cap's or floor's price less the previous one's.</summary>
    /// <returns>A copy of the prices: a row per optionlet tenor and a column per strike.</returns>
    public Matrix OptionletPrices() => optionletPrices.Clone();

    // A cap or floor of the i-th length, with one strike: on an overnight index the full leg's first i + 1 coupons.
    private CapFloor MakeCapFloor(
        CapFloorType capFloorType,
        int optionletIndex,
        double strike,
        ImmutableArray<CashFlow> fullOvernightLeg,
        EvaluationContext context)
    {
        ImmutableArray<CashFlow> leg;
        if (IsOvernightIndex)
        {
            if (!(optionletIndex < fullOvernightLeg.Length))
            {
                throw new NanoQLException("optionlet index out of range");
            }

            leg = fullOvernightLeg[..(optionletIndex + 1)];
        }
        else
        {
            leg = MakeCapFloorLeg(CapFloorLengths[optionletIndex], context);
        }

        return new CapFloor(capFloorType, leg, [strike]);
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}
