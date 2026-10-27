// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwisegreeks/bumpinstrumentjacobian.hpp ql/models/marketmodels/pathwisegreeks/bumpinstrumentjacobian.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The derivatives of swaptions' and caps' implied volatilities along a collection's vega bumps, and for each
/// instrument the smallest combination of the bumps that moves its implied volatility by one percent.
/// </summary>
/// <remarks>
/// <para>
/// The instruments are the swaptions, then the caps. The one-percent bump of an instrument with derivative vector v is
/// 0.01 v / ⟨v, v⟩.
/// </para>
/// <para>
/// Immutable (the S-mmp plan's MMPR1 (ii)) but for QuantLib's per-instrument cache, kept under Revision 79 (SMD7): each
/// instrument's derivatives are computed at its first read, once, from the immutable inputs, thread-safe.
/// </para>
/// <para>
/// <b>A refusal</b> (MMPR4 (xx), a documented divergence; <c>Divergences/VolatilityBumpInstrumentJacobianTests.cs</c>):
/// an instrument whose pseudo-derivative refuses its inputs refuses at every read. QuantLib marks it computed before
/// computing it (<c>bumpinstrumentjacobian.cpp:53</c>), so after the refusal a second read returns zeros and the bump
/// matrix a row it never wrote (<c>Matrix(rows, columns)</c> leaves its storage uninitialized).
/// </para>
/// </remarks>
public sealed class VolatilityBumpInstrumentJacobian
{
    private readonly ImmutableArray<VolatilityBumpInstrumentJacobianSwaption> swaptions;
    private readonly ImmutableArray<VolatilityBumpInstrumentJacobianCap> caps;
    private readonly Lazy<(ImmutableArray<double> Volatility, ImmutableArray<double> OnePercent)>[] derivatives;

    /// <summary>Initializes a new instance of the <see cref="VolatilityBumpInstrumentJacobian"/> class.</summary>
    /// <param name="bumps">The vega bumps, on the model the instruments' implied volatilities are read in.</param>
    /// <param name="swaptions">The swaptions.</param>
    /// <param name="caps">The caps.</param>
    public VolatilityBumpInstrumentJacobian(
        VegaBumpCollection bumps,
        IEnumerable<VolatilityBumpInstrumentJacobianSwaption> swaptions,
        IEnumerable<VolatilityBumpInstrumentJacobianCap> caps)
    {
        ArgumentNullException.ThrowIfNull(bumps);
        ArgumentNullException.ThrowIfNull(swaptions);
        ArgumentNullException.ThrowIfNull(caps);
        InputBumps = bumps;
        this.swaptions = [.. swaptions];
        this.caps = [.. caps];
        derivatives = new Lazy<(ImmutableArray<double> Volatility, ImmutableArray<double> OnePercent)>[this.swaptions.Length + this.caps.Length];
        for (int j = 0; j < derivatives.Length; ++j)
        {
            int instrument = j;
            derivatives[j] = new Lazy<(ImmutableArray<double> Volatility, ImmutableArray<double> OnePercent)>(
                () => Compute(instrument), LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>Gets the vega bumps.</summary>
    public VegaBumpCollection InputBumps { get; }

    /// <summary>The derivatives of an instrument's implied volatility along each bump.</summary>
    /// <param name="j">The instrument: a swaption's index, or the swaptions' number plus a cap's.</param>
    /// <returns>The derivatives, one per bump.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="j"/> isn't an instrument.</exception>
    /// <exception cref="ArgumentException">The instrument's pseudo-derivative refuses its inputs, at every read.</exception>
    /// <exception cref="NanoQLException">A cap's implied volatility can't be solved, at every read.</exception>
    public ImmutableArray<double> DerivativesVolatility(int j) => Instrument(j).Volatility;

    /// <summary>
    /// The smallest combination of the bumps that moves an instrument's implied volatility by one percent: 0.01 v / ⟨v,
    /// v⟩, v its <see cref="DerivativesVolatility"/>.
    /// </summary>
    /// <param name="j">The instrument: a swaption's index, or the swaptions' number plus a cap's.</param>
    /// <returns>The combination, one weight per bump.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="j"/> isn't an instrument.</exception>
    /// <exception cref="ArgumentException">The instrument's pseudo-derivative refuses its inputs, at every read.</exception>
    /// <exception cref="NanoQLException">A cap's implied volatility can't be solved, at every read.</exception>
    public ImmutableArray<double> OnePercentBump(int j) => Instrument(j).OnePercent;

    /// <summary>Every instrument's one-percent bump, a row each.</summary>
    /// <returns>A new matrix, a row per instrument and a column per bump.</returns>
    /// <exception cref="ArgumentException">An instrument's pseudo-derivative refuses its inputs.</exception>
    /// <exception cref="NanoQLException">A cap's implied volatility can't be solved.</exception>
    public Matrix GetAllOnePercentBumps()
    {
        var bumpMatrix = new Matrix(derivatives.Length, InputBumps.NumberBumps);
        for (int i = 0; i < derivatives.Length; ++i)
        {
            derivatives[i].Value.OnePercent.AsSpan().CopyTo(bumpMatrix.Row(i));
        }

        return bumpMatrix;
    }

    private (ImmutableArray<double> Volatility, ImmutableArray<double> OnePercent) Instrument(int j)
    {
        if (!(j >= 0 && j < derivatives.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(j), "too high index passed to VolatilityBumpInstrumentJacobian::derivativesVolatility");
        }

        return derivatives[j].Value;
    }

    private (ImmutableArray<double> Volatility, ImmutableArray<double> OnePercent) Compute(int j)
    {
        MarketModel model = InputBumps.AssociatedModel;
        int numberSteps = model.NumberOfSteps;
        Matrix[] fullDerivatives = new Matrix[numberSteps];
        if (j < swaptions.Length)
        {
            // ok its a swaptions
            var thisPseudo = new SwaptionPseudoDerivative(model, swaptions[j].StartIndex, swaptions[j].EndIndex);
            for (int i = 0; i < numberSteps; ++i)
            {
                fullDerivatives[i] = thisPseudo.VolatilityDerivative(i);
            }
        }
        else
        {
            // its a cap; the first df shouldn't make any difference
            VolatilityBumpInstrumentJacobianCap cap = caps[j - swaptions.Length];
            var thisPseudo = new CapPseudoDerivative(model, cap.Strike, cap.StartIndex, cap.EndIndex, 1.0);
            for (int i = 0; i < numberSteps; ++i)
            {
                fullDerivatives[i] = thisPseudo.VolatilityDerivative(i);
            }
        }

        ImmutableArray<VegaBumpCluster> bumps = InputBumps.AllBumps;
        double[] volatility = new double[bumps.Length];
        double sizesq = 0.0;
        for (int k = 0; k < bumps.Length; ++k)
        {
            VegaBumpCluster bump = bumps[k];
            double v = 0.0;
            for (int i = bump.StepBegin; i < bump.StepEnd; ++i)
            {
                Matrix fullDerivative = fullDerivatives[i];
                for (int f = bump.FactorBegin; f < bump.FactorEnd; ++f)
                {
                    for (int r = bump.RateBegin; r < bump.RateEnd; ++r)
                    {
                        v += fullDerivative[r, f];
                    }
                }
            }

            volatility[k] = v;
            sizesq += v * v;
        }

        double[] onePercent = new double[bumps.Length];
        for (int k = 0; k < bumps.Length; ++k)
        {
            onePercent[k] = 0.01 * volatility[k] / sizesq;
        }

        return ([.. volatility], [.. onePercent]);
    }
}
