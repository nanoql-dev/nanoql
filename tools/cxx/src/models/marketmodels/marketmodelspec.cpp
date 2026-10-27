// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/models/marketmodels/browniangenerators/mtbrowniangenerator.hpp>
#include <ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.hpp>
#include <ql/models/marketmodels/correlations/expcorrelations.hpp>
#include <ql/models/marketmodels/correlations/timehomogeneousforwardcorrelation.hpp>
#include <ql/models/marketmodels/evolutiondescription.hpp>
#include <ql/models/marketmodels/models/abcdvol.hpp>
#include <ql/models/marketmodels/models/flatvol.hpp>

#include <cstdint>
#include <initializer_list>

using namespace QuantLib;

namespace harness::marketmodels {

    const json::object& object(const json::value& v, const std::string& what) {
        if (!v.is_object())
            throw InvalidParams("expected " + what + " spec, got " + json::serialize(v));
        return v.get_object();
    }

    const json::value& key(const json::object& o, const char* name, const std::string& what) {
        const json::value* v = o.if_contains(name);
        if (v == nullptr)
            throw InvalidParams(what + " spec needs " + name);
        return *v;
    }

    void onlyKeys(const json::object& o, std::initializer_list<const char*> keys, const std::string& what) {
        for (const auto& kv : o) {
            bool known = false;
            for (const char* k : keys)
                known = known || kv.key() == k;
            if (!known)
                throw InvalidParams(what + " takes no key " + std::string(kv.key()));
        }
    }

    Mat matrix(const json::value& v) {
        if (!v.is_array())
            throw InvalidParams("expected a matrix, got " + json::serialize(v));
        Mat m;
        for (const json::value& row : v.get_array())
            m.push_back(reals(row));
        for (const auto& row : m)
            if (row.size() != m.front().size())
                throw InvalidParams("a matrix's rows must have one length");
        return m;
    }

    Matrix makeMatrix(const Mat& m) {
        Size rows = m.size(), columns = m.empty() ? 0 : m.front().size();
        Matrix result(rows, columns);
        for (Size i = 0; i < rows; ++i)
            for (Size j = 0; j < columns; ++j)
                result[i][j] = m[i][j];
        return result;
    }

    Size size(const json::value& v) {
        Integer n = integer(v);
        if (n == Null<Integer>() || n < 0)
            throw InvalidParams("expected a non-negative integer, got " + json::serialize(v));
        return static_cast<Size>(n);
    }

    std::vector<Size> sizes(const json::value& v) {
        std::vector<Size> out;
        for (Integer n : integers(v)) {
            if (n == Null<Integer>() || n < 0)
                throw InvalidParams("expected non-negative integers, got " + json::serialize(v));
            out.push_back(static_cast<Size>(n));
        }
        return out;
    }

    json::value result(const Matrix& m) {
        json::array rows;
        for (Size i = 0; i < m.rows(); ++i) {
            json::array r;
            for (Size j = 0; j < m.columns(); ++j)
                r.emplace_back(m[i][j]);
            rows.emplace_back(r);
        }
        return rows;
    }

    json::value result(const std::vector<Real>& v) {
        json::array a;
        for (Real x : v)
            a.emplace_back(x);
        return a;
    }

    json::value result(const std::vector<Size>& v) {
        json::array a;
        for (Size x : v)
            a.emplace_back(static_cast<std::int64_t>(x));
        return a;
    }

    CorrelationSpec correlation(const json::value& v) {
        const std::string what = "a correlation";
        const json::object& o = object(v, what);
        CorrelationSpec c;
        c.type = text(key(o, "type", what));
        if (c.type == "Exponential") {
            onlyKeys(o, {"type", "rateTimes", "longTermCorr", "beta", "gamma", "times"}, what);
            c.rateTimes = reals(key(o, "rateTimes", what));
            c.longTermCorr = real(key(o, "longTermCorr", what));
            c.beta = real(key(o, "beta", what));
            c.gamma = real(key(o, "gamma", what));
            c.times = reals(key(o, "times", what));
        } else if (c.type == "TimeHomogeneous") {
            onlyKeys(o, {"type", "fwdCorrelation", "rateTimes"}, what);
            c.fwdCorrelation = matrix(key(o, "fwdCorrelation", what));
            c.rateTimes = reals(key(o, "rateTimes", what));
        } else {
            throw InvalidParams("unknown correlation type " + c.type);
        }
        return c;
    }

