// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/jointstochasticprocess.hpp ql/processes/jointstochasticprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;
using static NanoQL.Numerics.MatrixFunctions;
using static NanoQL.Numerics.PseudoSqrtFunctions;

namespace NanoQL.Processes;

/// <summary>
/// A multi-model process for hybrid products: its constituents' states stacked, their factors correlated across models
/// by a derived class's cross-model correlation.
/// </summary>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib: a derived class gives the cross-model correlation, whether it depends on the
/// state, the numeraire and the hooks around each step. The constituents are fixed at construction.
/// </para>
/// <para>
/// Each step builds its correlation from the constituents' standard deviations at the step's state, where QuantLib
/// caches it by (t₀, dt) when the derived class says the cross-model correlation doesn't depend on the state
/// (<c>jointstochasticprocess.cpp:189-268</c>): its key leaves out the state the constituents' deviations read, so a
/// later path at the same step can reuse an earlier path's matrix, and no class in QuantLib derives the process to
/// measure what the cache pays (the S-hes plan's HR4 (iii)).
/// </para>
/// </remarks>
public abstract class JointStochasticProcess : StochasticProcess
{
    private readonly ImmutableArray<StochasticProcess> l;
    private readonly int size;
    private readonly int factors;
    private readonly int modelFactors;
    private readonly int[] vsize;
    private readonly int[] vfactors;

    /// <summary>Initializes a new instance of the <see cref="JointStochasticProcess"/> class.</summary>
    /// <param name="l">The constituent processes.</param>
    /// <param name="factors">The joint process's factors, at most its size, or <see langword="null"/> for the sum of the
    /// constituents' factors.</param>
    /// <exception cref="ArgumentNullException">A constituent is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="factors"/> exceeds the joint size ("too many factors
    /// given").</exception>
    protected JointStochasticProcess(ImmutableArray<StochasticProcess> l, int? factors = null)
    {
        this.l = l.IsDefault ? [] : l;
        vsize = new int[this.l.Length + 1];
        vfactors = new int[this.l.Length + 1];
        for (int i = 0; i < this.l.Length; ++i)
        {
            ArgumentNullException.ThrowIfNull(this.l[i], nameof(l));
            vsize[i] = size;
            size += this.l[i].Size;
            vfactors[i] = modelFactors;
            modelFactors += this.l[i].Factors;
        }

        vsize[this.l.Length] = size;
        vfactors[this.l.Length] = modelFactors;

        if (factors is not { } f)
        {
            this.factors = modelFactors;
        }
        else
        {
            if (!(f <= size))
            {
                throw new ArgumentException("too many factors given", nameof(factors));
            }

            this.factors = f;
        }
    }

    /// <inheritdoc/>
    public override int Size => size;

    /// <inheritdoc/>
    public override int Factors => factors;

    /// <summary>Gets the constituent processes.</summary>
    public ImmutableArray<StochasticProcess> Constituents => l;

    /// <inheritdoc/>
    public override Vector InitialValues()
    {
        var retVal = new Vector(Size);
        for (int i = 0; i < l.Length; ++i)
        {
            Vector pInitValues = l[i].InitialValues();
            pInitValues.AsSpan().CopyTo(retVal.AsSpan()[vsize[i]..]);
        }

        return retVal;
    }

    /// <inheritdoc/>
    public override Vector Drift(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        var retVal = new Vector(Size);
        for (int i = 0; i < l.Length; ++i)
        {
            Vector pDrift = l[i].Drift(t, Slice(x, i));
            pDrift.AsSpan().CopyTo(retVal.AsSpan()[vsize[i]..]);
        }

        return retVal;
    }

    /// <inheritdoc/>
    public override Vector Expectation(double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(x0);
        var retVal = new Vector(Size);
        for (int i = 0; i < l.Length; ++i)
        {
            Vector pExpectation = l[i].Expectation(t0, Slice(x0, i), dt);
            pExpectation.AsSpan().CopyTo(retVal.AsSpan()[vsize[i]..]);
        }

        return retVal;
    }

