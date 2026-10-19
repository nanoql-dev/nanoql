// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/operators.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Not ported: testConsistency (exercises only deprecated code: DZero and DPlusDMinus, D5)

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class OperatorsTests
{
    [Fact]
    public void TestTridiagonal()
    {
        // Testing tridiagonal operator...

        Size n = 8; // can use 3 for easier debugging

        var T = new TridiagonalOperator(n);
        T.SetFirstRow(1.0, 2.0);
        T.SetMidRows(0.0, 2.0, 0.0);
        T.SetLastRow(2.0, 1.0);

        var original = new Vector(n, 1.0);

        Vector intermediate = T.ApplyTo(original);

        Vector final = intermediate.Clone();
        T.SolveFor(final, final);
        for (Size i = 0; i < n; ++i)
        {
            if (final[i] != original[i])
            {
                Assert.Fail(
                    $"\n applyTo + solveFor does not equal identity:\n            original vector: {original}\n         transformed vector: {intermediate}\n inverse transformed vector: {final}");
            }
        }

        final = new Vector(n, 0.0);
        Vector temp = intermediate.Clone();
        T.SolveFor(temp, final);
        for (Size i = 0; i < n; ++i)
        {
            if (temp[i] != intermediate[i])
            {
                Assert.Fail(
                    $"\n solveFor altered rhs:\n            original vector: {original}\n         transformed vector: {intermediate}\n altered transformed vector: {temp}\n inverse transformed vector: {final}");
            }
        }

        for (Size i = 0; i < n; ++i)
        {
            if (final[i] != original[i])
            {
                Assert.Fail(
                    $"\n applyTo + solveFor does not equal identity:\n            original vector: {original}\n         transformed vector: {intermediate}\n inverse transformed vector: {final}");
            }
        }

        final = T.SolveFor(temp);
        for (Size i = 0; i < n; ++i)
        {
            if (temp[i] != intermediate[i])
            {
                Assert.Fail(
                    $"\n solveFor altered rhs:\n            original vector: {original}\n         transformed vector: {intermediate}\n altered transformed vector: {temp}\n inverse transformed vector: {final}");
            }
        }

        for (Size i = 0; i < n; ++i)
        {
            if (final[i] != original[i])
            {
                Assert.Fail(
                    $"\n applyTo + solveFor does not equal identity:\n            original vector: {original}\n         transformed vector: {intermediate}\n inverse transformed vector: {final}");
            }
        }

        Real delta, error = 0.0, tolerance = 1e-9;
        final = T.SOR(temp, tolerance);
        for (Size i = 0; i < n; ++i)
        {
            delta = final[i] - original[i];
            error += delta * delta;
            if (temp[i] != intermediate[i])
            {
                Assert.Fail(
                    $"\n SOR altered rhs:\n            original vector: {original}\n         transformed vector: {intermediate}\n altered transformed vector: {temp}\n inverse transformed vector: {final}");
            }
        }

        if (error > tolerance)
        {
            Assert.Fail(
                $"\n applyTo + SOR does not equal identity:\n            original vector: {original}\n         transformed vector: {intermediate}\n inverse transformed vector: {final}\n                      error: {error}\n                  tolerance: {tolerance}");
        }
    }
}