    ext::shared_ptr<PiecewiseConstantCorrelation> makeCorrelation(const CorrelationSpec& c) {
        if (c.type == "Exponential")
            return ext::make_shared<ExponentialForwardCorrelation>(c.rateTimes, c.longTermCorr, c.beta, c.gamma,
                                                                   c.times);
        return ext::make_shared<TimeHomogeneousForwardCorrelation>(makeMatrix(c.fwdCorrelation), c.rateTimes);
    }

    ModelSpec model(const json::value& v) {
        const std::string what = "a model";
        const json::object& o = object(v, what);
        ModelSpec m;
        m.type = text(key(o, "type", what));
        if (m.type == "FlatVol") {
            onlyKeys(o, {"type", "vols", "corr", "rateTimes", "evolutionTimes", "factors", "initialRates",
                         "displacements"}, what);
            m.vols = reals(key(o, "vols", what));
        } else if (m.type == "AbcdVol") {
            onlyKeys(o, {"type", "abcd", "ks", "corr", "rateTimes", "evolutionTimes", "factors", "initialRates",
                         "displacements"}, what);
            m.abcd = reals(key(o, "abcd", what));
            if (m.abcd.size() != 4)
                throw InvalidParams("an AbcdVol's abcd is four reals");
            m.vols = reals(key(o, "ks", what));
        } else {
            throw InvalidParams("unknown model type " + m.type);
        }
        m.corr = correlation(key(o, "corr", what));
        m.rateTimes = reals(key(o, "rateTimes", what));
        m.evolutionTimes = reals(key(o, "evolutionTimes", what));
        m.factors = size(key(o, "factors", what));
        m.initialRates = reals(key(o, "initialRates", what));
        m.displacements = reals(key(o, "displacements", what));
        return m;
    }

    ext::shared_ptr<MarketModel> makeModel(const ModelSpec& m) {
        EvolutionDescription evolution(m.rateTimes, m.evolutionTimes);
        if (m.type == "AbcdVol")
            return ext::make_shared<AbcdVol>(m.abcd[0], m.abcd[1], m.abcd[2], m.abcd[3], m.vols, makeCorrelation(m.corr),
                                             evolution, m.factors, m.initialRates, m.displacements);
        return ext::make_shared<FlatVol>(m.vols, makeCorrelation(m.corr), evolution, m.factors, m.initialRates,
                                         m.displacements);
    }

    GeneratorSpec generator(const json::value& v) {
        const std::string what = "a Brownian generator factory";
        const json::object& o = object(v, what);
        GeneratorSpec g;
        g.type = text(key(o, "type", what));
        long long seed = int64(key(o, "seed", what));
        if (seed < 1 || seed > 4294967295LL)
            throw InvalidParams("a seed must be in [1, 4294967295]");
        g.seed = static_cast<unsigned long>(seed);
        if (g.type == "MT") {
            onlyKeys(o, {"type", "seed"}, what);
        } else if (g.type == "Sobol") {
            onlyKeys(o, {"type", "ordering", "seed"}, what);
            g.ordering = integer(key(o, "ordering", what));
        } else if (g.type == "Burley") {
            onlyKeys(o, {"type", "ordering", "seed", "scrambleSeed"}, what);
            g.ordering = integer(key(o, "ordering", what));
            long long scrambleSeed = int64(key(o, "scrambleSeed", what));
            if (scrambleSeed < 1 || scrambleSeed > 4294967295LL)
                throw InvalidParams("a seed must be in [1, 4294967295]");
            g.scrambleSeed = static_cast<unsigned long>(scrambleSeed);
        } else {
            throw InvalidParams("unknown generator type " + g.type);
        }
        return g;
    }

