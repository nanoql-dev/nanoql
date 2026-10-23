// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ZABR interpolated smile section vectors,
/// <c>tests/vectors/termstructures/volatility/zabrinterpolatedsmilesection.json</c>: through its four named classes
/// (<see cref="ZabrShortMaturityLognormalInterpolatedSmileSection"/>,
/// <see cref="ZabrShortMaturityNormalInterpolatedSmileSection"/>,
/// <see cref="ZabrLocalVolatilityInterpolatedSmileSection"/> and <see cref="ZabrFullFdInterpolatedSmileSection"/>),
/// fits of a one-year smile with fixed and free parameters, unweighted, on floating strikes and with a quote missing,
/// each section's fitted parameters, errors and end criteria where stable (the S7 plan's S7R6), its inspectors, and its
/// volatilities and variances; and the sections QuantLib refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ZabrInterpolatedSmileSectionVectorTests
{
    [Fact]
    public void ZabrInterpolatedSmileSectionVector()
    {
        var file = VectorFile.Load("termstructures/volatility/zabrinterpolatedsmilesection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("section");
            switch (c.String("kind"))
            {
                case "section":
                    CheckSection(check, c, SectionOf(spec, context));
                    break;
                case "refusal":
                    check.Output(c, "section", () => SectionOf(spec, context).Alpha);
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    // The named section a spec describes, by Create from the case's context.
    private static Section SectionOf(JsonElement spec, EvaluationContext context)
    {
        string kernel = spec.GetProperty("kernel").GetString()!;
        var optionDate = new Date(spec.GetProperty("optionDate").GetInt32());
        double forward = IndexInputs.Real(spec.GetProperty("forward"));
        double[] strikes = VolatilityInputs.Reals(spec.GetProperty("strikes"));
        bool floating = spec.GetProperty("hasFloatingStrikes").GetBoolean();
        double atm = IndexInputs.Real(spec.GetProperty("atmVolatility"));
        double?[] vols = [.. spec.GetProperty("vols").EnumerateArray().Select(CouponInputs.NullableReal)];
        double? alpha = CouponInputs.NullableReal(spec.GetProperty("alpha"));
        double? beta = CouponInputs.NullableReal(spec.GetProperty("beta"));
        double? nu = CouponInputs.NullableReal(spec.GetProperty("nu"));
        double? rho = CouponInputs.NullableReal(spec.GetProperty("rho"));
        double? gamma = CouponInputs.NullableReal(spec.GetProperty("gamma"));
        bool alphaFixed = spec.GetProperty("isAlphaFixed").GetBoolean();
        bool betaFixed = spec.GetProperty("isBetaFixed").GetBoolean();
        bool nuFixed = spec.GetProperty("isNuFixed").GetBoolean();
        bool rhoFixed = spec.GetProperty("isRhoFixed").GetBoolean();
        bool gammaFixed = spec.GetProperty("isGammaFixed").GetBoolean();
        bool weighted = spec.GetProperty("vegaWeighted").GetBoolean();
        return kernel switch
        {
            "ShortMaturityLognormal" => new Section(ZabrShortMaturityLognormalInterpolatedSmileSection.Create(
                optionDate, forward, strikes, floating, atm, vols, alpha, beta, nu, rho, gamma, context, alphaFixed, betaFixed,
                nuFixed, rhoFixed, gammaFixed, weighted)),
            "ShortMaturityNormal" => new Section(ZabrShortMaturityNormalInterpolatedSmileSection.Create(
                optionDate, forward, strikes, floating, atm, vols, alpha, beta, nu, rho, gamma, context, alphaFixed, betaFixed,
                nuFixed, rhoFixed, gammaFixed, weighted)),
            "LocalVolatility" => new Section(ZabrLocalVolatilityInterpolatedSmileSection.Create(
                optionDate, forward, strikes, floating, atm, vols, alpha, beta, nu, rho, gamma, context, alphaFixed, betaFixed,
                nuFixed, rhoFixed, gammaFixed, weighted)),
            "FullFd" => new Section(ZabrFullFdInterpolatedSmileSection.Create(
                optionDate, forward, strikes, floating, atm, vols, alpha, beta, nu, rho, gamma, context, alphaFixed, betaFixed,
                nuFixed, rhoFixed, gammaFixed, weighted)),
            _ => throw new InvalidOperationException($"unknown ZABR kernel '{kernel}'"),
        };
    }

    private static void CheckSection(VectorAssert check, VectorCase c, Section s)
    {
        double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
        SmileSection smile = s.Smile;
        check.Output(c, "exerciseDate", smile.ExerciseDate);
        check.Output(c, "exerciseTime", smile.ExerciseTime);
        check.Output(c, "referenceDate", () => smile.ReferenceDate());
        check.Output(c, "dayCounter", smile.DayCounter?.Name);
        check.Output(c, "minStrike", smile.MinStrike());
        check.Output(c, "maxStrike", smile.MaxStrike());
        check.Output(c, "atmLevel", smile.AtmLevel());
        Check(check, c, "alpha", () => s.Alpha);
        Check(check, c, "beta", () => s.Beta);
        Check(check, c, "nu", () => s.Nu);
        Check(check, c, "rho", () => s.Rho);
        Check(check, c, "rmsError", () => s.RmsError);
        Check(check, c, "maxError", () => s.MaxError);
        Check(check, c, "endCriteria", () => s.EndCriteria);
        check.Output(c, "volatility", AtStrikes(strikes, k => smile.Volatility(k)));
        check.Output(c, "variance", AtStrikes(strikes, smile.Variance));
    }

    private static void Check(VectorAssert check, VectorCase c, string name, Func<object?> value)
    {
        if (c.Expected.TryGetProperty(name, out _))
        {
            check.Output(c, name, value);
        }
    }

    // Each value computed when compared, so that a throw is checked.
    private static Func<object?>[] AtStrikes(double[] strikes, Func<double, double> f) =>
        [.. strikes.Select(k => (Func<object?>)(() => f(k)))];

    // A section of any kernel, through the members the four named classes share.
    private sealed class Section
    {
        public Section(ZabrInterpolatedSmileSection<ZabrShortMaturityLognormal> s)
            : this(s, s.Alpha, s.Beta, s.Nu, s.Rho, s.RmsError, s.MaxError, s.EndCriteria)
        {
        }

        public Section(ZabrInterpolatedSmileSection<ZabrShortMaturityNormal> s)
            : this(s, s.Alpha, s.Beta, s.Nu, s.Rho, s.RmsError, s.MaxError, s.EndCriteria)
        {
        }

        public Section(ZabrInterpolatedSmileSection<ZabrLocalVolatility> s)
            : this(s, s.Alpha, s.Beta, s.Nu, s.Rho, s.RmsError, s.MaxError, s.EndCriteria)
        {
        }

        public Section(ZabrInterpolatedSmileSection<ZabrFullFd> s)
            : this(s, s.Alpha, s.Beta, s.Nu, s.Rho, s.RmsError, s.MaxError, s.EndCriteria)
        {
        }

        private Section(
            SmileSection smile, double alpha, double beta, double nu, double rho, double rmsError, double maxError, EndCriteriaType endCriteria)
        {
            Smile = smile;
            Alpha = alpha;
            Beta = beta;
            Nu = nu;
            Rho = rho;
            RmsError = rmsError;
            MaxError = maxError;
            EndCriteria = endCriteria;
        }

        public SmileSection Smile { get; }

        public double Alpha { get; }

        public double Beta { get; }

        public double Nu { get; }

        public double Rho { get; }

        public double RmsError { get; }

        public double MaxError { get; }

        public EndCriteriaType EndCriteria { get; }
    }
}
