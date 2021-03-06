﻿using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xigadee;
using System.Net;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace Test.Xigadee
{
    [Ignore]
    [TestClass]
    public class TcpTlsConnectionTests
    {
        [TestMethod]
        public void Connector1()
        {
            bool stop = false;
            var server = new TcpTlsServer(new IPEndPoint(IPAddress.Any, 9090), SslProtocols.None, null);

            server.Start();

            Thread toRun = new Thread(new ThreadStart(() => 
            {
                while (!stop)
                {
                    if (server.PollRequired)
                        Task.Run(async () => await server.Poll());
                    Thread.Sleep(10);
                }

            }));

            toRun.Start();

            var client = new TcpTlsClient(new IPEndPoint(IPAddress.Loopback, 9090), SslProtocols.None, null);

            client.Start();

            server.Poll().Wait();
        }


    }
}
