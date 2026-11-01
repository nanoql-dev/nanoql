// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/discretizedasset.hpp ql/discretizedasset.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL;

/// <summary>The discretized asset class used by numerical methods: an asset's values on a lattice, rolled back in time.</summary>
/// <remarks>
/// <para>
/// A working type, as a random-number generator is: mutable, owned by one calculation and not thread-safe. An engine
/// builds its assets inside its calculation, initializes them on a lattice, rolls them back and reads their value; no
/// immutable object holds one.
/// </para>
/// <para>
/// <b>Its values and buffers.</b> <see cref="Values"/> is a view of the current step's values in the asset's own
/// buffer, which the asset's code reads and writes as QuantLib's <c>values_</c>. The asset holds two buffers. A lattice
/// reserves both at the widest step a rollback from the initial time passes through (<see cref="ReserveCapacity"/>),
/// then steps back into the spare one (<see cref="StepValues"/>) and makes it the current one
/// (<see cref="CommitStep"/>), so a rollback allocates nothing, where QuantLib's allocates an array per step. A view is
/// valid until the asset's next step or reset.
/// </para>
/// <para>
/// <b>Adjustments.</b> <see cref="PreAdjustValues"/> and <see cref="PostAdjustValues"/> run the protected
/// <see cref="PreAdjustValuesImpl"/> and <see cref="PostAdjustValuesImpl"/> once per time: each remembers the last time
/// it adjusted at, starting at QuantLib's <c>QL_MAX_REAL</c>, and, as QuantLib's, <see cref="Initialize"/> doesn't
/// forget it, so an asset initialized again at its last adjustment time skips that adjustment.
/// </para>
/// <para>
/// An extension point, as in QuantLib: the discount bond, the option, the swap, the swaption and the callable bond
/// derive from it, as later ports' cap, vanilla option, barrier and convertible assets will.
/// </para>
/// </remarks>
[WorkingType]
public abstract class DiscretizedAsset
{
    private double[] current = [];
    private double[] spare = [];
    private int count;
    private Lattice? method;
    private double latestPreAdjustment = NumericLimits.QL_MAX_REAL;
    private double latestPostAdjustment = NumericLimits.QL_MAX_REAL;

    /// <summary>Initializes a new instance of the <see cref="DiscretizedAsset"/> class, with no values.</summary>
    protected DiscretizedAsset()
    {
    }

    /// <summary>Gets or sets the asset's time, which the lattice moves as it rolls the asset back.</summary>
    public double Time { get; set; }

    /// <summary>Gets the asset's values at its time: a view of the asset's current buffer, of the current step's size.</summary>
    /// <remarks>The view is valid until the asset's next step or reset; a caller that keeps the values copies them.</remarks>
    public Span<double> Values => current.AsSpan(0, count);

    /// <summary>Gets the lattice the asset was initialized on, or <see langword="null"/> before <see cref="Initialize"/>.</summary>
    public Lattice? Method => method;

    /// <summary>Initializes the asset on a lattice at the given time.</summary>
    /// <param name="lattice">The lattice.</param>
    /// <param name="t">The time.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lattice"/> is null.</exception>
    public void Initialize(Lattice lattice, double t)
    {
        ArgumentNullException.ThrowIfNull(lattice);
        method = lattice;
        lattice.Initialize(this, t);
    }

    /// <summary>Rolls the asset back until the given time, performing any needed adjustment.</summary>
    /// <param name="to">The time to roll back to.</param>
    /// <exception cref="NanoQLException">The asset isn't initialized.</exception>
    public void Rollback(double to) => InitializedMethod().Rollback(this, to);

    /// <summary>Rolls the asset back until the given time, without the final adjustment.</summary>
    /// <param name="to">The time to roll back to.</param>
    /// <exception cref="NanoQLException">The asset isn't initialized.</exception>
    public void PartialRollback(double to) => InitializedMethod().PartialRollback(this, to);

    /// <summary>The asset's present value on its lattice.</summary>
    /// <returns>The present value.</returns>
    /// <exception cref="NanoQLException">The asset isn't initialized.</exception>
    public double PresentValue() => InitializedMethod().PresentValue(this);

    /// <summary>Sets the asset's values to an array of the given size, with values depending on the particular asset.</summary>
    /// <param name="size">The number of values.</param>
    public abstract void Reset(int size);

    /// <summary>
    /// Runs the pre-adjustment, once per time: invoked after a rollback and before any other asset (an option on this
    /// one) looks at the values; payments at times the rollback already spanned are added here.
    /// </summary>
    public void PreAdjustValues()
    {
        if (!ComparisonFunctions.CloseEnough(Time, latestPreAdjustment))
        {
            PreAdjustValuesImpl();
            latestPreAdjustment = Time;
        }
    }

