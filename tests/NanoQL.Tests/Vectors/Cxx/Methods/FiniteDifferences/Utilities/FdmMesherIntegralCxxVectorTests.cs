// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's FdmMesherIntegral vectors,
/// <c>tests/vectors/cxx/methods/finitedifferences/utilities/fdmmesherintegral.json</c>: <see cref="FdmMesherIntegral"/>
/// by Simpson's and the trapezoid rule over 1-D meshers of odd and even sizes, 2-D meshers and QuantLib's test's 3-D
/// mesher, on a polynomial and on seeded values.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmMesherIntegralCxxVectorTests
{
    [Fact]
    public void FdmMesherIntegralCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/utilities/fdmmesherintegral");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var mesher = new FdmMesherComposite(
                c.Input("locations").EnumerateArray()
                    .Select(x => (Fdm1dMesher)new Predefined1dMesher(x.EnumerateArray().Select(e => e.GetDouble()))));
            var f = new Vector(c.Input("f").EnumerateArray().Select(e => e.GetDouble()).ToArray());
            check.Output(
                c,
                "integral",
                c.Input("rules").EnumerateArray().Select(rule => (object?)new FdmMesherIntegral(
                    mesher,
                    rule.GetString() == "simpson"
                        ? (x, y) => DiscreteSimpsonIntegral.Integrate(x.AsSpan(), y.AsSpan())
                        : (x, y) => DiscreteTrapezoidIntegral.Integrate(x.AsSpan(), y.AsSpan())).Integrate(f)).ToArray());
        }

        check.Complete();
    }
}
