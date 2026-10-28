// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Student-factor copulas' factor range (the S-xcr code review's C3 O1-4): QuantLib's three Student classes pass no
/// minimum, so the base's −5 stands and the integrals run over [−5, 10], losing the factor's left tail, where defaults
/// are nearly certain; nanoql's run over [−maximum, maximum]. The copula's identity, the integral of p equal to p, is
/// the check: QuantLib's reads 0.000897975 for 0.001 and 0.00947913 for 0.01 on Student 5/5 at correlation 0.3,
/// 0.000102474 and 0.00857744 on Student-Gaussian ν 3 at 0.6, where the symmetric range reads them within 2.3% and
/// 0.6% (the review's <c>ofc</c> probe).
/// </summary>
public class OneFactorStudentCopulaRangeTests
{
    public static TheoryData<string> Copulas => ["StudentT 5/5 0.3", "StudentT 5/5 0.6", "StudentGaussian 3 0.3", "StudentGaussian 3 0.6", "GaussianStudent 5 0.3"];

    [Theory]
    [MemberData(nameof(Copulas))]
    public void TheIntegralOfAProbabilityIsTheProbability(string name)
    {
        OneFactorCopula copula = Of(name);
        Assert.Equal(0.01, copula.Integral(0.01), 0.01 * 0.01);
        Assert.Equal(0.001, copula.Integral(0.001), 0.03 * 0.001);
    }

    private static OneFactorCopula Of(string name)
    {
        string[] p = name.Split(' ');
        double c = double.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture);
        return p[0] switch
        {
            "StudentT" => new OneFactorStudentCopula(c, 5, 5),
            "StudentGaussian" => new OneFactorStudentGaussianCopula(c, int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)),
            _ => new OneFactorGaussianStudentCopula(c, int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)),
        };
    }
}
