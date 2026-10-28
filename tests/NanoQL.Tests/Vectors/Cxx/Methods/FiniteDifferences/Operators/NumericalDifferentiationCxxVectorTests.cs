// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's numerical differentiation vectors,
/// <c>tests/vectors/cxx/methods/finitedifferences/operators/numericaldifferentiation.json</c>:
/// <see cref="NumericalDifferentiation"/>'s offsets, weights and derivatives of four functions on given distinct
/// offsets for orders 0 to 4 and by the central, backward and forward schemes, and the inputs QuantLib refuses. The
/// wheel doesn't expose it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NumericalDifferentiationCxxVectorTests
{
    // the harness's functions, written as it writes them
    private static readonly Dictionary<string, Func<double, double>> Functions = new()
    {
        ["sin(x)"] = Math.Sin,
        ["exp(x)"] = Math.Exp,
        ["x x x"] = x => x * x * x,
        ["1/(1+x x)"] = x => 1.0 / (1.0 + (x * x)),
    };

    [Fact]
    public void NumericalDifferentiationCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/operators/numericaldifferentiation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Func<double, double> f = Functions[c.String("function")];
            int order = c.Int("order");
            Func<NumericalDifferentiation> make = c.String("method") == "NumericalDifferentiation"
                ? () => new NumericalDifferentiation(f, order, c.Doubles("offsets"))
                : () => new NumericalDifferentiation(
                    f, order, c.Double("stepSize"), c.Int("steps"), Enum.Parse<NumericalDifferentiationScheme>(c.String("scheme")));
            if (c.Expected.TryGetProperty("construction", out _))
            {
                check.Output(c, "construction", () => make());
                continue;
            }

            NumericalDifferentiation nd = make();
            check.Output(c, "offsets", nd.Offsets().ToArray());
            check.Output(c, "weights", nd.Weights().ToArray());
            check.Output(c, "derivative", c.Doubles("at").Select(nd.Evaluate).ToArray());
        }

        check.Complete();
    }
}
