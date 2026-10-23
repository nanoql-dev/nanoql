// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdmaffinemodelswapinnervalue.hpp ql/methods/finitedifferences/utilities/fdmaffinemodelswapinnervalue.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Methods;

/// <summary>
/// The exercise value of a swap at a point of an affine model's mesh: the swap's coupons starting on or after the
/// exercise date, each forecast on the forwarding model's curve at the point and discounted on the discounting model's,
/// the fixed leg paid and the floating received for a payer, floored at 0.
/// </summary>
/// <typeparam name="TModel">The model: <see cref="G2"/> (its two factors in the mesh's direction and the next) or <see cref="HullWhite"/> (its short rate in the direction).</typeparam>
/// <remarks>
/// <para>
/// QuantLib clones the swap onto relinkable handles and, at each point, links them to a new
/// <see cref="FdmAffineModelTermStructure"/> for the point's state, whose notifications make the floating coupons
/// forecast on it. nanoql has no relinkable handles; the inner value holds, for each exercise date as QuantLib links a
/// new curve there, a private proxy curve pair whose state it sets at each point, the swap rebuilt on its index cloned
/// onto the forwarding proxy (the S-sr plan's SSR4). The proxies and the swap are a working state: they never leave the
/// object, which is owned by one computation and not thread-safe. The models' dynamics are built once.
/// </para>
/// <para>
/// A time not in the exercise map is refused, where QuantLib reads <c>find(t)->second</c> on <c>end()</c>, undefined
/// behaviour, which an American exercise's step condition reaches (SSR6 (x), master §2.2's undefined-path row).
/// </para>
/// <para>An extension point only for QuantLib-Python's two named classes, <see cref="FdmAffineG2ModelSwapInnerValue"/> and <see cref="FdmAffineHullWhiteModelSwapInnerValue"/>.</para>
/// </remarks>
public class FdmAffineModelSwapInnerValue<TModel> : FdmInnerValueCalculator
    where TModel : ShortRateModel, IAffineModel, ITermStructureConsistentModel
{
    private readonly TModel disModel;
    private readonly TModel fwdModel;
    private readonly FixedVsFloatingSwap swap;
    private readonly ImmutableSortedDictionary<double, Date> exerciseDates;
    private readonly FdmMesher mesher;
    private readonly int direction;
    private readonly EvaluationContext context;

    // the models' dynamics for Hull-White's state, built once
    private readonly OneFactorModelShortRateDynamics? disDynamics;
    private readonly OneFactorModelShortRateDynamics? fwdDynamics;

    // the point's states, and the exercise date's proxies and swap
    private readonly Vector disRate;
    private readonly Vector fwdRate;
    private Proxy? disTs;
    private Proxy? fwdTs;
    private FixedVsFloatingSwap? proxySwap;

    /// <summary>Initializes a new instance of the <see cref="FdmAffineModelSwapInnerValue{TModel}"/> class.</summary>
    /// <param name="disModel">The discounting model, on the discount curve.</param>
    /// <param name="fwdModel">The forwarding model, on the index's curve.</param>
    /// <param name="swap">The swap: a vanilla or an overnight-indexed swap, its nominal constant.</param>
    /// <param name="exerciseDates">The exercise dates by their times on the discount curve's day counter.</param>
    /// <param name="mesher">The mesher.</param>
    /// <param name="direction">The model's first direction in the mesher.</param>
    /// <param name="context">The evaluation context, whose fixings and settings the coupons' amounts read.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The model type is neither <see cref="G2"/> nor <see cref="HullWhite"/>.</exception>
    public FdmAffineModelSwapInnerValue(
        TModel disModel,
        TModel fwdModel,
        FixedVsFloatingSwap swap,
        IReadOnlyDictionary<double, Date> exerciseDates,
        FdmMesher mesher,
        int direction,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(disModel);
        ArgumentNullException.ThrowIfNull(fwdModel);
        ArgumentNullException.ThrowIfNull(swap);
        ArgumentNullException.ThrowIfNull(exerciseDates);
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(context);
        this.disModel = disModel;
        this.fwdModel = fwdModel;
        this.swap = swap;
        this.exerciseDates = exerciseDates.ToImmutableSortedDictionary();
        this.mesher = mesher;
        this.direction = direction;
        this.context = context;

        // QuantLib's getState is specialized for these two models only
        int stateSize;
        if (disModel is HullWhite hwDis && fwdModel is HullWhite hwFwd)
        {
            disDynamics = hwDis.Dynamics();
            fwdDynamics = hwFwd.Dynamics();
            stateSize = 1;
        }
        else if (disModel is G2)
        {
            stateSize = 2;
        }
        else
        {
            throw new ArgumentException($"no inner value on a {typeof(TModel).Name}: G2 and HullWhite only", nameof(disModel));
        }

        disRate = new Vector(stateSize);
        fwdRate = new Vector(stateSize);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">
    /// <paramref name="t"/> isn't an exercise date's time, or a coupon's amount can't be computed.
    /// </exception>
    public override double InnerValue(FdmLinearOpIterator iter, double t)
    {
        ArgumentNullException.ThrowIfNull(iter);
        if (!exerciseDates.TryGetValue(t, out Date iterExerciseDate))
        {
            throw new NanoQLException(
                $"no exercise date at time {t.ToString(System.Globalization.CultureInfo.InvariantCulture)}: the swap's inner value is "
                + "read on its exercise dates only, not on an American exercise's continuous window");
        }

        SetState(disDynamics, t, iter, disRate);
        SetState(fwdDynamics, t, iter, fwdRate);

        if (disTs is null || iterExerciseDate != disTs.ReferenceDate)
        {
            YieldTermStructure discount = disModel.TermStructure;
            disTs = new Proxy(disRate, discount.Calendar, discount.DayCounter, iterExerciseDate, discount.ReferenceDate, disModel);

            YieldTermStructure fwd = fwdModel.TermStructure;
            fwdTs = new Proxy(fwdRate, fwd.Calendar, fwd.DayCounter, iterExerciseDate, fwd.ReferenceDate, fwdModel);
            proxySwap = Rebuilt(swap, fwdTs, context);
        }
        else
        {
            disTs.SetVariable(disRate);
            fwdTs!.SetVariable(fwdRate);
        }

        FixedVsFloatingSwap built = proxySwap!;
        double npv = 0.0;
        for (int j = 0; j < 2; j++)
        {
            foreach (CashFlow i in built.Legs[j])
            {
                npv += ((Coupon)i).AccrualStartDate >= iterExerciseDate ? i.Amount(context) * disTs.Discount(i.Date) : 0.0;
            }

            if (j == 0)
            {
                npv *= -1.0;
            }
        }

        if (built.Type == SwapType.Receiver)
        {
            npv *= -1.0;
        }

        // std::max(0.0, npv)
        return 0.0 < npv ? npv : 0.0;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">
    /// <paramref name="t"/> isn't an exercise date's time, or a coupon's amount can't be computed.
    /// </exception>
    public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);

    // QuantLib's getState: Hull-White's short rate at the point, G2's two factors there
    private void SetState(OneFactorModelShortRateDynamics? dynamics, double t, FdmLinearOpIterator iter, Vector state)
    {
        if (dynamics is not null)
        {
            state[0] = dynamics.ShortRate(t, mesher.Location(iter, direction));
        }
        else
        {
            state[0] = mesher.Location(iter, direction);
            state[1] = mesher.Location(iter, direction + 1);
        }
    }

    // The swap on its index cloned onto the forwarding proxy, by the constructor QuantLib calls for its type.
    private static FixedVsFloatingSwap Rebuilt(FixedVsFloatingSwap swap, YieldTermStructure fwdTs, EvaluationContext context)
    {
        if (swap is OvernightIndexedSwap ois)
        {
            OvernightIndex clonedIndex = ois.OvernightIndex.Clone(fwdTs);
            return new OvernightIndexedSwap(
                ois.Type,
                ois.Nominal(),
                ois.FixedSchedule,
                ois.FixedRate,
                ois.FixedDayCount,
                ois.FloatingSchedule,
                clonedIndex,
                ois.Spread,
                ois.PaymentLag,
                ois.PaymentConvention,
                ois.PaymentCalendar,
                ois.TelescopicValueDates,
                ois.AveragingMethod,
                ois.LookbackDays,
                ois.LockoutDays,
                ois.ApplyObservationShift);
        }

        return new VanillaSwap(
            swap.Type,
            swap.Nominal(),
            swap.FixedSchedule,
            swap.FixedRate,
            swap.FixedDayCount,
            swap.FloatingSchedule,
            swap.IborIndex.Clone(fwdTs),
            swap.Spread,
            swap.FloatingDayCount,
            context,
            swap.PaymentConvention);
    }

    // The proxy: QuantLib's FdmAffineModelTermStructure with its state set in place, private to the inner value.
    private sealed class Proxy : YieldTermStructure
    {
        private readonly Vector r;
        private readonly double t;
        private readonly IAffineModel model;

        public Proxy(Vector r, Calendar? cal, DayCounter dayCounter, Date referenceDate, Date modelReferenceDate, IAffineModel model)
            : base(referenceDate, cal, dayCounter)
        {
            this.r = r.Clone();
            t = dayCounter.YearFraction(modelReferenceDate, referenceDate);
            this.model = model;
        }

        public override Date MaxDate => Date.MaxDate;

        public void SetVariable(Vector state) => state.AsSpan().CopyTo(r.AsSpan());

        protected override double DiscountImpl(double t) => model.DiscountBond(this.t, t + this.t, r);
    }
}
