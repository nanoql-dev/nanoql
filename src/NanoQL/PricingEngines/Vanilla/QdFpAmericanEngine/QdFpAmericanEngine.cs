// SPDX-FileCopyrightText: Copyright (C) 2022 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/qdfpamericanengine.hpp ql/pricingengines/vanilla/qdfpamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The high-performance, high-precision American engine based on a fixed-point iteration for the exercise boundary
/// (L. Andersen, M. Lake and D. Offengenden, "High Performance American Option Pricing", 2015; L. Andersen and M. Lake,
/// "Fast American Option Pricing: The Double-Boundary Case", 2021), seeded by QD+'s boundary.
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the value. Immutable: the three schemes QuantLib keeps as function statics are static
/// singletons; each price builds its boundary, and the fixed-point steps' new values give a new interpolation on the
/// same nodes (<see cref="ChebyshevInterpolation.WithY"/>), which the boundary function reads, where QuantLib updates
/// its interpolation in place. The static <c>Calculate</c> builds an engine and prices through it. An extension point,
/// as in QuantLib.
/// </para>
/// </remarks>
public class QdFpAmericanEngine : QdPutCallParityEngine
{
    private static readonly QdFpIterationScheme FastSchemeInstance = new QdFpLegendreScheme(7, 2, 7, 27);
    private static readonly QdFpIterationScheme AccurateSchemeInstance = new QdFpLegendreTanhSinhScheme(25, 5, 13, 1e-8);
    private static readonly QdFpIterationScheme HighPrecisionSchemeInstance = new QdFpTanhSinhIterationScheme(10, 30, 1e-10);

    private readonly QdFpIterationScheme iterationScheme;
    private readonly QdFpAmericanEngineFixedPointEquation fpEquation;

    /// <summary>Initializes a new instance of the <see cref="QdFpAmericanEngine"/> class.</summary>
    /// <param name="bsProcess">The Black-Scholes process.</param>
    /// <param name="iterationScheme">The iteration scheme, or <see langword="null"/> for <see cref="AccurateScheme"/>.</param>
    /// <param name="fpEquation">The fixed-point equation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bsProcess"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The equation isn't a member of its type.</exception>
    public QdFpAmericanEngine(
        GeneralizedBlackScholesProcess bsProcess,
        QdFpIterationScheme? iterationScheme = null,
        QdFpAmericanEngineFixedPointEquation fpEquation = QdFpAmericanEngineFixedPointEquation.Auto)
        : base(bsProcess)
    {
        EnumArguments.Check(fpEquation, nameof(fpEquation));
        this.iterationScheme = iterationScheme ?? AccurateScheme();
        this.fpEquation = fpEquation;
    }

