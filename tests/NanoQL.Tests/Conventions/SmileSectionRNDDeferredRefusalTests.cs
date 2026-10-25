// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The smile-section density calculator builds its quantile table at construction and, where that fails, raises the
/// refusal from the inverse, QuantLib's point (the S-van plan's VD10): the calculator is built, the density and the
/// distribution don't need the table, and the inverse raises QuantLib's message at every call. Its one time is the
/// section's: another is refused by all three, with QuantLib's message.
/// </summary>
public class SmileSectionRNDDeferredRefusalTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void ATableThatCantBeBuiltIsRefusedAtTheInverse()
    {
        // a flat 20% without an at-the-money level, whose density and distribution QuantLib still prices on the
        // section's option prices once the level is supplied; here the level is absent, so the table can't be built
        var noAtm = new FlatSmileSection(1.0, 0.20, new Actual365Fixed());
        var calculator = new SmileSectionRNDCalculator(noAtm);

        for (int call = 0; call < 2; ++call)
        {
            NanoQLException e = Assert.Throws<NanoQLException>(() => calculator.InvCdf(0.5));
            Assert.Contains("wrap with AtmSmileSection to supply one", e.Message, StringComparison.Ordinal);
        }

        // where the table can't be built for a zero volatility, the density and distribution still answer
        var zero = new FlatSmileSection(1.0, 0.0, new Actual365Fixed(), 100.0);
        var flat = new SmileSectionRNDCalculator(zero);
        Assert.Equal(0.0, flat.Cdf(Math.Log(90.0)));
        Assert.Equal(1.0, flat.Cdf(Math.Log(110.0)));
        NanoQLException z = Assert.Throws<NanoQLException>(() => flat.InvCdf(0.5));
        Assert.Contains("non-positive at-the-money volatility", z.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATimeOtherThanTheSectionsIsRefused()
    {
        var section = new FlatSmileSection(new Date(15, Month.October, 2027), 0.2, new Actual365Fixed(), Today, 100.0);
        var calculator = new SmileSectionRNDCalculator(section);
        double t = section.ExerciseTime;

        Assert.Equal(calculator.Pdf(Math.Log(100.0)), calculator.Pdf(Math.Log(100.0), t));
        foreach (Action query in new Action[]
                 {
                     () => calculator.Pdf(Math.Log(100.0), t + 0.01),
                     () => calculator.Cdf(Math.Log(100.0), t - 0.01),
                     () => calculator.InvCdf(0.5, 2 * t),
                 })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(query);
            Assert.Contains("does not match smile exercise time", e.Message, StringComparison.Ordinal);
        }
    }
}
