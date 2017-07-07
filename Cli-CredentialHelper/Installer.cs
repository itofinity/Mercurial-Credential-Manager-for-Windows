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
using Microsoft.Win32;
using Atlassian.Bitbucket.Alm.Mercurial;
using Trace = Microsoft.Alm.Trace;
using Where = Atlassian.Bitbucket.Alm.Mercurial.Where;

namespace Microsoft.Alm.Cli
{
    internal class Installer
    {
        internal const string ParamPathKey = "--path";
        internal const string ParamPassiveKey = "--passive";
        internal const string ParamForceKey = "--force";
        internal const string FailFace = "U_U";
        internal const string TadaFace = "^_^";
        internal const string ConfigExtKey = "hgext.mercurial_credential_manager";
        internal const string ConfigExtSection = "extensions";

        //private static readonly Version NetFxMinVersion = new Version(4, 5, 1);
        private static readonly IReadOnlyList<string> FileList = new string[]
        {
            // dot net GUI
            "Bitbucket.Alm.Mercurial.dll",
            "Bitbucket.Authentication.dll",
            "Microsoft.Alm.dll",
            "Microsoft.Alm.Authentication.dll",
            "mercurial-credential-manager.exe",
            "mercurial-askpass.exe",
            // python mercurial extension
            "mercurial_credential_manager.py",
            "mercurial_extension_utils.py",
            "mercurial_extension_utils_loader.py",
            "mercurial_extension_utils_win_doc.py",
        };

        private static readonly IReadOnlyList<string> DocsList = new string[]
        {
            "mercurial-askpass.html",
            "mercurial-credential-manager.html",
        };

        public Installer()
        {
            var args = Environment.GetCommandLineArgs();

            // parse arguments
            for (int i = 2; i < args.Length; i++)
            {
                if (string.Equals(args[i], ParamPathKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Length > i + 1)
                    {
                        i += 1;
                        _customPath = args[i];

                        Trace.WriteLine($"{ParamPathKey} = '{_customPath}'.");
                    }
                }
                else if (string.Equals(args[i], ParamPassiveKey, StringComparison.OrdinalIgnoreCase))
                {
                    _isPassive = true;

                    Trace.WriteLine($"{ParamPassiveKey} = true.");
                }
                else if (string.Equals(args[i], ParamForceKey, StringComparison.OrdinalIgnoreCase))
                {
                    _isForced = true;

                    Trace.WriteLine($"{ParamForceKey} = true.");
                }
            }
        }

        internal static string UserHgExtPath
        {
            get
            {
                if (_userHgExtPath == null)
                {
                    string val1 = null;
                    string val2 = null;
                    string val3 = null;
                    var vars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process);

                    // check %HOME% first
                    if ((val1 = vars["HOME"] as string) != null
                        && Directory.Exists(val1))
                    {
                        _userHgExtPath = val1;
                    }
                    // check %HOMEDRIVE%%HOMEPATH% second
                    else if ((val1 = vars["HOMEDRIVE"] as string) != null && (val2 = vars["HOMEPATH"] as string) != null
                        && Directory.Exists(val3 = val1 + val2))
                    {
                        _userHgExtPath = val3;
                    }
                    // check %USERPROFILE% last
                    else if ((val1 = vars["USERPROFILE"] as string) != null)
                    {
                        _userHgExtPath = val1;
                    }

                    if (_userHgExtPath != null)
                    {
                        // check %HOME%\bin to %PATH%
                        _userHgExtPath = Path.Combine(_userHgExtPath, "bin", "hgext", "mercurial-credential-manager");

                        Trace.WriteLine($"user hgext bin found at '{_userHgExtPath}'.");
                    }
                }
                return _userHgExtPath;
            }
        }

        private static string _userHgExtPath = null;

        public int ExitCode
        {
            get { return (int)Result; }
            set { Result = (ResultValue)value; }
        }

        public ResultValue Result { get; private set; }

        private bool _isPassive = false;
        private bool _isForced = false;
        private string _customPath = null;
        private TextWriter _stdout = null;
        private TextWriter _stderr = null;

        private string _deploymentPath = null;

