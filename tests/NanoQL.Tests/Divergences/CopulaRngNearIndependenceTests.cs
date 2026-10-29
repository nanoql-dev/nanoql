// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Clayton and Frank copula generators near independence (the S-xrt plan's XRR5 (xxxiv), fixed under R8). Their
/// conditional inverses cancel as θ → 0: QuantLib's Clayton draw is 7.1e-5 off at θ 1e-12 and 2.5e-7 at 1e-10, its
/// Frank draw 2.4e-4 and 8.3e-8 (the references: 40-digit mpmath of QuantLib's own formulas on the same uniforms,
/// the S-xrt run's <c>c4\copula_refs.py</c>). nanoql's <c>log1p</c>/<c>expm1</c> forms below |θ| 1e-2 meet the
/// references to the last bits; at and above 1e-2 the forms are QuantLib's.
/// </summary>
public class CopulaRngNearIndependenceTests
{
    // (v1, v2, theta, Clayton's u2, Frank's u2), the references to 30 digits
    public static readonly TheoryData<double, double, double, double, double> Cases = new()
    {
        { 0.3, 0.7, 1e-12, 0.699999999999949029199095354923, 0.699999999999957955591079009802 },
        { 0.3, 0.7, 1e-10, 0.699999999994907316391974770919, 0.699999999995799955591027214252 },
        { 0.3, 0.7, -1e-08, 0.700000000509263867969984650749, 0.700000000419999955073079082095 },
        { 0.3, 0.7, 1e-05, 0.69999949072862321665749957399, 0.699999579999481956118787629979 },
        { 0.3, 0.7, 0.005, 0.699743509553997983035702213797, 0.699789870566088338817841890509 },
        { 0.9, 0.2, 1e-12, 0.200000000000287984443042348727, 0.200000000000064011102230257942 },
        { 0.9, 0.2, 1e-10, 0.200000000028797345182894540971, 0.200000000006400011102347047106 },
        { 0.9, 0.2, -1e-08, 0.199999997120266597465963356519, 0.199999999360000012270230170514 },
        { 0.9, 0.2, 1e-05, 0.200002879727893388441305757163, 0.200000640001168011277273760222 },
        { 0.9, 0.2, 0.005, 0.201438481382580889837709443622, 0.200320292021594779222350532192 },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheDrawsMeetTheirReferences(double v1, double v2, double theta, double clayton, double frank)
    {
        double c = new ClaytonCopulaRng<FixedRng>(new FixedRng(v1, v2), theta).Next().Value[1];
        double f = new FrankCopulaRng<FixedRng>(new FixedRng(v1, v2), theta).Next().Value[1];
        Assert.True(Math.Abs(c - clayton) <= 1e-15 * clayton, $"Clayton {c:R} against {clayton:R}");
        Assert.True(Math.Abs(f - frank) <= 1e-15 * frank, $"Frank {f:R} against {frank:R}");
    }

    [Fact]
    public void QuantLibsFormsCancelAtTheta1e12()
    {
        // QuantLib's formulas on the same uniforms
        const double v1 = 0.3, v2 = 0.7, theta = 1e-12;
        double clayton = Math.Pow((Math.Pow(v1, -theta) * (Math.Pow(v2, -theta / (theta + 1.0)) - 1.0)) + 1.0, -1.0 / theta);
        double frank = (-1.0 / theta) * Math.Log(1.0 + (v2 * (1.0 - Math.Exp(-theta))
            / ((v2 * (Math.Exp(-theta * v1) - 1.0)) - Math.Exp(-theta * v1))));
        Assert.True(Math.Abs(clayton - 0.699999999999949029199095354923) > 1e-5, $"{clayton:R}");
        Assert.True(Math.Abs(frank - 0.699999999999957955591079009802) > 1e-6, $"{frank:R}");
    }

    // A generator of given uniforms, in turn, weight 1.
    public sealed class FixedRng(params double[] values) : IRng
    {
        private int next;

        public Sample<double> Next() => new(values[next++ % values.Length], 1.0);

        public IRng Clone() => new FixedRng(values) { next = next };
    }
}
