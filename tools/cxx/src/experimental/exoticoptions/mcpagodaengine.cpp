// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/exoticoptions/pagodaoption.hpp, .cpp and mcpagodaengine.hpp, .cpp, which the wheel doesn't expose.
// "MCPagodaEngine" takes (traits, processes, correlation, fixingDays, roof, fraction, antithetic, samples, seed):
// Black-Scholes-Merton processes, each [spot, r, q, volatility] on flat Act/365 (Fixed) forwards and a constant
// volatility from the evaluation date, in a StochasticProcessArray at that correlation off the diagonal; the pagoda
// option on fixings that many days after the evaluation date; MakeMCPagodaEngine on PseudoRandom ("pr") or
// LowDiscrepancy ("ld") with Statistics, the antithetic choice, that many samples and the seed. It returns {"NPV"}, and
// "errorEstimate" under the pseudo-random traits; the option's or the engine's refusal is QuantLib's.

#include "convert.hpp"

#include <ql/experimental/exoticoptions/mcpagodaengine.hpp>
#include <ql/experimental/exoticoptions/pagodaoption.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/processes/stochasticprocessarray.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

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

        template <class RNG>
        json::value priced(const std::vector<std::vector<Real>>& specs, Real correlation,
                           const std::vector<Integer>& days, Real roof, Real fraction, bool antithetic,
                           Integer samples, Integer seed, bool errorEstimate) {
            const Date today = Settings::instance().evaluationDate();
            std::vector<Date> fixings;
            for (Integer d : days)
                fixings.push_back(today + d);
            PagodaOption option(fixings, roof, fraction);
            ext::shared_ptr<PricingEngine> engine = MakeMCPagodaEngine<RNG>(processArray(specs, correlation))
                                                        .withAntitheticVariate(antithetic)
                                                        .withSamples(static_cast<Size>(samples))
                                                        .withSeed(static_cast<BigNatural>(seed));
            option.setPricingEngine(engine);
            json::object o;
            o["NPV"] = json::value(option.NPV());
            if (errorEstimate)
                o["errorEstimate"] = json::value(option.errorEstimate());
            return json::value(o);
        }

        const Register unit({
            {"MCPagodaEngine",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 std::string traits = text(a[0]);
                 if (traits != "pr" && traits != "ld")
                     throw InvalidParams("unknown traits " + traits);
                 std::vector<std::vector<Real>> specs = processSpecs(a[1]);
                 Real correlation = real(a[2]);
                 std::vector<Integer> days = integers(a[3]);
                 Real roof = real(a[4]);
                 Real fraction = real(a[5]);
                 bool antithetic = boolean(a[6]);
                 Integer samples = integer(a[7]);
                 Integer seed = integer(a[8]);
                 return [=] {
                     return traits == "pr"
                                ? priced<PseudoRandom>(specs, correlation, days, roof, fraction, antithetic, samples,
                                                       seed, true)
                                : priced<LowDiscrepancy>(specs, correlation, days, roof, fraction, antithetic,
                                                         samples, seed, false);
                 };
             }},
        });

    }

}
