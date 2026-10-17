// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Simplex on a problem with no variables: QuantLib sizes its function values at n + 1 = 1 and then reads the second
/// (<c>simplex.cpp:99-106,119</c>), a result C++ leaves undefined. nanoql refuses it with
/// <see cref="NanoQLException"/>, as Levenberg–Marquardt refuses it with QuantLib's own "no variables given".
/// </summary>
public class SimplexEmptyInitialValueTests
{
    [Fact]
    public void ASimplexWithNoVariablesIsRefused()
    {
        var problem = new Problem(new SimpleCostFunction(x => x.Clone()), new NoConstraint(), []);
        var endCriteria = new EndCriteria(100, 10, 1e-8, 1e-8, 1e-8);

        NanoQLException e = Assert.Throws<NanoQLException>(() => new Simplex(0.1).Minimize(problem, endCriteria));
        Assert.Equal("Simplex needs at least one variable: the initial value is empty", e.Message);
    }
}
