// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/btp.hpp ql/instruments/bonds/btp.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// The Rendistato calculator: the yield and modified duration of a basket of BTPs, weighted by their outstanding
/// amounts, and the Euribor swap of one to fifteen years whose fixed leg, as a bond at par, has the duration closest
/// below the basket's.
/// </summary>
/// <remarks>
/// It computes in its constructor, under the context it is built with, what QuantLib's first calculation computes;
/// QuantLib starts a recalculation's yield solves from the last calculation's yields, so its warm start is a documented
/// divergence. The swaps start one day after spot, at a fixed rate of 5%, and are priced on the discount curve without
/// the settlement date's flows. The search for the equivalent swap stops at the first swap whose duration exceeds the
/// basket's; the later swaps' rates, yields and durations aren't computed and are <see langword="null"/>, where
/// QuantLib's rates and durations are its null value and its yields its starting guess, 5% (a defect found while
/// porting it).
/// </remarks>
public sealed class RendistatoCalculator
{
    // TODO: generalize number of swaps and their lengths
    private const int NSwaps = 15;

    private readonly int equivalentSwapIndex;

    /// <summary>Initializes a new instance of the <see cref="RendistatoCalculator"/> class.</summary>
    /// <param name="basket">The basket of BTPs.</param>
    /// <param name="euriborIndex">The swaps' Euribor index, with its forwarding curve.</param>
    /// <param name="discountCurve">
    /// The swaps' discount curve, or <see langword="null"/>, QuantLib's empty handle, which the swaps' engine refuses.
    /// </param>
    /// <param name="context">The evaluation context the swaps are built and everything is computed under.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="basket"/>, <paramref name="euriborIndex"/> or <paramref name="context"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// A bond isn't tradable at the first bond's settlement date, a yield solve fails, or a swap can't be priced.
    /// </exception>
    public RendistatoCalculator(
        RendistatoBasket basket,
        Euribor euriborIndex,
        YieldTermStructure? discountCurve,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(basket);
        ArgumentNullException.ThrowIfNull(euriborIndex);
        ArgumentNullException.ThrowIfNull(context);
        Basket = basket;
        EuriborIndex = euriborIndex;
        DiscountCurve = discountCurve;

        double dummyRate = 0.05;
        ImmutableArray<double>.Builder swapLengths = ImmutableArray.CreateBuilder<double>(NSwaps);
        ImmutableArray<VanillaSwap>.Builder swaps = ImmutableArray.CreateBuilder<VanillaSwap>(NSwaps);
        for (int i = 0; i < NSwaps; ++i)
        {
            swapLengths.Add(i + 1);
            swaps.Add(new MakeVanillaSwap(new Period(i + 1, TimeUnit.Years), euriborIndex)
                .WithFixedRate(dummyRate)
                .WithForwardStart(new Period(1, TimeUnit.Days))
                .WithDiscountingTermStructure(discountCurve)
                .ToVanillaSwap(context));
        }

        SwapLengths = swapLengths.MoveToImmutable();
        Swaps = swaps.MoveToImmutable();

        ImmutableArray<BTP> btps = basket.Btps;
        ImmutableArray<double> quotes = basket.CleanPrices;
        Date bondSettlementDate = btps[0].SettlementDate(context);
        double[] yields = new double[basket.Size];
        double[] durations = new double[basket.Size];
        for (int i = 0; i < basket.Size; ++i)
        {
            yields[i] = BondFunctions.Yield(
                btps[i],
                new BondPrice(quotes[i], BondPriceType.Clean),
                new ActualActual(ActualActualConvention.ISMA),
                Compounding.Compounded,
                Frequency.Annual,
                context,
                bondSettlementDate,

                // accuracy, maxIterations, guess
                1.0e-10,
                100,
                0.05);
            durations[i] = BondFunctions.Duration(
                btps[i],
                yields[i],
                new ActualActual(ActualActualConvention.ISMA),
                Compounding.Compounded,
                Frequency.Annual,
                context,
                DurationType.Modified,
                bondSettlementDate);
        }

        Yields = [.. yields];
        Durations = [.. durations];
        double duration = 0.0;
        for (int i = 0; i < basket.Size; ++i)
        {
            duration += basket.Weights[i] * durations[i];
        }

        Duration = duration;

        double yield = 0.0;
        for (int i = 0; i < basket.Size; ++i)
        {
            yield += basket.Weights[i] * yields[i];
        }

        Yield = yield;

        int settlDays = 2;
        DayCounter fixedDayCount = Swaps[0].FixedDayCount;
        equivalentSwapIndex = NSwaps - 1;
        double?[] swapRates = new double?[NSwaps];
        double?[] swapBondYields = new double?[NSwaps];
        double?[] swapBondDurations = new double?[NSwaps];
        for (int i = 0; i < NSwaps; ++i)
        {
            double swapRate = FairRate(Swaps[i], discountCurve, context);
            swapRates[i] = swapRate;
            var swapBond = new FixedRateBond(
                settlDays,
                100.0, // faceAmount
                Swaps[i].FixedSchedule,
                [swapRate],
                fixedDayCount,
                BusinessDayConvention.Following, // paymentConvention
                100.0); // redemption
            double swapBondYield = BondFunctions.Yield(
                swapBond,
                new BondPrice(100.0, BondPriceType.Clean), // floating leg NPV including end payment
                new ActualActual(ActualActualConvention.ISMA),
                Compounding.Compounded,
                Frequency.Annual,
                context,
                bondSettlementDate,

                // accuracy, maxIterations, guess
                1.0e-10,
                100,
                0.05);
            swapBondYields[i] = swapBondYield;
            double swapBondDuration = BondFunctions.Duration(
                swapBond,
                swapBondYield,
                new ActualActual(ActualActualConvention.ISMA),
                Compounding.Compounded,
                Frequency.Annual,
                context,
                DurationType.Modified,
                bondSettlementDate);
            swapBondDurations[i] = swapBondDuration;

            // QuantLib computes the first swap before its loop, which compares from the second
            if (i > 0 && swapBondDuration > duration)
            {
                equivalentSwapIndex = i - 1;
                break; // exit the loop
            }
        }

        SwapRates = [.. swapRates];
        SwapYields = [.. swapBondYields];
        SwapDurations = [.. swapBondDurations];
    }

