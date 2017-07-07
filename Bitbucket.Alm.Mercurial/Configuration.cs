/**** Git Credential Manager for Windows ****
 *
 * Copyright (c) Microsoft Corporation
 * All rights reserved.
 *
 * MIT License
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the """"Software""""), to deal
 * in the Software without restriction, including without limitation the rights to
 * use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
 * the Software, and to permit persons to whom the Software is furnished to do so,
 * subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
 * FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
 * COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN
 * AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
 * WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE."
**/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Trace = Microsoft.Alm.Trace;
using Where = Atlassian.Bitbucket.Alm.Mercurial.Where;

namespace Atlassian.Bitbucket.Alm.Mercurial
{
    public abstract class Configuration
    {
        public const char HostSplitCharacter = ':';

        private static readonly Lazy<Regex> CommentRegex = new Lazy<Regex>(() => new Regex(@"^\s*[#;]", RegexOptions.Compiled | RegexOptions.CultureInvariant));
        private static readonly Lazy<Regex> KeyValueRegex = new Lazy<Regex>(() => new Regex(@"^\s*(.+)\s*=\s*(.*)", RegexOptions.Compiled | RegexOptions.CultureInvariant));
        private static readonly Lazy<Regex> SectionRegex = new Lazy<Regex>(() => new Regex(@"^\s*\[\s*(\w+)\s*(\""[^\]]+){0,1}\]", RegexOptions.Compiled | RegexOptions.CultureInvariant));

        public static IEnumerable<ConfigurationLevel> Levels
        {
            get
            {
                yield return ConfigurationLevel.Local;
                yield return ConfigurationLevel.Global;
                yield return ConfigurationLevel.System;
                yield return ConfigurationLevel.Portable;
            }
        }

        public virtual string this[string key]
        {
            get => throw new NotImplementedException();
        }

        public virtual int Count
        {
            get => throw new NotImplementedException();
        }

        public virtual bool ContainsKey(string key)
             => throw new NotImplementedException();

        public virtual bool ContainsKey(ConfigurationLevel levels, string key)
             => throw new NotImplementedException();

        public virtual void LoadMercurialConfiguration(string directory, ConfigurationLevel types)
             => throw new NotImplementedException();

        public static Configuration ReadConfiguration(string directory, bool loadLocal, bool loadSystem)
        {
            if (String.IsNullOrWhiteSpace(directory))
                throw new ArgumentNullException("directory");
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException(directory);

            ConfigurationLevel types = ConfigurationLevel.All;

            if (!loadLocal)
            {
                types ^= ConfigurationLevel.Local;
            }

            if (!loadSystem)
            {
                types ^= ConfigurationLevel.System;
            }

            var config = new Impl();
            config.LoadMercurialConfiguration(directory, types);

            return config;
        }

        public virtual bool TrySetEntry(ConfigurationLevel level, string section, string prefix, string key, string suffix, string value, string localDirectory = null)
             => throw new NotImplementedException();

        public virtual bool TryGetEntry(string compositeKey, out Entry entry)
             => throw new NotImplementedException();

        public virtual bool TryGetEntry(string prefix, string key, string suffix, out Entry entry)
             => throw new NotImplementedException();

