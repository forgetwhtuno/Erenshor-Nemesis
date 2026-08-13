using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ErenshorNemesis
{
    // Mod-owned sidecar persistence for per-character rivalry state (record, timestamps, dialogue
    // variety seeds). This is runtime state, not a player-facing setting, and it needs a dynamic
    // per-character section (including the legacy name-keyed -> slot-qualified migration) that
    // Lunaris typed config cannot express with fixed compile-time keys. Mirrors the exact
    // section/key/default/description Bind(...) shape the previous BepInEx ConfigFile-backed
    // implementation used, so call sites elsewhere in NemesisDirector needed minimal changes.
    internal sealed class NemesisStateEntry<T>
    {
        private readonly NemesisStateStore _owner;
        private readonly string _mapKey;
        private T _value;

        internal NemesisStateEntry(NemesisStateStore owner, string mapKey, T value)
        {
            _owner = owner;
            _mapKey = mapKey;
            _value = value;
        }

        internal T Value
        {
            get { return _value; }
            set { _value = value; _owner.MarkDirty(_mapKey, value); }
        }
    }

    internal sealed class NemesisStateStore
    {
        private readonly string _path;
        private readonly Dictionary<string, string> _raw = new Dictionary<string, string>(StringComparer.Ordinal);
        private bool _dirty;

        internal NemesisStateStore(string path)
        {
            _path = path;
            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;
                string[] lines = File.ReadAllLines(_path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int tab1 = line.IndexOf('\t');
                    int tab2 = tab1 < 0 ? -1 : line.IndexOf('\t', tab1 + 1);
                    if (tab1 < 0 || tab2 < 0) continue;
                    string section = line.Substring(0, tab1);
                    string key = line.Substring(tab1 + 1, tab2 - tab1 - 1);
                    string value = Unescape(line.Substring(tab2 + 1));
                    _raw[MapKey(section, key)] = value;
                }
            }
            catch { }
        }

        internal void Save()
        {
            if (!_dirty) return;
            try
            {
                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, string> kv in _raw)
                {
                    int split = kv.Key.IndexOf('\u0001');
                    string section = split < 0 ? kv.Key : kv.Key.Substring(0, split);
                    string key = split < 0 ? string.Empty : kv.Key.Substring(split + 1);
                    sb.Append(section).Append('\t').Append(key).Append('\t').Append(Escape(kv.Value)).Append('\n');
                }
                string tmp = _path + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(tmp, _path);
                _dirty = false;
            }
            catch { }
        }

        internal void MarkDirty(string mapKey, object value)
        {
            int split = mapKey.IndexOf('\u0001');
            string section = split < 0 ? mapKey : mapKey.Substring(0, split);
            string key = split < 0 ? string.Empty : mapKey.Substring(split + 1);
            _raw[mapKey] = Convert.ToString(value, CultureInfo.InvariantCulture);
            _dirty = true;
        }

        private static string MapKey(string section, string key) { return section + "\u0001" + key; }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private static string Unescape(string value)
        {
            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' && i + 1 < value.Length)
                {
                    char n = value[i + 1];
                    if (n == 'n') { sb.Append('\n'); i++; continue; }
                    if (n == 't') { sb.Append('\t'); i++; continue; }
                    if (n == 'r') { sb.Append('\r'); i++; continue; }
                    if (n == '\\') { sb.Append('\\'); i++; continue; }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        internal NemesisStateEntry<int> Bind(string section, string key, int defaultValue, string description)
        {
            string mapKey = MapKey(section, key);
            string raw;
            int value = defaultValue;
            if (_raw.TryGetValue(mapKey, out raw)) int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            return new NemesisStateEntry<int>(this, mapKey, value);
        }

        internal NemesisStateEntry<long> Bind(string section, string key, long defaultValue, string description)
        {
            string mapKey = MapKey(section, key);
            string raw;
            long value = defaultValue;
            if (_raw.TryGetValue(mapKey, out raw)) long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            return new NemesisStateEntry<long>(this, mapKey, value);
        }

        internal NemesisStateEntry<string> Bind(string section, string key, string defaultValue, string description)
        {
            string mapKey = MapKey(section, key);
            string raw;
            string value = _raw.TryGetValue(mapKey, out raw) ? raw : defaultValue;
            return new NemesisStateEntry<string>(this, mapKey, value);
        }
    }
}
