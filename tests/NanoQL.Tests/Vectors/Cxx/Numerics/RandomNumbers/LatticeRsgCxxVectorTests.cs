// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// The lattice rules' harness vectors, <c>tests/vectors/cxx/math/randomnumbers/latticersg.json</c>: each rule's
/// generating vector from <see cref="LatticeRule"/>, nanoql's generated table against QuantLib's arrays, and
/// <see cref="LatticeRsg"/>'s first points over each rule. Halfway, the draws continue from a copy.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LatticeRsgCxxVectorTests
{
    [Fact]
    public void LatticeRsgCxxVector()
    {
        var file = VectorFile.Load("cxx/math/randomnumbers/latticersg");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            LatticeRuleType rule = Enum.Parse<LatticeRuleType>(c.String("rule"));
            int n = c.Int("n");
            if (c.Name.StartsWith("rule ", StringComparison.Ordinal) && !c.Name.Contains(',', StringComparison.Ordinal))
            {
                check.Output(c, "getRule", LatticeRule.GetRule(rule, n).ToArray());
                continue;
            }

            var rsg = new LatticeRsg(c.Int("dimensionality"), LatticeRule.GetRule(rule, n), n);
            int draws = c.Int("draws");
            var sequences = new double[draws][];
            for (int i = 0; i < draws; i++)
            {
                if (i == draws / 2)
                {
                    rsg = rsg.Clone();
                }

                sequences[i] = rsg.NextSequence().Value.ToArray();
            }

            check.Output(c, "nextSequence", sequences);
        }

        check.Complete();
    }
}
