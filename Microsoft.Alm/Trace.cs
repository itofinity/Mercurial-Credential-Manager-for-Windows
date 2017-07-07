﻿/**** Git Credential Manager for Windows ****
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
using System.IO;
using System.Text;

namespace Microsoft.Alm
{
    internal interface ITrace
    {
        void AddListener(TextWriter listener);

        void Flush();

        void WriteLine(string message, string filePath, int lineNumber, string memberName);
    }

    public class Trace: ITrace, IDisposable
    {
        public const string EnvironmentVariableKey = "HGCM_TRACE";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        protected Trace()
        {
            _writers = new List<TextWriter>();

            try
            {
                string traceValue = Environment.GetEnvironmentVariable(EnvironmentVariableKey);
                int val = 0;

                // if the value is true or a number greater than zero, then trace to standard error
                if (StringComparer.OrdinalIgnoreCase.Equals(traceValue, "true")
                    || (Int32.TryParse(traceValue, out val) && val > 0))
                {
                    _writers.Add(Console.Error);
                }
                // if the value is a rooted path, then trace to that file and not to the console
                else if (Path.IsPathRooted(traceValue))
                {
                    // open or create the log file
                    var stream = File.Open(traceValue, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

                    // create the writer and add it to the list
                    var writer = new StreamWriter(stream, Encoding.UTF8, 4096, true);
                    _writers.Add(writer);
                }
            }
            catch { /* squelch */ }
        }

        ~Trace()
        {
            Dispose();
        }

        internal static ITrace Instance
        {
            get
            {
                lock (_syncpoint)
                {
                    if (_instance == null)
                    {
                        _instance = new Trace();
                    }
                    return _instance;
                }
            }
            set { _instance = value; }
        }

        private static ITrace _instance;

        private static readonly object _syncpoint = new object();
        private readonly List<TextWriter> _writers;

        public static void AddListener(TextWriter listener)
            => Instance.AddListener(listener);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public void Dispose()
        {
            lock (_syncpoint)
            {
                try
                {
                    for (int i = 0; i < _writers.Count; i += 1)
                    {
                        using (var writer = _writers[i])
                        {
                            _writers.Remove(writer);
                        }
                    }
                }
                catch
                { /* squelch */ }
            }

            GC.SuppressFinalize(this);
        }

        public static void Flush()
            => Instance.Flush();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed")]
        public static void WriteLine(string message,
            [System.Runtime.CompilerServices.CallerFilePath] string filePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0,
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
            => Instance.WriteLine(message, filePath, lineNumber, memberName);

        private static string FormatText(string message, string filePath, int lineNumber, string memberName)
        {
            const int SourceColumnMaxWidth = 23;

            // source column format is file:line
            string source = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:{1}", filePath, lineNumber);

            if (source.Length > SourceColumnMaxWidth)
            {
                int idx = 0;
                int maxlen = SourceColumnMaxWidth - 3;
                int srclen = source.Length;

                while (idx >= 0 && (srclen - idx) > maxlen)
                {
                    idx = source.IndexOf('\\', idx + 1);
                }

                // if we cannot find a path seperator which allows the path to be long enough, just
                // truncate the file name
                if (idx < 0)
                {
                    idx = srclen - maxlen;
                }

                source = "..." + source.Substring(idx);
            }

            // Git's trace format is "{timestamp,-15} {source,-23} trace: {details}"
            string text = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:HH:mm:ss.ffffff} {1,-23} trace: [{2}] {3}", DateTime.Now, source, memberName, message);

            return text;
        }

        void ITrace.AddListener(TextWriter listener)
        {
            lock (_syncpoint)
            {
                // try not to add the same listener more than once
                if (_writers.Contains(listener))
                    return;

                _writers.Add(listener);
            }
        }

        void ITrace.Flush()
        {
            lock (_syncpoint)
            {
                foreach (var writer in _writers)
                {
                    writer?.Flush();
                }
            }
        }

        void ITrace.WriteLine(string message, string filePath, int lineNumber, string memberName)
        {
            lock (_syncpoint)
            {
                if (_writers.Count == 0)
                    return;

                string text = FormatText(message, filePath, lineNumber, memberName);

                foreach (var writer in _writers)
                {
                    try
                    {
                        writer?.Write(text);
                        writer?.Write('\n');
                        writer?.Flush();
                    }
                    catch { /* squelch */ }
                }
            }
        }
    }
}