        public virtual bool TryGetEntry(string prefix, Uri targetUri, string key, out Entry entry)
             => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        internal static void ParseMercurialConfig(TextReader reader, IDictionary<string, string> destination)
        {
            Debug.Assert(reader != null, $"The `{nameof(reader)}` parameter is null.");
            Debug.Assert(destination != null, $"The `{nameof(destination)}` parameter is null.");

            Match match = null;
            string section = null;

            // parse each line in the config independently - Git's configs do not accept multi-line values
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                // skip empty and commented lines
                if (String.IsNullOrWhiteSpace(line))
                    continue;
                if (CommentRegex.Value.IsMatch(line))
                    continue;

                // sections begin with values like [section] or [section "section name"]. All
                // subsequent lines, until a new section is encountered, are children of the section
                if ((match = SectionRegex.Value.Match(line)).Success)
                {
                    if (match.Groups.Count >= 2 && !String.IsNullOrWhiteSpace(match.Groups[1].Value))
                    {
                        section = match.Groups[1].Value.Trim();

                        // check if the section is named, if so: process the name
                        if (match.Groups.Count >= 3 && !String.IsNullOrWhiteSpace(match.Groups[2].Value))
                        {
                            string val = match.Groups[2].Value.Trim();

                            // triming off enclosing quotes makes usage easier, only trim in pairs
                            if (val.Length > 0 && val[0] == '"')
                            {
                                if (val[val.Length - 1] == '"' && val.Length > 1)
                                {
                                    val = val.Substring(1, val.Length - 2);
                                }
                                else
                                {
                                    val = val.Substring(1, val.Length - 1);
                                }
                            }

                            section += HostSplitCharacter + val;
                        }
                    }
                }
                // section children should be in the format of name = value pairs
                else if ((match = KeyValueRegex.Value.Match(line)).Success)
                {
                    if (match.Groups.Count >= 3
                        && !String.IsNullOrEmpty(match.Groups[1].Value))
                    {
                        string key = section + HostSplitCharacter + match.Groups[1].Value.Trim();
                        string val = match.Groups[2].Value.Trim();

                        // triming off enclosing quotes makes usage easier, only trim in pairs
                        if (val.Length > 0 && val[0] == '"')
                        {
                            if (val[val.Length - 1] == '"' && val.Length > 1)
                            {
                                val = val.Substring(1, val.Length - 2);
                            }
                            else
                            {
                                val = val.Substring(1, val.Length - 1);
                            }
                        }

                        // Test for and handle include directives
                        if ("include.path".Equals(key))
                        {
                            try
                            {
                                // This is an include directive, import the configuration values from
                                // the included file
                                string includePath = (val.StartsWith("~/", StringComparison.OrdinalIgnoreCase))
                                    ? Where.Home() + val.Substring(1, val.Length - 1)
                                    : val;

                                includePath = Path.GetFullPath(includePath);

                                using (var includeFile = File.Open(includePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                                using (var includeReader = new StreamReader(includeFile))
                                {
                                    ParseMercurialConfig(includeReader, destination);
                                }
                            }
                            catch (Exception exception)
                            {
                                Trace.WriteLine($"failed to parse config file: {val}. {exception.Message}");
                            }
                        }
                        else
                        {
                            // Add or update the (key, value)
                            if (destination.ContainsKey(key))
                            {
                                destination[key] = val;
                            }
                            else
                            {
                                destination.Add(key, val);
                            }
                        }
                    }
                }
            }
        }

        public sealed class Impl: Configuration
        {
            public Impl()
            { }

            internal Impl(Dictionary<ConfigurationLevel, Dictionary<string, string>> values)
            {
                if (ReferenceEquals(values, null))
                    throw new ArgumentNullException(nameof(values));

                _values = new Dictionary<ConfigurationLevel, Dictionary<string, string>>(values.Count);

                // Copy the dictionary
                foreach (var level in values)
                {
                    var levelValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var item in level.Value)
                    {
                        levelValues.Add(item.Key, item.Value);
                    }

                    _values.Add(level.Key, levelValues);
                }
            }

            private readonly Dictionary<ConfigurationLevel, Dictionary<string, string>> _values = new Dictionary<ConfigurationLevel, Dictionary<string, string>>()
            {
                { ConfigurationLevel.Global, new Dictionary<string, string>(Entry.KeyComparer) },
                { ConfigurationLevel.Local, new Dictionary<string, string>(Entry.KeyComparer) },
                { ConfigurationLevel.Portable, new Dictionary<string, string>(Entry.KeyComparer) },
                { ConfigurationLevel.System, new Dictionary<string, string>(Entry.KeyComparer) },
            };

            public sealed override string this[string key]
            {
                get
                {
                    foreach (var level in Levels)
                    {
                        if (_values[level].ContainsKey(key))
                            return _values[level][key];
                    }

                    return null;
                }
            }

            public sealed override int Count
            {
                get
                {
                    return _values[ConfigurationLevel.Global].Count
                         + _values[ConfigurationLevel.Local].Count
                         + _values[ConfigurationLevel.Portable].Count
                         + _values[ConfigurationLevel.System].Count;
                }
            }

            public sealed override bool ContainsKey(string key)
            {
                return ContainsKey(ConfigurationLevel.All, key);
            }

            public sealed override bool ContainsKey(ConfigurationLevel levels, string key)
            {
                foreach (var level in Levels)
                {
                    if ((level & levels) != 0
                        && _values[level].ContainsKey(key))
                        return true;
                }

                return false;
            }

            public sealed override bool TrySetEntry(ConfigurationLevel level, string section, string prefix, string key, string suffix, string value, string localDirectory = null)
            {
                var compositeKey = GetCompositeKey(section, prefix, key, suffix);
                if (ContainsKey(level, compositeKey))
                {
                    Entry existingEntry;
                    TryGetEntry(compositeKey, out existingEntry);
                    Trace.WriteLine($"{level} already contains {existingEntry}");
                    return false;
                }
                Trace.WriteLine($"Setting {key} at {level}");
                _values[level][key] = value;
                AppendMercurialConfiguration(level, new Entry(key, value), localDirectory);
                Trace.WriteLine($"Set {key} at {level}");
                return true;
            }

            private string GetCompositeKey(string section, string prefix, string key, string suffix)
            {
                var compositeKey = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(section))
                {
                    compositeKey.Append(section);
                }
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    if (!string.IsNullOrWhiteSpace(section))
                    {
                        compositeKey.Append(Configuration.HostSplitCharacter);
                    }
                    compositeKey.Append(prefix);
                }
                if (!string.IsNullOrWhiteSpace(key))
                {
                    if (!string.IsNullOrWhiteSpace(prefix))
                    {
                        compositeKey.Append(".");
                    }
                    else if(!string.IsNullOrWhiteSpace(section))
                    {
                        compositeKey.Append(Configuration.HostSplitCharacter);
                    }
                    compositeKey.Append(key);
                }
                if (!string.IsNullOrWhiteSpace(suffix))
                {
                    if (compositeKey.Length > 0)
                    {
                        compositeKey.Append(".");
                    }
                    compositeKey.Append(suffix);
                }


