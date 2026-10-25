// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's fractional kernel approximation vectors,
/// <c>tests/vectors/cxx/math/ode/fractionalkernelapproximation.json</c>: the size, weights, rates and values of the
/// sum-of-exponentials approximation at four orders, four factor counts and three horizons.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FractionalKernelApproximationCxxVectorTests
{
    [Fact]
    public void FractionalKernelApproximationCxxVector()
    {
        var file = VectorFile.Load("cxx/math/ode/fractionalkernelapproximation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var kernel = new FractionalKernelApproximation(c.Double("alpha"), c.Input("n").GetInt32(), c.Double("tMax"));
            check.Output(c, "size", kernel.Size);
            check.Output(c, "weights", kernel.Weights.Select(x => (object?)x).ToArray());
            check.Output(c, "rates", kernel.Rates.Select(x => (object?)x).ToArray());
            check.Output(c, "values", c.Doubles("ts").Select(t => Lazy(() => kernel.Value(t))).ToArray());
        }

        check.Complete();
    }
}
