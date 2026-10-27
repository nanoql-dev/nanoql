// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Quotes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The functional quotes' vectors, <c>tests/vectors/quotes/derivedquote.json</c>, <c>compositequote.json</c> and
/// <c>multicompositequote.json</c>: <see cref="DerivedQuote"/>'s, <see cref="CompositeQuote"/>'s and
/// <see cref="MultiCompositeQuote"/>'s values and inspectors, each function the generator's arithmetic by name.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FunctionalQuoteVectorTests
{
    private static readonly Dictionary<string, Func<double, double>> Unary = new()
    {
        ["add10"] = x => x + 10,
        ["mul10"] = x => x * 10,
        ["sub10"] = x => x - 10,
        ["square"] = x => x * x,
        ["reciprocal"] = x => 1.0 / x,
        ["negThird"] = x => -x / 3,
    };

    private static readonly Dictionary<string, Func<double, double, double>> Binary = new()
    {
        ["add"] = (x, y) => x + y,
        ["mul"] = (x, y) => x * y,
        ["sub"] = (x, y) => x - y,
        ["div"] = (x, y) => x / y,
        ["affine"] = (x, y) => (x * y) + 1,
    };

    private static readonly Dictionary<string, Func<Vector, double>> OnArrays = new()
    {
        ["addAll"] = x =>
        {
            double s = 0.0;
            for (int i = 0; i < x.Size; i++)
            {
                s += x[i];
            }

            return s;
        },
        ["mulAll"] = x =>
        {
            double p = 1.0;
            for (int i = 0; i < x.Size; i++)
            {
                p *= x[i];
            }

            return p;
        },
        ["norm2"] = ArrayFunctions.Norm2,
    };

    [Fact]
    public void DerivedQuoteVector()
    {
        var file = VectorFile.Load("quotes/derivedquote");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var q = new DerivedQuote(c.Double("element"), Unary[c.String("function")]);
            check.Output(c, "value", q.Value);
        }

        check.Complete();
    }

    [Fact]
    public void CompositeQuoteVector()
    {
        var file = VectorFile.Load("quotes/compositequote");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var q = new CompositeQuote(c.Double("element1"), c.Double("element2"), Binary[c.String("function")]);
            check.Output(c, "value", q.Value);
            check.Output(c, "value1", q.Value1);
            check.Output(c, "value2", q.Value2);
        }

        check.Complete();
    }

    [Fact]
    public void MultiCompositeQuoteVector()
    {
        var file = VectorFile.Load("quotes/multicompositequote");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] elements = c.Doubles("elements");
            var q = new MultiCompositeQuote(elements, OnArrays[c.String("function")]);
            check.Output(c, "value", q.Value);
            check.Output(c, "inputValues", elements.Select((_, i) => q.InputValue(i)).ToArray());
        }

        check.Complete();
    }
}
