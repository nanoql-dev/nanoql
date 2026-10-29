// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/mcbasket/mcpathbasketengine.hpp, .cpp, mcamericanpathengine.hpp, .cpp,
// longstaffschwartzmultipathpricer.hpp, .cpp, pathpayoff.hpp, adaptedpathpayoff.hpp, .cpp and pathmultiassetoption.hpp,
// .cpp, which the wheel doesn't expose, on payoffs of the harness's own. A payoff is [kind, strike]: "AverageCall", the
// assets' average less the strike at the last fixing, floored at 0; "AsianBasketCall", the average over every fixing
// and asset less the strike at the last fixing, floored at 0; "BermudanMaxPut", the strike less the largest asset at any
// fixing, floored at 0, its regression state the assets' values (the American engine's payoff only). Two derive from
// AdaptedPathPayoff, reading and writing through its valuation data: "AdaptedAverageCall", the average call times the
// discount over a quarter of a year on the curve at the last fixing; "AdaptedBermudanMaxPut", the Bermudan maximum put
// (the American engine's payoff only). Each payoff's basis dimension is the number of assets. The processes are Black-Scholes-Merton processes, each [spot, r, q, volatility] on flat Act/365
// (Fixed) forwards and a constant volatility from the evaluation date, in a StochasticProcessArray at that correlation
// off the diagonal; the option fixes that many days after the evaluation date.
//
// "MCPathBasketEngine" takes (traits, processes, correlation, fixingDays, payoff, steps, stepsPerYear, antithetic,
// samples, seed): MakeMCPathBasketEngine on PseudoRandom ("pr") or LowDiscrepancy ("ld") with Statistics, with the steps
// or the steps per year (the other null), the antithetic choice, that many samples and the seed. "MCAmericanPathEngine"
// takes (processes, correlation, fixingDays, payoff, steps, stepsPerYear, antithetic, samples, seed,
// calibrationSamples): MakeMCAmericanPathEngine on PseudoRandom, likewise, with that many calibration samples (null:
// QuantLib's default). Each returns {"NPV"}, and "errorEstimate" under the pseudo-random traits; a refusal is
// QuantLib's.

#include "convert.hpp"

