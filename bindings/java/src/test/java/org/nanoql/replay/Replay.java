// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql.replay;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.PrintStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import org.nanoql.NanoQL;

/**
 * The Java replayer (docs/bindings.md, "The vector suite through each binding"): replays the recorder's trace through the
 * layer, one line a call, each C function through its Java member, and checks every result against C#'s. A fact replays
 * up to its first call that needs something the trace couldn't write; the run reports facts replayed whole, calls
 * replayed of calls recorded, refusals matched and mismatches, and passes at no mismatch and no live handle, and, given
 * B4's floor (bindings/replay-floor.json), at or above it. A fact stops where the layer can't make an input, its count
 * by reason: an enum value no constant has (a refusal's test), a 0-by-n matrix.
 * Usage: java org.nanoql.replay.Replay &lt;trace.jsonl&gt; [&lt;facts report.tsv&gt; [&lt;floor.json&gt;]]
 */
public final class Replay {
    private Replay() {
    }

    private static final class Fact {
        final String name;
        long runs;
        long whole;
        long calls;
        long replayed;

        Fact(String name) {
            this.name = name;
        }
    }

    /**
     * Replays a trace.
     *
     * @param args the trace, and the facts report to write
     * @throws IOException where the trace can't be read or the report written
     */
    public static void main(String[] args) throws IOException {
        if (args.length < 1) {
            System.err.println("usage: Replay <trace.jsonl> [<facts report.tsv> [<floor.json>]]");
            System.exit(2);
        }
        Rp r = new Rp();
        long stops = 0;
        if (System.getenv("NANOQL_REPLAY_SHOW") != null) {
            r.shown = Long.parseLong(System.getenv("NANOQL_REPLAY_SHOW"));
        }
        if (System.getenv("NANOQL_REPLAY_STOPS") != null) {
            stops = Long.parseLong(System.getenv("NANOQL_REPLAY_STOPS"));
        }
        if (System.getenv("NANOQL_REPLAY_TOLERANCE") != null) {
            Rp.tolerance = Double.parseDouble(System.getenv("NANOQL_REPLAY_TOLERANCE"));
        }
        if (System.getenv("NANOQL_REPLAY_ABS_TOLERANCE") != null) {
            Rp.absTolerance = Double.parseDouble(System.getenv("NANOQL_REPLAY_ABS_TOLERANCE"));
        }
        Map<String, int[]> table = new HashMap<>();
        for (int part = 0; part < DispatchTable.NAMES.length; part++) {
            for (int k = 0; k < DispatchTable.NAMES[part].length; k++) {
                table.put(DispatchTable.NAMES[part][k], new int[] {part, k});
            }
        }

        List<Fact> facts = new ArrayList<>();
        Map<String, Long> layerStops = new java.util.TreeMap<>();
        long calls = 0, replayed = 0;
        Fact current = null;
        boolean whole = false;
        try (BufferedReader in = Files.newBufferedReader(Path.of(args[0]), StandardCharsets.UTF_8)) {
            String line;
            while ((line = in.readLine()) != null) {
                r.line++;
                Jv v = Jv.parse(line);
                Jv name = Jv.member(v, "fact");
                if (name != null) {
                    if (current != null && whole) {
                        current.whole++;
                    }
                    current = null;
                    for (int k = facts.size() - 1; k >= 0 && k >= facts.size() - 64; k--) {
                        if (facts.get(k).name.equals(name.text)) {
                            current = facts.get(k);
                            break;
                        }
                    }
                    if (current == null) {
                        current = new Fact(name.text);
                        facts.add(current);
                    }
                    current.runs++;
                    whole = true;
                    r.stop = false;
                    continue;
                }
                calls++;
                if (current != null) {
                    current.calls++;
                }
                if (r.stop) {
                    continue;
                }
                Jv fn = Jv.member(v, "f");
                String key = fn != null && fn.type == Jv.STRING ? fn.text : "";
                int[] at = table.get(key);
                String why = null;
                if (at == null) {
                    why = "a function the layer doesn't have";
                } else {
                    Jv status = Jv.member(v, "t");
                    r.afterValues = Jv.member(v, "w");
                    r.fn = key;
                    int ok;
                    try {
                        ok = DispatchTable.run(at[0], at[1], r, Jv.member(v, "a"), Jv.member(v, "r"), (int) Jv.num(status));
                    } finally {
                        r.done();
                    }
                    if (ok == 0 || r.stop) {
                        why = r.why != null ? r.why : r.stop ? "an input the trace couldn't write or the replay didn't make" : "its call failed where C#'s didn't";
                        if (r.why != null) {
                            layerStops.merge(r.why, 1L, Long::sum);
                        }
                        r.why = null;
                    }
                }
                if (why != null) {
                    if (stops > 0 && whole) {
                        stops--;
                        System.out.printf("STOP line %d %s: %s: %s%n", r.line, current != null ? current.name : "(no fact)", key, why);
                    }
                    r.stop = true;
                    whole = false;
                    continue;
                }
                replayed++;
                if (current != null) {
                    current.replayed++;
                }
            }
        }
        if (current != null && whole) {
            current.whole++;
        }

        long wholeFacts = facts.stream().filter(x -> x.whole == x.runs).count();
        if (args.length > 1) {
            try (PrintStream out = new PrintStream(Files.newOutputStream(Path.of(args[1])), false, StandardCharsets.UTF_8)) {
                out.print("fact\truns\twhole\tcalls\treplayed\n");
                for (Fact x : facts) {
                    out.print(x.name + "\t" + x.runs + "\t" + x.whole + "\t" + x.calls + "\t" + x.replayed + "\n");
                }
            }
        }
        r.closeAll();
        long live = NanoQL.liveHandles();
        System.out.printf("Java replay: facts replayed whole %d of %d; calls replayed %d of %d; refusals matched %d; mismatches %d; live handles at the end %d%n",
                wholeFacts, facts.size(), replayed, calls, r.refusals, r.mismatches, live);
        layerStops.forEach((why, n) -> System.out.printf("facts stopped at %s: %d%n", why, n));
        if (Rp.tolerance > 0 || Rp.absTolerance > 0) {
            System.out.printf("tolerance %g relative, %g absolute: %d doubles within the relative, not bit for bit, the widest %.3g; %d more within the absolute, the widest %.3g%n",
                    Rp.tolerance, Rp.absTolerance, Rp.nearDoubles, Rp.widest, Rp.smallDoubles, Rp.widestAbs);
        }
        boolean below = args.length > 2 && belowFloor(Path.of(args[2]), facts, replayed, r.refusals);
        System.exit(r.mismatches == 0 && live == 0 && !below ? 0 : 1);
    }

