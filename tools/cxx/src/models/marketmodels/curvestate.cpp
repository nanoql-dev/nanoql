// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/curvestate.hpp, .cpp and curvestates/: the curve states' reads and the conversions, which the
// wheel doesn't expose but for LMMCurveState's.
//
// "CurveState.reads" takes (kind, rateTimes, values, firstValidIndex, stateSpanning, spans): a curve state of the kind
// ("LMMForwards" or "LMMDiscountRatios", an LMMCurveState set on forwards or discount ratios; "Coterminal", a
// CoterminalSwapCurveState set on coterminal swap rates; "CMSwap", a CMSwapCurveState of stateSpanning forwards set on
// its swap rates), set on the values from the index, and its reads (marketmodelspec.hpp's stateReads) with "swapRates",
// swapRate(begin, end) for every begin from the index and end after it.
//
// "forwardsFromDiscountRatios" takes (firstValidIndex, ds, taus, fwds): the forwards written over fwds.
// "coterminalFromDiscountRatios" takes (firstValidIndex, ds, taus, rates, annuities) and
// "constantMaturityFromDiscountRatios" (spanningForwards, firstValidIndex, ds, taus, rates, annuities): the rates and
// annuities written over the ones given.

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/models/marketmodels/curvestate.hpp>
#include <ql/models/marketmodels/curvestates/cmswapcurvestate.hpp>
#include <ql/models/marketmodels/curvestates/coterminalswapcurvestate.hpp>
#include <ql/models/marketmodels/curvestates/lmmcurvestate.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        json::value reads(const CurveState& cs, Size first, const std::vector<Size>& spans) {
            json::object out = stateReads(cs, first, spans).as_object();
            json::array swapRates;
            for (Size b = first; b < cs.numberOfRates(); ++b) {
                json::array row;
                for (Size e = b + 1; e <= cs.numberOfRates(); ++e)
                    row.emplace_back(cs.swapRate(b, e));
                swapRates.emplace_back(row);
            }
            out["swapRates"] = swapRates;
            return out;
        }

        const Register unit({
            {"CurveState.reads",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 std::string kind = text(a[0]);
                 Vec rateTimes = reals(a[1]), values = reals(a[2]);
                 Size first = size(a[3]), stateSpanning = size(a[4]);
                 std::vector<Size> spans = sizes(a[5]);
                 if (kind != "LMMForwards" && kind != "LMMDiscountRatios" && kind != "Coterminal" && kind != "CMSwap")
                     throw InvalidParams("unknown curve state kind " + kind);
                 return [=]() -> json::value {
                     if (kind == "LMMForwards" || kind == "LMMDiscountRatios") {
                         LMMCurveState cs(rateTimes);
                         if (kind == "LMMForwards")
                             cs.setOnForwardRates(values, first);
                         else
                             cs.setOnDiscountRatios(values, first);
                         return reads(cs, first, spans);
                     }
                     if (kind == "Coterminal") {
                         CoterminalSwapCurveState cs(rateTimes);
                         cs.setOnCoterminalSwapRates(values, first);
                         return reads(cs, first, spans);
                     }
                     CMSwapCurveState cs(rateTimes, stateSpanning);
                     cs.setOnCMSwapRates(values, first);
                     return reads(cs, first, spans);
                 };
             }},
            {"forwardsFromDiscountRatios",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Size first = size(a[0]);
                 Vec ds = reals(a[1]), taus = reals(a[2]), fwds = reals(a[3]);
                 return [=] {
                     std::vector<Rate> out(fwds);
                     forwardsFromDiscountRatios(first, ds, taus, out);
                     return result(out);
                 };
             }},
            {"coterminalFromDiscountRatios",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Size first = size(a[0]);
                 Vec ds = reals(a[1]), taus = reals(a[2]), rates = reals(a[3]), annuities = reals(a[4]);
                 return [=] {
                     std::vector<Rate> r(rates);
                     std::vector<Real> an(annuities);
                     coterminalFromDiscountRatios(first, ds, taus, r, an);
                     json::object out;
                     out["rates"] = result(r);
                     out["annuities"] = result(an);
                     return json::value(out);
                 };
             }},
            {"constantMaturityFromDiscountRatios",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Size span = size(a[0]), first = size(a[1]);
                 Vec ds = reals(a[2]), taus = reals(a[3]), rates = reals(a[4]), annuities = reals(a[5]);
                 return [=] {
                     std::vector<Rate> r(rates);
                     std::vector<Real> an(annuities);
                     constantMaturityFromDiscountRatios(span, first, ds, taus, r, an);
                     json::object out;
                     out["rates"] = result(r);
                     out["annuities"] = result(an);
                     return json::value(out);
                 };
             }},
        });

    }

}
