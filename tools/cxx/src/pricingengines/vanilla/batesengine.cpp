// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/vanilla/batesengine.hpp, .cpp: the det-jump and double-exponential Bates engines, and the engines'
// numbers of evaluations, which the wheel doesn't expose. "BatesEngine::results" takes (type, process, arguments,
// integration, optionType, strike, exerciseDate): type "BatesEngine" on a BatesModel, "BatesDetJumpEngine",
// "BatesDoubleExpEngine" or "BatesDoubleExpDetJumpEngine" on the model models/equity/batesmodel.cpp builds from the
// process and the arguments; the integration {"order"} for the Gauss-Laguerre constructor or {"relTolerance",
// "maxEvaluations"} for the Gauss-Lobatto one. It prices a European plain vanilla option and returns {"NPV",
// "numberOfEvaluations"}. The evaluation date is the case's Settings.

#include "convert.hpp"
#include "models/equity/hestonspec.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/vanillaoption.hpp>
#include <ql/models/equity/batesmodel.hpp>
#include <ql/pricingengines/vanilla/batesengine.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        Real argument(const std::vector<Real>& a, std::size_t i, Real fallback) {
            return i < a.size() ? a[i] : fallback;
        }

        struct Integration {
            bool laguerre;
            Size order = 0;
            Real relTolerance = 0.0;
            Size maxEvaluations = 0;
        };

        Integration integration(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected an integration spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            Integration s{o.contains("order")};
            if (s.laguerre) {
                Integer order = integer(o.at("order"));
                if (order < 0)
                    throw InvalidParams("an order must be non-negative");
                s.order = static_cast<Size>(order);
            } else {
                s.relTolerance = real(o.at("relTolerance"));
                Integer maxEvaluations = integer(o.at("maxEvaluations"));
                if (maxEvaluations < 0)
                    throw InvalidParams("a maximum must be non-negative");
                s.maxEvaluations = static_cast<Size>(maxEvaluations);
            }
            return s;
        }

        template <class E, class M>
        ext::shared_ptr<AnalyticHestonEngine> makeEngine(const ext::shared_ptr<M>& m, const Integration& i) {
            if (i.laguerre)
                return ext::make_shared<E>(m, i.order);
            return ext::make_shared<E>(m, i.relTolerance, i.maxEvaluations);
        }

        ext::shared_ptr<AnalyticHestonEngine> engine(const std::string& type, const equity::ProcessSpec& p,
                                                     const std::vector<Real>& a, const Integration& i) {
            if (type == "BatesEngine")
                return makeEngine<BatesEngine>(ext::make_shared<BatesModel>(equity::makeBatesProcess(p)), i);
            if (type == "BatesDetJumpEngine")
                return makeEngine<BatesDetJumpEngine>(
                    ext::make_shared<BatesDetJumpModel>(equity::makeBatesProcess(p), argument(a, 0, 1.0),
                                                        argument(a, 1, 0.1)), i);
            if (type == "BatesDoubleExpEngine")
                return makeEngine<BatesDoubleExpEngine>(
                    ext::make_shared<BatesDoubleExpModel>(equity::makeProcess(p), argument(a, 0, 0.1),
                                                          argument(a, 1, 0.1), argument(a, 2, 0.1),
                                                          argument(a, 3, 0.5)), i);
            return makeEngine<BatesDoubleExpDetJumpEngine>(
                ext::make_shared<BatesDoubleExpDetJumpModel>(equity::makeProcess(p), argument(a, 0, 0.1),
                                                             argument(a, 1, 0.1), argument(a, 2, 0.1),
                                                             argument(a, 3, 0.5), argument(a, 4, 1.0),
                                                             argument(a, 5, 0.1)), i);
        }

        const Register unit({
            {"BatesEngine::results",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 std::string type = text(a[0]);
                 if (type != "BatesEngine" && type != "BatesDetJumpEngine" && type != "BatesDoubleExpEngine" &&
                     type != "BatesDoubleExpDetJumpEngine")
                     throw InvalidParams("unknown engine " + type);
                 equity::ProcessSpec p = equity::process(a[1]);
                 std::vector<Real> arguments = reals(a[2]);
                 Integration i = integration(a[3]);
                 auto optionType = enumeration<Option::Type>(a[4]);
                 Real strike = real(a[5]);
                 DateSpec exercise = date(a[6]);
                 return [=] {
                     ext::shared_ptr<AnalyticHestonEngine> e = engine(type, p, arguments, i);
                     VanillaOption option(ext::make_shared<PlainVanillaPayoff>(optionType, strike),
                                          ext::make_shared<EuropeanExercise>(makeDate(exercise)));
                     option.setPricingEngine(e);
                     json::object out;
                     out["NPV"] = option.NPV();
                     out["numberOfEvaluations"] = e->numberOfEvaluations();
                     return json::value(out);
                 };
             }},
        });

    }

}
