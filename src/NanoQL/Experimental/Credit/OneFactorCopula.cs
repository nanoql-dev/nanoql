// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/onefactorcopula.hpp ql/experimental/credit/onefactorcopula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace NanoQL.Experimental;

/// <summary>
/// The one-factor copula models' base: John Hull and Alan White, "The Perfect Copula", June 2006. The variables
/// Yᵢ = a·M + √(1 − a²)·Zᵢ, M and Zᵢ independent of zero mean and unit variance, their correlation a² the copula's;
/// a name's default by t is Yᵢ below F_Y⁻¹(Qᵢ(t)), so that, given M = m, it defaults with probability
/// F_Z((F_Y⁻¹(p) − a·m)/√(1 − a²)). The densities of M and Z are the derived models'; F_Y is tabulated by them unless
/// it is analytic, and the integrals over M are Euler sums on a midpoint grid.
/// </summary>
/// <remarks>
/// <para>
/// Immutable (the S-xcr plan's XR5): QuantLib's lazy copula observes its correlation quote; here a correlation is a
/// <see langword="double"/> and a derived model tabulates F_Y at construction, in QuantLib's loop order. The
/// correlation lies in [0, 1]: QuantLib's accepts [−1, 1] and takes its square root, a negative one reading NaN
/// (<c>onefactorcopula.hpp:110-112</c>, <c>onefactorcopula.cpp:35</c>; XR8 (xxxiii)). At 1 the conditional
/// probability divides by √(1 − c) = 0 and reads the comonotonic step, QuantLib's continuous limit, kept (XR8
/// (xlviii)): the Gaussian's integral at p = 0.05 reads 0.0545030778829 at 1 and 0.0545030775379 at 0.9999, the Euler
/// grid's 0.0545 for 0.05 at such correlations QuantLib's own <c>\todo</c> (<c>onefactorcopula.hpp:100</c>), and
/// <see cref="CheckMoments"/> refuses both, as QuantLib's does (<c>Conventions/AnalyticLossModelConventionTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class OneFactorCopula
{
    private readonly double max;
    private readonly int steps;
    private readonly double min;

    // Tabulated numerical solution of the cumulated distribution of Y
    private ImmutableArray<double> y = [];
    private ImmutableArray<double> cumulativeY = [];

    /// <summary>Initializes a new instance of the <see cref="OneFactorCopula"/> class.</summary>
    /// <param name="correlation">The correlation a², in [0, 1].</param>
    /// <param name="maximum">The factor grid's upper end.</param>
    /// <param name="integrationSteps">The factor grid's number of steps.</param>
    /// <param name="minimum">The factor grid's lower end.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="correlation"/> lies outside [0, 1].</exception>
    protected OneFactorCopula(double correlation, double maximum = 5.0, int integrationSteps = 50, double minimum = -5.0)
    {
        if (!(correlation >= 0 && correlation <= 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(correlation), correlation, "correlation out of range [0, 1]: its square root is the factor's weight");
        }

        Correlation = correlation;
        max = maximum;
        steps = integrationSteps;
        min = minimum;
    }

    /// <summary>Gets the single correlation parameter, a².</summary>
    public double Correlation { get; }

    /// <summary>Gets the number of steps of the factor grid.</summary>
    protected int Steps => steps;

    /// <summary>The density of M; a derived model's has zero mean and unit variance.</summary>
    /// <param name="m">The factor's value.</param>
    /// <returns>The density.</returns>
    public abstract double Density(double m);

    /// <summary>The cumulative distribution of Z; a derived model's has zero mean and unit variance.</summary>
    /// <param name="z">The value.</param>
    /// <returns>The probability.</returns>
    public abstract double CumulativeZ(double z);

    /// <summary>The cumulative distribution of Y, linear on the tabulated values, flat beyond them.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="NanoQLException">The model tabulates no distribution ("cumulative Y not tabulated
    /// yet").</exception>
    public virtual double CumulativeY(double value)
    {
        if (y.IsEmpty)
        {
            throw new NanoQLException("cumulative Y not tabulated yet");
        }

        // linear interpolation on the tabulated cumulative distribution of Y
        if (value < y[0])
        {
            return cumulativeY[0];
        }

        for (int i = 0; i < y.Length; i++)
        {
            if (y[i] > value)
            {
                return (((y[i] - value) * cumulativeY[i - 1]) + ((value - y[i - 1]) * cumulativeY[i])) / (y[i] - y[i - 1]);
            }
        }

        return cumulativeY[^1];
    }

    /// <summary>The inverse cumulative distribution of Y, linear on the tabulated values, flat beyond them.</summary>
    /// <param name="p">The probability.</param>
    /// <returns>The value.</returns>
    /// <exception cref="NanoQLException">The model tabulates no distribution ("cumulative Y not tabulated
    /// yet").</exception>
    public virtual double InverseCumulativeY(double p)
    {
        if (y.IsEmpty)
        {
            throw new NanoQLException("cumulative Y not tabulated yet");
        }

        // linear interpolation on the tabulated cumulative distribution of Y
        if (p < cumulativeY[0])
        {
            return y[0];
        }

        for (int i = 0; i < cumulativeY.Length; i++)
        {
            if (cumulativeY[i] > p)
            {
                return (((cumulativeY[i] - p) * y[i - 1]) + ((p - cumulativeY[i - 1]) * y[i])) / (cumulativeY[i] - cumulativeY[i - 1]);
            }
        }

        return y[^1];
    }

    /// <summary>A name's default probability given the factor: F_Z((F_Y⁻¹(p) − a·m)/√(1 − a²)); below a probability
    /// of 1e-10, 0.</summary>
    /// <param name="prob">The unconditional probability.</param>
    /// <param name="m">The factor's value.</param>
    /// <returns>The conditional probability.</returns>
    /// <exception cref="NanoQLException">The probability falls outside [0, 1] ("conditional probability … out of
    /// range"), as it does at a correlation of 1 where F_Y⁻¹(p) is m.</exception>
    public double ConditionalProbability(double prob, double m)
    {
        // FIXME
        if (prob < 1e-10)
        {
            return 0;
        }

        double c = Correlation;
        double res = CumulativeZ((InverseCumulativeY(prob) - (Math.Sqrt(c) * m)) / Math.Sqrt(1.0 - c));
        if (!(res >= 0 && res <= 1))
        {
            throw new NanoQLException(string.Create(CultureInfo.InvariantCulture, $"conditional probability {res}out of range"));
        }

        return res;
    }

    /// <summary>The names' default probabilities given the factor.</summary>
    /// <param name="prob">The unconditional probabilities.</param>
    /// <param name="m">The factor's value.</param>
    /// <returns>The conditional probabilities.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prob"/> is <see langword="null"/>.</exception>
    public double[] ConditionalProbability(IReadOnlyList<double> prob, double m)
    {
        ArgumentNullException.ThrowIfNull(prob);
        double[] p = new double[prob.Count];
        for (int i = 0; i < p.Length; i++)
        {
            p[i] = ConditionalProbability(prob[i], m);
        }

        return p;
    }

    /// <summary>The conditional probability integrated over M's density: ∫ ρ(m) F_Z((F_Y⁻¹(p) − a·m)/√(1 − a²))
    /// dm.</summary>
    /// <param name="p">The unconditional probability, in [0, 1].</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="p"/> lies outside [0, 1].</exception>
    public double Integral(double p)
    {
        if (!(p >= 0 && p <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(p), p, string.Create(CultureInfo.InvariantCulture, $"probability p={p} out of range [0,1]"));
        }

        double avg = 0;
        for (int k = 0; k < Steps; k++)
        {
            double pp = ConditionalProbability(p, M(k));
            avg += pp * DensityDm(k);
        }

        return avg;
    }

    /// <summary>A function of the names' conditional probabilities integrated over M's density.</summary>
    /// <param name="f">The function.</param>
    /// <param name="probabilities">The unconditional probabilities.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public double Integral(Func<double[], double> f, IReadOnlyList<double> probabilities)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(probabilities);
        double avg = 0.0;
        for (int i = 0; i < steps; i++)
        {
            double[] conditional = ConditionalProbability(probabilities, M(i));
            double prob = f(conditional);
            avg += prob * DensityDm(i);
        }

        return avg;
    }

    /// <summary>The loss distribution of the names' conditional probabilities integrated over M's density.</summary>
    /// <param name="f">The loss distribution, of its buckets and maximum.</param>
    /// <param name="nominals">The names' volumes.</param>
    /// <param name="probabilities">The unconditional probabilities.</param>
    /// <returns>The distribution.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Distribution Integral(LossDist f, IReadOnlyList<double> nominals, IReadOnlyList<double> probabilities)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(probabilities);
        var dist = new Distribution(f.Buckets, 0.0, f.Maximum);
        for (int i = 0; i < Steps; i++)
        {
            double[] conditional = ConditionalProbability(probabilities, M(i));
            Distribution d = f.Evaluate(nominals, conditional);
            for (int j = 0; j < dist.Size; j++)
            {
                dist.AddDensity(j, d.Density(j) * DensityDm(i));
            }
        }

        return dist;
    }

    /// <summary>Checks the moments (unit norm, zero mean and unit variance) of M, Z and Y by integrating their
    /// densities.</summary>
    /// <param name="tolerance">The largest tolerable absolute error.</param>
    /// <returns>0.</returns>
    /// <exception cref="NanoQLException">A moment is out of tolerance ("norm out of tolerance range" and its
    /// kin).</exception>
    public int CheckMoments(double tolerance)
    {
        double norm = 0, mean = 0, var = 0;
        for (int i = 0; i < Steps; i++)
        {
            norm += DensityDm(i);
            mean += M(i) * DensityDm(i);
            var += Math.Pow(M(i), 2) * DensityDm(i);
        }

        Check(norm, mean, var, tolerance);

        // FIXME: define range for Y via cutoff quantil?
        double zMin = -10;
        double zMax = +10;
        const int zSteps = 200;
        norm = 0;
        mean = 0;
        var = 0;
        for (int i = 1; i < zSteps; i++)
        {
            double z1 = zMin + ((zMax - zMin) / zSteps * (i - 1));
            double z2 = zMin + ((zMax - zMin) / zSteps * i);
            double z = (z1 + z2) / 2;
            double densitydz = CumulativeZ(z2) - CumulativeZ(z1);
            norm += densitydz;
            mean += z * densitydz;
            var += Math.Pow(z, 2) * densitydz;
        }

        Check(norm, mean, var, tolerance);

        // FIXME: define range for Y via cutoff quantil?
        double yMin = -10;
        double yMax = +10;
        const int ySteps = 200;
        norm = 0;
        mean = 0;
        var = 0;
        for (int i = 1; i < ySteps; i++)
        {
            double y1 = yMin + ((yMax - yMin) / ySteps * (i - 1));
            double y2 = yMin + ((yMax - yMin) / ySteps * i);
            double yy = (y1 + y2) / 2;
            double densitydy = CumulativeY(y2) - CumulativeY(y1);
            norm += densitydy;
            mean += yy * densitydy;
            var += yy * yy * densitydy;
        }

        Check(norm, mean, var, tolerance);

        return 0;
    }

    /// <summary>The factor grid's step.</summary>
    /// <param name="i">The step's index, unread: the grid is uniform.</param>
    /// <returns>The step.</returns>
    protected double Dm(int i) => (max - min) / steps;

    /// <summary>The factor grid's midpoint of a step.</summary>
    /// <param name="i">The step's index.</param>
    /// <returns>The midpoint.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step ("index out of
    /// range").</exception>
    protected double M(int i)
    {
        if (!(i >= 0 && i < steps))
        {
            throw new ArgumentOutOfRangeException(nameof(i), i, "index out of range");
        }

        return min + (Dm(i) * i) + (Dm(i) / 2);
    }

    /// <summary>M's density times the grid's step at a step's midpoint.</summary>
    /// <param name="i">The step's index.</param>
    /// <returns>The weight.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step ("index out of
    /// range").</exception>
    protected double DensityDm(int i)
    {
        if (!(i >= 0 && i < steps))
        {
            throw new ArgumentOutOfRangeException(nameof(i), i, "index out of range");
        }

        return Density(M(i)) * Dm(i);
    }

    /// <summary>Sets the tabulated cumulative distribution of Y: a derived model's, at its construction.</summary>
    /// <param name="values">The values, ascending.</param>
    /// <param name="cumulative">Their probabilities.</param>
    private protected void Tabulate(ImmutableArray<double> values, ImmutableArray<double> cumulative)
    {
        y = values;
        cumulativeY = cumulative;
    }

    private static void Check(double norm, double mean, double var, double tolerance)
    {
        if (!(Math.Abs(norm - 1.0) < tolerance))
        {
            throw new NanoQLException("norm out of tolerance range");
        }

        if (!(Math.Abs(mean) < tolerance))
        {
            throw new NanoQLException("mean out of tolerance range");
        }

        if (!(Math.Abs(var - 1.0) < tolerance))
        {
            throw new NanoQLException("variance out of tolerance range");
        }
    }
}
