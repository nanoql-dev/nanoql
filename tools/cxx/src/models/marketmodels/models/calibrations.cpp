// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/models/ctsmmcapletcalibration.hpp, .cpp, capletcoterminalalphacalibration.hpp, .cpp,
// capletcoterminalmaxhomogeneity.hpp, .cpp, capletcoterminalswaptioncalibration.hpp, .cpp, alphaform.hpp,
// alphaformconcrete.hpp, .cpp, alphafinder.hpp, .cpp and pseudorootfacade.hpp, .cpp, which the wheel doesn't expose.
//
// A calibration spec is {"rateTimes", "forwards", "longTermCorr", "beta", "abcd", "capletVols", "displacement"}: the
// evolution EvolutionDescription(rateTimes), an LMMCurveState on the forwards, the swap rates' correlation
// CotSwapFromFwdCorrelation(ExponentialForwardCorrelation(rateTimes, longTermCorr, beta), state, displacement), and
// each swap rate's PiecewiseConstantAbcdVariance(a, b, c, d, i, rateTimes), QuantLib's caplet calibration cases' setup.
// An alpha form is "LinearHyperbolic" or "InverseLinear" on the rate times.
//
// "CTSMMCapletOriginalCalibration.calibrate" takes (spec, alpha, lowestRoot, useFullApprox, factors, maxIterations,
// tolerance, innerMaxIterations, innerTolerance); "CTSMMCapletAlphaFormCalibration.calibrate" (spec, alphaInitial,
// alphaMax, alphaMin, maximizeHomogeneity, form, and the five); "CTSMMCapletMaxHomogeneityCalibration.calibrate" (spec,
// caplet0Swaption1Priority, and the five). Each returns {"converged" (calibrate's value), "failures", "capletRmsError",
// "capletMaxError", "swaptionRmsError", "swaptionMaxError", "mktCapletVols", "mdlCapletVols", "mktSwaptionVols",
// "mdlSwaptionVols", "swapPseudoRoots", "timeDependentCalibratedSwaptionVols", "timeDependentUnCalibratedSwaptionVols",
// "displacements"}, the alpha form's "alpha" too. The deformation size is left out: the initializer's 987654321 for the
// alpha form and the original, null in nanoql, and QuantLib's index for max homogeneity
// (tests/NanoQL.Tests/Divergences/CTSMMCapletMaxHomogeneityDeformationTests.cs).
//
// The static functions: "CTSMMCapletOriginalCalibration::calibrationFunction" takes (spec, alpha, lowestRoot,
// useFullApprox, factors) and returns {"failures", "swapCovariancePseudoRoots"};
// "CTSMMCapletAlphaFormCalibration::capletAlphaFormCalibration" (spec, alphaInitial, alphaMax, alphaMin,
// maximizeHomogeneity, form, factors, steps, tolerance) and adds "alpha", "a" and "b";
// "CTSMMCapletMaxHomogeneityCalibration::capletMaxHomogeneityCalibration" (spec, caplet0Swaption1Priority, factors,
// maxIterations, tolerance) and adds "totalSwaptionError". The periodic calibration (capletcoterminalperiodic.hpp) has
// no call: each of its passes reads FwdPeriodAdapter's pseudo-roots, on QuantLib's Y matrix. "PseudoRootFacade" takes
// (pseudoRoots, rateTimes, initialRates, displacements) and returns the model's reads (modelResults).

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/math/matrix.hpp>
#include <ql/models/marketmodels/correlations/cotswapfromfwdcorrelation.hpp>
#include <ql/models/marketmodels/correlations/expcorrelations.hpp>
#include <ql/models/marketmodels/curvestates/lmmcurvestate.hpp>
#include <ql/models/marketmodels/evolutiondescription.hpp>
#include <ql/models/marketmodels/models/alphaformconcrete.hpp>
#include <ql/models/marketmodels/models/capletcoterminalalphacalibration.hpp>
#include <ql/models/marketmodels/models/capletcoterminalmaxhomogeneity.hpp>
#include <ql/models/marketmodels/models/capletcoterminalswaptioncalibration.hpp>
#include <ql/models/marketmodels/models/piecewiseconstantabcdvariance.hpp>
#include <ql/models/marketmodels/models/pseudorootfacade.hpp>

