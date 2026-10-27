// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/evolvers/: whole evolutions of the market-model evolvers the wheel doesn't expose.
//
// "MarketModelEvolver.evolve" takes (type, model, generator, numeraires, initialStep, paths, initialForwards): the
// evolver of the type ("LogNormalFwdRateEuler", "LogNormalFwdRatePc", "LogNormalFwdRateIpc", "LogNormalFwdRateBalland",
// "LogNormalFwdRateiBalland", "NormalFwdRatePc", "LogNormalCotSwapRatePc", or {"type": "LogNormalCmSwapRatePc",
// "spanningForwards"}) on the model and a generator factory (marketmodelspec.hpp), its initial state set unless the
// initial forwards are null: an LMMCurveState on them, for the coterminal evolver a CoterminalSwapCurveState on that
// state's coterminal swap rates and for the constant-maturity one a CMSwapCurveState on its swap rates of the span
// (each evolver's own state type, the only one QuantLib reads); and the paths: {"numeraires", "paths"}, each path
// {"weight", "steps"}, each step {"weight", "currentStep", "forwards" (the curve state's forward rates),
// "discountRatios" (each bond to the last), "coterminalSwapRates"}, an Euler step also "brownians".
//
// "LogNormalFwdRateEulerConstrained.evolve" takes (model, generator, numeraires, initialStep, paths, initialForwards,
// constrainedRates, rateConstraints, isConstraintActive): the constrained Euler evolver, its constraints set once and
// its constraint type (each step's forward rate, the rate after it ending its swap) set again before each step, so
// each step's covariances are its own pseudo-root's; the result as "MarketModelEvolver.evolve"'s.
//
// "SquareRootAndersen.paths" takes (meanLevel, reversionSpeed, volVar, v0, evolutionTimes, numberSubSteps, w1, w2,
// cutPoint, variates): the process's "variatesPerStep", "numberStateVariables" and "paths", each the steps of a path of
// the given variates (a step's array each), each step {"weight", "stepSd", "state"}.
//
// "SVDDFwdRatePc.evolve" takes (model, generator, volProcess, firstVolatilityFactor, volatilityFactorStep, numeraires,
// paths, initialForwards): the SVD-DD evolver from step 0 on a SquareRootAndersen {"meanLevel", "reversionSpeed",
// "volVar", "v0", "evolutionTimes", "numberSubSteps", "w1", "w2", "cutPoint"}; the result as
// "MarketModelEvolver.evolve"'s.

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/models/marketmodels/curvestates/cmswapcurvestate.hpp>
#include <ql/models/marketmodels/curvestates/coterminalswapcurvestate.hpp>
#include <ql/models/marketmodels/curvestates/lmmcurvestate.hpp>
#include <ql/models/marketmodels/evolutiondescription.hpp>
#include <ql/models/marketmodels/evolvers/lognormalcmswapratepc.hpp>
#include <ql/models/marketmodels/evolvers/lognormalcotswapratepc.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdrateballand.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdrateeuler.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdrateeulerconstrained.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdrateiballand.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdrateipc.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdratepc.hpp>
#include <ql/models/marketmodels/evolvers/normalfwdratepc.hpp>
#include <ql/models/marketmodels/evolvers/svddfwdratepc.hpp>
#include <ql/models/marketmodels/evolvers/volprocesses/squarerootandersen.hpp>

