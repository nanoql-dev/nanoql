// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql.reach;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.nio.charset.StandardCharsets;
import java.nio.file.Path;
import org.junit.jupiter.api.Test;
import org.nanoql.NanoQL;

/**
 * An object stays reachable while its handle is in a call (docs/bindings.md, "Java"): {@code TemporaryReceiver}'s loop in
 * a JVM whose young generation is 2 MB, refused with code 9 thousands of times without the layer's fences.
 */
class ReachabilityTest {
    @Test
    void aTemporaryReceiverStaysReachableThroughItsCall() throws Exception {
        String java = Path.of(System.getProperty("java.home"), "bin", "java").toString();
        Process p = new ProcessBuilder(java, "-Xmx256m", "-Xmn2m", "--enable-native-access=ALL-UNNAMED", "-Dnanoql.library=" + NanoQL.libraryPath(),
            "-cp", System.getProperty("surefire.test.class.path", System.getProperty("java.class.path")), TemporaryReceiver.class.getName(), "8").redirectErrorStream(true).start();
        String out = new String(p.getInputStream().readAllBytes(), StandardCharsets.UTF_8).strip();
        assertEquals(0, p.waitFor(), out);
    }
}
