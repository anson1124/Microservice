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

namespace Xigadee
{
    public static partial class WebApiExtensionMethods
    {
        /// <summary>
        /// This method will ensure the service returns a HTTP 503 service unavailable error if the underlying Microservice
        /// is not currently running.
        /// </summary>
        /// <typeparam name="P">The pipeline type.</typeparam>
        /// <param name="webpipe">The pipe.</param>
        /// <returns>Returns the pipeline.</returns>
        public static P ApiAddMicroserviceUnavailableFilter<P>(this P webpipe)
            where P : IPipelineWebApi
        {
            var filter = new WebApiServiceUnavailableFilter();

            webpipe.HttpConfig.Filters.Add(filter);

            return webpipe;
        }
    }
}