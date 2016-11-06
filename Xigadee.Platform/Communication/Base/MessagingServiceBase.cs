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

#region using
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
#endregion
namespace Xigadee
{
    /// <summary>
    /// This is the generic base class for messaging services.
    /// </summary>
    /// <typeparam name="C">The client class type.</typeparam>
    /// <typeparam name="M">The client message class type.</typeparam>
    /// <typeparam name="H">The client holder class type.</typeparam>
    /// <typeparam name="P">The partition configuration class type.</typeparam>
    public abstract class MessagingServiceBase<C, M, H, P> : ServiceBase<StatusBase>, 
        IMessaging, IPayloadSerializerConsumer, IServiceOriginator, IServiceLogger, IRequireBoundaryLogger
        where H: ClientHolder<C, M>, new()
        where P: PartitionConfig
    {
        #region Declarations
        /// <summary>
        /// This is the client collection.
        /// </summary>
        protected Dictionary<int, H> mClients= new Dictionary<int, H>();
        /// <summary>
        /// This is the default priority. 1 if present
        /// </summary>
        protected int? mDefaultPriority;
        /// <summary>
        /// This method is used to name the client based on the priority.
        /// </summary>
        protected Func<string, int, string> mPriorityClientNamer = (s, i) => string.Format("{0}{1}", s, i == 1 ? "" : i.ToString());
        #endregion

        //IMessaging
        #region BoundaryLogger
        /// <summary>
        /// The boundary logger can be used to log message metadata when it is received or transmitted.
        /// </summary>
        public IBoundaryLogger BoundaryLogger { get; set; }
        #endregion
        #region PriorityPartitions
        /// <summary>
        /// This is the set of priority partitions used to provide different priority for messages.
        /// </summary>
        public List<P> PriorityPartitions { get; set; } 
        #endregion
        #region ChannelId
        /// <summary>
        /// This is the ChannelId for the messaging service
        /// </summary>
        public string ChannelId
        {
            get;
            set;
        } 
        #endregion
        #region Clients
        /// <summary>
        /// This method returns the clients created for the messaging service.
        /// </summary>
        public IEnumerable<ClientHolder> Clients
        {
            get
            {
                return mClients == null ? null : mClients.Values;
            }
        } 
        #endregion

        #region SupportsChannel(string channel)
        /// <summary>
        /// This method compares the channel and returns true if it is supported.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns>Returns true if the channel is supported.</returns>
        public bool SupportsChannel(string channel)
        {
            return string.Equals(channel, ChannelId, StringComparison.InvariantCultureIgnoreCase);
        }
        #endregion

        #region StartInternal()
        /// <summary>
        /// This is the default start method for both listeners and senders.
        /// </summary>
        protected override void StartInternal()
        {
            if (ChannelId == null)
                throw new ArgumentNullException("ChannelId", "ChannelId cannot be null");

            if (PriorityPartitions == null)
                throw new ArgumentNullException("PriorityPartitions", "PriorityPartitions cannot be null");

            //Set the partition priority to a single partition if null or an empty set.
            if (PriorityPartitions.Count == 0)
                throw new ArgumentOutOfRangeException("PriorityPartitions", "PriorityPartitions must have at least one member.");

            try
            {
                TearUp();

                //Start the client in either listener or sender mode.
                foreach (var priority in PriorityPartitions)
                {
                    var client = ClientCreate(priority);

                    mClients.Add(priority.Priority, client);

                    if (client.CanStart)
                        ClientStart(client);
                    else
                        Logger.LogMessage(string.Format("Client not started: {0} :{1}/{2}", client.Type, client.Name, client.Priority));

                    if (priority.Priority == 1)
                        mDefaultPriority = 1;
                }

                //If the incoming priority cannot be reconciled we set it to the default
                //which is 1, unless 1 is not present and then we set it to the max value.
                if (!mDefaultPriority.HasValue)
                    mDefaultPriority = PriorityPartitions.Select((p) => p.Priority).Max();
            }
            catch (Exception ex)
            {
                LogExceptionLocation("StartInternal", ex);
                throw;
            }
        }
        #endregion
        #region StopInternal()
        /// <summary>
        /// This is the default stop for all listeners and senders
        /// </summary>
        protected override void StopInternal()
        {
            try
            {
                mClients.Values.ForEach((c)=>ClientStop(c));
                mClients.Clear();
            }
            catch (Exception ex)
            {
                LogExceptionLocation("StopInternal", ex);
            }
            mClients = null;
            TearDown();
        }
        #endregion

        #region TearUp()
        /// <summary>
        /// This override can be used to add additional logic during the start up phase.
        /// This method is called before the clients are created.
        /// </summary>
        protected virtual void TearUp()
        {
        } 
        #endregion
        #region TearDown()
        /// <summary>
        /// This method can be used to clean up any additional communication methods.
        /// It is called after the clients have been closed.
        /// </summary>
        protected virtual void TearDown()
        {

        } 
        #endregion

        #region ClientStart(H client)
        /// <summary>
        /// This method creates and starts the client.
        /// </summary>
        public virtual void ClientStart(H client)
        {
            client.FabricInitialize();
            client.Start();
        }
        #endregion
        #region ClientCreate()
        /// <summary>
        /// This is the default client create logic.
        /// </summary>
        /// <returns>Returns the client.</returns>
        protected virtual H ClientCreate(P partition)
        {
            var client = new H();

            //Set the message logger.
            client.Logger = Logger;

            //Set the boundary logger if this has been defined in the messaging service.
            client.BoundaryLogger = BoundaryLogger;

            client.ClientRefresh = () => { };

            client.Priority = partition.Priority;

            client.QueueLength = () => (int?)null;

            client.FabricInitialize = () => { };

            client.Start = () => { };

            client.Stop = () =>
            {
                if (client == null) return;
                if (client.Client == null) return;
                client.IsActive = false;
                client.ClientClose();
            };

            client.ClientReset = (ex) => ClientReset(client, ex);
            
            return client;
        }
        #endregion
        #region ClientStop(H client)
        /// <summary>
        /// This method stops the client.
        /// </summary>
        protected virtual void ClientStop(H client)
        {
            client.Stop?.Invoke();
        }
        #endregion
        #region ClientReset(H client, TransmissionException mex)
        /// <summary>
        /// This method closes and reset the fabric and the client.
        /// </summary>
        /// <param name="mex">The messaging exception.</param>
        protected virtual void ClientReset(H client, Exception mex)
        {
            try
            {
                client.Stop();
            }
            catch (Exception ex)
            {
                LogExceptionLocation(string.Format("ClientReset (Close) failed - {0}", client.Name), ex);
            }

            int? attemps = 0;
            do
            {
                try
                {
                    client.FabricInitialize();
                    client.Start();
                    attemps = null;
                }
                catch (Exception ex)
                {
                    LogExceptionLocation("ClientReset (Create)", ex);
                    attemps++;
                    //Stand off with each attemps
                    Thread.Sleep(100 * attemps.Value);
                }
            }
            while (attemps.HasValue);
        }
        #endregion
        #region ClientResolve(int priority)
        /// <summary>
        /// This method resolves the client based on the priority set.
        /// If then priority cannot be matched, it will use the default priority.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <returns>The specific client holder.</returns>
        protected virtual H ClientResolve(int priority)
        {
            if (mClients.ContainsKey(priority))
                return mClients[priority];

            return mClients[mDefaultPriority.Value];
        }
        #endregion

        //Microservice set
        #region PayloadSerializer
        /// <summary>
        /// This container is used to serialize and deserialize messaging payloads.
        /// </summary>
        public IPayloadSerializationContainer PayloadSerializer
        {
            get;
            set;
        } 
        #endregion
        #region OriginatorId
        /// <summary>
        /// This is the OriginatorId from the parent container.
        /// </summary>
        public virtual MicroserviceId OriginatorId
        {
            get;
            set;
        } 

        #endregion
        #region Logger
        /// <summary>
        /// This is the system wide logger.
        /// </summary>
        public ILoggerExtended Logger
        {
            get;
            set;
        } 
        #endregion
        #region LogExceptionLocation(string method)
        /// <summary>
        /// This helper method provides a class name and method name for debugging exceptions. 
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns>A combination string.</returns>
        protected void LogExceptionLocation(string method, Exception ex)
        {
            Logger.LogException(string.Format("{0}/{1}", GetType().Name, method), ex);
        }
        #endregion
    }
}
