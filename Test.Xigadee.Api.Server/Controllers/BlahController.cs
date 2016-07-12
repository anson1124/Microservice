﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Xigadee;

namespace Test.Xigadee.Api.Server.Controllers
{
    [MediaTypeConverter(typeof(JsonTransportSerializer<Blah>))]
    public class BlahController: ApiPersistenceControllerAsyncBase<Guid, Blah>
    {
        public BlahController(IRepositoryAsync<Guid, Blah> respository) : base(respository)
        {
        }
    }
}