#include <cstdint>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        struct CalibrationSpec {
            Vec rateTimes, forwards, abcd, capletVols;
            Real longTermCorr = 0.0, beta = 0.0, displacement = 0.0;
        };

        CalibrationSpec calibration(const json::value& v) {
            const std::string what = "a calibration spec";
            const json::object& o = object(v, what);
            onlyKeys(o, {"rateTimes", "forwards", "longTermCorr", "beta", "abcd", "capletVols", "displacement"}, what);
            CalibrationSpec c;
            c.rateTimes = reals(key(o, "rateTimes", what));
            c.forwards = reals(key(o, "forwards", what));
            c.longTermCorr = real(key(o, "longTermCorr", what));
            c.beta = real(key(o, "beta", what));
            c.abcd = reals(key(o, "abcd", what));
            c.capletVols = reals(key(o, "capletVols", what));
            c.displacement = real(key(o, "displacement", what));
            QL_REQUIRE(c.abcd.size() == 4, what << "'s abcd must hold a, b, c and d");
            return c;
        }

        struct Inputs {
            EvolutionDescription evolution;
            ext::shared_ptr<LMMCurveState> cs;
            ext::shared_ptr<PiecewiseConstantCorrelation> corr;
            std::vector<ext::shared_ptr<PiecewiseConstantVariance>> variances;
        };

        Inputs inputs(const CalibrationSpec& c) {
            Inputs in{EvolutionDescription(c.rateTimes), ext::make_shared<LMMCurveState>(c.rateTimes), nullptr, {}};
            in.cs->setOnForwardRates(c.forwards);
            ext::shared_ptr<PiecewiseConstantCorrelation> fwdCorr =
                ext::make_shared<ExponentialForwardCorrelation>(c.rateTimes, c.longTermCorr, c.beta);
            in.corr = ext::make_shared<CotSwapFromFwdCorrelation>(fwdCorr, *in.cs, c.displacement);
            for (Size i = 0; i + 1 < c.rateTimes.size(); ++i)
                in.variances.push_back(ext::make_shared<PiecewiseConstantAbcdVariance>(
                    c.abcd[0], c.abcd[1], c.abcd[2], c.abcd[3], i, c.rateTimes));
            return in;
        }

        ext::shared_ptr<AlphaForm> alphaForm(const std::string& type, const Vec& rateTimes) {
            if (type == "LinearHyperbolic")
                return ext::make_shared<AlphaFormLinearHyperbolic>(rateTimes);
            if (type == "InverseLinear")
                return ext::make_shared<AlphaFormInverseLinear>(rateTimes);
            QL_FAIL("unknown alpha form " << type);
        }

        json::value roots(const std::vector<Matrix>& m) {
            json::array out;
            for (const Matrix& x : m)
                out.emplace_back(result(x));
            return out;
        }

        struct Settings {
            Size factors = 0, maxIterations = 0, innerMaxIterations = 0;
            Real tolerance = 0.0, innerTolerance = 0.0;
        };

        Settings settings(const Args& a, std::size_t from) {
            return {size(a[from]), size(a[from + 1]), size(a[from + 3]), real(a[from + 2]), real(a[from + 4])};
        }

        json::value calibrationResults(CTSMMCapletCalibration& calibrator, const Settings& s, Size numberOfRates) {
            const bool converged = calibrator.calibrate(static_cast<Natural>(s.factors),
                                                        static_cast<Natural>(s.maxIterations), s.tolerance,
                                                        static_cast<Natural>(s.innerMaxIterations), s.innerTolerance);
            json::object out;
            out["converged"] = converged;
            out["failures"] = static_cast<std::int64_t>(calibrator.failures());
            out["capletRmsError"] = calibrator.capletRmsError();
            out["capletMaxError"] = calibrator.capletMaxError();
            out["swaptionRmsError"] = calibrator.swaptionRmsError();
            out["swaptionMaxError"] = calibrator.swaptionMaxError();
            out["mktCapletVols"] = result(calibrator.mktCapletVols());
            out["mdlCapletVols"] = result(calibrator.mdlCapletVols());
            out["mktSwaptionVols"] = result(calibrator.mktSwaptionVols());
            out["mdlSwaptionVols"] = result(calibrator.mdlSwaptionVols());
            out["swapPseudoRoots"] = roots(calibrator.swapPseudoRoots());
            json::array calibrated, uncalibrated;
            for (Size i = 0; i < numberOfRates; ++i) {
                calibrated.emplace_back(result(calibrator.timeDependentCalibratedSwaptionVols(i)));
                uncalibrated.emplace_back(result(calibrator.timeDependentUnCalibratedSwaptionVols(i)));
            }
            out["timeDependentCalibratedSwaptionVols"] = calibrated;
            out["timeDependentUnCalibratedSwaptionVols"] = uncalibrated;
            out["displacements"] = result(calibrator.displacements());
            return out;
        }

        const Register unit({
            {"CTSMMCapletOriginalCalibration.calibrate",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 const CalibrationSpec c = calibration(a[0]);
                 const Vec alpha = reals(a[1]);
                 const bool lowestRoot = boolean(a[2]), useFullApprox = boolean(a[3]);
                 const Settings s = settings(a, 4);
                 return [=] {
                     Inputs in = inputs(c);
                     CTSMMCapletOriginalCalibration calibrator(in.evolution, in.corr, in.variances, c.capletVols, in.cs,
                                                               c.displacement, alpha, lowestRoot, useFullApprox);
                     return calibrationResults(calibrator, s, in.evolution.numberOfRates());
                 };
             }},
            {"CTSMMCapletAlphaFormCalibration.calibrate",
             [](const Args& a) -> Call {
                 arity(a, 11);
                 const CalibrationSpec c = calibration(a[0]);
                 const Vec alphaInitial = reals(a[1]), alphaMax = reals(a[2]), alphaMin = reals(a[3]);
                 const bool maximizeHomogeneity = boolean(a[4]);
                 const std::string form = text(a[5]);
                 const Settings s = settings(a, 6);
                 return [=] {
                     Inputs in = inputs(c);
                     CTSMMCapletAlphaFormCalibration calibrator(in.evolution, in.corr, in.variances, c.capletVols,
                                                                in.cs, c.displacement, alphaInitial, alphaMax, alphaMin,
                                                                maximizeHomogeneity, alphaForm(form, c.rateTimes));
                     json::value out = calibrationResults(calibrator, s, in.evolution.numberOfRates());
                     out.as_object()["alpha"] = result(calibrator.alpha());
                     return out;
                 };
             }},
            {"CTSMMCapletMaxHomogeneityCalibration.calibrate",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 const CalibrationSpec c = calibration(a[0]);
                 const Real priority = real(a[1]);
                 const Settings s = settings(a, 2);
                 return [=] {
                     Inputs in = inputs(c);
                     CTSMMCapletMaxHomogeneityCalibration calibrator(in.evolution, in.corr, in.variances, c.capletVols,
                                                                     in.cs, c.displacement, priority);
                     return calibrationResults(calibrator, s, in.evolution.numberOfRates());
                 };
             }},
            {"CTSMMCapletOriginalCalibration::calibrationFunction",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 const CalibrationSpec c = calibration(a[0]);
                 const Vec alpha = reals(a[1]);
                 const bool lowestRoot = boolean(a[2]), useFullApprox = boolean(a[3]);
                 const Size factors = size(a[4]);
                 return [=] {
                     Inputs in = inputs(c);
                     std::vector<Matrix> pseudoRoots;
                     const Natural failures = CTSMMCapletOriginalCalibration::calibrationFunction(
                         in.evolution, *in.corr, in.variances, c.capletVols, *in.cs, c.displacement, alpha, lowestRoot,
                         useFullApprox, factors, pseudoRoots);
                     json::object out;
                     out["failures"] = static_cast<std::int64_t>(failures);
                     out["swapCovariancePseudoRoots"] = roots(pseudoRoots);
                     return json::value(out);
                 };
             }},
            {"CTSMMCapletAlphaFormCalibration::capletAlphaFormCalibration",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 const CalibrationSpec c = calibration(a[0]);
                 const Vec alphaInitial = reals(a[1]), alphaMax = reals(a[2]), alphaMin = reals(a[3]);
                 const bool maximizeHomogeneity = boolean(a[4]);
                 const std::string form = text(a[5]);
                 const Size factors = size(a[6]);
                 const Integer steps = integer(a[7]);
                 const Real tolerance = real(a[8]);
                 return [=] {
                     Inputs in = inputs(c);
                     std::vector<Real> alpha, aa, b;
                     std::vector<Matrix> pseudoRoots;
                     const Natural failures = CTSMMCapletAlphaFormCalibration::capletAlphaFormCalibration(
                         in.evolution, *in.corr, in.variances, c.capletVols, *in.cs, c.displacement, alphaInitial,
                         alphaMax, alphaMin, maximizeHomogeneity, alphaForm(form, c.rateTimes), factors, steps,
                         tolerance, alpha, aa, b, pseudoRoots);
                     json::object out;
                     out["failures"] = static_cast<std::int64_t>(failures);
                     out["alpha"] = result(alpha);
                     out["a"] = result(aa);
                     out["b"] = result(b);
                     out["swapCovariancePseudoRoots"] = roots(pseudoRoots);
                     return json::value(out);
                 };
             }},
            {"CTSMMCapletMaxHomogeneityCalibration::capletMaxHomogeneityCalibration",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 const CalibrationSpec c = calibration(a[0]);
                 const Real priority = real(a[1]);
                 const Size factors = size(a[2]), maxIterations = size(a[3]);
                 const Real tolerance = real(a[4]);
                 return [=] {
                     Inputs in = inputs(c);
                     Real deformationSize = 0.0, totalSwaptionError = 0.0;
                     std::vector<Matrix> pseudoRoots;
                     const Natural failures = CTSMMCapletMaxHomogeneityCalibration::capletMaxHomogeneityCalibration(
                         in.evolution, *in.corr, in.variances, c.capletVols, *in.cs, c.displacement, priority, factors,
                         maxIterations, tolerance, deformationSize, totalSwaptionError, pseudoRoots);
                     json::object out;
                     out["failures"] = static_cast<std::int64_t>(failures);
                     out["totalSwaptionError"] = totalSwaptionError;
                     out["swapCovariancePseudoRoots"] = roots(pseudoRoots);
                     return json::value(out);
                 };
             }},
            {"PseudoRootFacade",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 std::vector<Matrix> pseudoRoots;
                 for (const json::value& m : a[0].as_array())
                     pseudoRoots.push_back(makeMatrix(matrix(m)));
                 const Vec rateTimes = reals(a[1]), initialRates = reals(a[2]), displacements = reals(a[3]);
                 return [=] {
                     return modelResults(PseudoRootFacade(pseudoRoots, rateTimes, initialRates, displacements));
                 };
             }},
        });

    }

}
