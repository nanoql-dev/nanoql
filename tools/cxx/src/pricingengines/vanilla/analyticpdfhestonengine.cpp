// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/vanilla/analyticpdfhestonengine.hpp, .cpp: the engine's density and distribution members, which
// the wheel doesn't expose. "AnalyticPDFHestonEngine::members" takes (process, eps, maxIterations, xs, ss, ts):
// AnalyticPDFHestonEngine on the HestonModel of models/equity/hestonspec.cpp's process, and returns {"pv" (each log
// spot x at each t), "cdf" (each spot s at each t)}. The evaluation date is the case's Settings.

#include "convert.hpp"
#include "models/equity/hestonspec.hpp"

#include <ql/models/equity/hestonmodel.hpp>
#include <ql/pricingengines/vanilla/analyticpdfhestonengine.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"AnalyticPDFHestonEngine::members",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 equity::ProcessSpec p = equity::process(a[0]);
                 Real eps = real(a[1]);
                 Integer maxIterations = integer(a[2]);
                 if (maxIterations < 0)
                     throw InvalidParams("a maximum must be non-negative");
                 std::vector<Real> xs = reals(a[3]), ss = reals(a[4]), ts = reals(a[5]);
                 return [=] {
                     AnalyticPDFHestonEngine engine(ext::make_shared<HestonModel>(equity::makeProcess(p)), eps,
                                                    static_cast<Size>(maxIterations));
                     json::array pv, cdf;
                     for (Real x : xs)
                         for (Real t : ts)
                             pv.push_back(engine.Pv(x, t));
                     for (Real s : ss)
                         for (Real t : ts)
                             cdf.push_back(engine.cdf(s, t));
                     json::object out;
                     out["pv"] = pv;
                     out["cdf"] = cdf;
                     return json::value(out);
                 };
             }},
        });

    }

}
