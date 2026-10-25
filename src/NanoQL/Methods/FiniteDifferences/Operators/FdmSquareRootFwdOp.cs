// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2014 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmsquarerootfwdop.hpp ql/methods/finitedifferences/operators/fdmsquarerootfwdop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The square-root process's linear operator for the Fokker-Planck forward equation: the density of the variance (or
/// of a transformation of it) in one direction of a mesher, its drift κ(θ − v) and diffusion ½σ²v, with zero-flux
/// boundaries.
/// </summary>
/// <remarks>
/// A working type (the S-hes plan's HD8): built at construction, its time-independent operator applied and solved
/// into the caller's spans, so a step allocates nothing. The direction's grid needs at least two points, where
/// QuantLib reads past a shorter one.
/// </remarks>
public sealed class FdmSquareRootFwdOp : FdmLinearOpComposite
{
    private readonly int direction;
    private readonly double kappa;
    private readonly double theta;
    private readonly double sigma;
    private readonly FdmSquareRootFwdOpTransformationType transform;
    private readonly ModTripleBandLinearOp mapX;
    private readonly double[] v;

    /// <summary>Initializes a new instance of the <see cref="FdmSquareRootFwdOp"/> class.</summary>
    /// <param name="mesher">The mesher.</param>
    /// <param name="kappa">The mean-reversion speed κ.</param>
    /// <param name="theta">The mean-reversion level θ.</param>
    /// <param name="sigma">The volatility σ.</param>
    /// <param name="direction">The direction of the variance.</param>
    /// <param name="transform">The variable the density is written in.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mesher"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The direction isn't one of the layout's, or <paramref name="transform"/> isn't a member of its type.
    /// </exception>
    /// <exception cref="ArgumentException">The direction's grid has fewer than two points.</exception>
    public FdmSquareRootFwdOp(
        FdmMesher mesher,
        double kappa,
        double theta,
        double sigma,
        int direction,
        FdmSquareRootFwdOpTransformationType transform = FdmSquareRootFwdOpTransformationType.Plain)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentOutOfRangeException.ThrowIfNegative(direction);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(direction, mesher.Layout.Dim.Length);
        if (transform is < FdmSquareRootFwdOpTransformationType.Plain or > FdmSquareRootFwdOpTransformationType.Log)
        {
            throw new ArgumentOutOfRangeException(nameof(transform), transform, "unknown transform");
        }

        if (mesher.Layout.Dim[direction] < 2)
        {
            throw new ArgumentException(
                $"the square-root operator's direction needs at least two points, not {mesher.Layout.Dim[direction]}",
                nameof(mesher));
        }

        this.direction = direction;
        this.kappa = kappa;
        this.theta = theta;
        this.sigma = sigma;
        this.transform = transform;
        mapX = new ModTripleBandLinearOp(Map(mesher, kappa, theta, sigma, direction, transform));