    /// <inheritdoc/>
    /// <remarks>The pseudo square root of the covariance over a step of 0.001 per unit of time, QuantLib's.</remarks>
    public override Matrix Diffusion(double t, Vector x)
    {
        // might need some improvement in the future
        const double dt = 0.001;
        return PseudoSqrt(Covariance(t, x, dt) / dt);
    }

    /// <inheritdoc/>
    public override Matrix Covariance(double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(x0);

        // get the model intrinsic covariance matrix
        var retVal = new Matrix(Size, Size, 0.0);
        for (int j = 0; j < l.Length; ++j)
        {
            int vs = vsize[j];
            Matrix pCov = l[j].Covariance(t0, Slice(x0, j), dt);
            for (int i = 0; i < pCov.Rows; ++i)
            {
                for (int k = 0; k < pCov.Columns; ++k)
                {
                    retVal[vs + i, vs + k] = pCov[i, k];
                }
            }
        }

        // add the cross model covariance matrix
        Vector diagonal = retVal.Diagonal();
        var volatility = new Vector(diagonal.Size);
        for (int i = 0; i < diagonal.Size; ++i)
        {
            volatility[i] = Math.Sqrt(diagonal[i]);
        }

        Matrix crossModelCovar = CrossModelCorrelation(t0, x0);
        for (int i = 0; i < Size; ++i)
        {
            for (int j = 0; j < Size; ++j)
            {
                crossModelCovar[i, j] *= volatility[i] * volatility[j];
            }
        }

        return retVal + crossModelCovar;
    }

    /// <inheritdoc/>
    public override Matrix StdDeviation(double t0, Vector x0, double dt) => PseudoSqrt(Covariance(t0, x0, dt));

