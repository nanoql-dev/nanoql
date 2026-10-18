// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2012 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/solvers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testBisection, testFalsePosition, testSecant (B6.2's solvers); testNewton (B3.4); testRidder (B4.5); testHalley (B4.7)

using System.Runtime.CompilerServices;
using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class SolversTests
{
    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    [Fact]
    public void TestBrent()
    {
        // Testing Brent solver...
        TestSolver(new Brent(), "Brent", 1.0e-6);
    }

    [Fact]
    public void TestNewtonSafe()
    {
        // Testing Newton-safe solver...
        TestSolver(new NewtonSafe(), "NewtonSafe", 1.0e-9);
    }

    [Fact]
    public void TestFiniteDifferenceNewtonSafe()
    {
        // Testing finite-difference Newton-safe solver...
        TestSolver(new FiniteDifferenceNewtonSafe(), "FiniteDifferenceNewtonSafe", null);
    }

    private static void TestNotBracketed<TSelf, TFunction, TF>(
        Solver1D<TSelf, TFunction> solver, string name, TF f, Real guess)
        where TSelf : Solver1D<TSelf, TFunction>
        where TFunction : ISolver1DFunction
        where TF : struct, TFunction
    {
        Real[] accuracy = [1.0e-4, 1.0e-6, 1.0e-8];
        Real expected = 1.0;
        foreach (Real i in accuracy)
        {
            Real root = solver.Solve(f, i, guess, 0.1);
            if (Math.Abs(root - expected) > i)
            {
                Assert.Fail(
                    $"{name} solver (not bracketed):\n"
                    + $"    expected:   {expected}\n"
                    + $"    calculated: {root}\n"
                    + $"    accuracy:   {i}");
            }
        }
    }

    private static void TestBracketed<TSelf, TFunction, TF>(
        Solver1D<TSelf, TFunction> solver, string name, TF f, Real guess)
        where TSelf : Solver1D<TSelf, TFunction>
        where TFunction : ISolver1DFunction
        where TF : struct, TFunction
    {
        Real[] accuracy = [1.0e-4, 1.0e-6, 1.0e-8];
        Real expected = 1.0;
        foreach (Real i in accuracy)
        {
            // guess on the left side of the root, increasing function
            Real root = solver.Solve(f, i, guess, 0.0, 2.0);
            if (Math.Abs(root - expected) > i)
            {
                Assert.Fail(
                    $"{name} solver (bracketed):\n"
                    + $"    expected:   {expected}\n"
                    + $"    calculated: {root}\n"
                    + $"    accuracy:   {i}");
            }
        }
    }

    private static void TestLastCallWithRoot<TSelf, TFunction, TProbe>(
        Solver1D<TSelf, TFunction> solver,
        string name,
        bool bracketed,
        Real accuracy,
        Func<StrongBox<Real>, Real, TProbe> probe)
        where TSelf : Solver1D<TSelf, TFunction>
        where TFunction : ISolver1DFunction
        where TProbe : struct, TFunction
    {
        Real[] mins = [3.0, 2.25, 1.5, 1.0];
        Real[] maxs = [7.0, 5.75, 4.5, 3.0];
        Real[] steps = [0.2, 0.2, 0.1, 0.1];
        Real[] offsets = [25.0, 11.0, 5.0, 1.0];
        Real[] guesses = [4.5, 4.5, 2.5, 2.5];
        //Real expected[] = { 5.0, 4.0, 3.0, 2.0 };

        var argument = new StrongBox<Real>(0.0);
        Real result;

        for (Size i = 0; i < 4; ++i)
        {
            if (bracketed)
            {
                result = solver.Solve(probe(argument, offsets[i]), accuracy, guesses[i], mins[i], maxs[i]);
            }
            else
            {
                result = solver.Solve(probe(argument, offsets[i]), accuracy, guesses[i], steps[i]);
            }

            Real error = Math.Abs(result - argument.Value);
            // the solver should have called the function with
            // the very same value it's returning. But the internal
            // 80bit length of the x87 FPU register might lead to
            // a very small glitch when compiled with -mfpmath=387 on gcc
            if (error > 2 * QL_EPSILON)
            {
                Assert.Fail(
                    $"{name} solver ({(bracketed ? string.Empty : "not ")}bracketed):\n"
                    + $"    index:   {i}\n"
                    + $"    expected:   {result}\n"
                    + $"    calculated: {argument.Value}\n"
                    + $"    error: {error}");
            }
        }
    }

    // QuantLib's test_solver is a template over the solver, instantiated with F1, F2, F3 and Probe; C# can't show that
    // they meet an open solver's function concept, so each solver's overload passes them to the generic body.
    private static void TestSolver(Brent solver, string name, Real? accuracy) =>
        TestSolver(solver, name, accuracy, new F1(), new F2(), new F3(), (result, offset) => new Probe(result, offset));

    private static void TestSolver(NewtonSafe solver, string name, Real? accuracy) =>
        TestSolver(solver, name, accuracy, new F1(), new F2(), new F3(), (result, offset) => new Probe(result, offset));

    private static void TestSolver(FiniteDifferenceNewtonSafe solver, string name, Real? accuracy) =>
        TestSolver(solver, name, accuracy, new F1(), new F2(), new F3(), (result, offset) => new Probe(result, offset));

    private static void TestSolver<TSelf, TFunction, TF1, TF2, TF3, TProbe>(
        Solver1D<TSelf, TFunction> solver,
        string name,
        Real? accuracy,
        TF1 f1,
        TF2 f2,
        TF3 f3,
        Func<StrongBox<Real>, Real, TProbe> probe)
        where TSelf : Solver1D<TSelf, TFunction>
        where TFunction : ISolver1DFunction
        where TF1 : struct, TFunction
        where TF2 : struct, TFunction
        where TF3 : struct, TFunction
        where TProbe : struct, TFunction
    {
        // guess on the left side of the root, increasing function
        TestNotBracketed(solver, name, f1, 0.5);
        TestBracketed(solver, name, f1, 0.5);
        // guess on the right side of the root, increasing function
        TestNotBracketed(solver, name, f1, 1.5);
        TestBracketed(solver, name, f1, 1.5);
        // guess on the left side of the root, decreasing function
        TestNotBracketed(solver, name, f2, 0.5);
        TestBracketed(solver, name, f2, 0.5);
        // guess on the right side of the root, decreasing function
        TestNotBracketed(solver, name, f2, 1.5);
        TestBracketed(solver, name, f2, 1.5);
        // situation where bisection is used in the finite difference
        // newton solver as the first step and where the initial
        // guess is equal to the next estimate (which causes an infinite
        // derivative if we do not handle this case with special care)
        TestNotBracketed(solver, name, f3, 1.00001);
        // check that the last function call is made with the root value
        if (accuracy is Real a)
        {
            TestLastCallWithRoot(solver, name, false, a, probe);
            TestLastCallWithRoot(solver, name, true, a, probe);
        }
    }

    // secondDerivative, which the ported solvers don't call, is kept for Halley's case (B4.7).
    private readonly struct F1 : IDerivativeSolver1DFunction
    {
        public Real Evaluate(Real x) => (x * x) - 1.0;

        public Real Derivative(Real x) => 2.0 * x;

        public Real SecondDerivative(Real x) => 2.0;
    }

    private readonly struct F2 : IDerivativeSolver1DFunction
    {
        public Real Evaluate(Real x) => 1.0 - (x * x);

        public Real Derivative(Real x) => -2.0 * x;

        public Real SecondDerivative(Real x) => -2.0;
    }

    private readonly struct F3 : IDerivativeSolver1DFunction
    {
        public Real Evaluate(Real x) => Math.Atan(x - 1);

        public Real Derivative(Real x) => 1.0 / (1.0 + ((x - 1.0) * (x - 1.0)));

        public Real SecondDerivative(Real x)
        {
            Real u = x - 1.0;
            return -2 * u / ((1.0 + (u * u)) * (1.0 + (u * u)));
        }
    }

    // QuantLib's Probe holds a Real& to the test's argument, which it sets on each call, and the argument's value when
    // it is built; the box is the reference.
    private readonly struct Probe : IDerivativeSolver1DFunction
    {
        private readonly StrongBox<Real> result;
        private readonly Real previous;
        private readonly Real offset;

        public Probe(StrongBox<Real> result, Real offset)
        {
            this.result = result;
            previous = result.Value;
            this.offset = offset;
        }

        public Real Evaluate(Real x)
        {
            result.Value = x;
            return previous + offset - (x * x);
        }

        public Real Derivative(Real x) => 2.0 * x;

        public Real SecondDerivative(Real x) => 2.0;
    }
}
