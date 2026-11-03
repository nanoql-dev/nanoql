// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ZABR swaption volatility cube's vectors,
/// <c>tests/vectors/termstructures/volatility/swaption/zabrswaptionvolatilitycube.json</c>: cubes calibrated on the
/// test fixture with QuantLib's ZABR guesses, beta and gamma fixed, at-the-money calibrated and not, gamma fixed at 0.75
/// and free, backward flat, by maximum errors and by Simplex; their four matrices, whose errors the generator recomputes
/// from each row's parameters, their volatilities on and off the grid and their ZABR sections, each output kept only
/// where the stability filter found it stable. Its last two cases replace QuantLib's <c>testZabrObservability</c>: the
/// cube built under two evaluation dates, where QuantLib's cube built before the date moves follows it, and the
/// generator checks QuantLib's agreement.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ZabrSwaptionVolatilityCubeVectorTests
{
    private static readonly double[] Offsets = [-0.020, -0.005, 0.000, 0.005, 0.020, -0.01, 0.01, 0.03];

    private static readonly (Period Option, Period Swap, bool Extrapolate)[] SectionTenors =
    [
        (new(1, TimeUnit.Years), new(2, TimeUnit.Years), false),
        (new(10, TimeUnit.Years), new(10, TimeUnit.Years), false),
        (new(30, TimeUnit.Years), new(30, TimeUnit.Years), false),
        (new(5, TimeUnit.Years), new(7, TimeUnit.Years), false),
        (new(10, TimeUnit.Years), new(3, TimeUnit.Years), false),
        (new(2, TimeUnit.Years), new(20, TimeUnit.Years), false),
        (new(6, TimeUnit.Months), new(1, TimeUnit.Years), false),
        (new(35, TimeUnit.Years), new(5, TimeUnit.Years), true),
    ];

    private static readonly (double Time, double Length, bool Extrapolate)[] SectionTimes =
    [
        (0.5, 1.5, false), (3.0, 4.0, false), (15.0, 20.0, false), (40.0, 40.0, true),
    ];

    private static readonly double[] TimeStrikes = [0.03, 0.05, 0.07];

    [Fact]
    public void ZabrSwaptionVolatilityCubeVector()
    {
        var file = VectorFile.Load("termstructures/volatility/swaption/zabrswaptionvolatilitycube");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var cube = (ZabrSwaptionVolatilityCube)CubeInputs.CubeOf(c.Input("cube"), context);
            if (c.Expected.TryGetProperty("volatility", out _))
            {
                // testZabrObservability's 45 points: the cube's tenors at 3% plus each strike spread, not extrapolating.
                Period[] options = CubeInputs.Tenors(c.Input("cube").GetProperty("optionTenors"));
                Period[] swaps = CubeInputs.Tenors(c.Input("cube").GetProperty("swapTenors"));
                double[] spreads = VolatilityInputs.Reals(c.Input("cube").GetProperty("strikeSpreads"));
                check.Output(
                    c,
                    "volatility",
                    CubeInputs.Kept(
                        c,
                        "volatility",
                        from o in options
                        from w in swaps
                        from sp in spreads
                        select (Func<object?>)(() => cube.Volatility(o, w, 0.03 + sp, false))));
                continue;
            }

            var matrices = new List<(string Name, Matrix Value)> { ("sparseSabrParameters", cube.SparseSabrParameters()) };
            if (c.Expected.TryGetProperty("denseSabrParameters", out _))
            {
                matrices.Add(("denseSabrParameters", cube.DenseSabrParameters()));
            }

            matrices.Add(("marketVolCube", cube.MarketVolCube()));
            matrices.Add(("volCubeAtmCalibrated", cube.VolCubeAtmCalibrated()));

            void Output<T>(string name, IEnumerable<T> values) => check.Output(c, name, CubeInputs.Kept(c, name, values));
            void Lazy(string name, IEnumerable<Func<object?>> values) => check.Output(c, name, CubeInputs.Kept(c, name, values));

            Output("shapes", matrices.SelectMany(m => new[] { m.Value.Rows, m.Value.Columns }));
            foreach ((string name, Matrix value) in matrices)
            {
                Output(name, CubeInputs.Flat(value));
            }

            Lazy("volatilityGrid", Volatilities(cube, [new(1, TimeUnit.Years), new(10, TimeUnit.Years), new(30, TimeUnit.Years)], [new(2, TimeUnit.Years), new(10, TimeUnit.Years), new(30, TimeUnit.Years)], context));
            Lazy("volatilityOffGrid", Volatilities(cube, [new(2, TimeUnit.Years), new(5, TimeUnit.Years), new(20, TimeUnit.Years)], [new(5, TimeUnit.Years), new(15, TimeUnit.Years), new(20, TimeUnit.Years)], context));
            Lazy(
                "volatilityTimeTime",
                from p in SectionTimes
                from k in TimeStrikes
                select (Func<object?>)(() => cube.Volatility(p.Time, p.Length, k, p.Extrapolate)));
            Lazy("sectionsPeriodPeriod", SectionTenors.SelectMany(p => Section(() => cube.SmileSection(p.Option, p.Swap, p.Extrapolate))));
            Lazy("sectionsTimeTime", SectionTimes.SelectMany(p => Section(() => cube.SmileSection(p.Time, p.Length, p.Extrapolate))));
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
        ZabrSmileSection<ZabrShortMaturityLognormal> Zabr() => Assert.IsType<ZabrSmileSection<ZabrShortMaturityLognormal>>(section());
        return
        [
            () => Zabr().AtmLevel(),
            () => Zabr().ExerciseTime,
            () => Zabr().Shift,
            () => Zabr().VolatilityType,
            () => Zabr().Volatility(Zabr().AtmLevel()!.Value - 0.01),
            () => Zabr().Volatility(Zabr().AtmLevel()!.Value),
            () => Zabr().Volatility(Zabr().AtmLevel()!.Value + 0.01),
        ];
    }
}
