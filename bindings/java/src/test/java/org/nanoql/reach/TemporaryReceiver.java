// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql.reach;

import java.util.ArrayList;
import java.util.List;
import org.nanoql.NanoQLError;
import org.nanoql.extensions.conventions.JointCalendarRecord;

/**
 * A temporary receiver's member call while collections run: {@code new JointCalendarRecord(...).withMembers(list)}, the
 * receiver never closed, the list's strings converted after its handle is read. Without a reachability fence the cleaner
 * may release the receiver's handle before the call crosses, and the call refuses with code 9. Run by
 * {@code ReachabilityTest} in a JVM of a small young generation, so collections run often.
 * Usage: TemporaryReceiver &lt;seconds&gt;; prints the calls and the refusals, and exits 1 at any refusal.
 */
public final class TemporaryReceiver {
    private TemporaryReceiver() {
    }

    /**
     * Runs the loop.
     *
     * @param args the seconds to run
     */
    public static void main(String[] args) {
        List<String> members = new ArrayList<>();
        for (int i = 0; i < 20_000; i++) {
            members.add("TARGET" + i);
        }
        long end = System.nanoTime() + Long.parseLong(args[0]) * 1_000_000_000L;
        long calls = 0, refused = 0;
        String first = null;
        while (System.nanoTime() < end) {
            try {
                new JointCalendarRecord("joint").withMembers(members).close();
            } catch (RuntimeException e) {
                if (!(e instanceof NanoQLError q) || q.code() != 9) {
                    throw e;
                }
                refused++;
                if (first == null) {
                    first = "call " + calls + ": " + e.getMessage();
                }
            }
            calls++;
        }
        System.out.println("calls " + calls + ", refused with code 9 " + refused + (first != null ? ", the first at " + first : ""));
        System.exit(refused == 0 ? 0 : 1);
    }
}
