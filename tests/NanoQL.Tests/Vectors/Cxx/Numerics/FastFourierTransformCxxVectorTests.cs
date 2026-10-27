// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's FFT vectors, <c>tests/vectors/cxx/math/fastfouriertransform.json</c>: the minimum orders, the
/// output sizes, and the direct and inverse transforms of complex and real inputs at orders 0 to 7. A complex value is
/// compared as its [real, imaginary] pair.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FastFourierTransformCxxVectorTests
{
    [Fact]
    public void FastFourierTransformCxxVector()
    {
        var file = VectorFile.Load("cxx/math/fastfouriertransform");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("call"))
            {
                case "min_order":
                    check.Output(c, "orders", c.Ints("sizes").Select(FastFourierTransform.MinOrder).ToArray());
                    break;
                case "output_size":
                    check.Output(c, "sizes", c.Ints("orders").Select(o => new FastFourierTransform(o).OutputSize).ToArray());
                    break;
                default:
                {
                    var fft = new FastFourierTransform(c.Int("order"));
                    var output = new Complex[fft.OutputSize];
                    bool inverse = c.Bool("inverse");
                    if (c.String("call") == "transform")
                    {
                        Complex[] input = [.. c.Input("inputs").EnumerateArray().Select(ComplexOf)];
                        if (inverse)
                        {
                            fft.InverseTransform(input, output);
                        }
                        else
                        {
                            fft.Transform(input, output);
                        }
                    }
                    else
                    {
                        double[] input = c.Doubles("inputs");
                        if (inverse)
                        {
                            fft.InverseTransform(input, output);
                        }
                        else
                        {
                            fft.Transform(input, output);
                        }
                    }

                    check.Output(c, "output", output.Select(z => (double[])[z.Real, z.Imaginary]).ToArray());
                    break;
                }
            }
        }

        check.Complete();
    }

    private static Complex ComplexOf(JsonElement e) => new(e[0].GetDouble(), e[1].GetDouble());
}
