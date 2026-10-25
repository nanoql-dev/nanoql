// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/vanilla/coshestonengine.hpp, .cpp: the engine's members, which the wheel doesn't expose.
// "COSHestonEngine::members" takes (process, L, N, us, ts): COSHestonEngine on the HestonModel of
// models/equity/hestonspec.cpp's process, and returns {"chF" (each u at each t, [real, imaginary]), "c1", "c2", "c3",
// "c4", "mu", "var", "skew", "kurtosis" (each at each t)}. The evaluation date is the case's Settings.

#include "complexvalue.hpp"
#include "convert.hpp"
#include "models/equity/hestonspec.hpp"

#include <ql/models/equity/hestonmodel.hpp>
#include <ql/pricingengines/vanilla/coshestonengine.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"COSHestonEngine::members",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 equity::ProcessSpec p = equity::process(a[0]);
                 Real l = real(a[1]);
                 Integer n = integer(a[2]);
                 if (n < 0)
                     throw InvalidParams("N must be non-negative");
                 std::vector<Real> us = reals(a[3]), ts = reals(a[4]);
                 return [=] {
                     COSHestonEngine engine(ext::make_shared<HestonModel>(equity::makeProcess(p)), l,
                                            static_cast<Size>(n));
                     json::array chF, c1, c2, c3, c4, mu, var, skew, kurtosis;
                     for (Real u : us)
                         for (Real t : ts)
                             chF.push_back(complexResult(engine.chF(u, t)));
                     for (Real t : ts) {
                         c1.push_back(engine.c1(t));
                         c2.push_back(engine.c2(t));
                         c3.push_back(engine.c3(t));
                         c4.push_back(engine.c4(t));
                         mu.push_back(engine.mu(t));
                         var.push_back(engine.var(t));
                         skew.push_back(engine.skew(t));
                         kurtosis.push_back(engine.kurtosis(t));
                     }
                     json::object out;
                     out["chF"] = chF;
                     out["c1"] = c1;
                     out["c2"] = c2;
                     out["c3"] = c3;
                     out["c4"] = c4;
                     out["mu"] = mu;
                     out["var"] = var;
                     out["skew"] = skew;
                     out["kurtosis"] = kurtosis;
                     return json::value(out);
                 };
             }},
        });

    }

}