    /// <inheritdoc/>
    public override Vector Apply(Vector x0, Vector dx)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dx);
        var retVal = new Vector(Size);
        for (int i = 0; i < l.Length; ++i)
        {
            Vector pApply = l[i].Apply(Slice(x0, i), Slice(dx, i));
            pApply.AsSpan().CopyTo(retVal.AsSpan()[vsize[i]..]);
        }

        return retVal;
    }

    /// <inheritdoc/>
    /// <remarks>The step's correlation is built at each call (HR4 (iii)); the derived class's
    /// <see cref="PreEvolve"/> sees the correlated increments before the constituents step, and its
    /// <see cref="PostEvolve"/> returns the joint state from theirs.</remarks>
    public override Vector Evolve(double t0, Vector x0, double dt, Vector dw)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dw);

        Matrix cov = Covariance(t0, x0, dt);
        Vector diagonal = cov.Diagonal();
        var sqrtDiag = new Vector(diagonal.Size);
        for (int i = 0; i < diagonal.Size; ++i)
        {
            sqrtDiag[i] = Math.Sqrt(diagonal[i]);
        }

        for (int i = 0; i < cov.Rows; ++i)
        {
            for (int j = i; j < cov.Columns; ++j)
            {
                double div = sqrtDiag[i] * sqrtDiag[j];
                cov[i, j] = cov[j, i] = div > 0 ? cov[i, j] / div : 0.0;
            }
        }

        var diff = new Matrix(Size, modelFactors, 0.0);
        for (int j = 0; j < l.Length; ++j)
        {
            int vs = vsize[j];
            int vf = vfactors[j];

            Matrix stdDev = l[j].StdDeviation(t0, Slice(x0, j), dt);
            for (int i = 0; i < stdDev.Rows; ++i)
            {
                double inner = 0.0;
                for (int k = 0; k < stdDev.Columns; ++k)
                {
                    inner += stdDev[i, k] * stdDev[i, k];
                }

                double vol = Math.Sqrt(inner);
                for (int k = 0; k < stdDev.Columns; ++k)
                {
                    // keep the svd happy where the row is zero
                    stdDev[i, k] = vol > 0.0 ? stdDev[i, k] / vol : 100 * i * NumericLimits.QL_EPSILON;
                }
            }

            var svd = new SVD(stdDev);
            Vector s = svd.SingularValues();
            var w = new Matrix(s.Size, s.Size, 0.0);
            for (int i = 0; i < s.Size; ++i)
            {
                if (Math.Abs(s[i]) > Math.Sqrt(NumericLimits.QL_EPSILON))
                {
                    w[i, i] = 1.0 / s[i];
                }
            }

            Matrix inv = svd.U() * w * Transpose(svd.V());
            for (int i = 0; i < stdDev.Rows; ++i)
            {
                for (int k = 0; k < inv.Columns; ++k)
                {
                    diff[i + vs, vf + k] = inv[i, k];
                }
            }
        }

        Matrix rs = RankReducedSqrt(cov, factors, 1.0, SalvagingAlgorithmType.Spectral);
        if (rs.Columns < factors)
        {
            // less eigenvalues than expected factors: fill the rest with zeros
            var tmp = new Matrix(cov.Rows, factors, 0.0);
            for (int i = 0; i < cov.Rows; ++i)
            {
                for (int k = 0; k < rs.Columns; ++k)
                {
                    tmp[i, k] = rs[i, k];
                }
            }

            rs = tmp;
        }

        Matrix m = Transpose(diff) * rs;
        Vector dv = m * dw;

        PreEvolve(t0, x0, dt, dv);

        var retVal = new Vector(Size);
        for (int i = 0; i < l.Length; ++i)
        {
            var dz = new Vector(l[i].Factors);
            dv.AsSpan().Slice(vfactors[i], l[i].Factors).CopyTo(dz.AsSpan());
            var x = new Vector(l[i].Size);
            x0.AsSpan().Slice(vsize[i], l[i].Size).CopyTo(x.AsSpan());
            Vector r = l[i].Evolve(t0, x, dt, dz);
            r.AsSpan().CopyTo(retVal.AsSpan()[vsize[i]..]);
        }

        return PostEvolve(t0, x0, dt, dv, retVal);
    }

    /// <summary>Called with the correlated increments before the constituents step.</summary>
    /// <param name="t0">The step's start.</param>
    /// <param name="x0">The joint state at the start.</param>
    /// <param name="dt">The step.</param>
    /// <param name="dw">The correlated increments, the constituents' factors stacked.</param>
    public abstract void PreEvolve(double t0, Vector x0, double dt, Vector dw);

    /// <summary>The joint state after a step, from the constituents' stepped states.</summary>
    /// <param name="t0">The step's start.</param>
    /// <param name="x0">The joint state at the start.</param>
    /// <param name="dt">The step.</param>
    /// <param name="dw">The correlated increments.</param>
    /// <param name="y0">The constituents' stepped states, stacked.</param>
    /// <returns>The joint state.</returns>
    public abstract Vector PostEvolve(double t0, Vector x0, double dt, Vector dw, Vector y0);

    /// <summary>The numeraire at a time and state.</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The joint state.</param>
    /// <returns>The numeraire's value.</returns>
    public abstract double Numeraire(double t, Vector x);

    /// <summary>Whether the cross-model correlation depends on the state.</summary>
    /// <returns><see langword="true"/> where it does.</returns>
    public abstract bool CorrelationIsStateDependent();

    /// <summary>The cross-model correlation at a time and state: the correlation of each pair of variables of
    /// different constituents, zero within a constituent.</summary>
    /// <param name="t0">The time.</param>
    /// <param name="x0">The joint state.</param>
    /// <returns>The correlation, a <see cref="Size"/> square matrix the caller may modify.</returns>
    public abstract Matrix CrossModelCorrelation(double t0, Vector x0);

    /// <summary>The time of a date: the first constituent's.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The time.</returns>
    /// <exception cref="ArgumentException">There's no constituent ("process list is empty").</exception>
    public override double Time(Date date)
    {
        if (l.IsEmpty)
        {
            throw new ArgumentException("process list is empty");
        }

        return l[0].Time(date);
    }

    /// <summary>The <paramref name="i"/>-th constituent's variables of a joint state.</summary>
    /// <param name="x">The joint state.</param>
    /// <param name="i">The constituent's index.</param>
    /// <returns>A copy of its variables.</returns>
    protected Vector Slice(Vector x, int i)
    {
        ArgumentNullException.ThrowIfNull(x);
        int n = vsize[i + 1] - vsize[i];
        var y = new Vector(n);
        x.AsSpan().Slice(vsize[i], n).CopyTo(y.AsSpan());
        return y;
    }
}
