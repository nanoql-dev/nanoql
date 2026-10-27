// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "models/marketmodels/callability/callabilityspec.hpp"

#include <ql/instruments/payoffs.hpp>
#include <ql/models/marketmodels/callability/bermudanswaptionexercisevalue.hpp>
#include <ql/models/marketmodels/callability/lsstrategy.hpp>
#include <ql/models/marketmodels/callability/nothingexercisevalue.hpp>
#include <ql/models/marketmodels/callability/parametricexerciseadapter.hpp>
#include <ql/models/marketmodels/callability/swapbasissystem.hpp>
#include <ql/models/marketmodels/callability/swapforwardbasissystem.hpp>
#include <ql/models/marketmodels/callability/swapratetrigger.hpp>
#include <ql/models/marketmodels/callability/triggeredswapexercise.hpp>
#include <ql/models/marketmodels/evolutiondescription.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdrateballand.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdrateeuler.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdrateipc.hpp>
#include <ql/models/marketmodels/evolvers/lognormalfwdratepc.hpp>
#include <ql/models/marketmodels/evolvers/normalfwdratepc.hpp>

#include <valarray>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        std::pair<int, Real> payoff(const json::value& x) {
            if (!x.is_array() || x.get_array().size() != 2)
                throw InvalidParams("a payoff is [optionType, strike]");
            int type = static_cast<int>(integer(x.get_array()[0]));
            if (type != 1 && type != -1)
                throw InvalidParams("an option type is 1 or -1");
            return {type, real(x.get_array()[1])};
        }

        // The rate index each exercise time reads, QuantLib's basis systems' and triggers' loop.
        std::vector<Size> rateIndices(const Vec& rateTimes, const Vec& exerciseTimes) {
            std::vector<Size> out(exerciseTimes.size());
            Size j = 0;
            for (Size i = 0; i < exerciseTimes.size(); ++i) {
                while (j < rateTimes.size() && rateTimes[j] < exerciseTimes[i])
                    ++j;
                out[i] = j;
            }
            return out;
        }

        // Rows of any lengths: the coefficients' and parameters', one an exercise.
        Mat jagged(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of rows");
            Mat out;
            for (const json::value& r : v.get_array())
                out.push_back(reals(r));
            return out;
        }

        // An exercise reads a rate from its index: refused at or past the last rate time, which QuantLib reads past.
        void checkExerciseTimes(const Vec& rateTimes, const Vec& exerciseTimes) {
            if (rateTimes.size() < 2)
                throw InvalidParams("two rate times at least");
            for (Size r : rateIndices(rateTimes, exerciseTimes))
                if (r + 1 >= rateTimes.size())
                    throw InvalidParams("every exercise time must come before the last rate time");
        }

    }

    ExerciseValueSpec exerciseValue(const json::value& v) {
        const std::string what = "an exercise value";
        const json::object& o = object(v, what);
        ExerciseValueSpec s;
        s.type = text(key(o, "type", what));
        if (s.type == "BermudanSwaptionExerciseValue") {
            onlyKeys(o, {"type", "rateTimes", "payoffs"}, what);
            s.rateTimes = reals(key(o, "rateTimes", what));
            const json::value& p = key(o, "payoffs", what);
            if (!p.is_array())
                throw InvalidParams("expected an array of payoffs");
            for (const json::value& x : p.get_array())
                s.payoffs.push_back(payoff(x));
            if (s.rateTimes.size() < 2 || s.payoffs.size() + 1 < s.rateTimes.size())
                throw InvalidParams("a payoff an exercise, every rate time but the last");
        } else if (s.type == "NothingExerciseValue") {
            onlyKeys(o, {"type", "rateTimes", "isExerciseTime"}, what);
            s.rateTimes = reals(key(o, "rateTimes", what));
            const json::value& f = key(o, "isExerciseTime", what);
            if (!f.is_array())
                throw InvalidParams("expected an array of flags");
            for (const json::value& x : f.get_array())
                s.isExerciseTime.push_back(boolean(x));
        } else {
            throw InvalidParams("unknown exercise value type " + s.type);
        }
        return s;
    }

    std::unique_ptr<MarketModelExerciseValue> makeExerciseValue(const ExerciseValueSpec& s) {
        if (s.type == "BermudanSwaptionExerciseValue") {
            std::vector<ext::shared_ptr<Payoff>> payoffs;
            for (const auto& x : s.payoffs)
                payoffs.push_back(ext::make_shared<PlainVanillaPayoff>(Option::Type(x.first), x.second));
            return std::make_unique<BermudanSwaptionExerciseValue>(s.rateTimes, payoffs);
        }
        std::valarray<bool> flags(s.isExerciseTime.size());
        for (Size i = 0; i < s.isExerciseTime.size(); ++i)
            flags[i] = s.isExerciseTime[i];
        return std::make_unique<NothingExerciseValue>(s.rateTimes, flags);
    }

    ProviderSpec provider(const json::value& v) {
        const std::string what = "a node-data provider";
        const json::object& o = object(v, what);
        ProviderSpec s;
        s.type = text(key(o, "type", what));
        if (s.type == "SwapBasisSystem" || s.type == "SwapForwardBasisSystem") {
            onlyKeys(o, {"type", "rateTimes", "exerciseTimes"}, what);
        } else if (s.type == "TriggeredSwapExercise") {
            onlyKeys(o, {"type", "rateTimes", "exerciseTimes", "strikes"}, what);
            s.strikes = reals(key(o, "strikes", what));
        } else {
            throw InvalidParams("unknown node-data provider type " + s.type);
        }
        s.rateTimes = reals(key(o, "rateTimes", what));
        s.exerciseTimes = reals(key(o, "exerciseTimes", what));
        checkExerciseTimes(s.rateTimes, s.exerciseTimes);
        return s;
    }

    std::unique_ptr<MarketModelBasisSystem> makeBasisSystem(const ProviderSpec& s) {
        if (s.type == "SwapBasisSystem")
            return std::make_unique<SwapBasisSystem>(s.rateTimes, s.exerciseTimes);
        if (s.type == "SwapForwardBasisSystem")
            return std::make_unique<SwapForwardBasisSystem>(s.rateTimes, s.exerciseTimes);
        throw InvalidParams("a basis system is a SwapBasisSystem or a SwapForwardBasisSystem");
    }

    std::unique_ptr<MarketModelParametricExercise> makeParametric(const ProviderSpec& s) {
        if (s.type != "TriggeredSwapExercise")
            throw InvalidParams("a parametric exercise is a TriggeredSwapExercise");
        return std::make_unique<TriggeredSwapExercise>(s.rateTimes, s.exerciseTimes, s.strikes);
    }

    std::unique_ptr<MarketModelNodeDataProvider> makeProvider(const ProviderSpec& s) {
        if (s.type == "TriggeredSwapExercise")
            return makeParametric(s);
        return makeBasisSystem(s);
    }

    StrategySpec strategy(const json::value& v) {
        const std::string what = "an exercise strategy";
        const json::object& o = object(v, what);
        StrategySpec s;
        s.type = text(key(o, "type", what));
        if (s.type == "SwapRateTrigger") {
            onlyKeys(o, {"type", "rateTimes", "swapTriggers", "exerciseTimes"}, what);
            s.rateTimes = reals(key(o, "rateTimes", what));
            s.swapTriggers = reals(key(o, "swapTriggers", what));
            s.exerciseTimes = reals(key(o, "exerciseTimes", what));
            checkExerciseTimes(s.rateTimes, s.exerciseTimes);
        } else if (s.type == "LongstaffSchwartzExerciseStrategy") {
            onlyKeys(o, {"type", "basisSystem", "basisCoefficients", "rateTimes", "evolutionTimes", "numeraires",
                         "exercise", "control"}, what);
            s.basisSystem = provider(key(o, "basisSystem", what));
            s.coefficients = jagged(key(o, "basisCoefficients", what));
            s.rateTimes = reals(key(o, "rateTimes", what));
            s.evolutionTimes = reals(key(o, "evolutionTimes", what));
            s.numeraires = sizes(key(o, "numeraires", what));
            s.exercise = std::make_shared<ExerciseValueSpec>(exerciseValue(key(o, "exercise", what)));
            s.control = std::make_shared<ExerciseValueSpec>(exerciseValue(key(o, "control", what)));
            std::unique_ptr<MarketModelBasisSystem> b = makeBasisSystem(s.basisSystem);
            std::vector<Size> sizes = b->numberOfFunctions();
            if (s.coefficients.size() < sizes.size())
                throw InvalidParams("a coefficient vector an exercise of the basis system");
            for (Size i = 0; i < sizes.size(); ++i)
                if (s.coefficients[i].size() > sizes[i])
                    throw InvalidParams("an exercise's coefficients at most its basis functions");
        } else if (s.type == "ParametricExerciseAdapter") {
            onlyKeys(o, {"type", "exercise", "parameters"}, what);
            s.parametric = provider(key(o, "exercise", what));
            s.parameters = jagged(key(o, "parameters", what));
            if (s.parametric.type != "TriggeredSwapExercise")
                throw InvalidParams("the adapter's exercise is a TriggeredSwapExercise");
            if (s.parameters.size() < s.parametric.exerciseTimes.size())
                throw InvalidParams("a parameter vector an exercise");
            for (const Vec& p : s.parameters)
                if (p.empty())
                    throw InvalidParams("an exercise's parameter vector holds its trigger");
        } else {
            throw InvalidParams("unknown strategy type " + s.type);
        }
        return s;
    }

    std::unique_ptr<ExerciseStrategy<CurveState>> makeStrategy(const StrategySpec& s) {
        if (s.type == "SwapRateTrigger")
            return std::make_unique<SwapRateTrigger>(s.rateTimes, s.swapTriggers, s.exerciseTimes);
        if (s.type == "LongstaffSchwartzExerciseStrategy") {
            std::unique_ptr<MarketModelBasisSystem> b = makeBasisSystem(s.basisSystem);
            std::unique_ptr<MarketModelExerciseValue> e = makeExerciseValue(*s.exercise);
            std::unique_ptr<MarketModelExerciseValue> c = makeExerciseValue(*s.control);
            return std::make_unique<LongstaffSchwartzExerciseStrategy>(
                *b, s.coefficients, EvolutionDescription(s.rateTimes, s.evolutionTimes), s.numeraires, *e, *c);
        }
        std::unique_ptr<MarketModelParametricExercise> p = makeParametric(s.parametric);
        return std::make_unique<ParametricExerciseAdapter>(*p, s.parameters);
    }

    EvolverSpec evolver(const json::value& v) {
        if (!v.is_array() || v.get_array().size() != 5)
            throw InvalidParams("an evolver is [type, model, generator, numeraires, initialStep]");
        const json::array& a = v.get_array();
        EvolverSpec s;
        s.type = text(a[0]);
        if (s.type != "LogNormalFwdRatePc" && s.type != "LogNormalFwdRateEuler" && s.type != "LogNormalFwdRateIpc" &&
            s.type != "LogNormalFwdRateBalland" && s.type != "NormalFwdRatePc")
            throw InvalidParams("unknown evolver type " + s.type);
        s.model = model(a[1]);
        s.generator = generator(a[2]);
        s.numeraires = sizes(a[3]);
        s.initialStep = size(a[4]);
        return s;
    }

    ext::shared_ptr<MarketModelEvolver> makeEvolver(const EvolverSpec& s) {
        ext::shared_ptr<MarketModel> mm = makeModel(s.model);
        std::unique_ptr<BrownianGeneratorFactory> f = makeFactory(s.generator);
        if (s.type == "LogNormalFwdRatePc")
            return ext::make_shared<LogNormalFwdRatePc>(mm, *f, s.numeraires, s.initialStep);
        if (s.type == "LogNormalFwdRateEuler")
            return ext::make_shared<LogNormalFwdRateEuler>(mm, *f, s.numeraires, s.initialStep);
        if (s.type == "LogNormalFwdRateIpc")
            return ext::make_shared<LogNormalFwdRateIpc>(mm, *f, s.numeraires, s.initialStep);
        if (s.type == "LogNormalFwdRateBalland")
            return ext::make_shared<LogNormalFwdRateBalland>(mm, *f, s.numeraires, s.initialStep);
        return ext::make_shared<NormalFwdRatePc>(mm, *f, s.numeraires, s.initialStep);
    }

}
