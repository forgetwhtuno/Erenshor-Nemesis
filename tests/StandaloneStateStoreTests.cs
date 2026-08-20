using System;
using System.Collections.Generic;
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
            NemesisStateStore store1 = new NemesisStateStore(path);
            NemesisStateEntry<string> name1 = store1.Bind("Character.slot0_bob", "NemesisName", "", "desc");
            NemesisStateEntry<int> id1 = store1.Bind("Character.slot0_bob", "NemesisStableId", -1, "desc");
            NemesisStateEntry<string> origin1 = store1.Bind("Character.slot0_bob", "AssignmentOrigin", "legacy", "desc");
            NemesisStateEntry<int> wins1 = store1.Bind("Character.slot0_bob", "WinsAgainstNemesis", 0, "desc");
            NemesisStateEntry<long> ticks1 = store1.Bind("Character.slot0_bob", "DesignatedUtcTicks", 0L, "desc");
            NemesisStateEntry<string> conversation1 = store1.Bind("Character.slot0_bob", "RecentConversation", "", "desc");
            Check("01 fresh bind returns default", name1.Value == "");
            Check("02 fresh stable id is unset", id1.Value == -1);

            name1.Value = "Rivalname"; id1.Value = 42; origin1.Value = "auto"; wins1.Value = 3;
            ticks1.Value = 638123456789L; conversation1.Value = "P:SGVsbG8="; store1.Save();

            NemesisStateStore store2 = new NemesisStateStore(path);
            Check("03 string round-trips", store2.Bind("Character.slot0_bob", "NemesisName", "", "desc").Value == "Rivalname");
            Check("04 stable identity round-trips", store2.Bind("Character.slot0_bob", "NemesisStableId", -1, "desc").Value == 42);
            Check("05 assignment origin round-trips", store2.Bind("Character.slot0_bob", "AssignmentOrigin", "legacy", "desc").Value == "auto");
            Check("06 int round-trips", store2.Bind("Character.slot0_bob", "WinsAgainstNemesis", 0, "desc").Value == 3);
            Check("07 long round-trips", store2.Bind("Character.slot0_bob", "DesignatedUtcTicks", 0L, "desc").Value == 638123456789L);
            Check("08 conversation round-trips", store2.Bind("Character.slot0_bob", "RecentConversation", "", "desc").Value == "P:SGVsbG8=");
            Check("09 different character name isolated", store2.Bind("Character.slot1_alice", "NemesisName", "", "desc").Value == "");
            Check("10 different character stable id isolated", store2.Bind("Character.slot1_alice", "NemesisStableId", -1, "desc").Value == -1);

            NemesisStateEntry<string> zones2 = store2.Bind("Character.slot0_bob", "RecentZoneTaunts", "", "desc");
            zones2.Value = "Line one\twith tab\nand newline\\and backslash"; store2.Save();
            NemesisStateStore store3 = new NemesisStateStore(path);
            Check("11 escaped special characters round-trip", store3.Bind("Character.slot0_bob", "RecentZoneTaunts", "", "desc").Value == "Line one\twith tab\nand newline\\and backslash");
            NemesisStateEntry<string> legacyName = store3.Bind("Character.bob", "NemesisName", "", "desc"); legacyName.Value = "LegacyRival"; store3.Save();
            NemesisStateStore store4 = new NemesisStateStore(path);
            Check("12 legacy section cannot overwrite slot section", store4.Bind("Character.slot0_bob", "NemesisName", "", "desc").Value == "Rivalname");
            Check("13 legacy section keeps own value", store4.Bind("Character.bob", "NemesisName", "", "desc").Value == "LegacyRival");
            store4.Save(); Check("14 read-only save leaves file present", File.Exists(path));

            List<string> tokens = new List<string> { "sim:11", "sim:12", "sim:13", "sim:14" };
            int choice = NemesisAssignmentPolicy.StableChoiceIndex("slot0_bob", tokens);
            Check("15 automatic choice in range", choice >= 0 && choice < tokens.Count);
            Check("16 restart does not change stable choice", choice == NemesisAssignmentPolicy.StableChoiceIndex("slot0_bob", tokens));
            Check("17 zone transition does not change stable choice", choice == NemesisAssignmentPolicy.StableChoiceIndex("slot0_bob", tokens));
            Check("18 no candidates yields no choice", NemesisAssignmentPolicy.StableChoiceIndex("slot0_bob", new List<string>()) == -1);
            Check("19 first temporary miss is retained", !NemesisAssignmentPolicy.MissingIdentityIsPermanent(1, 90f));
            Check("20 repeated but too-short miss is retained", !NemesisAssignmentPolicy.MissingIdentityIsPermanent(3, 29f));
            Check("21 authoritative sustained miss invalidates", NemesisAssignmentPolicy.MissingIdentityIsPermanent(3, 30f));

            string msg;
            Check("22 exact comma address routes", NemesisConversationPolicy.TryExtractDirectAddress("Ariadne, keep talking.", "Ariadne", out msg) && msg == "keep talking.");
            Check("23 exact colon address routes", NemesisConversationPolicy.TryExtractDirectAddress("Ariadne: we'll see.", "Ariadne", out msg) && msg == "we'll see.");
            Check("24 exact dash address routes", NemesisConversationPolicy.TryExtractDirectAddress("Ariadne - you're confident.", "Ariadne", out msg) && msg == "you're confident.");
            Check("25 /group exact address routes", NemesisConversationPolicy.TryExtractDirectAddress("/group Ariadne, answer me.", "Ariadne", out msg) && msg == "answer me.");
            Check("26 /p exact address routes", NemesisConversationPolicy.TryExtractDirectAddress("/p Ariadne, answer me.", "Ariadne", out msg) && msg == "answer me.");
            Check("27 unrelated Sim not consumed", !NemesisConversationPolicy.TryExtractDirectAddress("Dancer, answer me.", "Ariadne", out msg));
            Check("28 normal party chat not consumed", !NemesisConversationPolicy.TryExtractDirectAddress("/group anyone ready?", "Ariadne", out msg));
            Check("29 name mention without directed punctuation not consumed", !NemesisConversationPolicy.TryExtractDirectAddress("Ariadne is nearby.", "Ariadne", out msg));
            Check("30 other slash command not consumed", !NemesisConversationPolicy.TryExtractDirectAddress("/whisper Ariadne hello", "Ariadne", out msg));
            Check("31 empty direct address not consumed", !NemesisConversationPolicy.TryExtractDirectAddress("Ariadne,   ", "Ariadne", out msg));
            Check("32 apostrophe/punctuation survives", NemesisConversationPolicy.TryExtractDirectAddress("Ariadne, you're awfully confident!", "Ariadne", out msg) && msg == "you're awfully confident!");

            List<NemesisConversationLine> lines = new List<NemesisConversationLine>();
            for (int i = 0; i < 9; i++) NemesisConversationPolicy.AddBounded(lines, i % 2 == 0, "line " + i);
            Check("33 conversation stays bounded", lines.Count == NemesisConversationPolicy.MaxRecentLines);
            Check("34 oldest conversation line evicted", lines[0].Text == "line 3");
            string heard = NemesisConversationPolicy.BuildHeardContext(lines, "we'll see about that.", 176);
            Check("35 context labels newest as heard", heard.Contains("PLAYER MESSAGE (HEARD): we'll see about that."));
            Check("36 context labels recent speakers", heard.Contains("RECENT HEARD CHAT:") && heard.Contains("P:") && heard.Contains("N:"));
            Check("37 context stays bounded", heard.Length <= 176);
            Check("38 compact preserves ordinary punctuation", NemesisConversationPolicy.Compact(" we're here!\nnext ", 80) == "we're here! next");

            string hubIdle = NemesisHubPresentation.Build(true, false, null, 0, null, false, 4);
            Check("39 hub awaiting-rival state is explicit", hubIdle == "Awaiting Rival | 4 candidate(s)");
            string hubRival = NemesisHubPresentation.Build(true, true, new string('R', 120), 17, "3W/2L", true, 0);
            Check("40 hub rival status stays bounded", hubRival.Length <= NemesisHubPresentation.MaxStatusLength);
            Check("41 hub exposes pending confirmation", hubRival.Contains("confirmation pending"));
            Check("42 candidate social selection policy", NemesisCandidateSelectionPolicy.RunSelfTests().StartsWith("PASS", StringComparison.Ordinal));
            Check("43 progression cohort policy", NemesisProgressionCohortPolicy.RunSelfTests().StartsWith("PASS", StringComparison.Ordinal));

            // Whisper/tell direct address: the live gap where "hey" fell through to a generic native
            // Sim reply because nothing recognized a plain whisper to the exact current Nemesis.
            string whisperMsg;
            Check("44 /whisper exact address routes", NemesisConversationPolicy.TryExtractWhisperAddress("/whisper Ariadne hey", "Ariadne", out whisperMsg) && whisperMsg == "hey");
            Check("45 /tell exact address routes", NemesisConversationPolicy.TryExtractWhisperAddress("/tell Ariadne you leveling?", "Ariadne", out whisperMsg) && whisperMsg == "you leveling?");
            Check("46 /w short form routes", NemesisConversationPolicy.TryExtractWhisperAddress("/w Ariadne hey", "Ariadne", out whisperMsg) && whisperMsg == "hey");
            Check("47 /t short form routes", NemesisConversationPolicy.TryExtractWhisperAddress("/t Ariadne hey", "Ariadne", out whisperMsg) && whisperMsg == "hey");
            Check("48 whisper target is case-insensitive", NemesisConversationPolicy.TryExtractWhisperAddress("/t ariadne hey", "Ariadne", out whisperMsg) && whisperMsg == "hey");
            Check("49 whisper to a different Sim is not consumed", !NemesisConversationPolicy.TryExtractWhisperAddress("/t Dancer hey", "Ariadne", out whisperMsg));
            Check("50 whisper with no message is not consumed", !NemesisConversationPolicy.TryExtractWhisperAddress("/t Ariadne", "Ariadne", out whisperMsg));
            Check("51 unrelated slash command is not consumed as a whisper", !NemesisConversationPolicy.TryExtractWhisperAddress("/enemesis status", "Ariadne", out whisperMsg));
            Check("52 ordinary party chat is not consumed as a whisper", !NemesisConversationPolicy.TryExtractWhisperAddress("/group anyone ready?", "Ariadne", out whisperMsg));

            Check("53 response policy self-tests", NemesisResponsePolicy.RunSelfTests().StartsWith("PASS", StringComparison.Ordinal));
        }
        finally { try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { } }

        Console.WriteLine("Nemesis deterministic tests: " + (_fail == 0 ? "ALL PASS" : "FAILURES") + " (" + _pass + " pass, " + _fail + " fail)");
        Environment.Exit(_fail == 0 ? 0 : 1);
    }
}
