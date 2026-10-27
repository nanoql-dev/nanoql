// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/evolvers/: whole evolutions of the market-model evolvers the wheel doesn't expose.
//
// "MarketModelEvolver.evolve" takes (type, model, generator, numeraires, initialStep, paths, initialForwards): the
// evolver of the type ("LogNormalFwdRateEuler", "LogNormalFwdRatePc") on the model and a generator factory
// (marketmodelspec.hpp), its initial state set from an LMMCurveState on the initial forwards unless they are null, and
// the paths: {"numeraires", "paths"}, each path {"weight", "steps"}, each step {"weight", "currentStep", "forwards" (the
// curve state's forward rates), "discountRatios" (each bond to the last), "coterminalSwapRates"}, an Euler step also
// "brownians".

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/models/marketmodels/curvestates/lmmcurvestate.hpp>
#include <ql/models/marketmodels/evolutiondescription.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdrateeuler.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdratepc.hpp>

#include <cstdint>
#include <memory>
#include <optional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        json::value step(const MarketModelEvolver& e, Real weight, const std::vector<Real>* brownians) {
            const CurveState& cs = e.currentState();
            Size n = cs.numberOfRates();
            json::array ratios;
            for (Size i = 0; i <= n; ++i)
                ratios.emplace_back(cs.discountRatio(i, n));
            json::object out;
            out["weight"] = weight;
            out["currentStep"] = static_cast<std::int64_t>(e.currentStep());
            out["forwards"] = result(cs.forwardRates());
            out["discountRatios"] = ratios;
            out["coterminalSwapRates"] = result(cs.coterminalSwapRates());
            if (brownians != nullptr)
                out["brownians"] = result(*brownians);
            return out;
        }

        const Register unit({
            {"MarketModelEvolver.evolve",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 std::string type = text(a[0]);
                 if (type != "LogNormalFwdRateEuler" && type != "LogNormalFwdRatePc")
                     throw InvalidParams("unknown evolver type " + type);
                 ModelSpec m = model(a[1]);
                 GeneratorSpec g = generator(a[2]);
                 std::vector<Size> numeraires = sizes(a[3]);
                 Size initialStep = size(a[4]), paths = size(a[5]);
                 std::optional<Vec> initialForwards;
                 if (!a[6].is_null())
                     initialForwards = reals(a[6]);
                 return [=] {
                     ext::shared_ptr<MarketModel> mm = makeModel(m);
                     std::unique_ptr<BrownianGeneratorFactory> factory = makeFactory(g);
                     std::unique_ptr<MarketModelEvolver> e;
                     LogNormalFwdRateEuler* euler = nullptr;
                     if (type == "LogNormalFwdRateEuler") {
                         auto p = std::make_unique<LogNormalFwdRateEuler>(mm, *factory, numeraires, initialStep);
                         euler = p.get();
                         e = std::move(p);
                     } else {
                         e = std::make_unique<LogNormalFwdRatePc>(mm, *factory, numeraires, initialStep);
                     }
                     if (initialForwards) {
                         LMMCurveState cs(mm->evolution().rateTimes());
                         cs.setOnForwardRates(*initialForwards);
                         e->setInitialState(cs);
                     }
                     json::array out;
                     Size steps = mm->numberOfSteps() - initialStep;
                     for (Size p = 0; p < paths; ++p) {
                         json::object path;
                         path["weight"] = e->startNewPath();
                         json::array ss;
                         for (Size s = 0; s < steps; ++s) {
                             Real w = e->advanceStep();
                             ss.emplace_back(step(*e, w, euler != nullptr ? &euler->browniansThisStep() : nullptr));
                         }
                         path["steps"] = ss;
                         out.emplace_back(path);
                     }
                     json::object r;
                     r["numeraires"] = result(e->numeraires());
                     r["paths"] = out;
                     return json::value(r);
                 };
             }},
        });

    }

}
