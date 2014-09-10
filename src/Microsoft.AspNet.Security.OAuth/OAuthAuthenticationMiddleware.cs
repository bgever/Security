﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Security.DataHandler;
using Microsoft.AspNet.Security.DataProtection;
using Microsoft.AspNet.Security.Infrastructure;
using Microsoft.Framework.Logging;

namespace Microsoft.AspNet.Security.OAuth
{
    /// <summary>
    /// An ASP.NET middleware for authenticating users using OAuth services.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Middleware are not disposable.")]
    public class OAuthAuthenticationMiddleware<TOptions, TNotifications> : AuthenticationMiddleware<TOptions>
        where TOptions : OAuthAuthenticationOptions<TNotifications>
        where TNotifications : IOAuthAuthenticationNotifications
    {
        /// <summary>
        /// Initializes a new <see cref="OAuthAuthenticationMiddleware"/>.
        /// </summary>
        /// <param name="next">The next middleware in the HTTP pipeline to invoke.</param>
        /// <param name="dataProtectionProvider"></param>
        /// <param name="loggerFactory"></param>
        /// <param name="options">Configuration options for the middleware.</param>
        public OAuthAuthenticationMiddleware(
            RequestDelegate next,
            IDataProtectionProvider dataProtectionProvider,
            ILoggerFactory loggerFactory,
            TOptions options)
            : base(next, options)
        {
            if (string.IsNullOrWhiteSpace(Options.ClientId))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Exception_OptionMustBeProvided, "ClientId"));
            }
            if (string.IsNullOrWhiteSpace(Options.ClientSecret))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Exception_OptionMustBeProvided, "ClientSecret"));
            }
            if (string.IsNullOrWhiteSpace(Options.AuthorizationEndpoint))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Exception_OptionMustBeProvided, "AuthorizationEndpoint"));
            }
            if (string.IsNullOrWhiteSpace(Options.TokenEndpoint))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Exception_OptionMustBeProvided, "TokenEndpoint"));
            }

            Logger = loggerFactory.Create(this.GetType().FullName);

            if (Options.StateDataFormat == null)
            {
                IDataProtector dataProtector = DataProtectionHelpers.CreateDataProtector(dataProtectionProvider,
                    this.GetType().FullName, options.AuthenticationType, "v1");
                Options.StateDataFormat = new PropertiesDataFormat(dataProtector);
            }

            Backchannel = new HttpClient(ResolveHttpMessageHandler(Options));
            Backchannel.Timeout = Options.BackchannelTimeout;
            Backchannel.MaxResponseContentBufferSize = 1024 * 1024 * 10; // 10 MB
        }

        protected HttpClient Backchannel { get; private set; }

        protected ILogger Logger { get; private set; }

        /// <summary>
        /// Provides the <see cref="AuthenticationHandler"/> object for processing authentication-related requests.
        /// </summary>
        /// <returns>An <see cref="AuthenticationHandler"/> configured with the <see cref="OAuthAuthenticationOptions"/> supplied to the constructor.</returns>
        protected override AuthenticationHandler<TOptions> CreateHandler()
        {
            return new OAuthAuthenticationHandler<TOptions, TNotifications>(Backchannel, Logger);
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Managed by caller")]
        private static HttpMessageHandler ResolveHttpMessageHandler(OAuthAuthenticationOptions<TNotifications> options)
        {
            HttpMessageHandler handler = options.BackchannelHttpHandler ??
#if ASPNET50
                new WebRequestHandler();
            // If they provided a validator, apply it or fail.
            if (options.BackchannelCertificateValidator != null)
            {
                // Set the cert validate callback
                var webRequestHandler = handler as WebRequestHandler;
                if (webRequestHandler == null)
                {
                    throw new InvalidOperationException(Resources.Exception_ValidatorHandlerMismatch);
                }
                webRequestHandler.ServerCertificateValidationCallback = options.BackchannelCertificateValidator.Validate;
            }
#else
                new WinHttpHandler();
#endif
            return handler;
        }
    }
}