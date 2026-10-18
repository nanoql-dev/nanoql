// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/yield/ratehelpers.hpp: what the wheel can't reach of DepositRateHelper and SwapRateHelper, each
// built inside the call from a spec of termstructures/yield/ratehelperspec.hpp under the settings already set. A
// helper's implied quote and quote error against a given curve, the helper bound to it by setTermStructure, which the
// wheel doesn't bind (a flat forward curve, or an interpolated discount curve, built inside the call and alive while
// the helper is read); the swap helper's forwardStart(), as [length, units]; and the pillar date of a helper as built,
// whose refusal is the helper's construction refused.

#include "convert.hpp"
#include "termstructures/yield/ratehelperspec.hpp"

#include <ql/termstructures/yield/ratehelpers.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using termstructures::BoundCurveSpec;
        using termstructures::RateHelperSpec;

        // The helper bound to the curve; the curve outlives the helper's use in the call.
        template <class F>
        json::value bound(const RateHelperSpec& h, const BoundCurveSpec& c, F read) {
            ext::shared_ptr<YieldTermStructure> curve = termstructures::makeBoundCurve(c);
            ext::shared_ptr<RateHelper> helper = termstructures::makeRateHelper(h);
            helper->setTermStructure(curve.get());
            return json::value(read(*helper));
        }

        const Register unit({
            {"RateHelper::pillarDate",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 RateHelperSpec h = termstructures::rateHelper(a[0]);
                 return [=] { return result(termstructures::makeRateHelper(h)->pillarDate()); };
             }},
            {"RateHelper::impliedQuote",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 RateHelperSpec h = termstructures::rateHelper(a[0]);
                 BoundCurveSpec c = termstructures::boundCurve(a[1]);
                 return [=] { return bound(h, c, [](const RateHelper& x) { return x.impliedQuote(); }); };
             }},
            {"RateHelper::quoteError",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 RateHelperSpec h = termstructures::rateHelper(a[0]);
                 BoundCurveSpec c = termstructures::boundCurve(a[1]);
                 return [=] { return bound(h, c, [](const RateHelper& x) { return x.quoteError(); }); };
             }},
            {"SwapRateHelper::forwardStart",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 RateHelperSpec h = termstructures::rateHelper(a[0]);
                 if (h.kind != "SwapRateHelper")
                     throw InvalidParams("forwardStart is a swap helper's");
                 return [=] {
                     auto swap = ext::dynamic_pointer_cast<SwapRateHelper>(termstructures::makeRateHelper(h));
                     const Period& p = swap->forwardStart();
                     return json::value(json::array{p.length(), static_cast<int>(p.units())});
                 };
             }},
        });

    }

}
