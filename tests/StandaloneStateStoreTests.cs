using System;
using System.IO;
using ErenshorNemesis;

internal static class StandaloneStateStoreTests
{
    private static int _pass, _fail;

    private static void Check(string name, bool ok)
    {
        if (ok) { _pass++; Console.WriteLine("PASS " + name); }
        else { _fail++; Console.WriteLine("FAIL " + name); }
    }

    private static void Main()
    {
        string dir = Path.Combine(Path.GetTempPath(), "NemesisStateStoreTests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "state.dat");
        try
        {
            // Fresh store: binds return defaults.
            NemesisStateStore store1 = new NemesisStateStore(path);
            NemesisStateEntry<string> name1 = store1.Bind("Character.slot0_bob", "NemesisName", "", "desc");
            Check("fresh bind returns default", name1.Value == "");

            NemesisStateEntry<int> wins1 = store1.Bind("Character.slot0_bob", "WinsAgainstNemesis", 0, "desc");
            NemesisStateEntry<long> ticks1 = store1.Bind("Character.slot0_bob", "DesignatedUtcTicks", 0L, "desc");

            name1.Value = "Rivalname"; wins1.Value = 3; ticks1.Value = 638123456789L;
            store1.Save();

            // Reopen: values persisted across a fresh store instance.
            NemesisStateStore store2 = new NemesisStateStore(path);
            NemesisStateEntry<string> name2 = store2.Bind("Character.slot0_bob", "NemesisName", "", "desc");
            NemesisStateEntry<int> wins2 = store2.Bind("Character.slot0_bob", "WinsAgainstNemesis", 0, "desc");
            NemesisStateEntry<long> ticks2 = store2.Bind("Character.slot0_bob", "DesignatedUtcTicks", 0L, "desc");
            Check("string round-trips", name2.Value == "Rivalname");
            Check("int round-trips", wins2.Value == 3);
            Check("long round-trips", ticks2.Value == 638123456789L);

            // Different section/key is isolated.
            NemesisStateEntry<string> other = store2.Bind("Character.slot1_alice", "NemesisName", "", "desc");
            Check("different section stays isolated", other.Value == "");

            // Values with escape-worthy characters (tabs/newlines) round-trip correctly.
            NemesisStateEntry<string> zones2 = store2.Bind("Character.slot0_bob", "RecentZoneTaunts", "", "desc");
            zones2.Value = "Line one\twith tab\nand newline\\and backslash";
            store2.Save();
            NemesisStateStore store3 = new NemesisStateStore(path);
            NemesisStateEntry<string> zones3 = store3.Bind("Character.slot0_bob", "RecentZoneTaunts", "", "desc");
            Check("escaped special characters round-trip", zones3.Value == "Line one\twith tab\nand newline\\and backslash");

            // Legacy migration shape: binding a name-only legacy section is independent of the
            // slot-qualified section, matching MigrateLegacyCharacterSection's expectations.
            NemesisStateEntry<string> legacyName = store3.Bind("Character.bob", "NemesisName", "", "desc");
            legacyName.Value = "LegacyRival";
            store3.Save();
            NemesisStateStore store4 = new NemesisStateStore(path);
            NemesisStateEntry<string> slotName = store4.Bind("Character.slot0_bob", "NemesisName", "", "desc");
            NemesisStateEntry<string> legacyName2 = store4.Bind("Character.bob", "NemesisName", "", "desc");
            Check("legacy section does not overwrite slot-qualified section", slotName.Value == "Rivalname");
            Check("legacy section keeps its own value", legacyName2.Value == "LegacyRival");

            // Save() without any prior mutation should be a cheap no-op (no dirty flag set).
            NemesisStateStore store5 = new NemesisStateStore(path);
            store5.Bind("Character.slot0_bob", "NemesisName", "", "desc");
            store5.Save();
            Check("read-only bind does not corrupt the file", File.Exists(path));
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }

        Console.WriteLine("Nemesis state store tests: " + (_fail == 0 ? "ALL PASS" : "FAILURES") + " (" + _pass + " pass, " + _fail + " fail)");
        Environment.Exit(_fail == 0 ? 0 : 1);
    }
}