#include <ql/experimental/mcbasket/adaptedpathpayoff.hpp>
#include <ql/experimental/mcbasket/mcamericanpathengine.hpp>
#include <ql/experimental/mcbasket/mcpathbasketengine.hpp>
#include <ql/experimental/mcbasket/pathmultiassetoption.hpp>
#include <ql/experimental/mcbasket/pathpayoff.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/processes/stochasticprocessarray.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <algorithm>
#include <string>
#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        class HarnessPayoff : public PathPayoff {
          public:
            HarnessPayoff(std::string kind, Real strike, Size assets)
            : kind_(std::move(kind)), strike_(strike), assets_(assets) {}
            std::string name() const override { return kind_; }
            std::string description() const override { return kind_; }
            Size basisSystemDimension() const override { return assets_; }
            void value(const Matrix& path,
                       const std::vector<Handle<YieldTermStructure>>&,
                       Array& payments,
                       Array& exercises,
                       std::vector<Array>& states) const override {
                const Size n = path.rows(), m = path.columns();
                if (kind_ == "AverageCall") {
                    Real sum = 0.0;
                    for (Size j = 0; j < n; ++j)
                        sum += path[j][m - 1];
                    payments[m - 1] = std::max(sum / n - strike_, 0.0);
                } else if (kind_ == "AsianBasketCall") {
                    Real sum = 0.0;
                    for (Size i = 0; i < m; ++i)
                        for (Size j = 0; j < n; ++j)
                            sum += path[j][i];
                    payments[m - 1] = std::max(sum / (n * m) - strike_, 0.0);
                } else {
                    for (Size i = 0; i < m; ++i) {
                        Real best = path[0][i];
                        for (Size j = 1; j < n; ++j)
                            best = std::max(best, path[j][i]);
                        payments[i] = 0.0;
                        exercises[i] = std::max(strike_ - best, 0.0);
                        Array state(n);
                        for (Size j = 0; j < n; ++j)
                            state[j] = path[j][i];
                        states[i] = state;
                    }
                }
            }

          private:
            std::string kind_;
            Real strike_;
            Size assets_;
        };

        class HarnessAdaptedPayoff : public AdaptedPathPayoff {
          public:
            HarnessAdaptedPayoff(std::string kind, Real strike, Size assets)
            : kind_(std::move(kind)), strike_(strike), assets_(assets) {}
            std::string name() const override { return kind_; }
            std::string description() const override { return kind_; }
            Size basisSystemDimension() const override { return assets_; }

          protected:
            void operator()(ValuationData& data) const override {
                const Size n = data.numberOfAssets(), m = data.numberOfTimes();
                if (kind_ == "AdaptedAverageCall") {
                    Real sum = 0.0;
                    for (Size j = 0; j < n; ++j)
                        sum += data.getAssetValue(m - 1, j);
                    Real deferral = data.getYieldTermStructure(m - 1)->discount(0.25);
                    data.setPayoffValue(m - 1, std::max(sum / n - strike_, 0.0) * deferral);
                } else {
                    for (Size i = 0; i < m; ++i) {
                        Array state(n);
                        state[0] = data.getAssetValue(i, 0);
                        Real best = state[0];
                        for (Size j = 1; j < n; ++j) {
                            state[j] = data.getAssetValue(i, j);
                            best = std::max(best, state[j]);
                        }
                        data.setExerciseData(i, std::max(strike_ - best, 0.0), state);
                    }
                }
            }

          private:
            std::string kind_;
            Real strike_;
            Size assets_;
        };

        class HarnessPathOption : public PathMultiAssetOption {
          public:
            HarnessPathOption(ext::shared_ptr<PathPayoff> payoff, std::vector<Date> dates)
            : payoff_(std::move(payoff)), dates_(std::move(dates)) {}
            ext::shared_ptr<PathPayoff> pathPayoff() const override { return payoff_; }
            std::vector<Date> fixingDates() const override { return dates_; }

          private:
            ext::shared_ptr<PathPayoff> payoff_;
            std::vector<Date> dates_;
        };

        std::vector<std::vector<Real>> processSpecs(const json::value& v) {
            if (!v.is_array() || v.get_array().empty())
                throw InvalidParams("expected a list of [spot, r, q, volatility], got " + json::serialize(v));
            std::vector<std::vector<Real>> specs;
            for (const json::value& p : v.get_array()) {
                std::vector<Real> s = reals(p);
                if (s.size() != 4)
                    throw InvalidParams("expected [spot, r, q, volatility], got " + json::serialize(p));
                specs.push_back(s);
            }
            return specs;
        }

        ext::shared_ptr<StochasticProcessArray> processArray(const std::vector<std::vector<Real>>& specs,
                                                             Real correlation) {
            const Date today = Settings::instance().evaluationDate();
            const DayCounter dc = Actual365Fixed();
            std::vector<ext::shared_ptr<StochasticProcess1D>> processes;
            for (const std::vector<Real>& s : specs)
                processes.push_back(ext::make_shared<BlackScholesMertonProcess>(
                    Handle<Quote>(ext::make_shared<SimpleQuote>(s[0])),
                    Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, s[2], dc)),
                    Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, s[1], dc)),
                    Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(today, NullCalendar(), s[3], dc))));
            Matrix rho(processes.size(), processes.size(), correlation);
            for (Size i = 0; i < processes.size(); ++i)
                rho[i][i] = 1.0;
            return ext::make_shared<StochasticProcessArray>(processes, rho);
        }

        struct PayoffSpec {
            std::string kind;
            Real strike;
        };

        PayoffSpec payoffSpec(const json::value& v) {
            if (!v.is_array() || v.get_array().size() != 2)
                throw InvalidParams("expected [kind, strike], got " + json::serialize(v));
            PayoffSpec p{text(v.get_array()[0]), real(v.get_array()[1])};
            if (p.kind != "AverageCall" && p.kind != "AsianBasketCall" && p.kind != "BermudanMaxPut" &&
                p.kind != "AdaptedAverageCall" && p.kind != "AdaptedBermudanMaxPut")
                throw InvalidParams("unknown payoff " + p.kind);
            return p;
        }

        HarnessPathOption option(const PayoffSpec& p, Size assets, const std::vector<Integer>& days) {
            const Date today = Settings::instance().evaluationDate();
            std::vector<Date> dates;
            for (Integer d : days)
                dates.push_back(today + d);
            ext::shared_ptr<PathPayoff> payoff;
            if (p.kind.rfind("Adapted", 0) == 0)
                payoff = ext::make_shared<HarnessAdaptedPayoff>(p.kind, p.strike, assets);
            else
                payoff = ext::make_shared<HarnessPayoff>(p.kind, p.strike, assets);
            return HarnessPathOption(payoff, dates);
        }

        json::value results(const Instrument& o, bool errorEstimate) {
            json::object out;
            out["NPV"] = json::value(o.NPV());
            if (errorEstimate)
                out["errorEstimate"] = json::value(o.errorEstimate());
            return json::value(out);
        }

        template <class RNG>
        json::value basket(const std::vector<std::vector<Real>>& specs, Real correlation,
                           const std::vector<Integer>& days, const PayoffSpec& p, Integer steps, Integer perYear,
                           bool antithetic, Integer samples, Integer seed, bool errorEstimate) {
            MakeMCPathBasketEngine<RNG> make(processArray(specs, correlation));
            if (steps != Null<Integer>())
                make.withSteps(static_cast<Size>(steps));
            if (perYear != Null<Integer>())
                make.withStepsPerYear(static_cast<Size>(perYear));
            make.withAntitheticVariate(antithetic)
                .withSamples(static_cast<Size>(samples))
                .withSeed(static_cast<BigNatural>(seed));
            ext::shared_ptr<PricingEngine> engine = make;
            HarnessPathOption o = option(p, specs.size(), days);
            o.setPricingEngine(engine);
            return results(o, errorEstimate);
        }

        const Register unit({
            {"MCPathBasketEngine",
             [](const Args& a) -> Call {
                 arity(a, 10);
                 std::string traits = text(a[0]);
                 if (traits != "pr" && traits != "ld")
                     throw InvalidParams("unknown traits " + traits);
                 std::vector<std::vector<Real>> specs = processSpecs(a[1]);
                 Real correlation = real(a[2]);
                 std::vector<Integer> days = integers(a[3]);
                 PayoffSpec p = payoffSpec(a[4]);
                 Integer steps = integer(a[5]);
                 Integer perYear = integer(a[6]);
                 bool antithetic = boolean(a[7]);
                 Integer samples = integer(a[8]);
                 Integer seed = integer(a[9]);
                 return [=] {
                     return traits == "pr" ? basket<PseudoRandom>(specs, correlation, days, p, steps, perYear,
                                                                   antithetic, samples, seed, true)
                                           : basket<LowDiscrepancy>(specs, correlation, days, p, steps, perYear,
                                                                    antithetic, samples, seed, false);
                 };
             }},
            {"MCAmericanPathEngine",
             [](const Args& a) -> Call {
                 arity(a, 10);
                 std::vector<std::vector<Real>> specs = processSpecs(a[0]);
                 Real correlation = real(a[1]);
                 std::vector<Integer> days = integers(a[2]);
                 PayoffSpec p = payoffSpec(a[3]);
                 Integer steps = integer(a[4]);
                 Integer perYear = integer(a[5]);
                 bool antithetic = boolean(a[6]);
                 Integer samples = integer(a[7]);
                 Integer seed = integer(a[8]);
                 Integer calibration = integer(a[9]);
                 return [=] {
                     MakeMCAmericanPathEngine<PseudoRandom> make(processArray(specs, correlation));
                     if (steps != Null<Integer>())
                         make.withSteps(static_cast<Size>(steps));
                     if (perYear != Null<Integer>())
                         make.withStepsPerYear(static_cast<Size>(perYear));
                     make.withAntitheticVariate(antithetic)
                         .withSamples(static_cast<Size>(samples))
                         .withSeed(static_cast<BigNatural>(seed));
                     if (calibration != Null<Integer>())
                         make.withCalibrationSamples(static_cast<Size>(calibration));
                     ext::shared_ptr<PricingEngine> engine = make;
                     HarnessPathOption o = option(p, specs.size(), days);
                     o.setPricingEngine(engine);
                     return results(o, true);
                 };
             }},
        });

    }

}