        public void DeployConsole()
        {
            SetOutput(_isPassive, _isPassive && _isForced);
            try
            {
#if !DEBUG
                System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
                if (!principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                {
                    DeployElevated();
                    return;
                }
#endif
                List<MercurialInstallation> installations = null;

                // use the custom installation path if supplied
                if (!string.IsNullOrEmpty(_customPath))
                {

                    Console.Out.WriteLine();
                    Console.Out.WriteLine($"Deploying to custom path: '{_customPath}'.");
                    _deploymentPath = _customPath;
                }
                // since no custom installation path was supplied, use default logic
                else
                {
                    Console.Out.WriteLine();
                    Console.Out.WriteLine($"Deploying to standard path: '{UserHgExtPath}'.");
                    _deploymentPath = UserHgExtPath;
                }

                Console.Out.WriteLine("Looking for Mercurial installation(s)...");
                if (Atlassian.Bitbucket.Alm.Mercurial.Where.FindMercurialInstallations(out installations))
                {
                    foreach (var installation in installations)
                    {
                        Console.Out.WriteLine($"  {installation.Path}");
                    }
                }

                if (installations == null)
                {
                    Program.LogEvent("No Mercurial installation found, unable to continue.", EventLogEntryType.Error);
                    Console.Out.WriteLine();
                    Program.WriteLine("Fatal: Mercurial was not detected, unable to continue. {FailFace}");
                    Pause();

                    Result = ResultValue.MercurialNotFound;
                    return;
                }

                List<string> copiedFiles;

                Console.Out.WriteLine();
                Console.Out.WriteLine($"Deploying from '{Program.Location}' to '{_deploymentPath}'.");

                if (!Directory.Exists(_deploymentPath))
                {
                    Directory.CreateDirectory(_deploymentPath);
                }

                if (CopyFiles(Program.Location, _deploymentPath, FileList, out copiedFiles))
                {
                    int copiedCount = copiedFiles.Count;

                    foreach (var file in copiedFiles)
                    {
                        Console.Out.WriteLine($"  {file}");
                    }

                    if (CopyFiles(Program.Location, _deploymentPath, DocsList, out copiedFiles))
                    {
                        copiedCount = copiedFiles.Count;

                        foreach (var file in copiedFiles)
                        {
                            Console.Out.WriteLine($"  {file}");
                        }
                    }

                    Program.LogEvent($"Deployment to '{_deploymentPath}' succeeded.", EventLogEntryType.Information);
                    Console.Out.WriteLine($"     {copiedCount} file(s) copied");
                }
                else if (_isForced)
                {
                    Program.LogEvent($"Deployment to '{_deploymentPath}' failed.", EventLogEntryType.Warning);
                    Program.WriteLine($"  deployment failed. {FailFace}");
                }
                else
                {
                    Program.LogEvent($"Deployment to '{_deploymentPath}' failed.", EventLogEntryType.Error);
                    Program.WriteLine($"  deployment failed. {FailFace}");
                    Pause();

                    Result = ResultValue.DeploymentFailed;
                    return;
                }

                ConfigurationLevel types = ConfigurationLevel.Global;

                ConfigurationLevel updateTypes;
                if (SetMercurialConfig(installations, MercurialConfigAction.Set, types, out updateTypes))
                {
                    if ((updateTypes & ConfigurationLevel.Global) == ConfigurationLevel.Global)
                    {
                        Console.Out.WriteLine("Updated your ~/.hgrc");
                    }
                    else
                    {
                        Console.Out.WriteLine();
                        Program.WriteLine("Fatal: Unable to update your ~/.hgrc correctly.");

                        Result = ResultValue.MercurialConfigGlobalFailed;
                        return;
                    }
                }

                // all necessary content has been deployed to the system
                Result = ResultValue.Success;

                Program.LogEvent($"{Program.Title} v{Program.Version.ToString(3)} successfully deployed.", EventLogEntryType.Information);
                Console.Out.WriteLine();
                Console.Out.WriteLine($"Success! {Program.Title} was deployed! {TadaFace}");
                Pause();
            }
            finally
            {
                SetOutput(true, true);
            }
        }

        public static bool DetectNetFx(out Version version)
        {
            const string NetFxKeyBase = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Net Framework Setup\NDP\v4\";
            const string NetFxKeyClient = NetFxKeyBase + @"\Client";
            const string NetFxKeyFull = NetFxKeyBase + @"\Full";
            const string ValueName = "Version";
            const string DefaultValue = "0.0.0";

            // default to not found state
            version = null;

            string netfxString = null;
            Version netfxVerson = null;

            // query for existing installations of .NET
            if ((netfxString = Registry.GetValue(NetFxKeyClient, ValueName, DefaultValue) as String) != null
                    && Version.TryParse(netfxString, out netfxVerson)
                || (netfxString = Registry.GetValue(NetFxKeyFull, ValueName, DefaultValue) as String) != null
                    && Version.TryParse(netfxString, out netfxVerson))
            {
                Program.LogEvent($"NetFx version {netfxVerson.ToString(3)} detected.", EventLogEntryType.Information);
                Trace.WriteLine($"NetFx version {netfxVerson.ToString(3)} detected.");

                version = netfxVerson;
            }

            return version != null;
        }

        public void RemoveConsole()
        {
            SetOutput(_isPassive, _isPassive && _isForced);
            try
            {
#if !DEBUG
                System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
                if (!principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                {
                    RemoveElevated();
                    return;
                }
#endif
                List<MercurialInstallation> installations = null;

                // use the custom installation path if supplied
                if (!string.IsNullOrEmpty(_customPath))
                {

                    Console.Out.WriteLine();
                    Console.Out.WriteLine($"Remove from custom path: '{_customPath}'.");
                    _deploymentPath = _customPath;
                }
                // since no custom installation path was supplied, use default logic
                else
                {
                    Console.Out.WriteLine();
                    Console.Out.WriteLine($"Remove from standard path: '{UserHgExtPath}'.");
                    _deploymentPath = UserHgExtPath;
                }

                Console.Out.WriteLine("Looking for Mercurial installation(s)...");
                if (Atlassian.Bitbucket.Alm.Mercurial.Where.FindMercurialInstallations(out installations))
                {
                    foreach (var installation in installations)
                    {
                        Console.Out.WriteLine($"  {installation.Path}");
                    }
                }

                if (installations == null)
                {
                    Program.LogEvent("No Mercurial installation found, unable to continue.", EventLogEntryType.Error);
                    Console.Out.WriteLine();
                    Program.WriteLine("Fatal: Mercurial was not detected, unable to continue. {FailFace}");
                    Pause();

                    Result = ResultValue.MercurialNotFound;
                    return;
                }

                ConfigurationLevel types = ConfigurationLevel.Global;

                ConfigurationLevel updateTypes;
                if (SetMercurialConfig(installations, MercurialConfigAction.Unset, types, out updateTypes))
                {
                    if ((updateTypes & ConfigurationLevel.Global) == ConfigurationLevel.Global)
                    {
                        Console.Out.WriteLine("Updated your ~/.hgrc");
                    }
                    else
                    {
                        Console.Out.WriteLine();
                        Program.WriteLine("Fatal: Unable to update your ~/.hgrc correctly.");

                        Result = ResultValue.MercurialConfigGlobalFailed;
                        return;
                    }
                }

                List<string> cleanedFiles;

                if (Directory.Exists(_deploymentPath))
                {
                    Console.Out.WriteLine();
                    Console.Out.WriteLine($"Removing from '{_deploymentPath}'.");

                    if (CleanFiles(_deploymentPath, FileList, out cleanedFiles))
                    {
                        int cleanedCount = cleanedFiles.Count;

                        foreach (var file in cleanedFiles)
                        {
                            Console.Out.WriteLine($"  {file}");
                        }

                        if (CleanFiles(_deploymentPath, DocsList, out cleanedFiles))
                        {
                            cleanedCount += cleanedFiles.Count;

                            foreach (var file in cleanedFiles)
                            {
                                Console.Out.WriteLine($"  {file}");
                            }
                        }

                        Console.Out.WriteLine($"     {cleanedCount} file(s) cleaned");
                    }
                    else if (_isForced)
                    {
                        Console.Error.WriteLine($"  removal failed. {FailFace}");
                    }
                    else
                    {
                        Console.Error.WriteLine($"  removal failed. {FailFace}");
                        Pause();

                        Result = ResultValue.RemovalFailed;
                        return;
                    }
                }

                // all necessary content has been deployed to the system
                Result = ResultValue.Success;

                Program.LogEvent($"{Program.Title} successfully removed.", EventLogEntryType.Information);

                Console.Out.WriteLine();
                Console.Out.WriteLine($"Success! {Program.Title} was removed! {TadaFace}");
                Pause();
            }
            finally
            {
                SetOutput(true, true);
            }
        }

        public bool SetMercurialConfig(List<MercurialInstallation> installations, MercurialConfigAction action, ConfigurationLevel type, out ConfigurationLevel updated)
        {
            Trace.WriteLine($"action = '{action}'.");

            updated = ConfigurationLevel.None;

            if ((installations == null || installations.Count == 0) && !Atlassian.Bitbucket.Alm.Mercurial.Where.FindMercurialInstallations(out installations))
            {
                Trace.WriteLine("No Mercurial installations detected to update.");
                return false;
            }

            if ((type & ConfigurationLevel.Global) == ConfigurationLevel.Global)
            {
                // the 0 entry in the installations list is the "preferred" instance of Mercurial
                string hgPath = installations[0].Mercurial;
                bool set = action == MercurialConfigAction.Set;
                var home = Where.Home();

                var config = Configuration.ReadConfiguration(home, false, true);
                //var prefix = "hgext";
                var section = "extensions";
                var key = "mercurial_credential_manager";

                var value = Path.Combine(_deploymentPath, "mercurial_credential_manager.py");
                var entry = new Configuration.Entry(key, value);

                if (set)
                {
                    if (config.TrySetEntry(ConfigurationLevel.Global, section, string.Empty, key, string.Empty, value))
                    {
                        updated |= ConfigurationLevel.Global;
                    }
                }
                else
                {
                    if (config.TryUnsetEntry(ConfigurationLevel.Global, section, string.Empty, key, string.Empty, value))
                    {
                        updated |= ConfigurationLevel.Global;
                    }
                }
            }

            return true;
        }

        private static bool CleanFiles(string path, IReadOnlyList<string> files, out List<string> cleanedFiles)
        {
            cleanedFiles = new List<string>();

            if (!Directory.Exists(path))
            {
                Trace.WriteLine($"path '{path}' does not exist.");
                return false;
            }

            try
            {
                foreach (string file in files)
                {
                    string target = Path.Combine(path, file);

                    Trace.WriteLine($"clean '{target}'.");

                    File.Delete(target);

                    cleanedFiles.Add(file);

                    if (target.EndsWith(".py"))
                    {
                        File.Delete(target + "c");
                        cleanedFiles.Add(file);
                    }
                }

                return true;
            }
            catch
            {
                Trace.WriteLine($"clean of '{path}' failed.");
                return false;
            }
        }

        private static bool CopyFiles(string srcPath, string dstPath, IReadOnlyList<string> files, out List<string> copiedFiles)
        {
            copiedFiles = new List<string>();

            if (!Directory.Exists(srcPath))
            {
                Trace.WriteLine($"source '{srcPath}' does not exist.");
                return false;
            }

            if (Directory.Exists(dstPath))
            {
                try
                {
                    foreach (string file in files)
                    {
                        Trace.WriteLine($"copy '{file}' from '{srcPath}' to '{dstPath}'.");

                        string src = Path.Combine(srcPath, file);
                        string dst = Path.Combine(dstPath, file);

                        if(!File.Exists(src))
                        {
                            Trace.WriteLine($"Unable to copy '{src}' does not exist.");
                            continue;
                        }

                        File.Copy(src, dst, true);

                        copiedFiles.Add(file);
                    }

                    return true;
                }
                catch
                {
                    Trace.WriteLine("copy failed.");
                    return false;
                }
            }
            else
            {
                Trace.WriteLine($"destination '{dstPath}' does not exist.");
            }

            Trace.WriteLine("copy failed.");
            return false;
        }

        private void DeployElevated()
        {
            if (_isPassive)
            {
                this.Result = ResultValue.Unprivileged;
            }
            else
            {
                /* cannot install while not elevated (need access to %PROGRAMFILES%), re-launch
                   self as an elevated process with identical arguments. */

                // build arguments
                var arguments = new System.Text.StringBuilder("install");
                if (_isPassive)
                {
                    arguments.Append(" ")
                             .Append(ParamPassiveKey);
                }
                if (_isForced)
                {
                    arguments.Append(" ")
                             .Append(ParamForceKey);
                }
                if (!string.IsNullOrEmpty(_customPath))
                {
                    arguments.Append(" ")
                             .Append(ParamForceKey)
                             .Append(" \"")
                             .Append(_customPath)
                             .Append("\"");
                }

                // build process start options
                var options = new ProcessStartInfo()
                {
                    FileName = "cmd",
                    Arguments = $"/c \"{Program.ExecutablePath}\" {arguments}",
                    UseShellExecute = true, // shellexecute for verb usage
                    Verb = "runas", // used to invoke elevation
                    WorkingDirectory = Program.Location,
                };

                Trace.WriteLine($"create process: cmd '{options.Verb}' '{options.FileName}' '{options.Arguments}' .");

                try
                {
                    // create the process
                    var elevated = Process.Start(options);

                    // wait for the process to complete
                    elevated.WaitForExit();

                    Trace.WriteLine($"process exited with {elevated.ExitCode}.");

                    // exit with the elevated process' exit code
                    this.ExitCode = elevated.ExitCode;
                }
                catch (Exception exception)
                {
                    Trace.WriteLine($"process failed with '{exception.Message}'");
                    this.Result = ResultValue.Unprivileged;
                }
            }
        }

        private void Pause()
        {
            if (!_isPassive)
            {
                Console.Out.WriteLine();
                Console.Out.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }

        private void RemoveElevated()
        {
            if (_isPassive)
            {
                this.Result = ResultValue.Unprivileged;
            }
            else
            {
                /* cannot uninstall while not elevated (need access to %PROGRAMFILES%), re-launch
                   self as an elevated process with identical arguments. */

                // build arguments
                var arguments = new System.Text.StringBuilder("remove");
                if (_isPassive)
                {
                    arguments.Append(" ")
                             .Append(ParamPassiveKey);
                }
                if (_isForced)
                {
                    arguments.Append(" ")
                             .Append(ParamForceKey);
                }
                if (!string.IsNullOrEmpty(_customPath))
                {
                    arguments.Append(" ")
                             .Append(ParamForceKey)
                             .Append(" \"")
                             .Append(_customPath)
                             .Append("\"");
                }

                // build process start options
                var options = new ProcessStartInfo()
                {
                    FileName = "cmd",
                    Arguments = $"/c \"{Program.ExecutablePath}\" {arguments}",
                    UseShellExecute = true, // shellexecute for verb usage
                    Verb = "runas", // used to invoke elevation
                    WorkingDirectory = Program.Location,
                };

                Trace.WriteLine($"create process: cmd '{options.Verb}' '{options.FileName}' '{options.Arguments}' .");

                try
                {
                    // create the process
                    var elevated = Process.Start(options);

                    // wait for the process to complete
                    elevated.WaitForExit();

                    Trace.WriteLine($"process exited with {elevated.ExitCode}.");

                    // exit with the elevated process' exit code
                    this.ExitCode = elevated.ExitCode;
                }
                catch (Exception exception)
                {
                    Trace.WriteLine($"! process failed with '{exception.Message}'.");
                    this.Result = ResultValue.Unprivileged;
                }
            }
        }

        private void SetOutput(bool muteStdout, bool muteStderr)
        {
            if (muteStdout)
            {
                _stdout = Console.Out;
                Console.SetOut(TextWriter.Null);
            }
            else if (_stdout != null)
            {
                Console.SetOut(_stdout);
                _stdout = null;
            }

            if (muteStderr)
            {
                _stderr = Console.Out;
                Console.SetOut(TextWriter.Null);
            }
            else if (_stderr != null)
            {
                Console.SetOut(_stderr);
                _stderr = null;
            }
        }

        public enum ResultValue: int
        {
            UnknownFailure = -1,
            Success = 0,
            InvalidCustomPath,
            DeploymentFailed,
            NetFxNotFound,
            Unprivileged,
            MercurialConfigGlobalFailed,
            MercurialConfigSystemFailed,
            MercurialNotFound,
            RemovalFailed,
        }

        public enum MercurialConfigAction
        {
            Set,
            Unset,
        }
    }
}
