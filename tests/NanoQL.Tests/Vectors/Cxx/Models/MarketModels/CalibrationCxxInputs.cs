// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's caplet calibration specs, as tools/cxx/src/models/marketmodels/models/calibrations.cpp reads them
/// (QuantLib's caplet calibration cases' setup on the given rate times, forwards, correlation, abcd and caplet
/// volatilities), and the calibrations' results it writes, checked against nanoql's.
/// </summary>
internal static class CalibrationCxxInputs
{
    /// <summary>The evolution, correlation, variances, caplet volatilities, curve state and displacement a spec
    /// describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The calibration's inputs.</returns>
    public static (EvolutionDescription Evolution, PiecewiseConstantCorrelation Corr, PiecewiseConstantVariance[] Variances,
        double[] CapletVols, LMMCurveState Cs, double Displacement) SpecOf(JsonElement spec)
    {
        double[] rateTimes = Reals(spec.GetProperty("rateTimes"));
        double[] abcd = Reals(spec.GetProperty("abcd"));
        double displacement = spec.GetProperty("displacement").GetDouble();
        var evolution = new EvolutionDescription(rateTimes);
        var cs = new LMMCurveState(rateTimes);
        cs.SetOnForwardRates(Reals(spec.GetProperty("forwards")));
        var fwdCorr = new ExponentialForwardCorrelation(
            rateTimes, spec.GetProperty("longTermCorr").GetDouble(), spec.GetProperty("beta").GetDouble());
        var corr = new CotSwapFromFwdCorrelation(fwdCorr, cs, displacement);
        PiecewiseConstantVariance[] variances =
        [
            .. Enumerable.Range(0, rateTimes.Length - 1)
                .Select(i => new PiecewiseConstantAbcdVariance(abcd[0], abcd[1], abcd[2], abcd[3], i, rateTimes)),
        ];
        return (evolution, corr, variances, Reals(spec.GetProperty("capletVols")), cs, displacement);
    }

    /// <summary>
    /// A periodic spec's specifier: its long variances on every period-th rate time from the rates' count mod the
    /// period, under <see cref="VolatilityInterpolationSpecifierabcd"/>.
    /// </summary>
    /// <param name="spec">The periodic spec.</param>
    /// <returns>The specifier.</returns>
    public static VolatilityInterpolationSpecifierabcd SpecifierOf(JsonElement spec)
    {
        double[] rateTimes = Reals(spec.GetProperty("rateTimes"));
        double[] abcd = Reals(spec.GetProperty("abcd"));
        int period = spec.GetProperty("period").GetInt32();
        int numberOfRates = rateTimes.Length - 1;
        int offset = numberOfRates % period;
        int numberBigRates = numberOfRates / period;
        double[] bigRateTimes = [.. Enumerable.Range(0, numberBigRates + 1).Select(i => rateTimes[(i * period) + offset])];
        PiecewiseConstantAbcdVariance[] longVariances =
        [
            .. Enumerable.Range(0, numberBigRates)
                .Select(i => new PiecewiseConstantAbcdVariance(abcd[0], abcd[1], abcd[2], abcd[3], i, bigRateTimes)),
        ];
        return new VolatilityInterpolationSpecifierabcd(period, offset, longVariances, rateTimes);
    }

    /// <summary>
    /// Checks a calibration's results, as the harness writes them, or its throw where the case records one.
    /// </summary>
    /// <param name="check">The file's assertion.</param>
    /// <param name="c">The case.</param>
    /// <param name="make">The calibration.</param>
    /// <param name="calibrate">Its calibration.</param>
    public static void CheckCalibration(
        VectorAssert check,
        VectorCase c,
        Func<CTSMMCapletCalibration> make,
        Func<CTSMMCapletCalibration, CTSMMCapletCalibrationResults> calibrate)
    {
        if (c.Expected.TryGetProperty("throws", out _))
        {
            check.Output(c, "throws", Lazy(() => calibrate(make())));
            return;
        }

        CTSMMCapletCalibration calibration = make();
        CTSMMCapletCalibrationResults r = calibrate(calibration);
        int n = calibration.MktCapletVols.Length;
        if (c.Expected.TryGetProperty("failures", out _))
        {
            check.Output(c, "converged", r.Converged);
            check.Output(c, "failures", r.Failures);
        }
        check.Output(c, "capletRmsError", r.CapletRmsError);
        check.Output(c, "capletMaxError", r.CapletMaxError);
        check.Output(c, "swaptionRmsError", r.SwaptionRmsError);
        check.Output(c, "swaptionMaxError", r.SwaptionMaxError);
        check.Output(c, "mktCapletVols", calibration.MktCapletVols);
        check.Output(c, "mdlCapletVols", r.MdlCapletVols);
        check.Output(c, "mktSwaptionVols", r.MktSwaptionVols);
        check.Output(c, "mdlSwaptionVols", r.MdlSwaptionVols);
        check.Output(c, "swapPseudoRoots", r.SwapPseudoRoots().Select(Rows).ToArray());
        check.Output(
            c, "timeDependentCalibratedSwaptionVols", Enumerable.Range(0, n).Select(r.TimeDependentCalibratedSwaptionVols).ToArray());
        check.Output(
            c,
            "timeDependentUnCalibratedSwaptionVols",
            Enumerable.Range(0, n).Select(calibration.TimeDependentUnCalibratedSwaptionVols).ToArray());
        check.Output(c, "displacements", calibration.Displacements);
        if (c.Expected.TryGetProperty("alpha", out _))
        {
            check.Output(c, "alpha", r.Alpha);
        }
    }
}