#include <cstdint>
#include <functional>
#include <memory>
#include <optional>
#include <string>
#include <valarray>
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

        // The paths of an evolver already built, before each step the call given (none if empty).
        json::value paths(MarketModelEvolver& e, Size steps, Size count, const std::vector<Real>* (*brownians)(
                              const MarketModelEvolver&), const std::function<void()>& beforeStep = {}) {
            json::array out;
            for (Size p = 0; p < count; ++p) {
                json::object path;
                path["weight"] = e.startNewPath();
                json::array ss;
                for (Size s = 0; s < steps; ++s) {
                    if (beforeStep)
                        beforeStep();
                    Real w = e.advanceStep();
                    ss.emplace_back(step(e, w, brownians != nullptr ? brownians(e) : nullptr));
                }
                path["steps"] = ss;
                out.emplace_back(path);
            }
            json::object r;
            r["numeraires"] = result(e.numeraires());
            r["paths"] = out;
            return json::value(r);
        }

        const std::vector<Real>* eulerBrownians(const MarketModelEvolver& e) {
            return &static_cast<const LogNormalFwdRateEuler&>(e).browniansThisStep();
        }

        std::optional<Vec> optionalReals(const json::value& v) {
            if (v.is_null())
                return std::nullopt;
            return reals(v);
        }

        struct EvolverType {
            std::string name;
            Size spanningForwards = 0;
        };

        EvolverType evolverType(const json::value& v) {
            EvolverType t;
            if (v.is_object()) {
                const std::string what = "an evolver type";
                const json::object& o = object(v, what);
                onlyKeys(o, {"type", "spanningForwards"}, what);
                t.name = text(key(o, "type", what));
                if (t.name != "LogNormalCmSwapRatePc")
                    throw InvalidParams("only LogNormalCmSwapRatePc takes spanningForwards");
                t.spanningForwards = size(key(o, "spanningForwards", what));
                return t;
            }
            t.name = text(v);
            if (t.name != "LogNormalFwdRateEuler" && t.name != "LogNormalFwdRatePc" && t.name != "LogNormalFwdRateIpc" &&
                t.name != "LogNormalFwdRateBalland" && t.name != "LogNormalFwdRateiBalland" &&
                t.name != "NormalFwdRatePc" && t.name != "LogNormalCotSwapRatePc")
                throw InvalidParams("unknown evolver type " + t.name);
            return t;
        }

        std::unique_ptr<MarketModelEvolver> makeEvolver(const EvolverType& t,
                                                        const ext::shared_ptr<MarketModel>& mm,
                                                        const BrownianGeneratorFactory& f,
                                                        const std::vector<Size>& numeraires,
                                                        Size initialStep) {
            if (t.name == "LogNormalFwdRateEuler")
                return std::make_unique<LogNormalFwdRateEuler>(mm, f, numeraires, initialStep);
            if (t.name == "LogNormalFwdRatePc")
                return std::make_unique<LogNormalFwdRatePc>(mm, f, numeraires, initialStep);
            if (t.name == "LogNormalFwdRateIpc")
                return std::make_unique<LogNormalFwdRateIpc>(mm, f, numeraires, initialStep);
            if (t.name == "LogNormalFwdRateBalland")
                return std::make_unique<LogNormalFwdRateBalland>(mm, f, numeraires, initialStep);
            if (t.name == "LogNormalFwdRateiBalland")
                return std::make_unique<LogNormalFwdRateiBalland>(mm, f, numeraires, initialStep);
            if (t.name == "NormalFwdRatePc")
                return std::make_unique<NormalFwdRatePc>(mm, f, numeraires, initialStep);
            if (t.name == "LogNormalCotSwapRatePc")
                return std::make_unique<LogNormalCotSwapRatePc>(mm, f, numeraires, initialStep);
            return std::make_unique<LogNormalCmSwapRatePc>(t.spanningForwards, mm, f, numeraires, initialStep);
        }

        // The evolver's own state type on the forwards.
        void setState(MarketModelEvolver& e, const EvolverType& t, const std::vector<Time>& rateTimes,
                      const Vec& forwards) {
            LMMCurveState lmm(rateTimes);
            lmm.setOnForwardRates(forwards);
            if (t.name == "LogNormalCotSwapRatePc") {
                CoterminalSwapCurveState cs(rateTimes);
                cs.setOnCoterminalSwapRates(lmm.coterminalSwapRates());
                e.setInitialState(cs);
            } else if (t.name == "LogNormalCmSwapRatePc") {
                CMSwapCurveState cs(rateTimes, t.spanningForwards);
                cs.setOnCMSwapRates(lmm.cmSwapRates(t.spanningForwards));
                e.setInitialState(cs);
            } else {
                e.setInitialState(lmm);
            }
        }

        struct VolProcessSpec {
            Real meanLevel = 0.0, reversionSpeed = 0.0, volVar = 0.0, v0 = 0.0;
            Vec evolutionTimes;
            Size numberSubSteps = 0;
            Real w1 = 0.0, w2 = 0.0, cutPoint = 0.0;
        };

        VolProcessSpec volProcess(const json::value& v) {
            const std::string what = "a SquareRootAndersen";
            const json::object& o = object(v, what);
            onlyKeys(o, {"meanLevel", "reversionSpeed", "volVar", "v0", "evolutionTimes", "numberSubSteps", "w1", "w2",
                         "cutPoint"}, what);
            VolProcessSpec p;
            p.meanLevel = real(key(o, "meanLevel", what));
            p.reversionSpeed = real(key(o, "reversionSpeed", what));
            p.volVar = real(key(o, "volVar", what));
            p.v0 = real(key(o, "v0", what));
            p.evolutionTimes = reals(key(o, "evolutionTimes", what));
            p.numberSubSteps = size(key(o, "numberSubSteps", what));
            p.w1 = real(key(o, "w1", what));
            p.w2 = real(key(o, "w2", what));
            p.cutPoint = real(key(o, "cutPoint", what));
            return p;
        }

        ext::shared_ptr<SquareRootAndersen> makeVolProcess(const VolProcessSpec& p) {
            return ext::make_shared<SquareRootAndersen>(p.meanLevel, p.reversionSpeed, p.volVar, p.v0, p.evolutionTimes,
                                                        p.numberSubSteps, p.w1, p.w2, p.cutPoint);
        }

        const Register unit({
            {"MarketModelEvolver.evolve",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 EvolverType type = evolverType(a[0]);
                 ModelSpec m = model(a[1]);
                 GeneratorSpec g = generator(a[2]);
                 std::vector<Size> numeraires = sizes(a[3]);
                 Size initialStep = size(a[4]), count = size(a[5]);
                 std::optional<Vec> initialForwards = optionalReals(a[6]);
                 return [=] {
                     ext::shared_ptr<MarketModel> mm = makeModel(m);
                     std::unique_ptr<BrownianGeneratorFactory> factory = makeFactory(g);
                     std::unique_ptr<MarketModelEvolver> e = makeEvolver(type, mm, *factory, numeraires, initialStep);
                     if (initialForwards)
                         setState(*e, type, mm->evolution().rateTimes(), *initialForwards);
                     return paths(*e, mm->numberOfSteps() - initialStep, count,
                                  type.name == "LogNormalFwdRateEuler" ? &eulerBrownians : nullptr);
                 };
             }},
            {"LogNormalFwdRateEulerConstrained.evolve",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 ModelSpec m = model(a[0]);
                 GeneratorSpec g = generator(a[1]);
                 std::vector<Size> numeraires = sizes(a[2]);
                 Size initialStep = size(a[3]), count = size(a[4]);
                 std::optional<Vec> initialForwards = optionalReals(a[5]);
                 std::vector<Size> start = sizes(a[6]);
                 Vec constraints = reals(a[7]);
                 if (!a[8].is_array())
                     throw InvalidParams("expected an array of booleans, got " + json::serialize(a[8]));
                 std::vector<bool> active;
                 for (const json::value& x : a[8].get_array())
                     active.push_back(boolean(x));
                 return [=] {
                     ext::shared_ptr<MarketModel> mm = makeModel(m);
                     std::unique_ptr<BrownianGeneratorFactory> factory = makeFactory(g);
                     LogNormalFwdRateEulerConstrained e(mm, *factory, numeraires, initialStep);
                     if (initialForwards) {
                         LMMCurveState cs(mm->evolution().rateTimes());
                         cs.setOnForwardRates(*initialForwards);
                         e.setInitialState(cs);
                     }
                     std::vector<Size> end(start.size());
                     for (Size i = 0; i < start.size(); ++i)
                         end[i] = start[i] + 1;
                     std::valarray<bool> isActive(active.size());
                     for (Size i = 0; i < active.size(); ++i)
                         isActive[i] = active[i];
                     e.setThisConstraint(constraints, isActive);
                     return paths(e, mm->numberOfSteps() - initialStep, count, nullptr,
                                  [&] { e.setConstraintType(start, end); });
                 };
             }},
            {"SquareRootAndersen.paths",
             [](const Args& a) -> Call {
                 arity(a, 10);
                 VolProcessSpec p;
                 p.meanLevel = real(a[0]);
                 p.reversionSpeed = real(a[1]);
                 p.volVar = real(a[2]);
                 p.v0 = real(a[3]);
                 p.evolutionTimes = reals(a[4]);
                 p.numberSubSteps = size(a[5]);
                 p.w1 = real(a[6]);
                 p.w2 = real(a[7]);
                 p.cutPoint = real(a[8]);
                 if (!a[9].is_array())
                     throw InvalidParams("expected an array of paths, got " + json::serialize(a[9]));
                 std::vector<std::vector<Vec>> variates;
                 for (const json::value& path : a[9].get_array()) {
                     if (!path.is_array())
                         throw InvalidParams("expected an array of steps, got " + json::serialize(path));
                     std::vector<Vec> steps;
                     for (const json::value& s : path.get_array())
                         steps.push_back(reals(s));
                     variates.push_back(steps);
                 }
                 return [=] {
                     ext::shared_ptr<SquareRootAndersen> process = makeVolProcess(p);
                     json::array out;
                     for (const std::vector<Vec>& path : variates) {
                         process->nextPath();
                         json::array ss;
                         for (const Vec& z : path) {
                             json::object s;
                             s["weight"] = process->nextstep(z);
                             s["stepSd"] = process->stepSd();
                             s["state"] = result(process->stateVariables());
                             ss.emplace_back(s);
                         }
                         out.emplace_back(ss);
                     }
                     json::object r;
                     r["variatesPerStep"] = static_cast<std::int64_t>(process->variatesPerStep());
                     r["numberStateVariables"] = static_cast<std::int64_t>(process->numberStateVariables());
                     r["paths"] = out;
                     return json::value(r);
                 };
             }},
            {"SVDDFwdRatePc.evolve",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 ModelSpec m = model(a[0]);
                 GeneratorSpec g = generator(a[1]);
                 VolProcessSpec p = volProcess(a[2]);
                 Size firstVolatilityFactor = size(a[3]), volatilityFactorStep = size(a[4]);
                 std::vector<Size> numeraires = sizes(a[5]);
                 Size count = size(a[6]);
                 std::optional<Vec> initialForwards = optionalReals(a[7]);
                 return [=] {
                     ext::shared_ptr<MarketModel> mm = makeModel(m);
                     std::unique_ptr<BrownianGeneratorFactory> factory = makeFactory(g);
                     SVDDFwdRatePc e(mm, *factory, makeVolProcess(p), firstVolatilityFactor, volatilityFactorStep,
                                     numeraires);
                     if (initialForwards) {
                         LMMCurveState cs(mm->evolution().rateTimes());
                         cs.setOnForwardRates(*initialForwards);
                         e.setInitialState(cs);
                     }
                     return paths(e, mm->numberOfSteps(), count, nullptr);
                 };
             }},
        });

    }

}