        v = new double[mesher.Layout.Dim[direction]];
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int k = 0; k < mesher.Layout.Size; ++k, iter.Increment())
        {
            v[iter.Coordinates[direction]] = mesher.Location(iter, direction);
        }

        // zero flux boundary condition
        SetLowerBC(mesher);
        SetUpperBC(mesher);
    }

    /// <inheritdoc/>
    public override int Size => 1;

    /// <inheritdoc/>
    /// <remarks>The operator doesn't depend on time.</remarks>
    public override void SetTime(double t1, double t2)
    {
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result) => mapX.Apply(r, result);

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result) => result[..r.Length].Clear();

    /// <inheritdoc/>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        if (direction == this.direction)
        {
            mapX.Apply(r, result);
        }
        else
        {
            result[..r.Length].Clear();
        }
    }

    /// <inheritdoc/>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == this.direction)
        {
            mapX.SolveSplitting(r, s, 1.0, result);
        }
        else
        {
            r.CopyTo(result);
        }
    }

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) =>
        SolveSplitting(direction, r, s, result);

    /// <inheritdoc/>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp() => [mapX.ToMatrix()];

    /// <summary>The zero-flux factor at the lower boundary in a transformation.</summary>
    /// <param name="type">The transformation.</param>
    /// <returns>The factor.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type, with
    /// QuantLib's message.</exception>
    public double LowerBoundaryFactor(FdmSquareRootFwdOpTransformationType type = FdmSquareRootFwdOpTransformationType.Plain) =>
        type switch
        {
            FdmSquareRootFwdOpTransformationType.Plain => F0Plain(),
            FdmSquareRootFwdOpTransformationType.Power => F0Power(),
            FdmSquareRootFwdOpTransformationType.Log => F0Log(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "unknown transform"),
        };

    /// <summary>The zero-flux factor at the upper boundary in a transformation.</summary>
    /// <param name="type">The transformation.</param>
    /// <returns>The factor.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type, with
    /// QuantLib's message.</exception>
    public double UpperBoundaryFactor(FdmSquareRootFwdOpTransformationType type = FdmSquareRootFwdOpTransformationType.Plain) =>
        type switch
        {
            FdmSquareRootFwdOpTransformationType.Plain => F1Plain(),
            FdmSquareRootFwdOpTransformationType.Power => F1Power(),
            FdmSquareRootFwdOpTransformationType.Log => F1Log(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "unknown transform"),
        };

    /// <summary>
    /// The grid's i-th value, from 1, the grid's points; 0 and the size plus one are the points beyond its ends
    /// (QuantLib's ghost points).
    /// </summary>
    /// <param name="i">The index, from 0 to the grid's size plus one.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is outside [0, size + 1], with QuantLib's
    /// message.</exception>
    public double V(int i)
    {
        if (i > 0 && i <= v.Length)
        {
            return v[i - 1];
        }
        else if (i == 0)
        {
            if (transform == FdmSquareRootFwdOpTransformationType.Log)
            {
                return (2 * v[0]) - v[1];
            }
            else
            {
                return Max(0.5 * v[0], v[0] - (0.01 * (v[1] - v[0])));
            }
        }
        else if (i == v.Length + 1)
        {
            return v[^1] + (v[^1] - v[^2]);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(i), i, "unknown index");
        }
    }

    private static TripleBandLinearOp Map(
        FdmMesher mesher, double kappa, double theta, double sigma, int direction, FdmSquareRootFwdOpTransformationType transform)
    {
        Vector locations = mesher.Locations(direction);
        int size = mesher.Layout.Size;
        var first = new Vector(size);
        var second = new Vector(size);
        var constant = new Vector(size);
        switch (transform)
        {
            case FdmSquareRootFwdOpTransformationType.Plain:
                for (int i = 0; i < size; ++i)
                {
                    first[i] = (kappa * (locations[i] - theta)) + (sigma * sigma);
                    second[i] = 0.5 * sigma * sigma * locations[i];
                    constant[i] = kappa;
                }

                return new FirstDerivativeOp(direction, mesher).Mult(first)
                    .Add(new SecondDerivativeOp(direction, mesher).Mult(second))
                    .Add(constant);
            case FdmSquareRootFwdOpTransformationType.Power:
                for (int i = 0; i < size; ++i)
                {
                    second[i] = 0.5 * sigma * sigma * locations[i];
                    first[i] = kappa * (locations[i] + theta);
                    constant[i] = 2 * kappa * kappa * theta / (sigma * sigma);
                }

                return new SecondDerivativeOp(direction, mesher).Mult(second)
                    .Add(new FirstDerivativeOp(direction, mesher).Mult(first))
                    .Add(constant);
            default:
                double c = (-0.5 * sigma * sigma) - (kappa * theta);
                for (int i = 0; i < size; ++i)
                {
                    double e = Math.Exp(-locations[i]);
                    first[i] = (e * c) + kappa;
                    second[i] = 0.5 * sigma * sigma * e;
                    constant[i] = kappa * theta * e;
                }

                return new FirstDerivativeOp(direction, mesher).Mult(first)
                    .Add(new SecondDerivativeOp(direction, mesher).Mult(second))
                    .Add(constant);
        }
    }

    // std::max
    private static double Max(double a, double b) => a < b ? b : a;

    private void SetLowerBC(FdmMesher mesher)
    {
        const int n = 1;
        (double _, double beta, double gamma) = GetCoeff(n);
        double f = LowerBoundaryFactor(transform);

        double b = -(H(n - 1) + H(n)) / Zeta(n);
        double c = H(n - 1) / Zetap(n);

        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int k = 0; k < mesher.Layout.Size; ++k, iter.Increment())
        {
            if (iter.Coordinates[direction] == 0)
            {
                int idx = iter.Index;
                mapX.Diag(idx) = beta + (f * b);
                mapX.Upper(idx) = gamma + (f * c);
            }
        }
    }

    private void SetUpperBC(FdmMesher mesher)
    {
        int n = v.Length;
        (double alpha, double beta, double _) = GetCoeff(n);
        double f = UpperBoundaryFactor(transform);

        double b = (H(n) + H(n - 1)) / Zeta(n);
        double c = -H(n) / Zetam(n);

        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int k = 0; k < mesher.Layout.Size; ++k, iter.Increment())
        {
            if (iter.Coordinates[direction] == n - 1)
            {
                int idx = iter.Index;
                mapX.Diag(idx) = beta + (f * b);
                mapX.Lower(idx) = alpha + (f * c);
            }
        }
    }

    private double F0Plain()
    {
        const int n = 1;
        double a = -((2 * H(n - 1)) + H(n)) / Zetam(n);
        double alpha = (sigma * sigma * V(n) / Zetam(n)) - (Mu(n) * H(n) / Zetam(n));
        double nu = (a * V(n - 1)) + (((2 * kappa * (V(n - 1) - theta)) + (sigma * sigma)) / (sigma * sigma));

        return alpha / nu * V(n - 1);
    }

    private double F1Plain()
    {
        int n = v.Length;
        double a = ((2 * H(n)) + H(n - 1)) / Zetap(n);
        double gamma = (sigma * sigma * V(n) / Zetap(n)) + (Mu(n) * H(n - 1) / Zetap(n));
        double nu = (a * V(n + 1)) + (((2 * kappa * (V(n + 1) - theta)) + (sigma * sigma)) / (sigma * sigma));

        return gamma / nu * V(n + 1);
    }

    private double F0Power()
    {
        const int n = 1;
        double mu = kappa * (V(n) + theta);
        double a = -((2 * H(n - 1)) + H(n)) / Zetam(n);
        double alpha = (sigma * sigma * V(n) / Zetam(n)) - (mu * H(n) / Zetam(n));
        double nu = (a * V(n - 1)) + (2 * (kappa * V(n - 1) / (sigma * sigma)));

        return alpha / nu * V(n - 1);
    }

    private double F1Power()
    {
        int n = v.Length;
        double mu = kappa * (V(n) + theta);
        double a = ((2 * H(n)) + H(n - 1)) / Zetap(n);
        double gamma = (sigma * sigma * V(n) / Zetap(n)) + (mu * H(n - 1) / Zetap(n));
        double nu = (a * V(n + 1)) + (2 * (kappa * V(n + 1) / (sigma * sigma)));

        return gamma / nu * V(n + 1);
    }

    private double F0Log()
    {
        const int n = 1;
        double mu = (((-kappa * theta) - (sigma * sigma / 2.0)) * Math.Exp(-V(1))) + kappa;
        double a = -((2 * H(n - 1)) + H(n)) / Zetam(n);
        double alpha = (sigma * sigma * Math.Exp(-V(n)) / Zetam(n)) - (mu * H(n) / Zetam(n));
        double nu = (a * Math.Exp(-V(n - 1))) + (2 * kappa * (1 - (theta * Math.Exp(-V(n - 1)))) / (sigma * sigma));

        return alpha / nu * Math.Exp(-V(n - 1));
    }

    private double F1Log()
    {
        int n = v.Length;
        double mu = (((-kappa * theta) - (sigma * sigma / 2.0)) * Math.Exp(-V(n))) + kappa;
        double a = ((2 * H(n)) + H(n - 1)) / Zetap(n);
        double gamma = (sigma * sigma * Math.Exp(-V(n)) / Zetap(n)) + (mu * H(n - 1) / Zetap(n));
        double nu = (a * Math.Exp(-V(n + 1))) + (2 * kappa * (1 - (theta * Math.Exp(-V(n + 1)))) / (sigma * sigma));

        return gamma / nu * Math.Exp(-V(n + 1));
    }

    private double H(int i) => V(i + 1) - V(i);

    private double Mu(int i) => (kappa * (V(i) - theta)) + (sigma * sigma);

    private double Zetam(int i) => H(i - 1) * (H(i - 1) + H(i));

    private double Zeta(int i) => H(i - 1) * H(i);

    private double Zetap(int i) => H(i) * (H(i - 1) + H(i));

    private (double Alpha, double Beta, double Gamma) GetCoeff(int n) => transform switch
    {
        FdmSquareRootFwdOpTransformationType.Plain => GetCoeffPlain(n),
        FdmSquareRootFwdOpTransformationType.Power => GetCoeffPower(n),
        _ => GetCoeffLog(n),
    };

    private (double Alpha, double Beta, double Gamma) GetCoeffPlain(int n)
    {
        double alpha = (sigma * sigma * V(n) / Zetam(n)) - (Mu(n) * H(n) / Zetam(n));
        double beta = (-sigma * sigma * V(n) / Zeta(n)) + (Mu(n) * (H(n) - H(n - 1)) / Zeta(n)) + kappa;
        double gamma = (sigma * sigma * V(n) / Zetap(n)) + (Mu(n) * H(n - 1) / Zetap(n));
        return (alpha, beta, gamma);
    }

    private (double Alpha, double Beta, double Gamma) GetCoeffLog(int n)
    {
        double mu = (((-kappa * theta) - (sigma * sigma / 2.0)) * Math.Exp(-V(n))) + kappa;
        double alpha = (sigma * sigma * Math.Exp(-V(n)) / Zetam(n)) - (mu * H(n) / Zetam(n));
        double beta = (-sigma * sigma * Math.Exp(-V(n)) / Zeta(n)) + (mu * (H(n) - H(n - 1)) / Zeta(n))
            + (kappa * theta * Math.Exp(-V(n)));
        double gamma = (sigma * sigma * Math.Exp(-V(n)) / Zetap(n)) + (mu * H(n - 1) / Zetap(n));
        return (alpha, beta, gamma);
    }

    private (double Alpha, double Beta, double Gamma) GetCoeffPower(int n)
    {
        double mu = kappa * (theta + V(n));
        double alpha = ((sigma * sigma * V(n)) - (mu * H(n))) / Zetam(n);
        double beta = (((-sigma * sigma * V(n)) + (mu * (H(n) - H(n - 1)))) / Zeta(n)) + (2 * kappa * kappa * theta / (sigma * sigma));
        double gamma = ((sigma * sigma * V(n)) + (mu * H(n - 1))) / Zetap(n);
        return (alpha, beta, gamma);
    }
}
