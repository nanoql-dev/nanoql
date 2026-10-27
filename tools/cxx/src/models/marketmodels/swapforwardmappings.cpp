// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/swapforwardmappings.hpp, .cpp, which the wheel doesn't expose.
//
// "SwapForwardMappings.all" takes (kind, rateTimes, values, stateSpanning, displacement, spans): on a curve state as
// curvestate.cpp's "CurveState.reads" builds it, from index 0, {"annuities" (annuity(start, end, numeraire) for every
// start below every end, the numeraire each of 0, the end and the last bond), "swapDerivatives" (swapDerivative(start,
// end, forward) for every start below every end, each forward), "coterminalJacobian", "coterminalZed",
// "coinitialJacobian", "coinitialZedUpper" (the coinitial Z matrix's rows from their diagonal on, the entries QuantLib
// scales), "cmJacobians" and "cmZeds" (at each spanning given)}.
// "SwapForwardMappings::swaptionImpliedVolatility" takes (model, startIndex, endIndex).

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/models/marketmodels/curvestates/cmswapcurvestate.hpp>
#include <ql/models/marketmodels/curvestates/coterminalswapcurvestate.hpp>
#include <ql/models/marketmodels/curvestates/lmmcurvestate.hpp>
#include <ql/models/marketmodels/swapforwardmappings.hpp>

#include <memory>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        json::value all(const CurveState& cs, Real d, const std::vector<Size>& spans) {
            Size n = cs.numberOfRates();
            json::array annuities, derivatives;
            for (Size s = 0; s < n; ++s)
                for (Size e = s + 1; e <= n; ++e) {
                    json::array an;
                    for (Size num : {Size(0), e, n})
                        an.emplace_back(SwapForwardMappings::annuity(cs, s, e, num));
                    annuities.emplace_back(an);
                    json::array der;
                    for (Size f = 0; f < n; ++f)
                        der.emplace_back(SwapForwardMappings::swapDerivative(cs, s, e, f));
                    derivatives.emplace_back(der);
                }
            Matrix coinitialZed = SwapForwardMappings::coinitialSwapZedMatrix(cs, d);
            json::array upper;
            for (Size i = 0; i < n; ++i) {
                json::array row;
                for (Size j = i; j < n; ++j)
                    row.emplace_back(coinitialZed[i][j]);
                upper.emplace_back(row);
            }
            json::array cmJ, cmZ;
            for (Size span : spans) {
                cmJ.emplace_back(result(SwapForwardMappings::cmSwapForwardJacobian(cs, span)));
                cmZ.emplace_back(result(SwapForwardMappings::cmSwapZedMatrix(cs, span, d)));
            }
            json::object out;
            out["annuities"] = annuities;
            out["swapDerivatives"] = derivatives;
            out["coterminalJacobian"] = result(SwapForwardMappings::coterminalSwapForwardJacobian(cs));
            out["coterminalZed"] = result(SwapForwardMappings::coterminalSwapZedMatrix(cs, d));
            out["coinitialJacobian"] = result(SwapForwardMappings::coinitialSwapForwardJacobian(cs));
            out["coinitialZedUpper"] = upper;
            out["cmJacobians"] = cmJ;
            out["cmZeds"] = cmZ;
            return out;
        }

        const Register unit({
            {"SwapForwardMappings.all",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 std::string kind = text(a[0]);
                 Vec rateTimes = reals(a[1]), values = reals(a[2]);
                 Size stateSpanning = size(a[3]);
                 Real d = real(a[4]);
                 std::vector<Size> spans = sizes(a[5]);
                 if (kind != "LMMForwards" && kind != "Coterminal" && kind != "CMSwap")
                     throw InvalidParams("unknown curve state kind " + kind);
                 return [=]() -> json::value {
                     if (kind == "LMMForwards") {
                         LMMCurveState cs(rateTimes);
                         cs.setOnForwardRates(values);
                         return all(cs, d, spans);
                     }
                     if (kind == "Coterminal") {
                         CoterminalSwapCurveState cs(rateTimes);
                         cs.setOnCoterminalSwapRates(values);
                         return all(cs, d, spans);
                     }
                     CMSwapCurveState cs(rateTimes, stateSpanning);
                     cs.setOnCMSwapRates(values);
                     return all(cs, d, spans);
                 };
             }},
            {"SwapForwardMappings::swaptionImpliedVolatility",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 ModelSpec m = model(a[0]);
                 Size start = size(a[1]), end = size(a[2]);
                 if (end > m.rateTimes.size() - 1)
                     throw InvalidParams("QuantLib reads past its Z matrix at an end past the rates");
                 return [=] {
                     return json::value(SwapForwardMappings::swaptionImpliedVolatility(*makeModel(m), start, end));
                 };
             }},
        });

    }

}
