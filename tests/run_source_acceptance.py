#!/usr/bin/env python3
from pathlib import Path
import re, sys

ROOT = Path(__file__).resolve().parents[1]
plugin = (ROOT/'src/ErenshorNemesisPlugin.cs').read_text(encoding='utf-8')
director = (ROOT/'src/NemesisDirector.cs').read_text(encoding='utf-8')
assign = (ROOT/'src/NemesisAssignmentPolicy.cs').read_text(encoding='utf-8')
conversation = (ROOT/'src/NemesisConversationPolicy.cs').read_text(encoding='utf-8')
native = (ROOT/'src/NemesisNativeSocialRoster.cs').read_text(encoding='utf-8')
csproj = (ROOT/'ErenshorNemesis.csproj').read_text(encoding='utf-8')
tests = (ROOT/'tests/StandaloneStateStoreTests.cs').read_text(encoding='utf-8')
readme = (ROOT/'README.md').read_text(encoding='utf-8')
all_src = '\n'.join(p.read_text(encoding='utf-8') for p in (ROOT/'src').glob('*.cs'))

checks=[]
def ck(name, ok): checks.append((name, bool(ok)))

def balanced(text):
    # Lightweight C# lexical structural guard. Full syntax remains the real compiler's job.
    opens=closes=0; i=0; state='code'; esc=False
    while i < len(text):
        c=text[i]; n=text[i+1] if i+1 < len(text) else ''
        if state=='code':
            if c=='/' and n=='/': state='line'; i+=2; continue
            if c=='/' and n=='*': state='block'; i+=2; continue
            if c=='@' and n=='"': state='vstr'; i+=2; continue
            if c=='"': state='str'; i+=1; continue
            if c=="'": state='char'; i+=1; continue
            if c=='{': opens+=1
            elif c=='}': closes+=1
            i+=1; continue
        if state=='line':
            if c=='\n': state='code'
            i+=1; continue
        if state=='block':
            if c=='*' and n=='/': state='code'; i+=2
            else: i+=1
            continue
        if state=='str':
            if esc: esc=False
            elif c=='\\': esc=True
            elif c=='"': state='code'
            i+=1; continue
        if state=='vstr':
            if c=='"' and n=='"': i+=2; continue
            if c=='"': state='code'
            i+=1; continue
        if state=='char':
            if esc: esc=False
            elif c=='\\': esc=True
            elif c=="'": state='code'
            i+=1; continue
    return state=='code' and opens==closes

# Version / ownership surface
ck('01 version is 0.3.0', 'PluginVersion = "0.3.0"' in plugin)
ck('02 no second model/client symbols', not any(x.lower() in all_src.lower() for x in ['httpclient','/api/generate','/api/chat','qwen3.5:2b','qwen3.5:4b']))
ck('03 csproj only compiles local src glob', '<Compile Include="src\\*.cs"' in csproj)

# Automatic assignment
for n,t in [
('04 auto assignment tick','EnsureAutomaticAssignment(now);'),
('05 stable chooser','ChooseStableAutomaticCandidate'),
('06 same auto candidate model','List<SimPlayerTracking> candidates = AutomaticCandidates();'),
('07 stable identity persisted','NemesisStableId'),
('08 simIndex identity source','tracking.simIndex'),
('09 origin persisted','AssignmentOrigin'),
('10 manual origin persists','? "auto" : "manual"'),
('11 stopped rivalry suppresses auto','"disabled"'),
('12 awaiting retry bounded','AwaitingCandidateRetrySeconds'),
('13 sustained invalidity threshold','MissingIdentityIsPermanent'),
('14 authoritative roster gate','PersistentRosterAuthoritative()'),
('15 display name refreshed from stable id','display_name_refreshed'),
('16 reroll command exists','arg.Equals("reroll"'),
]: ck(n,t in director or t in assign)
ck('17 picker is not alphabetical first', 'return 0;' not in assign and 'h % (uint)candidateTokens.Count' in assign)
ck('18 no old Random.Range candidate picker', 'list[UnityEngine.Random.Range(0, list.Count)]' not in director)

