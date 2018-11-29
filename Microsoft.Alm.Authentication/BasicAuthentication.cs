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
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Microsoft.Alm.Authentication
{
    /// <summary>
    /// Facilitates basic authentication using simple username and password schemes.
    /// </summary>
    public sealed class BasicAuthentication: BaseAuthentication, IAuthentication
    {
        public static readonly Credential NtlmCredentials = WwwAuthenticateHelper.Credentials;

        /// <summary>
        /// Creates a new <see cref="BasicAuthentication"/> object with an underlying credential store.
        /// </summary>
        /// <param name="credentialStore">The <see cref="ICredentialStore"/> to delegate to.</param>
        /// <param name="ntlmSupport">
        /// <para>The level of NTLM support to be provided by this instance.</para>
        /// <para>
        /// If ` <see cref="NtlmSupport.Always"/>` is used, the `
        /// <paramref name="acquireCredentialsCallback"/>` and `
        /// <paramref name="acquireResultCallback"/>` will be ignored by ` <see cref="GetCredentials(TargetUri)"/>`.
        /// </para>
        /// </param>
        /// <param name="acquireCredentialsCallback">(optional) delegate for acquiring credentials.</param>
        /// <param name="acquireResultCallback">
        /// (optional) delegate for notification of acquisition results.
        /// </param>
        public BasicAuthentication(
            ICredentialStore credentialStore,
            NtlmSupport ntlmSupport,
            AcquireCredentialsDelegate acquireCredentialsCallback,
            AcquireResultDelegate acquireResultCallback)
        {
            if (credentialStore == null)
                throw new ArgumentNullException(nameof(credentialStore));

            _acquireCredentials = acquireCredentialsCallback;
            _acquireResult = acquireResultCallback;
            _credentialStore = credentialStore;
            _ntlmSupport = ntlmSupport;
        }

        public BasicAuthentication(ICredentialStore credentialStore)
            : this(credentialStore, NtlmSupport.Auto, null, null)
        { }

        /// <summary>
        /// Gets the underlying credential store for this instance of <see cref="BasicAuthentication"/>.
        /// </summary>
        internal ICredentialStore CredentialStore
        {
            get { return _credentialStore; }
        }

        /// <summary>
        /// Gets the level of NTLM support for this instance of <see cref="BasicAuthentication"/>.
        /// </summary>
        public NtlmSupport NtlmSupport
        {
            get { return _ntlmSupport; }
        }

        private readonly AcquireCredentialsDelegate _acquireCredentials;
        private readonly AcquireResultDelegate _acquireResult;
        private readonly ICredentialStore _credentialStore;
        private AuthenticationHeaderValue[] _httpAuthenticateOptions;
        private NtlmSupport _ntlmSupport;

        /// <summary>
        /// Acquires credentials via the registered callbacks.
        /// </summary>
        /// <param name="targetUri">
        /// The uniform resource indicator used to uniquely identify the credentials.
        /// </param>
        /// <returns>
        /// If successful a <see cref="Credential"/> object from the authentication object, authority
        /// or storage; otherwise <see langword="null"/>.
        /// </returns>
        public async Task<Credential> AcquireCredentials(TargetUri targetUri)
        {
            BaseSecureStore.ValidateTargetUri(targetUri);

            if (_ntlmSupport != NtlmSupport.Never)
            {
                // get the WWW-Authenticate headers (if any)
                if (_httpAuthenticateOptions == null)
                {
                    _httpAuthenticateOptions = await WwwAuthenticateHelper.GetHeaderValues(targetUri);
                }

                // if the headers contain NTML as an option, then fall back to NTLM
                if (_httpAuthenticateOptions.Any(x => WwwAuthenticateHelper.IsNtlm(x)))
                {
                    Trace.WriteLine($"'{targetUri}' supports NTLM, sending NTLM credentials instead");

                    return NtlmCredentials;
                }
            }

            Credential credentials = null;

            if (_ntlmSupport != NtlmSupport.Always && _acquireCredentials != null)
            {
                Trace.WriteLine($"prompting user for credentials for '{targetUri}'.");

                credentials = _acquireCredentials(targetUri);

                if (_acquireResult != null)
                {
                    AcquireCredentialResult result = (credentials == null)
                        ? AcquireCredentialResult.Failed
                        : AcquireCredentialResult.Suceeded;

                    _acquireResult(targetUri, result);
                }
            }

            // If credentials have been acquired, write them to the secret store.
            if (credentials != null)
            {
                _credentialStore.WriteCredentials(targetUri, credentials);
            }

            return credentials;
        }

        /// <summary>
        /// Deletes a <see cref="Credential"/> from the storage used by the authentication object.
        /// </summary>
        /// <param name="targetUri">
        /// The uniform resource indicator used to uniquely identify the credentials.
        /// </param>
        public override void DeleteCredentials(TargetUri targetUri)
        {
            BaseSecureStore.ValidateTargetUri(targetUri);

            this.CredentialStore.DeleteCredentials(targetUri);
        }

        /// <summary>
        /// Gets a <see cref="Credential"/> from the storage used by the authentication object.
        /// </summary>
        /// <param name="targetUri">
        /// The uniform resource indicator used to uniquely identify the credentials.
        /// </param>
        /// <returns>
        /// If successful a <see cref="Credential"/> object from the authentication object, authority
        /// or storage; otherwise <see langword="null"/>.
        /// </returns>
        public override Credential GetCredentials(TargetUri targetUri)
        {
            BaseSecureStore.ValidateTargetUri(targetUri);

            return this.CredentialStore.ReadCredentials(targetUri);
        }

        /// <summary>
        /// Sets a <see cref="Credential"/> in the storage used by the authentication object.
        /// </summary>
        /// <param name="targetUri">
        /// The uniform resource indicator used to uniquely identify the credentials.
        /// </param>
        /// <param name="credentials">The value to be stored.</param>
        /// <returns><see langword="true"/> if successful; otherwise <see langword="false"/>.</returns>
        public override void SetCredentials(TargetUri targetUri, Credential credentials)
        {
            BaseSecureStore.ValidateTargetUri(targetUri);
            BaseSecureStore.ValidateCredential(credentials);

            this.CredentialStore.WriteCredentials(targetUri, credentials);
        }
    }
}
