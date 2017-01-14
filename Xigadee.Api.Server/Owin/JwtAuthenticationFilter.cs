﻿#region Copyright
// Copyright Hitachi Consulting
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IdentityModel.Claims;
using System.Security.Claims;
using System.Threading;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Web.Http.Results;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Principal;
using System.Web.Http;
using System.Net;

namespace Xigadee
{
    public class JwtAuthenticationFilter: IAuthenticationFilter
    {
        private JwtTokenVerificationPolicy mValidationPolicy;

        public JwtAuthenticationFilter(JwtTokenVerificationPolicy policy)
        {
            mValidationPolicy = policy;
        }

        public bool AllowMultiple
        {
            get { return true; }
        }


        public async Task AuthenticateAsync(HttpAuthenticationContext context, CancellationToken cancellationToken)
        {
            try
            {
                // Look for credentials in the request.
                AuthenticationHeaderValue auth = context.Request.Headers.Authorization;

                // If there aren't any credentials - or the filter does not recognize the authentication scheme - do nothing.
                if (auth == null
                    || !auth.Scheme.Equals("bearer", StringComparison.InvariantCultureIgnoreCase))
                    return;

                var token = new JwtToken(auth.Parameter, mValidationPolicy.Secret);

                if (mValidationPolicy.ValidateAudience &&
                    !token.Claims.Audience.Equals(mValidationPolicy.Audience, StringComparison.InvariantCultureIgnoreCase))
                    throw new WebApiJwtFilterValidationException(JwtClaims.HeaderAudience);

                if (mValidationPolicy.ValidateExpiry &&
                    (!token.Claims.ExpirationTime.HasValue || token.Claims.ExpirationTime.Value < DateTime.UtcNow))
                    throw new WebApiJwtFilterValidationException(JwtClaims.HeaderExpirationTime);

                if (mValidationPolicy.ValidateNotBefore &&
                    (!token.Claims.NotBefore.HasValue || token.Claims.NotBefore.Value >= DateTime.UtcNow))
                    throw new WebApiJwtFilterValidationException(JwtClaims.HeaderNotBefore);

                context.Principal = new MicroserviceSecurityPrincipal(token);

                return;
            }
            catch (Exception ex)
            {
                
            }

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.Forbidden);

            //On error, set to unauthorized.
            context.ErrorResult = new StatusResult(HttpStatusCode.Forbidden, context.Request);

            return;
        }

        public async Task ChallengeAsync(HttpAuthenticationChallengeContext context, CancellationToken cancellationToken)
        {
            return;
        }

        public void OnAuthorization(HttpActionContext actionContext)
        {
            //var identity = new GenericIdentity("Paul", "Xigadee");

            //var principal = new GenericPrincipal(identity, null);

            //Thread.CurrentPrincipal = principal;

            //actionContext.Response = actionContext.Request.CreateErrorResponse(HttpStatusCode.Unauthorized,
            //        new SecurityException("Invalid API key Provided"));

            //base.OnAuthorization(actionContext);

            //      var req = actionContext.Request;
            //      // Get credential from the Authorization header 
            //      //(if present) and authenticate
            //      if (req.Headers.Authorization != null &&
            //        "somescheme".Equals(req.Headers.Authorization.Scheme,
            //          StringComparison.OrdinalIgnoreCase))
            //      {
            //          var creds = req.Headers.Authorization.Parameter;
            //          if (creds == "opensesame") // Replace with a real check
            //          {
            //              var claims = new List<Claim>()
            //{
            //  new Claim(ClaimTypes.Name, "badri"),
            //  new Claim(ClaimTypes.Role, "admin")
            //};
            //              var id = new ClaimsIdentity(claims, "Token");
            //              var principal = new ClaimsPrincipal(new[] { id });
            //              // The request message contains valid credential
            //              actionContext.Principal = principal;
            //          }
            //          else
            //          {
            //              // The request message contains invalid credential
            //              actionContext.ErrorResult = new UnauthorizedResult(
            //                new AuthenticationHeaderValue[0], context.Request);
            //          }
            //      }
            //      return Task.FromResult(0);
        }

    }

    public class WebApiJwtFilterValidationException: TokenValidationException
    {
        public WebApiJwtFilterValidationException(string value) : base(value)
        {
        }
    }

    public class StatusResult: IHttpActionResult
    {
        public StatusResult(HttpStatusCode status, HttpRequestMessage request, string reasonPhrase = null)
        {
            Status = status;
            ReasonPhrase = reasonPhrase;
            Request = request;
        }

        public HttpStatusCode Status { get; private set; }

        public string ReasonPhrase { get; private set; }

        public HttpRequestMessage Request { get; private set; }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Execute());
        }

        private HttpResponseMessage Execute()
        {
            HttpResponseMessage response = new HttpResponseMessage(Status);
            response.RequestMessage = Request;
            response.ReasonPhrase = ReasonPhrase ?? Enum.GetName(typeof(HttpStatusCode), Status);
            return response;
        }
    }

}
