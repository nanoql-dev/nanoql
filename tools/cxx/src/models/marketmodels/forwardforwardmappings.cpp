// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/forwardforwardmappings.hpp, .cpp's RestrictCurveState, which the wheel doesn't expose; its
// Jacobian and Y matrix, which nanoql fixes (the S-mm plan's MR3 (i)), are left to the divergence facts.
//
// "ForwardForwardMappings::RestrictCurveState" takes (rateTimes, forwards, multiplier, offset), the offset at most the
// rates: the state restricted from an LMMCurveState on the forwards, {"rateTimes", "forwardRates", "discountRatios"
// (each bond to the last)}.

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/models/marketmodels/curvestates/lmmcurvestate.hpp>
#include <ql/models/marketmodels/forwardforwardmappings.hpp>

#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        const Register unit({
            {"ForwardForwardMappings::RestrictCurveState",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Vec rateTimes = reals(a[0]), forwards = reals(a[1]);
                 Size multiplier = size(a[2]), offset = size(a[3]);
                 if (rateTimes.empty() || offset > rateTimes.size() - 1)
                     throw InvalidParams("QuantLib's unsigned count wraps at an offset past the rates");
                 return [=] {
                     LMMCurveState cs(rateTimes);
                     cs.setOnForwardRates(forwards);
                     LMMCurveState r = ForwardForwardMappings::RestrictCurveState(cs, multiplier, offset);
                     Size n = r.numberOfRates();
                     json::array ratios;
                     for (Size i = 0; i <= n; ++i)
                         ratios.emplace_back(r.discountRatio(i, n));
                     json::object out;
                     out["rateTimes"] = result(r.rateTimes());
                     out["forwardRates"] = result(r.forwardRates());
                     out["discountRatios"] = ratios;
                     return json::value(out);
                 };
             }},
        });

    }

}
