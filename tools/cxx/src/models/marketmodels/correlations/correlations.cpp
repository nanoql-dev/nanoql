// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/correlations/: exponentialCorrelations, the time-homogeneous forward correlation and the
// coterminal swap rates' correlation from the forwards', which the wheel doesn't expose.
//
// "exponentialCorrelations" takes (rateTimes, longTermCorr, beta, gamma, t): the matrix.
// "TimeHomogeneousForwardCorrelation" takes (fwdCorrelation, rateTimes): {"times", "rateTimes", "numberOfRates",
// "correlations"}. "TimeHomogeneousForwardCorrelation::evolvedMatrices" takes (fwdCorrelation), with at least its rows
// less one columns: the matrices. "CotSwapFromFwdCorrelation" takes (correlation, rateTimes, forwards, displacement):
// the correlation spec's (marketmodelspec.hpp) on an LMMCurveState of the rate times set on the forwards, its reads as
// "TimeHomogeneousForwardCorrelation"'s.

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/models/marketmodels/correlations/cotswapfromfwdcorrelation.hpp>
#include <ql/models/marketmodels/correlations/expcorrelations.hpp>
#include <ql/models/marketmodels/curvestates/lmmcurvestate.hpp>
#include <ql/models/marketmodels/correlations/timehomogeneousforwardcorrelation.hpp>

#include <cstdint>
#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        json::value matrices(const std::vector<Matrix>& ms) {
            json::array a;
            for (const Matrix& m : ms)
                a.emplace_back(result(m));
            return a;
        }

        json::value correlationReads(const PiecewiseConstantCorrelation& c) {
            json::object out;
            out["times"] = result(c.times());
            out["rateTimes"] = result(c.rateTimes());
            out["numberOfRates"] = static_cast<std::int64_t>(c.numberOfRates());
            out["correlations"] = matrices(c.correlations());
            return json::value(out);
        }

        const Register unit({
            {"exponentialCorrelations",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Vec rateTimes = reals(a[0]);
                 Real longTermCorr = real(a[1]), beta = real(a[2]), gamma = real(a[3]), t = real(a[4]);
                 return [=] { return result(exponentialCorrelations(rateTimes, longTermCorr, beta, gamma, t)); };
             }},
            {"TimeHomogeneousForwardCorrelation",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Mat fwd = matrix(a[0]);
                 Vec rateTimes = reals(a[1]);
                 return [=] { return correlationReads(TimeHomogeneousForwardCorrelation(makeMatrix(fwd), rateTimes)); };
             }},
            {"CotSwapFromFwdCorrelation",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 CorrelationSpec spec = correlation(a[0]);
                 Vec rateTimes = reals(a[1]), forwards = reals(a[2]);
                 Real displacement = real(a[3]);
                 return [=] {
                     LMMCurveState cs(rateTimes);
                     cs.setOnForwardRates(forwards);
                     return correlationReads(CotSwapFromFwdCorrelation(makeCorrelation(spec), cs, displacement));
                 };
             }},
            {"TimeHomogeneousForwardCorrelation::evolvedMatrices",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Mat fwd = matrix(a[0]);
                 if (!fwd.empty() && fwd.front().size() + 1 < fwd.size())
                     throw InvalidParams("QuantLib reads past a matrix with fewer columns than its rows less one");
                 return [=] { return matrices(TimeHomogeneousForwardCorrelation::evolvedMatrices(makeMatrix(fwd))); };
             }},
        });

    }

}
