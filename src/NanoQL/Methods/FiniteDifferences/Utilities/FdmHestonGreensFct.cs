// SPDX-FileCopyrightText: Copyright (C) 2014 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdmhestongreensfct.hpp ql/methods/finitedifferences/utilities/fdmhestongreensfct.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The Heston process's Green's function on a mesher of the log spot by the variance (or a transformation of it): the
/// density at a short time from the spot and v₀, the start of a Fokker-Planck forward solve.
/// </summary>
/// <remarks>
/// Immutable. The curves are read on a clock, each at the instants its times denote: QuantLib reads the rates at the
/// risk-free curve's times (HR7 (xxi)). The public constructor's clock is <see cref="FdmHestonOp.Clock"/>'s, the
/// risk-free curve itself where the dividend curve isn't dated before it, QuantLib's reads.
/// </remarks>
public sealed class FdmHestonGreensFct
{
    private readonly double l0;
    private readonly FdmMesher mesher;
    private readonly HestonProcess process;
    private readonly FdmSquareRootFwdOpTransformationType trafoType;
    private readonly TermStructure clock;

    /// <summary>Initializes a new instance of the <see cref="FdmHestonGreensFct"/> class.</summary>
    /// <param name="mesher">The mesher: the log spot in direction 0, the variance or its transformation in direction
    /// 1.</param>
    /// <param name="process">The Heston process.</param>
    /// <param name="trafoType">The variable the density is written in, in the variance's direction.</param>
    /// <param name="l0">The leverage at the start, scaling the log spot's volatility.</param>
    /// <exception cref="ArgumentNullException">The mesher or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The mesher has fewer than two directions, or <paramref name="trafoType"/> isn't a member of its type, with
    /// QuantLib's message.
    /// </exception>
    public FdmHestonGreensFct(
        FdmMesher mesher, HestonProcess process, FdmSquareRootFwdOpTransformationType trafoType, double l0 = 1.0)
        : this(mesher, process, trafoType, l0, null)
    {
    }

    // The SLV model's form: the rates read on the model's clock.
    internal FdmHestonGreensFct(
        FdmMesher mesher,
        HestonProcess process,
        FdmSquareRootFwdOpTransformationType trafoType,
        double l0,
        TermStructure? clock)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfLessThan(mesher.Layout.Dim.Length, 2, nameof(mesher));
        if (trafoType is < FdmSquareRootFwdOpTransformationType.Plain or > FdmSquareRootFwdOpTransformationType.Log)
        {
            throw new ArgumentOutOfRangeException(nameof(trafoType), trafoType, "unknown transformation type");
        }

        this.l0 = l0;
        this.mesher = mesher;
        this.process = process;
        this.trafoType = trafoType;
        this.clock = clock ?? FdmHestonOp.Clock(process);
    }

    /// <summary>The density at a time on the mesher's points.</summary>
    /// <param name="t">The time, on the clock.</param>
    /// <param name="algorithm">The approximation.</param>
    /// <returns>The density at each point of the mesher's layout, in the transformation's variable.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="algorithm"/> isn't a member of its type, with
    /// QuantLib's message.</exception>
    /// <exception cref="ArgumentException">A curve can't be read at the time.</exception>
    /// <exception cref="NanoQLException">The semi-analytical density's integration fails.</exception>
    public Vector Get(double t, FdmHestonGreensFctAlgorithm algorithm)
    {
        if (algorithm is < FdmHestonGreensFctAlgorithm.ZeroCorrelation or > FdmHestonGreensFctAlgorithm.SemiAnalytical)
        {
            throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "unknown algorithm");
        }

        double r = process.RiskFreeRate.ContinuousForwardRateAt(clock, 0, t);
        double q = process.DividendYield.ContinuousForwardRateAt(clock, 0, t);

        double s0 = process.S0;
        double v0 = process.V0;
        double x0 = Math.Log(s0) + ((r - q - (0.5 * v0 * l0 * l0)) * t);

        double rho = process.Rho;
        double theta = process.Theta;
        double kappa = process.Kappa;
        double sigma = process.Sigma;

        // QuantLib builds the square-root density at every point; it holds no state
        SquareRootProcessRNDCalculator? squareRoot = algorithm == FdmHestonGreensFctAlgorithm.ZeroCorrelation
            ? new SquareRootProcessRNDCalculator(v0, kappa, theta, sigma)
            : null;

        var p = new Vector(mesher.Layout.Size);
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int k = 0; k < mesher.Layout.Size; ++k, iter.Increment())
        {
            double x = mesher.Location(iter, 0);
            double v = (trafoType != FdmSquareRootFwdOpTransformationType.Log)
                ? mesher.Location(iter, 1)
                : Math.Exp(mesher.Location(iter, 1));

            double retVal;
            switch (algorithm)
            {
                case FdmHestonGreensFctAlgorithm.ZeroCorrelation:
                {
                    double sdX = l0 * Math.Sqrt(v0 * t);
                    double pX = MathConstants.M_1_SQRTPI * MathConstants.M_SQRT1_2 / sdX
                        * Math.Exp(-0.5 * Squared((x - x0) / sdX));
                    double pV = squareRoot!.Pdf(v, t);

                    retVal = pV * pX;
                    break;
                }

                case FdmHestonGreensFctAlgorithm.SemiAnalytical:
                    retVal = process.Pdf(x, v, t, 1e-4);
                    break;
                default:
                {
                    double sdX = l0 * Math.Sqrt(v0 * t);
                    double sdV = sigma * Math.Sqrt(v0 * t);
                    double z0 = v0 + (kappa * (theta - v0) * t);
                    retVal = 1.0 / (MathConstants.M_TWOPI * sdX * sdV * Math.Sqrt(1 - (rho * rho)))
                        * Math.Exp(-(Squared((x - x0) / sdX)
                                     + Squared((v - z0) / sdV)
                                     - (2 * rho * (x - x0) * (v - z0) / (sdX * sdV)))
                                   / (2 * (1 - (rho * rho))));
                    break;
                }
            }

            switch (trafoType)
            {
                case FdmSquareRootFwdOpTransformationType.Log:
                    retVal *= v;
                    break;
                case FdmSquareRootFwdOpTransformationType.Plain:
                    break;
                default:
                    retVal *= Math.Pow(v, 1.0 - (2 * kappa * theta / (sigma * sigma)));
                    break;
            }

            p[iter.Index] = retVal;
        }

        return p;
    }

    private static double Squared(double x) => x * x;
}
