using System;

namespace R3Ext;

// Instrumentation counters for generated binding/notification wiring. Enabled when R3EXT_TRACE compilation symbol is defined.
public static class R3ExtGeneratedInstrumentation
{
    public static int NotifyWires; // Number of times chain wiring executed.
    public static int BindUpdates; // Number of host/target updates performed in two-way binding.

    public static void Reset()
    {
        NotifyWires = 0;
        BindUpdates = 0;
    }
}