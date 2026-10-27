// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/driftcomputation/: the normal LIBOR, coterminal swap and constant-maturity swap drift
// calculators, which the wheel doesn't expose.
//
// "LMMNormalDriftCalculator.compute" takes (pseudo, taus, numeraire, alive, forwards, form): the drifts by compute,
// computePlain or computeReduced (form), over a vector of zeros, from the first alive rate. "SMMDriftCalculator.compute"
// takes (pseudo, displacements, rateTimes, numeraire, alive, swapRates): the drifts of a CoterminalSwapCurveState on the
// rate times, set on the coterminal swap rates from index 0. "CMSMMDriftCalculator.compute" takes (pseudo,
// displacements, rateTimes, numeraire, alive, spanningFwds, swapRates): the drifts of a CMSwapCurveState of the same
// spanning set on its swap rates from index 0. Each computes twice, on one calculator, and returns
// {"drifts", "again"}: the calculators keep scratch between calls.

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/models/marketmodels/curvestates/cmswapcurvestate.hpp>
#include <ql/models/marketmodels/curvestates/coterminalswapcurvestate.hpp>
#include <ql/models/marketmodels/driftcomputation/cmsmmdriftcalculator.hpp>
#include <ql/models/marketmodels/driftcomputation/lmmnormaldriftcalculator.hpp>
#include <ql/models/marketmodels/driftcomputation/smmdriftcalculator.hpp>
#include <ql/models/marketmodels/utilities.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        json::value twice(const std::vector<Real>& first, const std::vector<Real>& second) {
            json::object out;
            out["drifts"] = result(first);
            out["again"] = result(second);
            return out;
        }

        std::vector<Time> tausOf(const Vec& rateTimes) {
            std::vector<Time> taus;
            checkIncreasingTimesAndCalculateTaus(rateTimes, taus);
            return taus;
        }

        const Register unit({
            {"LMMNormalDriftCalculator.compute",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Mat pseudo = matrix(a[0]);
                 Vec taus = reals(a[1]);
                 Size numeraire = size(a[2]), alive = size(a[3]);
                 Vec forwards = reals(a[4]);
                 std::string form = text(a[5]);
                 if (form != "compute" && form != "computePlain" && form != "computeReduced")
                     throw InvalidParams("unknown form " + form);
                 if (forwards.size() != taus.size())
                     throw InvalidParams("one forward per rate");
                 return [=] {
                     LMMNormalDriftCalculator c(makeMatrix(pseudo), taus, numeraire, alive);
                     std::vector<Real> d1(taus.size(), 0.0), d2(taus.size(), 0.0);
                     for (std::vector<Real>* d : {&d1, &d2}) {
                         if (form == "compute")
                             c.compute(forwards, *d);
                         else if (form == "computePlain")
                             c.computePlain(forwards, *d);
                         else
                             c.computeReduced(forwards, *d);
                     }
                     return twice(d1, d2);
                 };
             }},
            {"SMMDriftCalculator.compute",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Mat pseudo = matrix(a[0]);
                 Vec displacements = reals(a[1]), rateTimes = reals(a[2]);
                 Size numeraire = size(a[3]), alive = size(a[4]);
                 Vec swapRates = reals(a[5]);
                 return [=] {
                     std::vector<Time> taus = tausOf(rateTimes);
                     SMMDriftCalculator c(makeMatrix(pseudo), displacements, taus, numeraire, alive);
                     CoterminalSwapCurveState cs(rateTimes);
                     cs.setOnCoterminalSwapRates(swapRates);
                     std::vector<Real> d1(taus.size(), 0.0), d2(taus.size(), 0.0);
                     c.compute(cs, d1);
                     c.compute(cs, d2);
                     return twice(d1, d2);
                 };
             }},
            {"CMSMMDriftCalculator.compute",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 Mat pseudo = matrix(a[0]);
                 Vec displacements = reals(a[1]), rateTimes = reals(a[2]);
                 Size numeraire = size(a[3]), alive = size(a[4]), span = size(a[5]);
                 Vec swapRates = reals(a[6]);
                 if (span < 1)
                     throw InvalidParams("at least one spanning forward: QuantLib reads uninitialized entries at 0");
                 return [=] {
                     std::vector<Time> taus = tausOf(rateTimes);
                     CMSMMDriftCalculator c(makeMatrix(pseudo), displacements, taus, numeraire, alive, span);
                     CMSwapCurveState cs(rateTimes, span);
                     cs.setOnCMSwapRates(swapRates);
                     std::vector<Real> d1(taus.size(), 0.0), d2(taus.size(), 0.0);
                     c.compute(cs, d1);
                     c.compute(cs, d2);
                     return twice(d1, d2);
                 };
             }},
        });

    }

}
