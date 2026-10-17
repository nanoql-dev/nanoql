// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The zero value of each inverse-cumulative configuration struct (playbook §2.6): <c>default(T)</c>, <c>new T()</c>
/// and an array element evaluate, bit for bit, as QuantLib's default construction, the standard normal or a Poisson
/// lambda of 1. None of the four exposes its parameters, as QuantLib's don't, so the evaluations show the defaults.
/// </summary>
public class InverseCumulativeZeroValueTests
{
    private static readonly double[] Probabilities = [1e-10, 0.001, 0.02425, 0.3, 0.5, 0.7, 0.9, 0.999999];

    [Fact]
    public void InverseCumulativeNormalZeroValueIsTheStandardNormal()
    {
        var defaults = new InverseCumulativeNormal(0.0, 1.0);

        // QuantLib's InverseCumulativeNormal() gives 0.5244005132792952 at 0.7.
        Assert.Equal(0.5244005132792952, default(InverseCumulativeNormal).Evaluate(0.7));
        InverseCumulativeNormal[] zeros = [default, new InverseCumulativeNormal(), (new InverseCumulativeNormal[1])[0]];
        foreach (InverseCumulativeNormal zero in zeros)
        {
            foreach (double p in Probabilities)
            {
                AssertBits(defaults.Evaluate(p), zero.Evaluate(p));
            }
        }
    }

    [Fact]
    public void MoroInverseCumulativeNormalZeroValueIsTheStandardNormal()
    {
        var defaults = new MoroInverseCumulativeNormal(0.0, 1.0);

        MoroInverseCumulativeNormal[] zeros = [default, new MoroInverseCumulativeNormal(), (new MoroInverseCumulativeNormal[1])[0]];
        foreach (MoroInverseCumulativeNormal zero in zeros)
        {
            foreach (double p in Probabilities)
            {
                AssertBits(defaults.Evaluate(p), zero.Evaluate(p));
            }
        }
    }

    [Fact]
    public void MaddockInverseCumulativeNormalZeroValueIsTheStandardNormal()
    {
        var defaults = new MaddockInverseCumulativeNormal(0.0, 1.0);

        MaddockInverseCumulativeNormal[] zeros = [default, new MaddockInverseCumulativeNormal(), (new MaddockInverseCumulativeNormal[1])[0]];
        foreach (MaddockInverseCumulativeNormal zero in zeros)
        {
            foreach (double p in Probabilities)
            {
                AssertBits(defaults.Evaluate(p), zero.Evaluate(p));
            }
        }
    }

    [Fact]
    public void InverseCumulativePoissonZeroValueHasLambdaOne()
    {
        var defaults = new InverseCumulativePoisson(1.0);

        InverseCumulativePoisson[] zeros = [default, new InverseCumulativePoisson(), (new InverseCumulativePoisson[1])[0]];
        foreach (InverseCumulativePoisson zero in zeros)
        {
            foreach (double p in Probabilities)
            {
                AssertBits(defaults.Evaluate(p), zero.Evaluate(p));
            }
        }

        // With lambda 1 the probability 0.9 maps to 2, as QuantLib's test suite has it; a lambda read as 0 would give 0.
        Assert.Equal(2.0, default(InverseCumulativePoisson).Evaluate(0.9));
    }

    private static void AssertBits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
