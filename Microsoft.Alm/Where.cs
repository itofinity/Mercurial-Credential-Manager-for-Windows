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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Microsoft.Alm
{
    public abstract class Where
    {
        /// <summary>
        /// Finds the "best" path to an app of a given name.
        /// </summary>
        /// <param name="name">The name of the application, without extension, to find.</param>
        /// <param name="path">
        /// Path to the first match file which the operating system considers executable.
        /// </param>
        /// <returns><see langword="True"/> if succeeds; <see langword="false"/> otherwise.</returns>
        static public bool FindApp(string name, out string path)
        {
            if (!String.IsNullOrWhiteSpace(name))
            {
                string pathext = Environment.GetEnvironmentVariable("PATHEXT");
                string envpath = Environment.GetEnvironmentVariable("PATH");

                string[] exts = pathext.Split(';');
                string[] paths = envpath.Split(';');

                for (int i = 0; i < paths.Length; i++)
                {
                    if (String.IsNullOrWhiteSpace(paths[i]))
                        continue;

                    for (int j = 0; j < exts.Length; j++)
                    {
                        if (String.IsNullOrWhiteSpace(exts[j]))
                            continue;

                        string value = String.Format("{0}\\{1}{2}", paths[i], name, exts[j]);
                        if (File.Exists(value))
                        {
                            value = value.Replace("\\\\", "\\");
                            path = value;
                            return true;
                        }
                    }
                }
            }

            path = null;
            return false;
        }

        /// <summary>
        /// Calculate the path to the user's home directory (~/ or %HOME%) that Git will rely on.
        /// </summary>
        /// <returns>The path to the user's home directory.</returns>
        public static string Home()
        {
            // Git relies on the %HOME% environment variable to represent the users home directory it
            // can contain embedded environment variables, so we need to expand it
            string path = Environment.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Process);

            if (path != null)
            {
                path = Environment.ExpandEnvironmentVariables(path);

                // If the path is good, return it.
                if (Directory.Exists(path))
                    return path;
            }

            // Absent the %HOME% variable, Git will construct it via %HOMEDRIVE%%HOMEPATH%, so we'll
            // attempt to that here and if it is valid, return it as %HOME%.
            string homeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE", EnvironmentVariableTarget.Process);
            string homePath = Environment.GetEnvironmentVariable("HOMEPATH", EnvironmentVariableTarget.Process);

            if (homeDrive != null && homePath != null)
            {
                path = homeDrive + homePath;

                if (Directory.Exists(path))
                    return path;
            }

            // When all else fails, Git falls back to %USERPROFILE% as the user's home directory, so
            // should we.
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
    }
}
