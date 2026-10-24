// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/stickyratchet.hpp: the double sticky/ratchet payoff and its six named forms, which the wheel doesn't
// bind, each built inside the call:
//
//     StickyRatchetPayoff  (class, arguments, forwards): [name(), description(), the payoff at each forward]
//
// The class is "DoubleStickyRatchetPayoff" (type1, type2, gearing1, gearing2, gearing3, spread1, spread2, spread3,
// initialValue1, initialValue2, accrualFactor), "RatchetPayoff" or "StickyPayoff" (gearing1, gearing2, spread1,
// spread2, initialValue, accrualFactor), or "RatchetMaxPayoff", "RatchetMinPayoff", "StickyMaxPayoff" or
// "StickyMinPayoff" (gearing1, gearing2, gearing3, spread1, spread2, spread3, initialValue1, initialValue2,
// accrualFactor), the arguments in the constructor's order. A payoff QuantLib refuses at a forward is null there.

#include "convert.hpp"

#include <ql/instruments/stickyratchet.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        ext::shared_ptr<DoubleStickyRatchetPayoff> payoff(const std::string& cls, const std::vector<Real>& x) {
            auto need = [&](std::size_t n) {
                if (x.size() != n)
                    throw InvalidParams(cls + " takes " + std::to_string(n) + " arguments, got " +
                                        std::to_string(x.size()));
            };
            if (cls == "DoubleStickyRatchetPayoff") {
                need(11);
                return ext::make_shared<DoubleStickyRatchetPayoff>(x[0], x[1], x[2], x[3], x[4], x[5], x[6], x[7],
                                                                    x[8], x[9], x[10]);
            }
            if (cls == "RatchetPayoff") {
                need(6);
                return ext::make_shared<RatchetPayoff>(x[0], x[1], x[2], x[3], x[4], x[5]);
            }
            if (cls == "StickyPayoff") {
                need(6);
                return ext::make_shared<StickyPayoff>(x[0], x[1], x[2], x[3], x[4], x[5]);
            }
            if (cls == "RatchetMaxPayoff") {
                need(9);
                return ext::make_shared<RatchetMaxPayoff>(x[0], x[1], x[2], x[3], x[4], x[5], x[6], x[7], x[8]);
            }
            if (cls == "RatchetMinPayoff") {
                need(9);
                return ext::make_shared<RatchetMinPayoff>(x[0], x[1], x[2], x[3], x[4], x[5], x[6], x[7], x[8]);
            }
            if (cls == "StickyMaxPayoff") {
                need(9);
                return ext::make_shared<StickyMaxPayoff>(x[0], x[1], x[2], x[3], x[4], x[5], x[6], x[7], x[8]);
            }
            if (cls == "StickyMinPayoff") {
                need(9);
                return ext::make_shared<StickyMinPayoff>(x[0], x[1], x[2], x[3], x[4], x[5], x[6], x[7], x[8]);
            }
            throw InvalidParams("unknown sticky/ratchet payoff " + cls);
        }

        const Register unit({
            {"StickyRatchetPayoff",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 std::string cls = text(a[0]);
                 std::vector<Real> args = reals(a[1]), forwards = reals(a[2]);
                 return [=] {
                     auto p = payoff(cls, args);
                     json::array values;
                     for (Real f : forwards) {
                         try {
                             values.emplace_back((*p)(f));
                         } catch (const Error&) {
                             values.emplace_back(nullptr);
                         }
                     }
                     json::array out;
                     out.emplace_back(p->name());
                     out.emplace_back(p->description());
                     out.emplace_back(values);
                     return json::value(out);
                 };
             }},
        });

    }

}