    // B4's floor: facts replayed whole, each with calls, at least as many as the floor's facts; calls replayed at least
    // its calls; refusals matched; and each fact it names whole. True when the run is below it.
    private static boolean belowFloor(Path path, List<Fact> facts, long replayed, long refusals) throws IOException {
        Jv doc = Jv.parse(Files.readString(path, StandardCharsets.UTF_8));
        Jv floorFacts = Jv.member(doc, "facts");
        Jv wholeFacts = Jv.member(doc, "whole");
        long floorCalls = Jv.num(Jv.member(doc, "calls"));
        long whole = facts.stream().filter(x -> x.whole == x.runs && x.calls > 0).count();
        boolean named = true;
        for (int k = 0; wholeFacts != null && k < wholeFacts.count(); k++) {
            String name = wholeFacts.items[k].text;
            Fact x = facts.stream().filter(f -> f.name.equals(name)).findFirst().orElse(null);
            if (x == null || x.whole != x.runs || x.calls == 0) {
                System.out.printf("floor: %s isn't replayed whole%n", name);
                named = false;
            }
        }
        boolean ok = whole >= floorFacts.count() && replayed >= floorCalls && refusals > 0 && named;
        System.out.printf("floor: facts replayed whole with calls %d (floor %d); calls replayed %d (floor %d); refusals matched %d; %s%n",
                whole, floorFacts.count(), replayed, floorCalls, refusals, ok ? "at or above it" : "BELOW IT");
        return !ok;
    }
}
