﻿using System;
using System.Globalization;
using System.Net.Http;
using Microsoft.Owin;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.DataHandler;
using Microsoft.Owin.Security.DataProtection;
using Microsoft.Owin.Security.Infrastructure;
using Owin.Security.Providers.Properties;

namespace Owin.Security.Providers.Asana
{
    public class AsanaAuthenticationMiddleware : AuthenticationMiddleware<AsanaAuthenticationOptions>
    {
        private readonly HttpClient httpClient;
        private readonly ILogger logger;

        public AsanaAuthenticationMiddleware(OwinMiddleware next, IAppBuilder app,
            AsanaAuthenticationOptions options)
            : base(next, options)
        {
            if (String.IsNullOrWhiteSpace(Options.ClientId))
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture,
                    Resources.Exception_OptionMustBeProvided, "ClientId"));
            if (String.IsNullOrWhiteSpace(Options.ClientSecret))
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture,
                    Resources.Exception_OptionMustBeProvided, "ClientSecret"));

            logger = app.CreateLogger<AsanaAuthenticationMiddleware>();

            if (Options.Provider == null)
                Options.Provider = new AsanaAuthenticationProvider();

            if (Options.StateDataFormat == null)
            {
                IDataProtector dataProtector = app.CreateDataProtector(
                    typeof (AsanaAuthenticationMiddleware).FullName,
                    Options.AuthenticationType, "v1");
                Options.StateDataFormat = new PropertiesDataFormat(dataProtector);
            }

            if (String.IsNullOrEmpty(Options.SignInAsAuthenticationType))
                Options.SignInAsAuthenticationType = app.GetDefaultSignInAsAuthenticationType();

            httpClient = new HttpClient(ResolveHttpMessageHandler(Options))
            {
                Timeout = Options.BackchannelTimeout,
                MaxResponseContentBufferSize = 1024*1024*10,
            };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Microsoft Owin Asana middleware");
            httpClient.DefaultRequestHeaders.ExpectContinue = false;
        }

        /// <summary>
        ///     Provides the <see cref="T:Microsoft.Owin.Security.Infrastructure.AuthenticationHandler" /> object for processing
        ///     authentication-related requests.
        /// </summary>
        /// <returns>
        ///     An <see cref="T:Microsoft.Owin.Security.Infrastructure.AuthenticationHandler" /> configured with the
        ///     <see cref="T:Owin.Security.Providers.Asana.AsanaAuthenticationOptions" /> supplied to the constructor.
        /// </returns>
        protected override AuthenticationHandler<AsanaAuthenticationOptions> CreateHandler()
        {
            return new AsanaAuthenticationHandler(httpClient, logger);
        }

        private HttpMessageHandler ResolveHttpMessageHandler(AsanaAuthenticationOptions options)
        {
            HttpMessageHandler handler = options.BackchannelHttpHandler ?? new WebRequestHandler();

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

            return handler;
        }
    }
}