// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's no-arbitrage SABR cube vectors,
/// <c>tests/vectors/cxx/experimental/volatility/noarbsabrswaptionvolatilitycube.json</c>:
/// <see cref="NoArbSabrSwaptionVolatilityCube"/> on QuantLib's SABR cube fixture at options of 5 years and more, beta
/// fixed, at-the-money calibrated and not; its parameter and volatility matrices, its volatilities on and off its grid
/// and by time, and its smile sections by tenors and by time, the elements S7R6's one-ulp refits leave stable. The cube
/// as <c>tools/cxx/src/termstructures/volatility/swaption/sabrswaptionvolatilitycube.cpp</c> builds it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NoArbSabrSwaptionVolatilityCubeCxxVectorTests
{
    private static readonly double[] Offsets = [-0.020, -0.005, 0.000, 0.005, 0.020, -0.01, 0.01, 0.03];

    private static readonly (Period Option, Period Swap, bool Extrapolate)[] SectionTenors =
    [
        (new(5, TimeUnit.Years), new(2, TimeUnit.Years), false),
        (new(10, TimeUnit.Years), new(10, TimeUnit.Years), false),
        (new(20, TimeUnit.Years), new(30, TimeUnit.Years), false),
        (new(7, TimeUnit.Years), new(7, TimeUnit.Years), false),
        (new(10, TimeUnit.Years), new(3, TimeUnit.Years), false),
        (new(15, TimeUnit.Years), new(20, TimeUnit.Years), false),
        (new(25, TimeUnit.Years), new(5, TimeUnit.Years), true),
        (new(35, TimeUnit.Years), new(5, TimeUnit.Years), true),
    ];

    private static readonly (double Time, double Length, bool Extrapolate)[] SectionTimes =
    [
        (5.5, 1.5, false), (8.0, 4.0, false), (15.0, 20.0, false), (40.0, 40.0, true),
    ];

    private static readonly double[] TimeStrikes = [0.03, 0.05, 0.07];

    [Fact]
    public void NoArbSabrSwaptionVolatilityCubeCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/volatility/noarbsabrswaptionvolatilitycube");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var s = (NoArbSabrSwaptionVolatilityCube)CubeInputs.CubeOf(c.Input("cube"), context);
            var matrices = new List<(string Name, Matrix Value)> { ("sparseSabrParameters", s.SparseSabrParameters()) };
            if (c.Expected.TryGetProperty("denseSabrParameters", out _))
            {
                matrices.Add(("denseSabrParameters", s.DenseSabrParameters()));
            }

            matrices.Add(("marketVolCube", s.MarketVolCube()));
            matrices.Add(("volCubeAtmCalibrated", s.VolCubeAtmCalibrated()));

            void Output<T>(string name, IEnumerable<T> values) => check.Output(c, name, CubeInputs.Kept(c, name, values));
            void Lazy(string name, IEnumerable<Func<object?>> values) => check.Output(c, name, CubeInputs.Kept(c, name, values));

            Output("shapes", matrices.SelectMany(m => new[] { m.Value.Rows, m.Value.Columns }));
            foreach ((string name, Matrix value) in matrices)
            {
                Output(name, CubeInputs.Flat(value));
            }

            Lazy("volatilityGrid", Volatilities(s, [new(5, TimeUnit.Years), new(10, TimeUnit.Years), new(20, TimeUnit.Years)], [new(2, TimeUnit.Years), new(10, TimeUnit.Years), new(30, TimeUnit.Years)], context));
            Lazy("volatilityOffGrid", Volatilities(s, [new(7, TimeUnit.Years), new(15, TimeUnit.Years), new(25, TimeUnit.Years)], [new(5, TimeUnit.Years), new(15, TimeUnit.Years), new(20, TimeUnit.Years)], context));
            Lazy(
                "volatilityTimeTime",
                from p in SectionTimes
                from k in TimeStrikes
                select (Func<object?>)(() => s.Volatility(p.Time, p.Length, k, p.Extrapolate)));
            Lazy("sectionsPeriodPeriod", SectionTenors.SelectMany(p => Section(() => s.SmileSection(p.Option, p.Swap, p.Extrapolate))));
            Lazy("sectionsTimeTime", SectionTimes.SelectMany(p => Section(() => s.SmileSection(p.Time, p.Length, p.Extrapolate))));
        }

        check.Complete();
    }

    // The volatilities at each option and swap tenor, at the at-the-money strike plus each offset, extrapolating.
    private static IEnumerable<Func<object?>> Volatilities(
        SwaptionVolatilityCube cube, Period[] options, Period[] swaps, EvaluationContext context)
    {
        foreach (Period o in options)
        {
            Date d = cube.OptionDateFromTenor(o);
            foreach (Period s in swaps)
            {
                double k = cube.AtmStrike(d, s, context);
                foreach (double sp in Offsets)
                {
                    yield return () => cube.Volatility(d, s, k + sp, true);
                }
            }
        }
    }

    // A section's seven outputs, each a throw where the section is refused.
    private static IEnumerable<Func<object?>> Section(Func<SmileSection> section)
    {
        NoArbSabrSmileSection NoArb() => Assert.IsType<NoArbSabrSmileSection>(section());
        return
        [
            () => NoArb().AtmLevel(),
            () => NoArb().ExerciseTime,
            () => NoArb().Shift,
            () => NoArb().VolatilityType,
            () => NoArb().Volatility(NoArb().AtmLevel()!.Value - 0.01),
            () => NoArb().Volatility(NoArb().AtmLevel()!.Value),
            () => NoArb().Volatility(NoArb().AtmLevel()!.Value + 0.01),
        ];
    }
}
