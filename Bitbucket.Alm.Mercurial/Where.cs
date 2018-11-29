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
using Microsoft.Alm;

namespace Atlassian.Bitbucket.Alm.Mercurial
{
    public class Where : Microsoft.Alm.Where
    {
        public static bool FindMercurialInstallation(string path, KnownMercurialDistribution distro, out MercurialInstallation installation)
        {
            installation = new MercurialInstallation(path, distro);
            return MercurialInstallation.IsValid(installation);
        }

        /// <summary>
        /// Finds and returns paths to Git installations in common locations.
        /// </summary>
        /// <param name="hints">(optional) List of paths the caller believes Git can be found.</param>
        /// <param name="paths">
        /// All discovered paths to the root of Git installations, ordered by 'priority' with first
        /// being the best installation to use when shelling out to Git.exe.
        /// </param>
        /// <returns><see langword="True"/> if Git was detected; <see langword="false"/> otherwise.</returns>
        public static bool FindMercurialInstallations(out List<MercurialInstallation> installations)
        {
            const string MercurialAppName = @"Mercurial";
            //const string GitSubkeyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Git_is1";
            const string MercurialValueName = "InstallLocation";

            installations = null;

            var programFiles32Path = String.Empty;
            var programFiles64Path = String.Empty;
            var appDataRoamingPath = String.Empty;
            var appDataLocalPath = String.Empty;
            var programDataPath = String.Empty;
            //var reg32HklmPath = String.Empty;
            //var reg64HklmPath = String.Empty;
            //var reg32HkcuPath = String.Empty;
            //var reg64HkcuPath = String.Empty;
            var shellPathValue = String.Empty;

            //using (var reg32HklmKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            //using (var reg32HkcuKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32))
            //using (var reg32HklmSubKey = reg32HklmKey?.OpenSubKey(GitSubkeyName))
            //using (var reg32HkcuSubKey = reg32HkcuKey?.OpenSubKey(GitSubkeyName))
            //{
            //    reg32HklmPath = reg32HklmSubKey?.GetValue(GitValueName, reg32HklmPath) as String;
            //    reg32HkcuPath = reg32HkcuSubKey?.GetValue(GitValueName, reg32HkcuPath) as String;
            //}

            if ((programFiles32Path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)) != null)
            {
                programFiles32Path = Path.Combine(programFiles32Path, MercurialAppName);
            }

            if (Environment.Is64BitOperatingSystem)
            {
                //using (var reg64HklmKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                //using (var reg64HkcuKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
                //using (var reg64HklmSubKey = reg64HklmKey?.OpenSubKey(GitSubkeyName))
                //using (var reg64HkcuSubKey = reg64HkcuKey?.OpenSubKey(GitSubkeyName))
                //{
                //    reg64HklmPath = reg64HklmSubKey?.GetValue(GitValueName, reg64HklmPath) as String;
                //    reg64HkcuPath = reg64HkcuSubKey?.GetValue(GitValueName, reg64HkcuPath) as String;
                //}

                if ((programFiles64Path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)) != null)
                {
                    programFiles64Path = Path.Combine(programFiles64Path, MercurialAppName);
                }
            }

            if ((appDataRoamingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)) != null)
            {
                appDataRoamingPath = Path.Combine(appDataRoamingPath, MercurialAppName);
            }

            if ((appDataLocalPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)) != null)
            {
                appDataLocalPath = Path.Combine(appDataLocalPath, MercurialAppName);
            }

            if ((programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)) != null)
            {
                programDataPath = Path.Combine(programDataPath, MercurialAppName);
            }

            List<MercurialInstallation> candidates = new List<MercurialInstallation>();
            // add candidate locations in order of preference
            if (Where.FindApp(MercurialAppName, out shellPathValue))
            {
                candidates.Add(new MercurialInstallation(shellPathValue, KnownMercurialDistribution.Mercurialv4));
            }
            
            if (!String.IsNullOrEmpty(programFiles32Path))
            {
                candidates.Add(new MercurialInstallation(programFiles64Path, KnownMercurialDistribution.Mercurialv4));
            }
            if (!String.IsNullOrEmpty(programFiles32Path))
            {
                candidates.Add(new MercurialInstallation(programFiles32Path, KnownMercurialDistribution.Mercurialv4));
            }
            if (!String.IsNullOrEmpty(programDataPath))
            {
                candidates.Add(new MercurialInstallation(programDataPath, KnownMercurialDistribution.Mercurialv4));
            }
            if (!String.IsNullOrEmpty(appDataLocalPath))
            {
                candidates.Add(new MercurialInstallation(appDataLocalPath, KnownMercurialDistribution.Mercurialv4));
            }
            if (!String.IsNullOrEmpty(appDataRoamingPath))
            {
                candidates.Add(new MercurialInstallation(appDataRoamingPath, KnownMercurialDistribution.Mercurialv4));
            }

            HashSet<MercurialInstallation> pathSet = new HashSet<MercurialInstallation>();
            foreach (var candidate in candidates)
            {
                if (MercurialInstallation.IsValid(candidate))
                {
                    pathSet.Add(candidate);
                }
            }

            installations = pathSet.ToList();

            Microsoft.Alm.Trace.WriteLine($"found {installations.Count} Git installation(s).");

            return installations.Count > 0;
        }

        public static bool FindMercurialConfiguration(string customPath, KnownMercurialDistribution mercurialDistribution, out MercurialInstallation installation)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// <repo>/.hg/hgrc (per-repository)
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool MercurialLocalConfig(out List<string> paths)
        {
            return MercurialLocalConfig(Environment.CurrentDirectory, out paths);
        }

        internal static bool MercurialLocalConfig(string directory, out List<string> paths)
        {
            const string HgFolderName = ".hg";
            const string LocalConfigFileName = "hgrc";

            paths = null;
            return false;
        }

        /// <summary>
        /// %USERPROFILE%\.hgrc (per-user)
        /// %USERPROFILE%\Mercurial.ini(per-user)
        /// %HOME%\.hgrc(per-user)
        /// %HOME%\Mercurial.ini(per-user)
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public static bool MercurialGlobalConfig(out List<string> paths)
        {
            const string GlobalMercurialIniFileName = "Mercurial.ini";
            const string GlobalhgrcFileName = ".hgrc";

            paths = new List<string>();

            // Get the user's home directory, then append the global config file name.
            string home = Home();

            var globalHgrcPath = Path.Combine(home, GlobalhgrcFileName);

            // if the path is valid, return it to the user.
            if (File.Exists(globalHgrcPath))
            {
                paths.Add(globalHgrcPath);
            }

            var globalMercurialIniPath = Path.Combine(home, GlobalMercurialIniFileName);

            // if the path is valid, return it to the user.
            if (File.Exists(globalMercurialIniPath))
            {
                paths.Add(globalMercurialIniPath);
            }

            if(paths.Any())
            {
                return true;
            }

            return false;
        }


        /// <summary>
        /// HKEY_LOCAL_MACHINE\SOFTWARE\Mercurial (per-installation)
        /// HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Mercurial is used when running 32-bit Python on 64-bit Windows.
        /// <install-dir>\hgrc.d\*.rc(per-installation)
        /// <install-dir>\Mercurial.ini(per-installation)
        /// <internal>/default.d/*.rc (defaults)
        /// </summary>
        /// <param name="p"></param>
        /// <param name="systemConfig"></param>
        /// <returns></returns>
        internal static bool MercurialSystemConfig(object p, out string path)
        {
            path = null;
            return false;
        }


        internal static bool MercurialPortableConfig(out string path)
        {
            path = null;
            return false;
        }
    }
}