                return compositeKey.ToString();
            }

            private void AppendMercurialConfiguration(ConfigurationLevel level, Entry entry, string localDirectory)
            {
                string portableConfig = null;
                string systemConfig = null;
                List<string> globalConfigs = null;
                List<string> localConfigs = null;

                // save the value to file immediately while we know what is new
                // find and parse Git's portable config
                if ((level & ConfigurationLevel.Portable) != 0
                    && Where.MercurialPortableConfig(out portableConfig))
                {
                    AppendMercurialConfig(portableConfig, "extensions", entry);
                }

                // find and parse Git's system config
                if ((level & ConfigurationLevel.System) != 0
                    && Where.MercurialSystemConfig(null, out systemConfig))
                {
                    AppendMercurialConfig(systemConfig, "extensions", entry);
                }

                // find and parse Git's global config
                if ((level & ConfigurationLevel.Global) != 0
                    && Where.MercurialGlobalConfig(out globalConfigs))
                {
                    globalConfigs.Where(c => c.EndsWith(".hgrc")).ToList().ForEach(c => AppendMercurialConfig(c, "extensions", entry));
                }

                // find and parse Git's local config
                if ((level & ConfigurationLevel.Local) != 0
                    && Where.MercurialLocalConfig(localDirectory, out localConfigs))
                {
                    localConfigs.Where(c => c.EndsWith(".hgrc")).ToList().ForEach(c => AppendMercurialConfig(c, "extensions", entry));
                }
            }

            public sealed override bool TryGetEntry(string prefix, string key, string suffix, out Entry entry)
            {
                if (ReferenceEquals(prefix, null))
                    throw new ArgumentNullException(nameof(prefix));
                if (ReferenceEquals(suffix, null))
                    throw new ArgumentNullException(nameof(suffix));

                return TryGetEntry(GetCompositeKey(null, prefix, key, suffix), out entry);
                
            }

            public sealed override bool TryGetEntry(string compositeKey, out Entry entry)
            {
                // if there's a match, return it
                if (ContainsKey(compositeKey))
                {
                    entry = new Entry(compositeKey, this[compositeKey]);
                    return true;
                }

                // nothing found
                entry = default(Entry);
                return false;
            }

            public sealed override bool TryGetEntry(string prefix, Uri targetUri, string key, out Entry entry)
            {
                if (ReferenceEquals(key, null))
                    throw new ArgumentNullException(nameof(key));

                if (targetUri != null)
                {
                    // return match seeking from most specific (<prefix>.<scheme>://<host>.<key>) to
                    // least specific (credential.<key>)
                    if (TryGetEntry(prefix, String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}://{1}", targetUri.Scheme, targetUri.Host), key, out entry)
                        || TryGetEntry(prefix, targetUri.Host, key, out entry))
                        return true;

                    if (!String.IsNullOrWhiteSpace(targetUri.Host))
                    {
                        string[] fragments = targetUri.Host.Split(HostSplitCharacter);
                        string host = null;

                        // look for host matches stripping a single sub-domain at a time off don't
                        // match against a top-level domain (aka ".com")
                        for (int i = 1; i < fragments.Length - 1; i++)
                        {
                            host = String.Join(".", fragments, i, fragments.Length - i);
                            if (TryGetEntry(prefix, host, key, out entry))
                                return true;
                        }
                    }
                }

                // try to find an unadorned match as a complete fallback
                if (TryGetEntry(prefix, String.Empty, key, out entry))
                    return true;

                // nothing found
                entry = default(Entry);
                return false;
            }

            public sealed override void LoadMercurialConfiguration(string directory, ConfigurationLevel types)
            {
                string portableConfig = null;
                string systemConfig = null;
                List<string> globalConfigs = null;
                List<string> localConfigs = null;

                // read Git's four configs from lowest priority to highest, overwriting values as
                // higher priority configurations are parsed, storing them in a handy lookup table

                // find and parse Git's portable config
                if ((types & ConfigurationLevel.Portable) != 0
                    && Where.MercurialPortableConfig(out portableConfig))
                {
                    ParseMercurialConfig(ConfigurationLevel.Portable, portableConfig);
                }

                // find and parse Git's system config
                if ((types & ConfigurationLevel.System) != 0
                    && Where.MercurialSystemConfig(null, out systemConfig))
                {
                    ParseMercurialConfig(ConfigurationLevel.System, systemConfig);
                }

                // find and parse Git's global config
                if ((types & ConfigurationLevel.Global) != 0
                    && Where.MercurialGlobalConfig(out globalConfigs))
                {
                    globalConfigs.ForEach(c => ParseMercurialConfig(ConfigurationLevel.Global, c));
                }

                // find and parse Git's local config
                if ((types & ConfigurationLevel.Local) != 0
                    && Where.MercurialLocalConfig(directory, out localConfigs))
                {
                    localConfigs.ForEach(c=> ParseMercurialConfig(ConfigurationLevel.Local, c));
                }

                Trace.WriteLine($"git {types} config read, {Count} entries.");
            }


            private void ParseMercurialConfig(ConfigurationLevel level, string configPath)
            {
                Debug.Assert(Enum.IsDefined(typeof(ConfigurationLevel), level), $"The `{nameof(level)}` parameter is not defined.");
                Debug.Assert(!String.IsNullOrWhiteSpace(configPath), $"The `{nameof(configPath)}` parameter is null or invalid.");
                Debug.Assert(File.Exists(configPath), $"The `{nameof(configPath)}` parameter references a non-existent file.");

                if (!_values.ContainsKey(level))
                    return;
                if (!File.Exists(configPath))
                    return;

                using (var stream = File.OpenRead(configPath))
                using (var reader = new StreamReader(stream))
                {
                    ParseMercurialConfig(reader, _values[level]);
                }
            }

            private void AppendMercurialConfig(string configPath, string section, Entry entry)
            {
                try
                {
                    var lines = new List<string>();
                    lines.Add($"[{section}]");
                    lines.Add($"{entry.Key}={entry.Value}");
                    File.AppendAllLines(configPath, lines);
                }
                catch(Exception ex)
                {
                    Trace.WriteLine($"Failed to write {entry} to {configPath}");
                }
            }

        }

        public struct Entry: IEquatable<Entry>
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
            public static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
            public static readonly StringComparer ValueComparer = StringComparer.OrdinalIgnoreCase;

            public Entry(string key, string value)
            {
                Key = key;
                Value = value;
            }
            
            public readonly string Key;
            public readonly string Value;

            public override bool Equals(object obj)
            {
                return (obj is Entry)
                        && Equals((Entry)obj);
            }

            public bool Equals(Entry other)
            {
                return KeyComparer.Equals(Key, other.Key)
                    && ValueComparer.Equals(Value, other.Value);
            }

            public override int GetHashCode()
            {
                return KeyComparer.GetHashCode(Key);
            }

            public override string ToString()
            {
                return $"{Key} = {Value}";
            }

            public static bool operator ==(Entry left, Entry right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(Entry left, Entry right)
                => !(left == right);
        }
    }
}
