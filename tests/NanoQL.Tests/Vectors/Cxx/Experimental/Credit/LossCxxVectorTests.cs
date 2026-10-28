// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's loss vectors, <c>tests/vectors/cxx/experimental/credit/loss.json</c>: the constructors and the
/// comparisons, which read the times alone.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LossCxxVectorTests
{
    [Fact]
    public void LossCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/loss");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var empty = default(Loss);
            check.Output(c, "default", new object[] { empty.Time, empty.Amount });
            check.Output(c, "inspect", c.Input("losses").EnumerateArray().Select(x =>
            {
                var l = new Loss(VectorCase.Real(x[0]), VectorCase.Real(x[1]));
                return (object)new object[] { l.Time, l.Amount };
            }).ToArray());
            check.Output(c, "compare", c.Input("pairs").EnumerateArray().Select(p =>
            {
                var l1 = new Loss(VectorCase.Real(p[0]), VectorCase.Real(p[1]));
                var l2 = new Loss(VectorCase.Real(p[2]), VectorCase.Real(p[3]));
                return (object)new object[] { l1 < l2, l1 > l2, l1 == l2, l1 != l2 };
            }).ToArray());
        }

        check.Complete();
    }
}