    /// <summary>
    /// Runs the post-adjustment, once per time: invoked after a rollback and after any other asset had its chance to
    /// look at the values; payments at the present time, which an option exercised now doesn't include, are added here.
    /// </summary>
    public void PostAdjustValues()
    {
        if (!ComparisonFunctions.CloseEnough(Time, latestPostAdjustment))
        {
            PostAdjustValuesImpl();
            latestPostAdjustment = Time;
        }
    }

    /// <summary>Runs both the pre- and the post-adjustment.</summary>
    public void AdjustValues()
    {
        PreAdjustValues();
        PostAdjustValues();
    }

    /// <summary>The times at which the numerical method should stop while rolling the asset back: payment and exercise times.</summary>
    /// <returns>The times, not guaranteed to be sorted.</returns>
    public abstract ImmutableArray<double> MandatoryTimes();

    /// <summary>
    /// For a lattice: reserves both buffers at the given width, the widest step the asset's rollback passes through, so
    /// that its steps allocate nothing. A lattice calls it in its <see cref="Lattice.Initialize"/>, before
    /// <see cref="Reset"/>.
    /// </summary>
    /// <param name="width">The width, in values.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> is negative.</exception>
    public void ReserveCapacity(int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        if (current.Length < width)
        {
            double[] wider = new double[width];
            current.AsSpan(0, count).CopyTo(wider);
            current = wider;
        }

        if (spare.Length < width)
        {
            spare = new double[width];
        }
    }

    /// <summary>
    /// For a lattice: the spare buffer, as a view of the given size, which the lattice fills with the asset's values one
    /// step back and then hands back with <see cref="CommitStep"/>.
    /// </summary>
    /// <param name="size">The number of values at the step back.</param>
    /// <returns>The view, whose contents are undefined until the lattice writes them.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    /// <remarks>A width the lattice didn't reserve is grown here, which allocates.</remarks>
    public Span<double> StepValues(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        if (spare.Length < size)
        {
            spare = new double[size];
        }

        return spare.AsSpan(0, size);
    }

    /// <summary>
    /// For a lattice: makes the values written into <see cref="StepValues"/> the asset's, of the given size, at the given
    /// time. The two buffers swap, so the previous values' buffer becomes the spare one.
    /// </summary>
    /// <param name="size">The number of values written.</param>
    /// <param name="time">The asset's new time.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="size"/> is negative, or larger than the view <see cref="StepValues"/> gave.
    /// </exception>
    public void CommitStep(int size, double time)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, spare.Length);
        (current, spare) = (spare, current);
        count = size;
        Time = time;
    }

    /// <summary>Sets the values to the given size, each to the given value: QuantLib's <c>values_ = Array(size, value)</c>.</summary>
    /// <param name="size">The number of values.</param>
    /// <param name="value">The value of each.</param>
    /// <returns>The values, as <see cref="Values"/> gives them.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    protected Span<double> ResetValues(int size, double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        if (current.Length < size)
        {
            current = new double[size];
        }

        count = size;
        Span<double> values = current.AsSpan(0, size);
        values.Fill(value);
        return values;
    }

    /// <summary>Checks whether the asset was rolled to the given time.</summary>
    /// <param name="t">The time.</param>
    /// <returns><see langword="true"/> when the grid's time for <paramref name="t"/> is the asset's time.</returns>
    /// <exception cref="NanoQLException">
    /// The asset isn't initialized, or <paramref name="t"/> isn't on the lattice's grid (the grid's own refusal).
    /// </exception>
    protected bool IsOnTime(double t)
    {
        TimeGrid grid = InitializedMethod().TimeGrid;
        return ComparisonFunctions.CloseEnough(grid[grid.Index(t)], Time);
    }

    /// <summary>Performs the actual pre-adjustment; does nothing by default.</summary>
    protected virtual void PreAdjustValuesImpl()
    {
    }

    /// <summary>Performs the actual post-adjustment; does nothing by default.</summary>
    protected virtual void PostAdjustValuesImpl()
    {
    }

    private Lattice InitializedMethod()
    {
        // QuantLib dereferences its null method_ here.
        return method ?? throw new NanoQLException("asset not initialized");
    }

    /// <summary>Indicates whether a coupon is adjusted in <see cref="PreAdjustValues"/> or <see cref="PostAdjustValues"/>.</summary>
    /// <remarks>
    /// <c>protected internal</c>, where QuantLib's is protected: the discretized swaption passes its snapped coupons'
    /// adjustments to the discretized swap's internal constructor, which C# refuses a protected type (CS0051).
    /// </remarks>
    protected internal enum CouponAdjustment
    {
        /// <summary>In the pre-adjustment.</summary>
        Pre = 0,

        /// <summary>In the post-adjustment.</summary>
        Post = 1,
    }
}
