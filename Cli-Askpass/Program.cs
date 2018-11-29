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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Alm.Authentication;
using Atlassian.Bitbucket.Alm.Mercurial;

namespace Microsoft.Alm.Cli
{
    internal partial class Program
    {
        public const string Title = "Askpass Utility for Windows";
        public const string Description = "Secure askpass utility for Windows, by Microsoft";
        public const string DefinitionUrlPassphrase = "https://www.visualstudio.com/docs/git/gcm-ssh-passphrase";

        private static readonly Regex AskCredentialRegex = new Regex(@"(\S+)\s+for\s+['""]([^'""]+)['""]:\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex AskPassphraseRegex = new Regex(@"Enter\s+passphrase\s*for\s*key\s*['""]([^'""]+)['""]\:\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex AskPasswordRegex = new Regex(@"(\S+)'s\s+password:\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex AskAuthenticityRegex = new Regex(@"^\s*The authenticity of host '([^']+)' can't be established.\s+RSA key fingerprint is ([^\s:]+:[^\.]+).", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        internal static bool TryParseUrlCredentials(string targetUrl, out string username, out string password)
        {
            // config stored credentials come in the format of <username>[:<password>]@<url>
            // with password being optional scheme terminator is actually "://" so we need
            // adjust to get the correct index
            int schemeTerminator = targetUrl.IndexOf(':') + 2;
            int credentialTerminator = targetUrl.IndexOf('@', schemeTerminator + 1);

            if (credentialTerminator > 0)
            {
                // only check within the credential portion of the url, don't look past the
                // '@' because the port token is the same as the username / password seperator.
                int credentialLength = credentialTerminator - schemeTerminator;
                credentialLength = Math.Max(0, credentialLength);

                // Now that we have a proper set of bounds for the credentials portion of the url,
                // check for a username:password pair.
                int passwordTerminator = targetUrl.IndexOf(':', schemeTerminator + 1, credentialLength);

                if (passwordTerminator > 0)
                {
                    username = targetUrl.Substring(schemeTerminator + 1, passwordTerminator - schemeTerminator - 1);
                    password = targetUrl.Substring(passwordTerminator + 1, credentialTerminator - passwordTerminator + 1);

                    // Unescape credentials
                    username = Uri.UnescapeDataString(username);
                    password = Uri.UnescapeDataString(password);
                }
                else
                {
                    username = targetUrl.Substring(schemeTerminator + 1, credentialTerminator - schemeTerminator - 1);
                    password = null;

                    // Unescape credentials
                    username = Uri.UnescapeDataString(username);
                }

                return true;
            }

            username = null;
            password = null;

            return false;
        }

        private static void Askpass(string[] args)
        {
            if (args == null || args.Length == 0)
                throw new ArgumentException("Arguments cannot be empty.");

            Gui.UserPromptKind promptKind = Gui.UserPromptKind.SshPassphrase;

            Match match;
            if ((match = AskPasswordRegex.Match(args[0])).Success)
            {
                promptKind = Gui.UserPromptKind.CredentialsPassword;
            }
            else if ((match = AskPassphraseRegex.Match(args[0])).Success)
            {
                promptKind = Gui.UserPromptKind.SshPassphrase;
            }

            if (match.Success)
            {
                Trace.WriteLine("querying for passphrase key.");

                if (match.Groups.Count < 2)
                    throw new ArgumentException("Unable to understand command.");

                // string request = match.Groups[0].Value;
                string resource = match.Groups[1].Value;

                Trace.WriteLine($"open dialog for '{resource}'.");

                System.Windows.Application application = new System.Windows.Application();
                Gui.UserPromptDialog prompt = new Gui.UserPromptDialog(promptKind, resource);
                application.Run(prompt);

                if (!prompt.Failed && !string.IsNullOrEmpty(prompt.Response))
                {
                    string passphase = prompt.Response;

                    Trace.WriteLine("passphase acquired.");

                    Console.Out.Write(passphase + "\n");
                    return;
                }

                Die("failed to interactively acquire credentials.");
            }

            if ((match = AskCredentialRegex.Match(args[0])).Success)
            {
                Trace.WriteLine("querying for basic credentials.");

                if (match.Groups.Count < 3)
                    throw new ArgumentException("Unable to understand command.");

                string seeking = match.Groups[1].Value;
                string targetUrl = match.Groups[2].Value;

                // Since we're looking for HTTP(s) credentials, we can use NetFx `Uri` class.
                if (Uri.TryCreate(targetUrl, UriKind.Absolute, out Uri targetUri))
                {
                    Trace.WriteLine($"success parsing URL, targetUri = '{targetUri}'.");

                    if (TryParseUrlCredentials(targetUrl, out string username, out string password))
                    {
                        if (password != null
                            && seeking.Equals("Password", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.Out.Write(password + '\n');
                            return;
                        }

                        // print the username if it sought
                        if (seeking.Equals("Username", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.Out.Write(username + '\n');
                            return;
                        }
                    }

                    // create a target Url with the credential portion stripped, because Git doesn't
                    // report hosts with credentials
                    targetUrl = targetUri.Scheme + "://";

                    // Add the username@ portion of the url if it exists
                    if (username != null)
                    {
                        targetUrl += Uri.EscapeDataString(username);

                        targetUrl += '@';
                    }

                    targetUrl += targetUri.Host;

                    // retain the port if specified
                    if (!targetUri.IsDefaultPort)
                    {
                        targetUrl += $":{targetUri.Port}";
                    }

                    // retain the path if specified
                    if (!string.IsNullOrWhiteSpace(targetUri.LocalPath))
                    {
                        targetUrl += targetUri.LocalPath;
                    }

                    if (Uri.TryCreate(targetUrl, UriKind.Absolute, out targetUri))
                    {
                        Trace.WriteLine($"success parsing URL, targetUri = '{targetUri}'.");

                        OperationArguments operationArguments = new OperationArguments.Impl(targetUri);
                        operationArguments.SetCredentials(username, password);

                        // load up the operation arguments, enable tracing, and query for credentials
                        LoadOperationArguments(operationArguments);
                        EnableTraceLogging(operationArguments);

                        Credential credentials;
                        if ((credentials = QueryCredentials(operationArguments)) != null)
                        {
                            if (seeking.Equals("Username", StringComparison.OrdinalIgnoreCase))
                            {
                                Trace.WriteLine($"username for '{targetUrl}' asked for and found.");

                                Console.Out.Write(credentials.Username + '\n');
                                return;
                            }

                            if (seeking.Equals("Password", StringComparison.OrdinalIgnoreCase))
                            {
                                Trace.WriteLine($"password for '{targetUrl}' asked for and found.");

                                Console.Out.Write(credentials.Password + '\n');
                                return;
                            }
                        }
                        else
                        {
                            Trace.WriteLine($"user cancelled credential dialog.");
                            return;
                        }
                    }
                    else
                    {
                        Trace.WriteLine("error: unable to parse target URL.");
                    }
                }
                else
                {
                    Trace.WriteLine("error: unable to parse supplied URL.");
                }

                Die($"failed to detect {seeking} in target URL.");
            }

            if ((match = AskAuthenticityRegex.Match(args[0])).Success)
            {
                string host = match.Groups[1].Value;
                string fingerprint = match.Groups[2].Value;

                Trace.WriteLine($"requesting authorization to add {host} ({fingerprint}) to known hosts.");

                System.Windows.Application application = new System.Windows.Application();
                Gui.UserPromptDialog prompt = new Gui.UserPromptDialog(host, fingerprint);
                application.Run(prompt);

                if (prompt.Failed)
                {
                    Trace.WriteLine("denied authorization of host.");
                    Console.Out.Write("no\n");
                }
                else
                {
                    Trace.WriteLine("approved authorization of host.");
                    Console.Out.Write("yes\n");
                }

                return;
            }

            Die("failed to acquire credentials.");
        }

        [STAThread]
        private static void Main(string[] args)
        {
            EnableDebugTrace();

            if (args.Length == 0
                || string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "\\?", StringComparison.Ordinal))
            {
                PrintHelpMessage();
                return;
            }

            PrintArgs(args);

            try
            {
                Askpass(args);
            }
            catch (AggregateException exception)
            {
                // print out more useful information when an `AggregateException` is encountered
                exception = exception.Flatten();

                // find the first inner exception which isn't an `AggregateException` with fallback
                // to the canonical `.InnerException`
                Exception innerException = exception.InnerExceptions.FirstOrDefault(e => !(e is AggregateException))
                                        ?? exception.InnerException;

                Die(innerException);
            }
            catch (Exception exception)
            {
                Die(exception);
            }

            Trace.Flush();
        }

        private static void PrintHelpMessage()
        {
            const string HelpFileName = "git-askpass.html";

            Console.Out.WriteLine("usage: git askpass '<user_prompt_text>'");

            List<MercurialInstallation> installations;
            if (Atlassian.Bitbucket.Alm.Mercurial.Where.FindMercurialInstallations(out installations))
            {
                foreach (var installation in installations)
                {
                    if (Directory.Exists(installation.Doc))
                    {
                        string doc = Path.Combine(installation.Doc, HelpFileName);

                        // if the help file exists, send it to the operating system to display to the user
                        if (File.Exists(doc))
                        {
                            Trace.WriteLine($"opening help documentation '{doc}'.");

                            Process.Start(doc);

                            return;
                        }
                    }
                }
            }

            Die("Unable to open help documentation.");
        }
    }
}
