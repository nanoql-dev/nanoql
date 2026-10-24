// SPDX-FileCopyrightText: Copyright (C) 2018 Roy Zywina
// SPDX-FileCopyrightText: Copyright (C) 2019 Eisuke Tani
// SPDX-FileCopyrightText: Copyright (C) 2019, 2020 Eisuke Tani
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/overnightindexfutureratehelper.hpp ql/termstructures/yield/overnightindexfutureratehelper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A rate helper bootstrapping a curve on an overnight index future's price.</summary>
/// <remarks>
/// The future is priced on a copy of the index forwarding on the curve the helper is bound to (the S-cms plan's
/// CMD12, S5's helper contract), QuantLib's index cloned on the helper's relinkable handle; its dates are fixed, so it
/// has none to re-date. Its earliest date is the reference period's first day, its latest the delivery date.
/// </remarks>
public class OvernightIndexFutureRateHelper : RateHelper
{
    // Written by the constructor, and on a fresh copy only: the future on the bound curve's index.
    private OvernightIndexFuture future;

    /// <summary>Initializes a new instance of the <see cref="OvernightIndexFutureRateHelper"/> class.</summary>
    /// <param name="price">The future's price.</param>
    /// <param name="valueDate">The first day of the reference period.</param>
    /// <param name="maturityDate">The delivery date.</param>
    /// <param name="overnightIndex">The overnight index; its own curve is replaced by the one the helper is bound
    /// to.</param>
    /// <param name="convexityAdjustment">The convexity adjustment, or <see langword="null"/> for none.</param>
    /// <param name="averagingMethod">Whether the future's rate compounds or averages.</param>
    /// <param name="pillar">The pillar choice.</param>
    /// <param name="customPillarDate">The pillar date for <see cref="PillarChoice.CustomDate"/>, or
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="overnightIndex"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">A custom pillar is missing, before the reference period or after it, each
    /// with QuantLib's text.</exception>
    public OvernightIndexFutureRateHelper(
        double price,
        Date valueDate,
        Date maturityDate,
        OvernightIndex overnightIndex,
        double? convexityAdjustment = null,
        RateAveragingType averagingMethod = RateAveragingType.Compound,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillarDate = null)
        : base(price)
    {
        ArgumentNullException.ThrowIfNull(overnightIndex);
        EnumArguments.Check(pillar, nameof(pillar));
        future = new OvernightIndexFuture(
            overnightIndex.Clone(null), valueDate, maturityDate, convexityAdjustment, averagingMethod);
        earliestDate = valueDate;
        latestDate = maturityDate;
        switch (pillar)
        {
            case PillarChoice.MaturityDate:
                pillarDate = maturityDate;
                break;
            case PillarChoice.LastRelevantDate:
                pillarDate = latestDate;
                break;
            default:
                if (customPillarDate is not Date custom || custom == default)
                {
                    throw new ArgumentException("custom pillar date must be provided", nameof(customPillarDate));
                }

                if (!(custom >= valueDate))
                {
                    throw new ArgumentException(
                        "custom pillar date before start of reference period", nameof(customPillarDate));
                }

                if (!(custom <= maturityDate))
                {
                    throw new ArgumentException(
                        "custom pillar date after end of reference period", nameof(customPillarDate));
                }

                pillarDate = custom;
                break;
        }
    }

    /// <summary>Gets the future's convexity adjustment, 0 for none.</summary>
    public double ConvexityAdjustment => future.ConvexityAdjustment;

    /// <summary>Gets the future, on the bound curve once the helper is bound.</summary>
    public OvernightIndexFuture Future => future;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">No term structure is bound, or the future refuses, as on a missing past
    /// fixing.</exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        return future.Calculate(context).NPV ?? throw new NanoQLException("result not available");
    }

    /// <inheritdoc/>
    protected internal override BootstrapHelper<YieldTermStructure> WithTermStructure(YieldTermStructure t)
    {
        var copy = (OvernightIndexFutureRateHelper)base.WithTermStructure(t);
        copy.future = future.WithIndex(future.OvernightIndex.Clone(t));
        return copy;
    }
}