    /// <summary>Prices an American vanilla option by the fixed-point iteration.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="bsProcess">The Black-Scholes process.</param>
    /// <param name="iterationScheme">The iteration scheme, or <see langword="null"/> for <see cref="AccurateScheme"/>.</param>
    /// <param name="fpEquation">The fixed-point equation.</param>
    /// <returns>The option's results: the value.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The engine refuses the option.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess bsProcess,
        QdFpIterationScheme? iterationScheme = null,
        QdFpAmericanEngineFixedPointEquation fpEquation = QdFpAmericanEngineFixedPointEquation.Auto) =>
        new QdFpAmericanEngine(bsProcess, iterationScheme, fpEquation).Calculate(option, context);

    /// <summary>The fast scheme: Gauss-Legendre (7, 2, 7)-27.</summary>
    /// <returns>The scheme, one instance.</returns>
    public static QdFpIterationScheme FastScheme() => FastSchemeInstance;

    /// <summary>The accurate scheme, the default: Legendre-tanh-sinh (25, 5, 13)-1e-8.</summary>
    /// <returns>The scheme, one instance.</returns>
    public static QdFpIterationScheme AccurateScheme() => AccurateSchemeInstance;

    /// <summary>The high-precision scheme: tanh-sinh (10, 30)-1e-10.</summary>
    /// <returns>The scheme, one instance.</returns>
    public static QdFpIterationScheme HighPrecisionScheme() => HighPrecisionSchemeInstance;

    /// <inheritdoc/>
    protected override double CalculatePut(double s, double k, double r, double q, double vol, double t)
    {
        if (r < 0.0 && q < r)
        {
            throw new NanoQLException("double-boundary case q<r<0 for a put option is given");
        }

        double xmax = QdPlusAmericanEngine.XMax(k, r, q);
        int n = iterationScheme.GetNumberOfChebyshevInterpolationNodes();

        ChebyshevInterpolation interp = new QdPlusAmericanEngine(
                Process, n + 1, QdPlusAmericanEngineSolverType.Halley, 1e-8)
            .GetPutExerciseBoundary(s, k, r, q, vol, t);

        Vector z = interp.Nodes();
        var x = new Vector(z.Size);
        for (int i = 0; i < z.Size; ++i)
        {
            x[i] = 0.5 * Math.Sqrt(t) * (1.0 + z[i]);
        }

        // the interpolation the boundary reads: each step's, where QuantLib updates its own in place
        double B(double tau)
        {
            double zz = (2 * Math.Sqrt(Math.Abs(tau) / t)) - 1;
            return xmax * Math.Exp(-Math.Sqrt(Max(0.0, interp.Evaluate(zz, true))));
        }

        double H(double fv)
        {
            double l = Math.Log(fv / xmax);
            return l * l;
        }

        DqFpEquation eqn = fpEquation == QdFpAmericanEngineFixedPointEquation.FpA
            || (fpEquation == QdFpAmericanEngineFixedPointEquation.Auto && Math.Abs(r - q) < 0.001)
            ? new DqFpEquationA(k, r, q, vol, B, iterationScheme.GetFixedPointIntegrator())
            : new DqFpEquationB(k, r, q, vol, B, iterationScheme.GetFixedPointIntegrator());

        var y = new double[x.Size];
        y[0] = 0.0;

        int nNewton = iterationScheme.GetNumberOfJacobiNewtonFixedPointSteps();
        for (int step = 0; step < nNewton; ++step)
        {
            for (int i = 1; i < x.Size; ++i)
            {
                double tau = x[i] * x[i];
                double b = B(tau);

                (double bigN, double d, double fv) = eqn.F(tau, b);

                if (tau < NumericLimits.QL_EPSILON)
                {
                    y[i] = H(fv);
                }
                else
                {
                    (double nd, double dd) = eqn.NDd(tau, b);

                    double fd = k * Math.Exp(-(r - q) * tau) * ((nd / d) - (dd * bigN / (d * d)));

                    y[i] = H(b - ((fv - b) / (fd - 1)));
                }
            }

            interp = interp.WithY(y);
        }

        int nFp = iterationScheme.GetNumberOfNaiveFixedPointSteps();
        for (int step = 0; step < nFp; ++step)
        {
            for (int i = 1; i < x.Size; ++i)
            {
                double tau = x[i] * x[i];
                double fv = eqn.F(tau, B(tau)).Fv;

                y[i] = H(fv);
            }

            interp = interp.WithY(y);
        }

        var aov = new QdPlusAddOnValue(t, s, k, r, q, vol, xmax, interp);
        double addOn = iterationScheme.GetExerciseBoundaryToPriceIntegrator().Integrate(aov.Evaluate, 0.0, Math.Sqrt(t));

        double europeanValue = new BlackCalculator(
            OptionType.Put, k, s * Math.Exp((r - q) * t), vol * Math.Sqrt(t), Math.Exp(-r * t)).Value;

        return Max(europeanValue, 0.0) + Max(0.0, addOn);
    }

    // std::max's semantics, which keep or drop a NaN by position where Math.Max propagates it.
    private static double Max(double a, double b) => a < b ? b : a;
}