    std::unique_ptr<BrownianGeneratorFactory> makeFactory(const GeneratorSpec& g) {
        if (g.type == "MT")
            return std::make_unique<MTBrownianGeneratorFactory>(g.seed);
        if (g.type == "Burley")
            return std::make_unique<Burley2020SobolBrownianGeneratorFactory>(
                static_cast<SobolBrownianGenerator::Ordering>(g.ordering), g.seed, SobolRsg::Jaeckel, g.scrambleSeed);
        return std::make_unique<SobolBrownianGeneratorFactory>(
            static_cast<SobolBrownianGenerator::Ordering>(g.ordering), g.seed);
    }

    json::value modelResults(const MarketModel& m) {
        json::object out;
        out["numberOfRates"] = static_cast<std::int64_t>(m.numberOfRates());
        out["numberOfFactors"] = static_cast<std::int64_t>(m.numberOfFactors());
        out["numberOfSteps"] = static_cast<std::int64_t>(m.numberOfSteps());
        out["initialRates"] = result(m.initialRates());
        out["displacements"] = result(m.displacements());
        json::array roots, covariances, totals, vols;
        for (Size j = 0; j < m.numberOfSteps(); ++j) {
            roots.emplace_back(result(m.pseudoRoot(j)));
            covariances.emplace_back(result(m.covariance(j)));
            totals.emplace_back(result(m.totalCovariance(j)));
        }
        for (Size i = 0; i < m.numberOfRates(); ++i)
            vols.emplace_back(result(m.timeDependentVolatility(i)));
        out["pseudoRoots"] = roots;
        out["covariances"] = covariances;
        out["totalCovariances"] = totals;
        out["timeDependentVolatilities"] = vols;
        return out;
    }

    json::value stateReads(const CurveState& cs, Size first, const std::vector<Size>& spans) {
        Size n = cs.numberOfRates();
        json::array discountRatios, forwardRates, cotRates, cotAnnuities;
        for (Size i = first; i <= n; ++i)
            discountRatios.emplace_back(cs.discountRatio(i, n));
        for (Size i = first; i < n; ++i) {
            forwardRates.emplace_back(cs.forwardRate(i));
            cotRates.emplace_back(cs.coterminalSwapRate(i));
            cotAnnuities.emplace_back(cs.coterminalSwapAnnuity(n, i));
        }
        json::array cmRates, cmAnnuities, cmVectors;
        for (Size span : spans) {
            json::array rates, annuities;
            for (Size i = first; i < n; ++i) {
                rates.emplace_back(cs.cmSwapRate(i, span));
                annuities.emplace_back(cs.cmSwapAnnuity(n, i, span));
            }
            cmRates.emplace_back(rates);
            cmAnnuities.emplace_back(annuities);
            const std::vector<Rate>& v = cs.cmSwapRates(span);
            cmVectors.emplace_back(result(std::vector<Real>(v.begin() + first, v.end())));
        }
        json::object out;
        out["discountRatios"] = discountRatios;
        out["forwardRates"] = forwardRates;
        out["coterminalSwapRates"] = cotRates;
        out["coterminalSwapAnnuities"] = cotAnnuities;
        out["cmSwapRates"] = cmRates;
        out["cmSwapAnnuities"] = cmAnnuities;
        const std::vector<Rate>& f = cs.forwardRates();
        out["forwardRatesVector"] = result(std::vector<Real>(f.begin() + first, f.end()));
        const std::vector<Rate>& c = cs.coterminalSwapRates();
        out["coterminalSwapRatesVector"] = result(std::vector<Real>(c.begin() + first, c.end()));
        out["cmSwapRatesVectors"] = cmVectors;
        return out;
    }

}
