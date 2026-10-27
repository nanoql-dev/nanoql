// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/callability/ and ql/methods/montecarlo/'s regression and parametric optimisation: the
// exercise values, node-data providers, strategies, node-data collection and upper-bound engine the wheel doesn't
// expose, stepped on curve states the harness sets or run on a few paths (callabilityspec.hpp's specs).
//
// "MarketModelExerciseValue.steps" takes (exerciseValue, stepForwards): reset, then at each step's state nextStep and
// value; the result {"numberOfExercises", "possibleCashFlowTimes", "evolutionTimes", "isExerciseTime", "steps"}, each
// step [timeIndex, amount].
//
// "MarketModelNodeDataProvider.steps" takes (provider, stepForwards): reset, then at each step's state nextStep and, at
// an exercise time, values; the result {"numberOfExercises", "numberOfData", "evolutionTimes", "isExerciseTime",
// "steps"}, each step the values or null; a parametric exercise adds "numberOfVariables", "numberOfParameters", each
// exercise's "guesses" and, each exercise step, "decisions" on its guess.
//
// "ExerciseStrategy.steps" takes (strategy, rateTimes, stepForwards): reset, then at each relevant time's state nextStep
// and, at an exercise time, exercise; the result {"exerciseTimes", "relevantTimes", "decisions"}, each relevant step's
// decision or null.
//
// "CollectNodeData.collect" takes (evolver, product, provider, rebate, control, paths): the node data, each row of
// nodes [exerciseValue, cumulatedCashFlows, values, controlValue, isValid]. "GenericLongstaffSchwartzRegression.run"
// takes the same and regresses on the collected data: {"estimate", "coefficients", "cumulatedCashFlows"} (row 0's
// after the regression). "GenericEarlyExerciseOptimization.run" takes the same with a parametric provider and
// (simplexLambda, [maxIterations, maxStationaryStateIterations, rootEpsilon, functionEpsilon, gradientNormEpsilon]):
// {"estimate", "parameters", "cumulatedCashFlows"}.
//
// "UpperBoundEngine.values" takes (evolver, innerEvolvers, underlying, rebate, hedge, hedgeRebate, strategy,
// initialNumeraireValue, outerPaths, innerPaths): the engine's values in a Statistics, {"samples", "weightSum",
// "mean", "errorEstimate"}.

#include "convert.hpp"
#include "models/marketmodels/callability/callabilityspec.hpp"
#include "models/marketmodels/marketmodelspec.hpp"
#include "models/marketmodels/products/productspec.hpp"

#include <ql/errors.hpp>
#include <ql/math/optimization/endcriteria.hpp>
#include <ql/math/optimization/simplex.hpp>
#include <ql/math/statistics/statistics.hpp>
#include <ql/methods/montecarlo/genericlsregression.hpp>
#include <ql/methods/montecarlo/nodedata.hpp>
#include <ql/methods/montecarlo/parametricexercise.hpp>
#include <ql/models/marketmodels/callability/collectnodedata.hpp>
#include <ql/models/marketmodels/discounter.hpp>
#include <ql/models/marketmodels/callability/upperboundengine.hpp>
#include <ql/models/marketmodels/curvestates/lmmcurvestate.hpp>
#include <ql/models/marketmodels/evolutiondescription.hpp>
#include <ql/models/marketmodels/utilities.hpp>

