// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/vanilla/fdhestonhullwhitevanillaengine.hpp, .cpp: the engine's multiple-strikes caching, which the
// wheel doesn't expose. The process is models/equity/hestonspec.hpp's spec, the exercise ["European" | "American",
// date] (American from the process's reference date), the scheme [type, theta, mu] (QuantLib's FdmSchemeDesc). The
// evaluation date is the case's Settings.
//
// "FdHestonHullWhiteVanillaEngine::multipleStrikes" takes (process, a, sigma, correlation, optionType, referenceStrike,
// exercise, strikes, tGrid, xGrid, vGrid, rGrid, dampingSteps, controlVariate, scheme): an
// FdHestonHullWhiteVanillaEngine on HestonModel(process) and a HullWhiteProcess on the process's risk-free curve with the
// reversion a and the volatility sigma, at the correlation, with the grids, the control variate and the scheme,
// enableMultipleStrikesCaching(strikes), a plain vanilla option at the reference strike priced on it, then an option at
// each strike, of the same type and exercise, priced on it from the cache; it returns {"strikes"}, each strike's
// {"NPV", "delta", "gamma", "theta"}.

#include "convert.hpp"
#include "models/equity/hestonspec.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/vanillaoption.hpp>
#include <ql/models/equity/hestonmodel.hpp>
#include <ql/pricingengines/vanilla/fdhestonhullwhitevanillaengine.hpp>
#include <ql/processes/hullwhiteprocess.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct ExerciseSpec {
            bool american;
            DateSpec date;
        };

        ExerciseSpec exercise(const json::value& v) {
            if (!v.is_array() || v.get_array().size() != 2)
                throw InvalidParams("expected [type, date], got " + json::serialize(v));
            std::string type = text(v.get_array()[0]);
            if (type != "European" && type != "American")
                throw InvalidParams("an exercise type is European or American");
            return {type == "American", date(v.get_array()[1])};
        }

        ext::shared_ptr<Exercise> makeExercise(const ExerciseSpec& e, const equity::ProcessSpec& p) {
            if (e.american)
                return ext::make_shared<AmericanExercise>(makeDate(p.referenceDate), makeDate(e.date));
            return ext::make_shared<EuropeanExercise>(makeDate(e.date));
        }

        Size size(const json::value& v) {
            Integer n = integer(v);
            if (n < 0)
                throw InvalidParams("a size must be non-negative");
            return static_cast<Size>(n);
        }

        const Register unit({
            {"FdHestonHullWhiteVanillaEngine::multipleStrikes",
             [](const Args& a) -> Call {
                 arity(a, 15);
                 equity::ProcessSpec p = equity::process(a[0]);
                 Real hwA = real(a[1]), hwSigma = real(a[2]), correlation = real(a[3]);
                 auto optionType = enumeration<Option::Type>(a[4]);
                 Real referenceStrike = real(a[5]);
                 ExerciseSpec e = exercise(a[6]);
                 std::vector<Real> strikes = reals(a[7]);
                 Size tGrid = size(a[8]), xGrid = size(a[9]), vGrid = size(a[10]), rGrid = size(a[11]),
                      dampingSteps = size(a[12]);
                 bool controlVariate = boolean(a[13]);
                 if (!a[14].is_array() || a[14].get_array().size() != 3)
                     throw InvalidParams("expected [type, theta, mu], got " + json::serialize(a[14]));
                 const json::array& sc = a[14].get_array();
                 auto schemeType = enumeration<FdmSchemeDesc::FdmSchemeType>(sc[0]);
                 Real theta = real(sc[1]), mu = real(sc[2]);
                 return [=] {
                     ext::shared_ptr<HestonProcess> process = equity::makeProcess(p);
                     auto hullWhite = ext::make_shared<HullWhiteProcess>(process->riskFreeRate(), hwA, hwSigma);
                     auto engine = ext::make_shared<FdHestonHullWhiteVanillaEngine>(
                         ext::make_shared<HestonModel>(process), hullWhite, correlation, tGrid, xGrid, vGrid, rGrid,
                         dampingSteps, controlVariate, FdmSchemeDesc(schemeType, theta, mu));
                     engine->enableMultipleStrikesCaching(strikes);
                     ext::shared_ptr<Exercise> ex = makeExercise(e, p);
                     VanillaOption reference(ext::make_shared<PlainVanillaPayoff>(optionType, referenceStrike), ex);
                     reference.setPricingEngine(engine);
                     reference.NPV();
                     json::array out;
                     for (Real k : strikes) {
                         VanillaOption option(ext::make_shared<PlainVanillaPayoff>(optionType, k), ex);
                         option.setPricingEngine(engine);
                         out.push_back(equity::vanillaResults(option.NPV(), option.delta(), option.gamma(),
                                                              option.theta()));
                     }
                     json::object r;
                     r["strikes"] = out;
                     return json::value(r);
                 };
             }},
        });

    }

}
