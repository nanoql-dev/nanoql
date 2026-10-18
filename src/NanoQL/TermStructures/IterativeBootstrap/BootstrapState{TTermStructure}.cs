// SPDX-FileCopyrightText: Copyright (C) 2008, 2011, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/iterativebootstrap.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The working state of a curve under construction: QuantLib's friend access to the curve's instruments, dates, times,
/// data, interpolation and maximum date, which its bootstrap reads and writes.
/// </summary>
/// <typeparam name="TTermStructure">The kind of term structure bootstrapped.</typeparam>
/// <remarks>
/// <para>
/// A working type, as <see cref="ProblemState"/> is: it lives only while its curve's constructor runs, belongs to that
/// one computation and isn't thread-safe. The curve under construction answers from it, as QuantLib's curve answers
/// from its members while its bootstrap fills them, and freezes it into its immutable nodes when its bootstrap
/// returns. A bootstrap of one's own reads and writes its members and sets <see cref="Interpolation"/> itself.
/// </para>
/// <para>
/// The instruments are the curve's helpers, each re-dated for the curve's evaluation date (see
/// <see cref="BootstrapHelper{TTermStructure}"/>). A bootstrap binds the ones it prices to the curve with
/// <see cref="Bind"/> and keeps the bound copies in <see cref="Instruments"/>, as QuantLib's binds its curve's helpers
/// in place.
/// </para>
/// </remarks>
public sealed class BootstrapState<TTermStructure> : IBootstrapState
    where TTermStructure : TermStructure
{
    internal BootstrapState(
        TTermStructure curve,
        IEnumerable<BootstrapHelper<TTermStructure>> instruments,
        double accuracy,
        EvaluationContext context,
        string paramName)
    {
        Curve = curve;
        Accuracy = accuracy;
        Context = context;
        var helpers = new List<BootstrapHelper<TTermStructure>>();
        foreach (BootstrapHelper<TTermStructure> helper in instruments)
        {
            if (helper is null)
            {
                throw new ArgumentException("null rate helper", paramName);
            }

            helpers.Add(helper.RedatedFor(context));
        }

        Instruments = [.. helpers];
    }

    /// <summary>Gets the curve under construction.</summary>
    public TTermStructure Curve { get; }

    /// <summary>
    /// Gets the curve's helpers, re-dated for its evaluation date; a bootstrap sorts them in place and replaces the ones
    /// it prices with copies bound to the curve.
    /// </summary>
    public BootstrapHelper<TTermStructure>[] Instruments { get; }

    /// <summary>
    /// Gets or sets a global bootstrap's additional helpers, which add no pillar of their own: re-dated for the curve's
    /// evaluation date, and the ones alive at its first date bound to the curve. Empty for another bootstrap.
    /// </summary>
    public BootstrapHelper<TTermStructure>[] AdditionalHelpers { get; set; } = [];

    /// <summary>Gets or sets the dates of the curve's nodes, the first one the curve's initial date.</summary>
    public Date[] Dates { get; set; } = [];

    /// <summary>Gets or sets the times of the curve's nodes, from the reference date.</summary>
    public double[] Times { get; set; } = [];

    /// <summary>Gets or sets the curve's data at its nodes, as its traits read them (discount factors, rates).</summary>
    public double[] Data { get; set; } = [];

    /// <summary>Gets or sets the interpolation the curve answers from, over its first nodes.</summary>
    public Interpolation? Interpolation { get; set; }

    /// <summary>
    /// Gets or sets the latest date the curve's helpers need, which may be past the last node: QuantLib's
    /// <c>maxDate_</c>.
    /// </summary>
    public Date? MaxDate { get; set; }

    /// <summary>Gets the accuracy a bootstrap uses when it isn't given one: QuantLib's curve's <c>accuracy_</c>.</summary>
    public double Accuracy { get; }

    /// <summary>Gets the evaluation context the curve is built in.</summary>
    public EvaluationContext Context { get; }

    /// <inheritdoc/>
    ReadOnlySpan<double> IBootstrapState.Times => Times;

    /// <inheritdoc/>
    ReadOnlySpan<double> IBootstrapState.Data => Data;

    /// <inheritdoc/>
    IReadOnlyList<IBootstrapHelper> IBootstrapState.Instruments => Instruments;

    /// <inheritdoc/>
    IReadOnlyList<IBootstrapHelper> IBootstrapState.AdditionalHelpers => AdditionalHelpers;

    // The interpolation's recipe, QuantLib's interpolateWithoutUpdate on the curve's first nodes: how many points, and
    // whether a global interpolator that refused them is replaced by linear interpolation. Rebuild is QuantLib's
    // interpolation_.update().
    internal int Points { get; set; }

    internal bool LinearFallback { get; set; }

    /// <summary>
    /// A copy of a helper re-dated for the curve's evaluation date and bound to the curve under construction: QuantLib's
    /// <c>setTermStructure(this)</c>.
    /// </summary>
    /// <param name="helper">The helper.</param>
    /// <returns>The bound copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="helper"/> is <see langword="null"/>.</exception>
    public BootstrapHelper<TTermStructure> Bind(BootstrapHelper<TTermStructure> helper)
    {
        ArgumentNullException.ThrowIfNull(helper);
        return helper.RedatedFor(Context).WithTermStructure(Curve);
    }

    internal void Rebuild<TInterpolator>(TInterpolator interpolator)
        where TInterpolator : struct, IInterpolator
    {
        ReadOnlySpan<double> x = Times.AsSpan(0, Points);
        ReadOnlySpan<double> y = Data.AsSpan(0, Points);
        Interpolation = LinearFallback ? default(Linear).Interpolate(x, y) : interpolator.Interpolate(x, y);
    }
}