# Candidate safety policy retained
ck('19 native friend exclusion proof remains', 'sim.FriendedBy == currentSlot' in native)
ck('20 GM exclusion proof remains', '!sim.IsGMCharacter' in native)
ck('21 explicit friends labeled manual-only', 'FRIENDS - explicit /enemesis select only' in director)
ck('22 guild fallback retained', 'GUILD FALLBACK:' in director)

# Natural input ownership / duplicate prevention
ck('23 exact current-name parser wired', 'NemesisConversationPolicy.TryExtractDirectAddress(rawText, Name.Value' in director)
ck('24 ordinary chat falls through', 'return false;' in plugin and 'TryHandleNaturalAddress' in plugin)
ck('25 Deep Sims ordering declared', 'HarmonyBefore("forgetwhtuno.erenshor.deepsims")' in plugin)
ck('26 owned input is cleared', 'ClearInput(input);' in plugin)
ck('27 explicit reply command retained', 'reply <text>' in director and 'arg.StartsWith("reply "' in director)
ck('28 parser rejects other slash commands', 'else if (text.StartsWith("/", StringComparison.Ordinal)) return false;' in conversation)
ck('29 parser requires directed punctuation', "separator != ','" in conversation and "separator != ':'" in conversation)

# Conversation provenance / boundedness
ck('30 bounded thread max six', 'MaxRecentLines = 6' in conversation)
ck('31 heard label present', 'PLAYER MESSAGE (HEARD)' in conversation)
ck('32 recent heard label present', 'RECENT HEARD CHAT' in conversation)
ck('33 verified record separate', 'VerifiedRecord()' in director and 'RecentConversation' in director)
ck('34 conversation persisted per bound character section', 'RecentConversation = State.Bind(section' in director)

# Deep Sims optional bridge/fallback
ck('35 optional bridge type', 'ErenshorDeepSims.NemesisEventBridge' in director)
ck('36 RequestNemesisLine reused', 'RequestNemesisLine' in director)
ck('37 template fallback on unavailable/refusal', 'EmitLine(type, Name.Value, fallback, "template")' in director)
ck('38 callback stale guards retained', 'ContextStillValid' in director and 'ExpectedCharacter' in director and 'ExpectedScene' in director)

# Chat UI fix
ck('39 rival visible message has no markup literal', 'ChatRivalTell(Clean(speaker, 60) + " tells you: " + text)' in director)
ck('40 no hardcoded magenta emission', '"magenta"' not in director)
ck('41 native style learned from actual social-log calls', 'NoteNativeSocialStyle' in plugin and 'NemesisNativeChatStylePatch' in plugin)
ck('42 native two-arg path carries color separately', 'UpdateSocialLog.LogAdd(value, nativeColor)' in plugin)
ck('43 safe one-arg fallback exists', 'UpdateSocialLog.LogAdd(value);' in plugin)
ck('44 self-emission style learning suppressed', '_emittingNemesisChat' in plugin)
ck('45 diagnostic exposes chat style', 'chatStyle=' in director and 'ChatStyleStatus()' in director)

# Diagnostics / tests / source structural checks
ck('46 status exposes assignment', 'assignment=' in director)
ck('47 status exposes Deep Sims', 'DeepSims=' in director)
ck('48 diagnose exposes candidate count', 'candidateCount=' in director)
ck('49 standalone deterministic test count >=30', len(re.findall(r'Check\("\d\d ', tests)) >= 30)
ck('50 director brace/string structure balanced', balanced(director))
ck('51 plugin brace/string structure balanced', balanced(plugin))
ck('52 conversation policy structure balanced', balanced(conversation))
ck('53 assignment policy structure balanced', balanced(assign))
ck('54 README documents no-command first run', 'Zero-command first run' in readme)

fails=[n for n,ok in checks if not ok]
for n,ok in checks: print(('PASS ' if ok else 'FAIL ')+n)
print(f'Nemesis source acceptance: {len(checks)-len(fails)}/{len(checks)} pass')
if fails:
    print('Failures:', '; '.join(fails))
    sys.exit(1)
