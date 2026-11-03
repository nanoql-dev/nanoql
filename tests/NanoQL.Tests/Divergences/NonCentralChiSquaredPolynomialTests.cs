// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The non-central χ² polynomial's recursion on exact moments, α and β rounded once. QuantLib sums each moment's closed
/// form in double and runs the ill-conditioned moment recursion in double: at QuantLib's own CLV cases its 14 nodes are
/// 2.6e-2 relative off the exact ones at ν 6, λ 31.7, and 0.61 at ν 9, λ 31.7, the smallest node 2.97 for 7.66; at λ 50
/// and 75 Boost refuses a negative node and at λ 100 the rule doesn't return. nanoql's nodes meet references from the
/// same exact moments by the Hankel matrix's Cholesky factor and the Jacobi matrix's eigenvalues at 160 digits
/// (mpmath), an independent route, to 1e-13 relative.
/// </summary>
public class NonCentralChiSquaredPolynomialTests
{
    public static TheoryData<double, double, double[]> References => new()
    {
        {
            6.0, 31.7,
            [
                6.279786631341354409746947, 12.30724022404895189041644, 19.22642187245446966625786,
                27.06063207189097383357075, 35.83547979617889422942137, 45.59764006949136931281957,
                56.42038055448923542294054, 68.4111492154402305609877, 81.72519640177565773351819,
                96.59090379335484011376403, 113.3601610852020157692062, 132.6214252814851285951867,
                155.5101930233217414559252, 184.9602705121725769502535,
            ]
        },
        {
            9.0, 31.7,
            [
                7.659886867281533432572439, 14.00567175869734619271918, 21.16711761327476874172572,
                29.20515274322777753539421, 38.15986067189534585980951, 48.0854582801627525561869,
                59.05985192112600879499998, 71.19370840472816935466822, 84.64477324601262875591769,
                99.64361672189391813798262, 116.5443693088032505019268, 135.9382890409395867231134,
                158.9654181919554109354755, 188.5705014155497831085223,
            ]
        },
        {
            4.0, 1.0,
            [
                0.8861597286857816670287626, 2.976336393473330344534551, 6.287927518254424721560564,
                10.88134879402838863930129, 16.87575793263501910623165, 24.48327896746077316823951,
                34.08371245875171589237801, 46.4309297263329709566515, 63.48373171436963104650529,
            ]
        },
        {
            6.0, 31.7,
            [
                11.19543838527352049026367, 20.74043205689432863945475, 31.63909581924397335144507,
                44.15997970685629388024557, 58.62950577715987839480445, 75.5898890008934498411029,
                96.09881897388961278516089, 122.9050319764774091673203,
            ]
        },
    };

    [Theory]
    [MemberData(nameof(References))]
    public void TheNodesMeetTheirReferences(double nu, double lambda, double[] reference)
    {
        double[] x = [.. new GaussianQuadrature(reference.Length, new GaussNonCentralChiSquaredPolynomial(nu, lambda)).X()];
        Array.Sort(x);
        for (int i = 0; i < x.Length; i++)
        {
            Assert.True(Math.Abs(x[i] - reference[i]) <= 1e-13 * reference[i], $"node {i}: {x[i]} for {reference[i]}");
        }

        if (nu == 9.0)
        {
            // QuantLib 1.44's smallest node at ν 9, λ 31.7
            Assert.True(Math.Abs(x[0] - 2.97) > 4.6);
        }
    }

    [Fact]
    public void TheRuleHoldsAtALargeNonCentrality()
    {
        // QuantLib 1.44's rule refuses at λ 50 and 75 (a negative node) and doesn't return at 100
        foreach (double lambda in new[] { 50.0, 75.0, 100.0, 200.0 })
        {
            double[] x = [.. new GaussianQuadrature(14, new GaussNonCentralChiSquaredPolynomial(9.0, lambda)).X()];
            Assert.All(x, node => Assert.True(node > 0.0 && double.IsFinite(node)));
        }
    }

    [Fact]
    public void TheMomentsStopAtQuantLibsTable()
    {
        var poly = new GaussNonCentralChiSquaredPolynomial(4.0, 1.0);
        Assert.Equal(37.0, poly.Moment(2));
        NanoQLException e = Assert.Throws<NanoQLException>(() => poly.Moment(28));
        Assert.Equal("only moments up to 28 are supported.", e.Message);
    }
}