#include <cstdint>
#include <functional>
#include <memory>
#include <vector>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        json::value attempt(const std::function<json::value()>& f) {
            try {
                return f();
            } catch (Error&) {
                json::object t;
                t["throws"] = true;
                return t;
            }
        }

        json::value flags(const std::valarray<bool>& v) {
            json::array out;
            for (bool b : v)
                out.emplace_back(b);
            return out;
        }

        void checkStates(const Mat& forwards, Size rates) {
            for (const Vec& f : forwards)
                if (f.size() != rates)
                    throw InvalidParams("each step's forwards must be one a rate");
        }

        json::value nodeData(const std::vector<std::vector<NodeData>>& data) {
            json::array rows;
            for (const auto& row : data) {
                json::array nodes;
                for (const NodeData& d : row) {
                    json::array node;
                    node.emplace_back(d.exerciseValue);
                    node.emplace_back(d.cumulatedCashFlows);
                    node.emplace_back(result(d.values));
                    node.emplace_back(d.controlValue);
                    node.emplace_back(d.isValid);
                    nodes.emplace_back(node);
                }
                rows.emplace_back(nodes);
            }
            return rows;
        }

        json::value matrixResult(const std::vector<std::vector<Real>>& m) {
            json::array out;
            for (const auto& r : m)
                out.emplace_back(result(r));
            return out;
        }

        json::value rowZero(const std::vector<std::vector<NodeData>>& data) {
            std::vector<Real> out;
            for (const NodeData& d : data.front())
                out.push_back(d.cumulatedCashFlows);
            return result(out);
        }

        struct Collect {
            EvolverSpec evolver;
            ProductSpec product;
            ProviderSpec provider;
            ExerciseValueSpec rebate, control;
            Size paths;
        };

        Collect collectArgs(const Args& a) {
            Collect c{evolver(a[0]), product(a[1]), provider(a[2]), exerciseValue(a[3]), exerciseValue(a[4]),
                      size(a[5])};
            // QuantLib reads its evolver's numeraires and the provider's, rebate's and control's flags against the
            // product's steps (collectnodedata.cpp:49-53's TODO): the vectors hold those that fit
            std::unique_ptr<MarketModelMultiProduct> p = makeProduct(c.product);
            const std::vector<Time>& times = p->evolution().evolutionTimes();
            if (c.evolver.numeraires.size() != times.size())
                throw InvalidParams("the evolver must step at the product's times");
            if (p->numberOfProducts() != 1)
                throw InvalidParams("a single product is required");
            return c;
        }

        std::vector<std::vector<NodeData>> collected(const Collect& c) {
            ext::shared_ptr<MarketModelEvolver> e = makeEvolver(c.evolver);
            std::unique_ptr<MarketModelMultiProduct> p = makeProduct(c.product);
            std::unique_ptr<MarketModelNodeDataProvider> d = makeProvider(c.provider);
            std::unique_ptr<MarketModelExerciseValue> r = makeExerciseValue(c.rebate);
            std::unique_ptr<MarketModelExerciseValue> k = makeExerciseValue(c.control);
            std::vector<std::vector<NodeData>> data;
            collectNodeData(*e, *p, *d, *r, *k, c.paths, data);
            return data;
        }

        const Register unit({
            {"MarketModelExerciseValue.steps",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 ExerciseValueSpec s = exerciseValue(a[0]);
                 Mat forwards = matrix(a[1]);
                 checkStates(forwards, s.rateTimes.size() - 1);
                 return [=] {
                     std::unique_ptr<MarketModelExerciseValue> v = makeExerciseValue(s);
                     const EvolutionDescription& evolution = v->evolution();
                     if (forwards.size() > evolution.numberOfSteps())
                         throw InvalidParams("a state an evolution step at most");
                     json::object out;
                     out["numberOfExercises"] = static_cast<std::int64_t>(v->numberOfExercises());
                     out["possibleCashFlowTimes"] = result(v->possibleCashFlowTimes());
                     out["evolutionTimes"] = result(evolution.evolutionTimes());
                     out["isExerciseTime"] = flags(v->isExerciseTime());
                     json::array steps;
                     v->reset();
                     for (const Vec& f : forwards) {
                         LMMCurveState cs(s.rateTimes);
                         cs.setOnForwardRates(f);
                         v->nextStep(cs);
                         MarketModelMultiProduct::CashFlow cf = v->value(cs);
                         json::array step;
                         step.emplace_back(static_cast<std::int64_t>(cf.timeIndex));
                         step.emplace_back(cf.amount);
                         steps.emplace_back(step);
                     }
                     out["steps"] = steps;
                     return json::value(out);
                 };
             }},
            {"MarketModelNodeDataProvider.steps",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 ProviderSpec s = provider(a[0]);
                 Mat forwards = matrix(a[1]);
                 checkStates(forwards, s.rateTimes.size() - 1);
                 if (forwards.size() > s.exerciseTimes.size())
                     throw InvalidParams("a state an evolution step at most");
                 return [=] {
                     std::unique_ptr<MarketModelNodeDataProvider> p = makeProvider(s);
                     json::object out;
                     out["numberOfExercises"] = static_cast<std::int64_t>(p->numberOfExercises());
                     out["numberOfData"] = result(p->numberOfData());
                     out["evolutionTimes"] = result(p->evolution().evolutionTimes());
                     std::valarray<bool> isExercise = p->isExerciseTime();
                     out["isExerciseTime"] = flags(isExercise);
                     auto* parametric = dynamic_cast<MarketModelParametricExercise*>(p.get());
                     if (parametric != nullptr) {
                         out["numberOfVariables"] = result(parametric->numberOfVariables());
                         out["numberOfParameters"] = result(parametric->numberOfParameters());
                         json::array guesses;
                         for (Size i = 0; i < p->numberOfExercises(); ++i) {
                             std::vector<Real> g;
                             parametric->guess(i, g);
                             guesses.emplace_back(result(g));
                         }
                         out["guesses"] = guesses;
                     }
                     json::array steps, decisions;
                     p->reset();
                     Size exercise = 0;
                     for (Size k = 0; k < forwards.size(); ++k) {
                         LMMCurveState cs(s.rateTimes);
                         cs.setOnForwardRates(forwards[k]);
                         p->nextStep(cs);
                         if (isExercise[k]) {
                             std::vector<Real> values;
                             p->values(cs, values);
                             steps.emplace_back(result(values));
                             if (parametric != nullptr) {
                                 std::vector<Real> g;
                                 parametric->guess(exercise, g);
                                 decisions.emplace_back(parametric->exercise(exercise, g, values));
                             }
                             ++exercise;
                         } else {
                             steps.emplace_back(nullptr);
                             if (parametric != nullptr)
                                 decisions.emplace_back(nullptr);
                         }
                     }
                     out["steps"] = steps;
                     if (parametric != nullptr)
                         out["decisions"] = decisions;
                     return json::value(out);
                 };
             }},
            {"ExerciseStrategy.steps",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 StrategySpec s = strategy(a[0]);
                 Vec rateTimes = reals(a[1]);
                 Mat forwards = matrix(a[2]);
                 if (rateTimes.size() < 2)
                     throw InvalidParams("two rate times at least");
                 checkStates(forwards, rateTimes.size() - 1);
                 return [=] {
                     std::unique_ptr<ExerciseStrategy<CurveState>> st = makeStrategy(s);
                     std::vector<Time> relevant = st->relevantTimes();
                     std::vector<Time> exercises = st->exerciseTimes();
                     if (forwards.size() > relevant.size())
                         throw InvalidParams("a state a relevant time at most");
                     std::valarray<bool> isExercise = isInSubset(relevant, exercises);
                     json::object out;
                     out["exerciseTimes"] = result(exercises);
                     out["relevantTimes"] = result(relevant);
                     json::array decisions;
                     st->reset();
                     for (Size k = 0; k < forwards.size(); ++k) {
                         LMMCurveState cs(rateTimes);
                         cs.setOnForwardRates(forwards[k]);
                         st->nextStep(cs);
                         if (isExercise[k])
                             decisions.emplace_back(st->exercise(cs));
                         else
                             decisions.emplace_back(nullptr);
                     }
                     out["decisions"] = decisions;
                     return json::value(out);
                 };
             }},
            {"CollectNodeData.collect",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Collect c = collectArgs(a);
                 return [=] { return nodeData(collected(c)); };
             }},
            {"GenericLongstaffSchwartzRegression.run",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Collect c = collectArgs(a);
                 return [=] {
                     std::vector<std::vector<NodeData>> data = collected(c);
                     std::vector<std::vector<Real>> coefficients;
                     Real estimate = genericLongstaffSchwartzRegression(data, coefficients);
                     json::object out;
                     out["estimate"] = estimate;
                     out["coefficients"] = matrixResult(coefficients);
                     out["cumulatedCashFlows"] = rowZero(data);
                     return json::value(out);
                 };
             }},
            {"GenericEarlyExerciseOptimization.run",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 Collect c = collectArgs(a);
                 Real lambda = real(a[6]);
                 if (c.provider.type != "TriggeredSwapExercise")
                     throw InvalidParams("the provider must be a parametric exercise");
                 const json::value& ec = a[7];
                 if (!ec.is_array() || ec.get_array().size() != 5)
                     throw InvalidParams("end criteria are [maxIterations, maxStationaryStateIterations, rootEpsilon, "
                                         "functionEpsilon, gradientNormEpsilon]");
                 Size maxIterations = size(ec.get_array()[0]), stationary = size(ec.get_array()[1]);
                 Real rootEpsilon = real(ec.get_array()[2]), functionEpsilon = real(ec.get_array()[3]),
                      gradientNormEpsilon = real(ec.get_array()[4]);
                 return [=] {
                     std::vector<std::vector<NodeData>> data = collected(c);
                     std::unique_ptr<MarketModelParametricExercise> p = makeParametric(c.provider);
                     Simplex om(lambda);
                     EndCriteria criteria(maxIterations, stationary, rootEpsilon, functionEpsilon, gradientNormEpsilon);
                     std::vector<std::vector<Real>> parameters;
                     Real estimate = genericEarlyExerciseOptimization(data, *p, parameters, criteria, om);
                     json::object out;
                     out["estimate"] = estimate;
                     out["parameters"] = matrixResult(parameters);
                     out["cumulatedCashFlows"] = rowZero(data);
                     return json::value(out);
                 };
             }},
            {"UpperBoundEngine.values",
             [](const Args& a) -> Call {
                 arity(a, 10);
                 EvolverSpec outer = evolver(a[0]);
                 std::vector<EvolverSpec> inner;
                 if (!a[1].is_array())
                     throw InvalidParams("expected an array of inner evolvers");
                 for (const json::value& x : a[1].get_array())
                     inner.push_back(evolver(x));
                 ProductSpec underlying = product(a[2]);
                 ExerciseValueSpec rebate = exerciseValue(a[3]);
                 ProductSpec hedge = product(a[4]);
                 ExerciseValueSpec hedgeRebate = exerciseValue(a[5]);
                 StrategySpec st = strategy(a[6]);
                 Real initialNumeraireValue = real(a[7]);
                 Size outerPaths = size(a[8]), innerPaths = size(a[9]);
                 // QuantLib takes an inner evolver at each exercise date before the last step, reading past fewer
                 if (inner.size() < makeStrategy(st)->exerciseTimes().size())
                     throw InvalidParams("an inner evolver an exercise time");
                 return [=] {
                     std::unique_ptr<MarketModelMultiProduct> u = makeProduct(underlying);
                     std::unique_ptr<MarketModelMultiProduct> h = makeProduct(hedge);
                     std::unique_ptr<MarketModelExerciseValue> r = makeExerciseValue(rebate);
                     std::unique_ptr<MarketModelExerciseValue> hr = makeExerciseValue(hedgeRebate);
                     std::unique_ptr<ExerciseStrategy<CurveState>> s = makeStrategy(st);
                     std::vector<ext::shared_ptr<MarketModelEvolver>> innerEvolvers;
                     for (const EvolverSpec& e : inner)
                         innerEvolvers.push_back(makeEvolver(e));
                     UpperBoundEngine engine(makeEvolver(outer), innerEvolvers, *u, *r, *h, *hr, *s,
                                             initialNumeraireValue);
                     Statistics stats;
                     engine.multiplePathValues(stats, outerPaths, innerPaths);
                     json::object out;
                     out["samples"] = static_cast<std::int64_t>(stats.samples());
                     out["weightSum"] = stats.weightSum();
                     out["mean"] = stats.mean();
                     out["errorEstimate"] = attempt([&] { return json::value(stats.errorEstimate()); });
                     return json::value(out);
                 };
             }},
        });

    }

}
