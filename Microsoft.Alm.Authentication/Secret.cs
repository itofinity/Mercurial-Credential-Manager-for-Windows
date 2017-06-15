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

namespace Microsoft.Alm.Authentication
{ 
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1055:UriReturnValuesShouldNotBeStrings")]
    public abstract class Secret
    {
        public static string UriToName(TargetUri targetUri, string @namespace)
        {
            BaseSecureStore.ValidateTargetUri(targetUri);
            if (string.IsNullOrWhiteSpace(@namespace))
                throw new ArgumentNullException(@namespace);

            string targetName = $"{@namespace}:{targetUri}";
            targetName = targetName.TrimEnd('/', '\\');

            return targetName;
        }

        public static string UriToUrl(TargetUri targetUri, string @namespace)
        {
            BaseSecureStore.ValidateTargetUri(targetUri);
            if (string.IsNullOrWhiteSpace(@namespace))
                throw new ArgumentNullException(@namespace);

            string targetName = $"{@namespace}:{targetUri.ToString(false, true, true)}";
            targetName = targetName.TrimEnd('/', '\\');

            return targetName;
        }

        /// <summary>
        ///     Generate a key based on the ActualUri.
        ///     This may include username, port, etc
        /// </summary>
        public static string UriToActualUrl(TargetUri targetUri, string @namespace)
        {
            BaseSecureStore.ValidateTargetUri(targetUri);
            if (String.IsNullOrWhiteSpace(@namespace))
                throw new ArgumentNullException(@namespace);

            var baseUrl = $"{targetUri.ActualUri.Scheme}://{targetUri.ActualUri.Host}{targetUri.ActualUri.AbsolutePath}";
            string targetName = $"{@namespace}:{baseUrl}";
            targetName = targetName.TrimEnd('/', '\\');

            return targetName;
        }

        public static string UriToMercurialKeyringNamePerHost(TargetUri targetUri, string @namespace)
        {
            // ignore the namespace
            var username = targetUri.ActualUri.UserInfo;

            string baseUrl = targetUri.ToString();
            string targetName = null;
            if (string.IsNullOrWhiteSpace(username))
            {
                targetName = $"@{baseUrl}";
            }
            else
            {
                targetName = $"{username}@@{baseUrl}";
            }

            targetName = targetName.TrimEnd('/', '\\');

            return $"{targetName}@Mercurial";

        }

        public static string UriToMercurialKeyringNamePerRepository(TargetUri targetUri, string @namespace)
        {
            // ignore the namespace
            var username = targetUri.ActualUri.UserInfo;

            string baseUrl = targetUri.ActualUri.AbsoluteUri;
            if(!string.IsNullOrWhiteSpace(username))
            {
                // clean away the username
                baseUrl = $"{targetUri.ActualUri.Scheme}://{targetUri.ActualUri.Host}{targetUri.ActualUri.AbsolutePath}";
            }

            string targetName = null;
            if (string.IsNullOrWhiteSpace(username))
            {
                targetName = $"@{baseUrl}";
            }
            else
            {
                targetName = $"{username}@@{baseUrl}";
            }

            targetName = targetName.TrimEnd('/', '\\');

            return $"{targetName}@Mercurial";

        }
        public delegate string UriNameConversion(TargetUri targetUri, string @namespace);
    }
}
