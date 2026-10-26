// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/singlefactorbsmbasketengine.hpp ql/pricingengines/basket/singlefactorbsmbasketengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using static NanoQL.NumericLimits;

namespace NanoQL.PricingEngines;

// QuantLib's detail::SumExponentialsRootSolver: the root of Σ aᵢ·exp(σᵢx) − K, its function, derivatives and root.
// Immutable: QuantLib's mutable call counters are each root solve's, returned with its root (PR6.9).
internal sealed class SumExponentialsRootSolver
{
    private readonly double[] a;
    private readonly double[] sig;
    private readonly double k;

    internal SumExponentialsRootSolver(double[] a, double[] sig, double k)
    {
        if (!(a.Length == sig.Length))
        {
            throw new ArgumentException("Arrays must have the same size", nameof(sig));
        }

        this.a = (double[])a.Clone();
        this.sig = (double[])sig.Clone();
        this.k = k;
    }

    // QuantLib's solver strategies, in their order
    internal enum Strategy
    {
        Ridder,
        Newton,
        Brent,
        Halley,
    }

    // QuantLib's operator()
    internal double Evaluate(double x)
    {
        double s = 0.0;
        for (int i = 0; i < a.Length; ++i)
        {
            s += a[i] * Math.Exp(sig[i] * x);
        }

        return s - k;
    }

    internal double Derivative(double x)
    {
        double s = 0.0;
        for (int i = 0; i < a.Length; ++i)
        {
            s += a[i] * sig[i] * Math.Exp(sig[i] * x);
        }

        return s;
    }

    internal double SecondDerivative(double x)
    {
        double s = 0.0;
        for (int i = 0; i < a.Length; ++i)
        {
            s += a[i] * (sig[i] * sig[i]) * Math.Exp(sig[i] * x);
        }

        return s;
    }

    // QuantLib's getRoot, with the counts of the function's, the derivative's and the second derivative's calls
    internal RootResult GetRoot(double xTol = 1e6 * QL_EPSILON, Strategy strategy = Strategy.Brent)
    {
        double denom = 0.0;
        bool attrNonNegative = true;
        bool logProb = true;
        for (int i = 0; i < a.Length; ++i)
        {
            double attr = a[i] * sig[i];
            attrNonNegative &= attr >= 0.0;
            logProb &= a[i] > 0;
            denom += attr;
        }

        if (!attrNonNegative)
        {
            throw new NanoQLException("a*sig should not be negative");
        }

        if (!(k > 0 || !logProb))
        {
            throw new NanoQLException("non-positive strikes only allowed for spread options");
        }

        double sumA = 0.0;
        foreach (double ai in a)
        {
            sumA += ai;
        }

        double xInit = Math.Abs(denom) > 1000 * QL_EPSILON ? Min(10.0, Max(-10.0, (k - sumA) / denom)) : 0.0;

        int[] counts = new int[3];
        var f = new Counting(this, counts);
        double root = strategy switch
        {
            Strategy.Brent => new Brent().Solve(f, xTol, xInit, 1.0),
            Strategy.Newton => new Newton().Solve(f, xTol, xInit, 1.0),
            Strategy.Ridder => new Ridder().Solve(f, xTol, xInit, 1.0),
            Strategy.Halley => new Halley().Solve(f, xTol, xInit, 1.0),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "unknown strategy type"),
        };
        return new RootResult(root, counts[0], counts[1], counts[2]);
    }

    // std::max(a, b) and std::min(a, b)
    private static double Max(double x, double y) => x < y ? y : x;

    private static double Min(double x, double y) => y < x ? y : x;

    // A root and the calls that found it: QuantLib's getFCtr, getDerivativeCtr and getSecondDerivativeCtr.
    internal readonly record struct RootResult(double Root, int FCtr, int DerivativeCtr, int SecondDerivativeCtr);

    // The solver as the solvers' function, counting its calls as QuantLib's members do.
    private readonly struct Counting(SumExponentialsRootSolver solver, int[] counts) : ISecondDerivativeSolver1DFunction
    {
        public double Evaluate(double x)
        {
            ++counts[0];
            return solver.Evaluate(x);
        }

        public double Derivative(double x)
        {
            ++counts[1];
            return solver.Derivative(x);
        }

        public double SecondDerivative(double x)
        {
            ++counts[2];
            return solver.SecondDerivative(x);
        }
    }
}
