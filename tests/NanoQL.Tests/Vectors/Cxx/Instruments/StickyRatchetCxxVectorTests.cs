// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The sticky and ratchet payoffs' harness vectors, <c>tests/vectors/cxx/instruments/stickyratchet.json</c>:
/// <see cref="DoubleStickyRatchetPayoff"/> on every pair of types and its six named forms, their names, descriptions
/// and values, and QuantLib's refusal of an illegal type.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StickyRatchetCxxVectorTests
{
    [Fact]
    public void StickyRatchetCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/stickyratchet");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] x = c.Doubles("arguments");
            DoubleStickyRatchetPayoff payoff = c.String("class") switch
            {
                "DoubleStickyRatchetPayoff" => new DoubleStickyRatchetPayoff(x[0], x[1], x[2], x[3], x[4], x[5], x[6], x[7], x[8], x[9], x[10]),
                "RatchetPayoff" => new RatchetPayoff(x[0], x[1], x[2], x[3], x[4], x[5]),
                "StickyPayoff" => new StickyPayoff(x[0], x[1], x[2], x[3], x[4], x[5]),
                "RatchetMaxPayoff" => new RatchetMaxPayoff(x[0], x[1], x[2], x[3], x[4], x[5], x[6], x[7], x[8]),
                "RatchetMinPayoff" => new RatchetMinPayoff(x[0], x[1], x[2], x[3], x[4], x[5], x[6], x[7], x[8]),
                "StickyMaxPayoff" => new StickyMaxPayoff(x[0], x[1], x[2], x[3], x[4], x[5], x[6], x[7], x[8]),
                "StickyMinPayoff" => new StickyMinPayoff(x[0], x[1], x[2], x[3], x[4], x[5], x[6], x[7], x[8]),
                string cls => throw new InvalidOperationException($"unknown payoff '{cls}'"),
            };
            check.Output(c, "name", payoff.Name);
            check.Output(c, "description", payoff.Description);
            check.Output(c, "values", c.Doubles("forwards").Select(f => (Func<object?>)(() => payoff.Evaluate(f))).ToArray());
        }

        check.Complete();
    }
}
