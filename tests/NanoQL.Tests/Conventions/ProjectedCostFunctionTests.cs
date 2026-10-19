// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="ProjectedCostFunction"/> built from a <see cref="Projection"/> takes QuantLib's sliced copy
/// (<c>projectedcostfunction.cpp:32-35</c>): it copies the projection's values and flags, so a subclass's overrides of
/// <c>Include</c> and <c>Project</c> aren't run. Its <c>Value</c> and <c>Values</c> check the free values' count with
/// QuantLib's <c>mapFreeParameters</c> text (<c>projectedcostfunction.cpp:38, 43</c>, <c>projection.cpp:43-46</c>), an
/// <see cref="ArgumentException"/> naming <c>freeParameters</c>.
/// </summary>
public class ProjectedCostFunctionTests
{
    [Fact]
    public void AProjectionIsCopiedAndTheFreeCountChecked()
    {
        var doubling = new Doubling([1.0, 2.0, 3.0], [false, true, false]);
        Vector free = Of(10.0, 30.0);
        Assert.Equal([20.0, 4.0, 60.0], doubling.Include(free));
        Assert.Equal([14.0, 18.0], doubling.Project(Of(7.0, 8.0, 9.0)));

        // The sliced copy: the base's Include and Project, not the subclass's.
        var pcf = new ProjectedCostFunction(new Identity(), doubling);
        Assert.Equal([10.0, 2.0, 30.0], pcf.Include(free));
        Assert.Equal([7.0, 9.0], pcf.Project(Of(7.0, 8.0, 9.0)));
        Assert.Equal([10.0, 2.0, 30.0], pcf.Values(free));
        Assert.Equal(Math.Sqrt(1004.0 / 3), pcf.Value(free));

        foreach (Vector wrong in new[] { Of(1.0), Of(1.0, 2.0, 3.0) })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(() => pcf.Value(wrong));
            Assert.Equal("freeParameters", e.ParamName);
            Assert.StartsWith("parameterValues.size()!=numberOfFreeParameters", e.Message, StringComparison.Ordinal);
            e = Assert.Throws<ArgumentException>(() => pcf.Values(wrong));
            Assert.Equal("freeParameters", e.ParamName);
            Assert.StartsWith("parameterValues.size()!=numberOfFreeParameters", e.Message, StringComparison.Ordinal);
        }
    }

    private static Vector Of(params double[] values)
    {
        var v = new Vector(values.Length);
        for (int i = 0; i < values.Length; ++i)
        {
            v[i] = values[i];
        }

        return v;
    }

    // A projection whose overrides double what the base gives.
    private sealed class Doubling(double[] values, bool[] flags) : Projection(values, flags)
    {
        public override Vector Include(Vector projectedParameters)
        {
            Vector y = base.Include(projectedParameters);
            for (int i = 0; i < y.Size; ++i)
            {
                y[i] *= 2.0;
            }

            return y;
        }

        public override Vector Project(Vector parameters)
        {
            Vector y = base.Project(parameters);
            for (int i = 0; i < y.Size; ++i)
            {
                y[i] *= 2.0;
            }

            return y;
        }
    }

    // Its values are its parameters.
    private sealed class Identity : CostFunction
    {
        public override Vector Values(Vector x)
        {
            var y = new Vector(x.Size);
            for (int i = 0; i < x.Size; ++i)
            {
                y[i] = x[i];
            }

            return y;
        }
    }
}
