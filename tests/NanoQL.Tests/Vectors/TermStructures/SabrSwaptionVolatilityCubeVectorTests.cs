// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The SABR swaption volatility cube's vectors,
/// <c>tests/vectors/termstructures/volatility/swaption/sabrswaptionvolatilitycube.json</c>: cubes calibrated on the
/// test fixture with every parameter free and with beta fixed, at-the-money calibrated and not, backward flat, in a
/// single pass, on a normal matrix, by Simplex, by maximum errors, with a cutoff strike and on one strike spread; their
/// four matrices, their volatilities on and off the grid and their SABR sections, each output kept only where the
/// stability filter found it stable. Its last four cases replace QuantLib's <c>testObservability</c>: a SABR and an
/// interpolated cube each built under two evaluation dates, where QuantLib's cube built before the date moves follows
/// it, and the generator checks QuantLib's agreement.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SabrSwaptionVolatilityCubeVectorTests
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
    public void SabrSwaptionVolatilityCubeVector()
    {
        var file = VectorFile.Load("termstructures/volatility/swaption/sabrswaptionvolatilitycube");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            SwaptionVolatilityCube cube = CubeInputs.CubeOf(c.Input("cube"), context);
            if (c.Expected.TryGetProperty("volatility", out _))
            {
                // testObservability's 45 points: the cube's tenors at 3% plus each strike spread, not extrapolating.
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

            var s = (SabrSwaptionVolatilityCube)cube;
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

            Lazy("volatilityGrid", Volatilities(s, [new(1, TimeUnit.Years), new(10, TimeUnit.Years), new(30, TimeUnit.Years)], [new(2, TimeUnit.Years), new(10, TimeUnit.Years), new(30, TimeUnit.Years)], context));
            Lazy("volatilityOffGrid", Volatilities(s, [new(2, TimeUnit.Years), new(5, TimeUnit.Years), new(20, TimeUnit.Years)], [new(5, TimeUnit.Years), new(15, TimeUnit.Years), new(20, TimeUnit.Years)], context));
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

    // A section's eleven outputs, each a throw where the section is refused.
    private static IEnumerable<Func<object?>> Section(Func<SmileSection> section)
    {
        SabrSmileSection Sabr() => Assert.IsType<SabrSmileSection>(section());
        return
        [
            () => Sabr().Alpha,
            () => Sabr().Beta,
            () => Sabr().Nu,
            () => Sabr().Rho,
            () => Sabr().AtmLevel(),
            () => Sabr().ExerciseTime,
            () => Sabr().Shift,
            () => Sabr().VolatilityType,
            () => Sabr().Volatility(Sabr().AtmLevel()!.Value - 0.01),
            () => Sabr().Volatility(Sabr().AtmLevel()!.Value),
            () => Sabr().Volatility(Sabr().AtmLevel()!.Value + 0.01),
        ];
    }
}
