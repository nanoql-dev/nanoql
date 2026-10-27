// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The market-model callability specs the harness's callability units share (ql/models/marketmodels/callability), with
// the names of tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MarketModelCallabilityCxxInputs.cs.
//
// An exercise value is {"type": "BermudanSwaptionExerciseValue", "rateTimes", "payoffs"} (each payoff [optionType,
// strike]) or {"type": "NothingExerciseValue", "rateTimes", "isExerciseTime"} (empty for every rate time but the
// last). A node-data provider is {"type": "SwapBasisSystem" or "SwapForwardBasisSystem", "rateTimes",
// "exerciseTimes"} or {"type": "TriggeredSwapExercise", "rateTimes", "exerciseTimes", "strikes"}. A strategy is
// {"type": "SwapRateTrigger", "rateTimes", "swapTriggers", "exerciseTimes"}, {"type":
// "LongstaffSchwartzExerciseStrategy", "basisSystem", "basisCoefficients", "rateTimes", "evolutionTimes",
// "numeraires", "exercise", "control"} (its evolution EvolutionDescription(rateTimes, evolutionTimes)) or {"type":
// "ParametricExerciseAdapter", "exercise", "parameters"} (the exercise a TriggeredSwapExercise). An evolver is
// [type, model, generator, numeraires, initialStep], the type "LogNormalFwdRatePc", "LogNormalFwdRateEuler",
// "LogNormalFwdRateIpc", "LogNormalFwdRateBalland" or "NormalFwdRatePc" (marketmodelspec.hpp's model and generator).

#ifndef NANOQL_CXX_MODELS_MARKETMODELS_CALLABILITY_CALLABILITYSPEC_HPP
#define NANOQL_CXX_MODELS_MARKETMODELS_CALLABILITY_CALLABILITYSPEC_HPP

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/methods/montecarlo/exercisestrategy.hpp>
#include <ql/models/marketmodels/callability/exercisevalue.hpp>
#include <ql/models/marketmodels/callability/marketmodelbasissystem.hpp>
#include <ql/models/marketmodels/callability/marketmodelparametricexercise.hpp>
#include <ql/models/marketmodels/curvestate.hpp>
#include <ql/models/marketmodels/evolver.hpp>

#include <memory>
#include <string>
#include <utility>
#include <vector>

namespace harness::marketmodels {

    struct ExerciseValueSpec {
        std::string type;
        Vec rateTimes;
        std::vector<std::pair<int, QuantLib::Real>> payoffs;
        std::vector<bool> isExerciseTime;
    };
    ExerciseValueSpec exerciseValue(const json::value& v);
    std::unique_ptr<QuantLib::MarketModelExerciseValue> makeExerciseValue(const ExerciseValueSpec& s);

    struct ProviderSpec {
        std::string type;
        Vec rateTimes, exerciseTimes, strikes;
    };
    ProviderSpec provider(const json::value& v);
    std::unique_ptr<QuantLib::MarketModelNodeDataProvider> makeProvider(const ProviderSpec& s);
    std::unique_ptr<QuantLib::MarketModelBasisSystem> makeBasisSystem(const ProviderSpec& s);
    std::unique_ptr<QuantLib::MarketModelParametricExercise> makeParametric(const ProviderSpec& s);

    struct StrategySpec {
        std::string type;
        Vec rateTimes, swapTriggers, exerciseTimes, evolutionTimes;
        ProviderSpec basisSystem, parametric;
        Mat coefficients, parameters;
        std::vector<QuantLib::Size> numeraires;
        std::shared_ptr<ExerciseValueSpec> exercise, control;
    };
    StrategySpec strategy(const json::value& v);
    std::unique_ptr<QuantLib::ExerciseStrategy<QuantLib::CurveState>> makeStrategy(const StrategySpec& s);

    struct EvolverSpec {
        std::string type;
        ModelSpec model;
        GeneratorSpec generator;
        std::vector<QuantLib::Size> numeraires;
        QuantLib::Size initialStep = 0;
    };
    EvolverSpec evolver(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::MarketModelEvolver> makeEvolver(const EvolverSpec& s);

}

#endif
