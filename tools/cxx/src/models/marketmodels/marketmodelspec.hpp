// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The market-model specs the harness's market-model units share (ql/models/marketmodels), with the names of
// tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MarketModelCxxInputs.cs.
//
// A matrix is an array of equal-length arrays of reals, a row each. A correlation is {"type": "Exponential",
// "rateTimes", "longTermCorr", "beta", "gamma", "times"} (QuantLib's ExponentialForwardCorrelation, times empty for its
// default) or {"type": "TimeHomogeneous", "fwdCorrelation", "rateTimes"}. A model is {"type": "FlatVol", "vols",
// "corr", "rateTimes", "evolutionTimes", "factors", "initialRates", "displacements"} or {"type": "AbcdVol", "abcd" (a,
// b, c and d), "ks", and the rest as FlatVol's}, its evolution EvolutionDescription(rateTimes, evolutionTimes). A
// Brownian generator factory is {"type": "MT", "seed"}, {"type": "Sobol", "ordering", "seed"} (QuantLib's
// SobolBrownianGeneratorFactory with Jaeckel's direction integers) or {"type": "Burley", "ordering", "seed",
// "scrambleSeed"} (Burley2020SobolBrownianGeneratorFactory, Jaeckel's too).

#ifndef NANOQL_CXX_MODELS_MARKETMODELS_MARKETMODELSPEC_HPP
#define NANOQL_CXX_MODELS_MARKETMODELS_MARKETMODELSPEC_HPP

#include "convert.hpp"

#include <ql/math/matrix.hpp>
#include <ql/models/marketmodels/browniangenerator.hpp>
#include <ql/models/marketmodels/curvestate.hpp>
#include <ql/models/marketmodels/marketmodel.hpp>
#include <ql/models/marketmodels/piecewiseconstantcorrelation.hpp>
#include <ql/shared_ptr.hpp>

#include <memory>
#include <string>
#include <vector>

namespace harness::marketmodels {

    using Vec = std::vector<QuantLib::Real>;
    using Mat = std::vector<std::vector<QuantLib::Real>>;

    const json::object& object(const json::value& v, const std::string& what);
    const json::value& key(const json::object& o, const char* name, const std::string& what);
    void onlyKeys(const json::object& o, std::initializer_list<const char*> keys, const std::string& what);

    Mat matrix(const json::value& v);
    QuantLib::Matrix makeMatrix(const Mat& m);
    // An array of non-negative integers, QuantLib's Size.
    std::vector<QuantLib::Size> sizes(const json::value& v);
    QuantLib::Size size(const json::value& v);

    json::value result(const QuantLib::Matrix& m);
    json::value result(const std::vector<QuantLib::Real>& v);
    json::value result(const std::vector<QuantLib::Size>& v);

    struct CorrelationSpec {
        std::string type;
        Vec rateTimes;
        QuantLib::Real longTermCorr = 0.0, beta = 0.0, gamma = 0.0;
        Vec times;
        Mat fwdCorrelation;
    };
    CorrelationSpec correlation(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::PiecewiseConstantCorrelation> makeCorrelation(const CorrelationSpec& c);

    struct ModelSpec {
        std::string type;
        Vec vols;  // FlatVol's volatilities, AbcdVol's ks
        Vec abcd;
        CorrelationSpec corr;
        Vec rateTimes, evolutionTimes;
        QuantLib::Size factors = 0;
        Vec initialRates, displacements;
    };
    ModelSpec model(const json::value& v);
    QuantLib::ext::shared_ptr<QuantLib::MarketModel> makeModel(const ModelSpec& m);

    struct GeneratorSpec {
        std::string type;
        unsigned long seed = 0, scrambleSeed = 0;
        int ordering = 0;
    };
    GeneratorSpec generator(const json::value& v);
    std::unique_ptr<QuantLib::BrownianGeneratorFactory> makeFactory(const GeneratorSpec& g);

    // A model's reads: {"numberOfRates", "numberOfFactors", "numberOfSteps", "initialRates", "displacements",
    // "pseudoRoots", "covariances", "totalCovariances", "timeDependentVolatilities"}, a step's or a rate's each.
    json::value modelResults(const QuantLib::MarketModel& m);

    // A curve state's reads from its first valid index: {"discountRatios" (to the last bond, i from first to n),
    // "forwardRates", "coterminalSwapRates", "coterminalSwapAnnuities" (in the last bond), "cmSwapRates" and
    // "cmSwapAnnuities" (at each spanning given, in the last bond), "forwardRatesVector", "coterminalSwapRatesVector",
    // "cmSwapRatesVectors"}, the vectors' entries from the first valid index.
    json::value stateReads(const QuantLib::CurveState& cs, QuantLib::Size first, const std::vector<QuantLib::Size>& spans);

}

#endif