    /// <summary>Gets the basket of BTPs.</summary>
    public RendistatoBasket Basket { get; }

    /// <summary>Gets the swaps' Euribor index.</summary>
    public Euribor EuriborIndex { get; }

    /// <summary>Gets the swaps' discount curve; <see langword="null"/> for QuantLib's empty handle.</summary>
    public YieldTermStructure? DiscountCurve { get; }

    /// <summary>Gets the basket's yield, the bonds' yields weighted by their outstanding amounts.</summary>
    public double Yield { get; }

    /// <summary>Gets the basket's modified duration, the bonds' weighted by their outstanding amounts.</summary>
    public double Duration { get; }

    /// <summary>Gets each bond's yield, on Actual/Actual (ISMA) compounded annually, from its clean price.</summary>
    public ImmutableArray<double> Yields { get; }

    /// <summary>Gets each bond's modified duration at its yield.</summary>
    public ImmutableArray<double> Durations { get; }

    /// <summary>Gets the swaps' lengths in years, 1 to 15.</summary>
    public ImmutableArray<double> SwapLengths { get; }

    /// <summary>
    /// Gets each swap's fair rate; <see langword="null"/> for a swap past the one whose duration exceeds the basket's.
    /// </summary>
    public ImmutableArray<double?> SwapRates { get; }

    /// <summary>
    /// Gets the yield of each swap's fixed leg as a bond at par; <see langword="null"/> for a swap past the one whose
    /// duration exceeds the basket's.
    /// </summary>
    public ImmutableArray<double?> SwapYields { get; }

    /// <summary>
    /// Gets the modified duration of each swap's fixed leg as a bond at par; <see langword="null"/> for a swap past the
    /// one whose duration exceeds the basket's.
    /// </summary>
    public ImmutableArray<double?> SwapDurations { get; }

    /// <summary>Gets the equivalent swap, at QuantLib's fixed rate of 5%.</summary>
    public VanillaSwap EquivalentSwap => Swaps[equivalentSwapIndex];

    /// <summary>Gets the equivalent swap's fair rate.</summary>
    public double EquivalentSwapRate => SwapRates[equivalentSwapIndex]!.Value;

    /// <summary>Gets the yield of the equivalent swap's fixed leg as a bond at par.</summary>
    public double EquivalentSwapYield => SwapYields[equivalentSwapIndex]!.Value;

    /// <summary>Gets the modified duration of the equivalent swap's fixed leg as a bond at par.</summary>
    public double EquivalentSwapDuration => SwapDurations[equivalentSwapIndex]!.Value;

    /// <summary>Gets the equivalent swap's length in years.</summary>
    public double EquivalentSwapLength => SwapLengths[equivalentSwapIndex];

    /// <summary>Gets the basket's yield less the equivalent swap's fair rate.</summary>
    public double EquivalentSwapSpread => Yield - EquivalentSwapRate;

    /// <summary>Gets the fifteen swaps, at QuantLib's fixed rate of 5%.</summary>
    internal ImmutableArray<VanillaSwap> Swaps { get; }

    private static double FairRate(VanillaSwap swap, YieldTermStructure? discountCurve, EvaluationContext context)
    {
        FixedVsFloatingSwapResults results = discountCurve is null
            ? DiscountingSwapEngine.CalculateWithoutCurve(swap, context)
            : DiscountingSwapEngine.Calculate(swap, context, discountCurve, includeSettlementDateFlows: false);
        return results.FairRate ?? throw new NanoQLException("result not available");
    }
}
