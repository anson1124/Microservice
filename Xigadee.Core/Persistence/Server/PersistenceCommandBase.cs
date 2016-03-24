﻿#region using

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#endregion
namespace Xigadee
{
    /// <summary>
    /// This is the base abstract persistence provider class.
    /// </summary>
    /// <typeparam name="K">The key type.</typeparam>
    /// <typeparam name="E">The entity type.</typeparam>
    public abstract class PersistenceCommandBase<K,E,S,P> : CommandBase<S,P>, 
        IPersistenceMessageHandler, IRequireSharedServices
        where K : IEquatable<K>
        where S : PersistenceStatistics, new()
        where P : PersistenceCommandPolicy, new()
    {
        #region Declarations
        /// <summary>
        /// This is the entity transform holder.
        /// </summary>
        protected readonly EntityTransformHolder<K,E> mTransform;
        /// <summary>
        /// This is the cache manager.
        /// </summary>
        protected readonly ICacheManager<K, E> mCacheManager;
        #endregion
        #region Constructor
        /// <summary>
        /// This constructor specifies whether the service should be registered as a shared service
        /// that can be called directly by other message handler and Microservice components.
        /// </summary>
        /// <param name="persistenceRetryPolicy"></param>
        protected PersistenceCommandBase(
              PersistenceRetryPolicy persistenceRetryPolicy = null
            , ResourceProfile resourceProfile = null
            , ICacheManager<K, E> cacheManager = null
            , TimeSpan? defaultTimeout = null
            , string entityName = null
            , VersionPolicy<E> versionPolicy = null
            , Func<E, K> keyMaker = null
            , Func<string, E> entityDeserializer = null
            , Func<E, string> entitySerializer = null
            , Func<K, string> keySerializer = null
            , Func<string, K> keyDeserializer = null
            , Func<E, IEnumerable<Tuple<string, string>>> referenceMaker = null
            )
        {
            mTransform = EntityTransformCreate(entityName, versionPolicy, keyMaker
                , entityDeserializer, entitySerializer
                , keySerializer, keyDeserializer, referenceMaker);

            mPolicy.DefaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(5);
            mPolicy.PersistenceRetryPolicy = persistenceRetryPolicy ?? new PersistenceRetryPolicy();
            mPolicy.ResourceProfile = resourceProfile;

            mCacheManager = cacheManager ?? new NullCacheManager<K, E>();
        }

        /// <summary>
        /// This constructor specifies whether the service should be registered as a shared service
        /// that can be called directly by other message handler and Microservice components.
        /// </summary>
        protected PersistenceCommandBase(
              EntityTransformHolder<K, E> entityTransform
            , PersistenceRetryPolicy persistenceRetryPolicy = null
            , ResourceProfile resourceProfile = null
            , ICacheManager<K, E> cacheManager = null
            , TimeSpan? defaultTimeout = null
            )
        {
            if (entityTransform == null)
                throw new ArgumentNullException("entityTransform cannot be null");

            mTransform = entityTransform;

            mPolicy.DefaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(5);
            mPolicy.PersistenceRetryPolicy = persistenceRetryPolicy ?? new PersistenceRetryPolicy();
            mPolicy.ResourceProfile = resourceProfile;

            mCacheManager = cacheManager ?? new NullCacheManager<K, E>();
        }
        #endregion

        #region EntityTransformCreate...
        /// <summary>
        /// The transform holder manages the serialization and deserialization of the entity and key 
        /// for the entity and key, and identifies the references for the entity.
        /// </summary>
        /// <param name="entityName">The entity name.</param>
        /// <param name="versionPolicy">The version policy for the entity.</param>
        /// <param name="keyMaker">The keymaker function that creates a key for the entity.</param>
        /// <param name="entityDeserializer">The entity deserializer that converts the entity in to a string.</param>
        /// <param name="entitySerializer">The entity serializer that turns a string representation in to an entity.</param>
        /// <param name="keySerializer">The serializer that converts the key in to a string.</param>
        /// <param name="keyDeserializer">The deserializer that converts a string in to a key.</param>
        /// <param name="referenceMaker">A function that returns references from the entity in a set of string Tuples.</param>
        /// <returns>Returns the transform holder.</returns>
        protected virtual EntityTransformHolder<K, E> EntityTransformCreate(
              string entityName = null
            , VersionPolicy<E> versionPolicy = null
            , Func<E, K> keyMaker = null
            , Func<string, E> entityDeserializer = null
            , Func<E, string> entitySerializer = null
            , Func<K, string> keySerializer = null
            , Func<string, K> keyDeserializer = null
            , Func<E, IEnumerable<Tuple<string, string>>> referenceMaker = null)
        {
            var mTransform = new EntityTransformHolder<K, E>();

            mTransform.KeyMaker = keyMaker;

            mTransform.KeySerializer = keySerializer ?? ((i) => i.ToString());
            mTransform.KeyDeserializer = keyDeserializer;

            mTransform.ReferenceMaker = referenceMaker ?? ((e) => new Tuple<string, string>[] { });
            mTransform.Version = versionPolicy ?? new VersionPolicy<E>();

            mTransform.EntityName = entityName ?? typeof(E).Name.ToLowerInvariant();

            mTransform.EntityDeserializer = entityDeserializer;
            mTransform.EntitySerializer = entitySerializer;

            return mTransform;
        } 
        #endregion

        #region EntityType
        /// <summary>
        /// This is the entity type Name used for matching request and payloadRs messages.
        /// </summary>
        public virtual string EntityType
        {
            get
            {
                return mTransform.EntityName;
            }
        }
        #endregion

        #region Start/Stop Internal
        protected override void StartInternal()
        {
            base.StartInternal();

            var resourceTracker = SharedServices.GetService<IResourceTracker>();
            if (resourceTracker != null && mPolicy.ResourceProfile != null)
                mPolicy.ResourceConsumer = resourceTracker.RegisterConsumer(EntityType, mPolicy.ResourceProfile);
        }

        protected override void StopInternal()
        {
            mPolicy.ResourceConsumer = null;

            base.StopInternal();
        }
        #endregion

        #region SharedServices
        /// <summary>
        /// This is the shared service connector
        /// </summary>
        public virtual ISharedService SharedServices
        {
            get; set;
        } 
        #endregion

        #region CommandsRegister()
        /// <summary>
        /// This method register the supported commands for a persistence handler.
        /// </summary>
        public override void CommandsRegister()
        {
            PersistenceCommandRegister<K, E>(EntityActions.Create, ProcessCreate, true, timeoutcorrect: TimeoutCorrectCreateUpdate);

            PersistenceCommandRegister<K, E>(EntityActions.Read, ProcessRead);
            PersistenceCommandRegister<K, E>(EntityActions.ReadByRef, ProcessReadByRef);

            PersistenceCommandRegister<K, E>(EntityActions.Update, ProcessUpdate, true, timeoutcorrect: TimeoutCorrectCreateUpdate);

            PersistenceCommandRegister<K, Tuple<K, string>>(EntityActions.Delete, ProcessDelete, true, timeoutcorrect: TimeoutCorrectDelete);
            PersistenceCommandRegister<K, Tuple<K, string>>(EntityActions.DeleteByRef, ProcessDeleteByRef, true, timeoutcorrect: TimeoutCorrectDelete);

            PersistenceCommandRegister<K, Tuple<K, string>>(EntityActions.Version, ProcessVersion);
            PersistenceCommandRegister<K, Tuple<K, string>>(EntityActions.VersionByRef, ProcessVersionByRef);

            PersistenceCommandRegister<K, Tuple<K, string>>(EntityActions.Search, ProcessSearch);
        }
        #endregion

        #region TimeoutCorrectCreateUpdate...
        /// <summary>
        /// This method can be overridden to correct timeouts for state changing requests and to log to the event source event on a timeout failure condition.
        /// This would be used when the underlying communication times out, but the action was ultimately successful.
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="E"></typeparam>
        /// <param name="rq"></param>
        /// <param name="rs"></param>
        /// <param name="m"></param>
        /// <param name="l"></param>
        /// <returns>Return true if the message should logged </returns>
        protected virtual async Task<bool> TimeoutCorrectCreateUpdate(PersistenceRequestHolder<K, E> holder)
        {
            return false;
        }
        #endregion
        #region TimeoutCorrectDelete...
        /// <summary>
        /// This method can be overridden to correct timeouts for state changing requests and to log to the event source event on a timeout failure condition.
        /// This would be used when the underlying communication times out, but the action was ultimately successful.
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="E"></typeparam>
        /// <param name="rq"></param>
        /// <param name="rs"></param>
        /// <param name="m"></param>
        /// <param name="l"></param>
        /// <returns>Return true if the message should logged </returns>
        protected virtual async Task<bool> TimeoutCorrectDelete(PersistenceRequestHolder<K, Tuple<K, string>> holder)
        {
            return false;
        } 
        #endregion

        #region Log<KT, ET>...
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam Name="KT">The key type.</typeparam>
        /// <typeparam Name="ET">The entity type.</typeparam>
        /// <param name="action"></param>
        /// <param name="prq"></param>
        /// <param name="rq"></param>
        /// <param name="rs"></param>
        /// <param name="loggingLevel">Logging level</param>
        /// <param name="message"></param>
        protected void Log<KT, ET>(string action, PersistenceRequestHolder<KT, ET> holder
            , LoggingLevel loggingLevel = LoggingLevel.Info, string message = null, string category = null)
        {
            var logEvent = new PersistencePayloadLogEvent(holder.prq, holder.rq, holder.rs, loggingLevel) {Message = message ?? string.Empty, Category = category};
            Logger.Log(logEvent);
        } 
        #endregion
        #region LogException<KT, ET>...
        /// <summary>
        /// We don't want to pass the exception details back to the calling party as this may
        /// leak sensitive information aboout the application and persistence agent.
        /// This method logs the error and assigns it a trackable id and sends that instead to the 
        /// front end.
        /// </summary>
        /// <typeparam Name="KT">The key type.</typeparam>
        /// <typeparam Name="ET">The entity type.</typeparam>
        /// <param Name="action">the entity action.</param>
        /// <param Name="prq">The payload request.</param>
        /// <param Name="ex">The exception.</param>
        /// <param name="rq">The request.</param>
        /// <param Name="rs">The response.</param>
        protected void LogException<KT, ET>(string action, PersistenceRequestHolder<KT, ET> holder, Exception ex)
        {

            try
            {
                var logEvent = new PersistencePayloadLogEvent(holder.prq, holder.rq, holder.rs, LoggingLevel.Info, ex);
                Logger.Log(logEvent);

                Guid errorId = Guid.NewGuid();
                string errMessage = string.Format("Exception tracker {0}/{1}/{2}", action, (holder.prq != null && holder.prq.Message != null ? holder.prq.Message.OriginatorKey : string.Empty), errorId);
                holder.rs.ResponseMessage = errMessage;

                if (holder.rq != null)
                    errMessage += string.Format("/{0}-{1}", holder.rq.Key, (holder.rq.Entity == null) ? string.Empty : holder.rq.Entity.ToString());

                Logger.LogException(errMessage, ex);
            }
            catch (Exception)
            {
                // Do not fail due to an issue logging
            }

            holder.rs.ResponseCode = 500;
        }
        #endregion

        #region LogEventSource<KT, ET>...
        /// <summary>
        /// This method logs entities to the event source. This method will try indefinitely
        /// as we do not want the Event Source to fail.
        /// </summary>
        /// <typeparam Name="KT">The key type.</typeparam>
        /// <typeparam Name="ET">The entity type.</typeparam>
        /// <param name="actionType"></param>
        /// <param name="prq"></param>
        /// <param name="rq"></param>
        /// <param name="rs"></param>
        protected async virtual Task LogEventSource<KT, ET>(string actionType, PersistenceRequestHolder<KT, ET> holder)
        {
            // Only pass through the entity if is of the correct entity type. ET might be an entity or it might be a Tuple<K, string>> in which case pass a null  
            E entity = typeof (ET) == typeof (E) ? (E)Convert.ChangeType(holder.rs.Entity, typeof(E)) : default(E);
            await LogEventSource(actionType, holder.prq.Message.OriginatorKey, holder.rs.Key, entity, holder.rq.Settings);
        }
        #endregion
        #region LogEventSource<KT, ET>...
        /// <summary>
        /// This method logs entities to the event source. This method will try indefinitely
        /// as we do not want the Event Source to fail.
        /// </summary>
        /// <typeparam Name="KT">The key type.</typeparam>
        /// <param name="actionType"></param>
        /// <param name="originatorKey"></param>
        /// <param name="key"></param>
        /// <param name="entity"></param>
        /// <param name="settings"></param>
        protected async virtual Task LogEventSource<KT>(string actionType, string originatorKey, KT key, E entity, RepositorySettings settings)
        {
            try
            {
                var data = new EventSourceEntry<KT, E>
                {
                    EntityType = typeof(E).Name,
                    EventType = actionType,
                    Entity = entity,
                    EntityKey = key
                };

                if (settings != null)
                {
                    data.BatchId = settings.BatchId;
                    data.CorrelationId = settings.CorrelationId;
                    data.EntityVersionOld = settings.VersionId;

                    data.EntityVersion = settings.VersionId;
                }

                await EventSource.Write(originatorKey, data, sync: true);
            }
            catch (Exception ex)
            {
                Logger.LogException(string.Format("Exception thrown for log to event source on {0}-{1}-{2}", typeof(E).Name, actionType, originatorKey), ex);
            }
        }
        #endregion

        #region ProfileStart/ProfileEnd/ProfileRetry

        protected virtual Guid ProfileStart(TransmissionPayload payload)
        {
            if (mPolicy.ResourceConsumer == null)
                return Guid.NewGuid();

            return mPolicy.ResourceConsumer.Start(payload.Message.ToKey(), payload.Id);
        }

        protected virtual void ProfileEnd(Guid profileId, int start, ResourceRequestResult reason)
        {
            if (mPolicy.ResourceConsumer != null)
                mPolicy.ResourceConsumer.End(profileId, start, reason);
        }

        protected virtual void ProfileRetry(Guid profileId, int retryStart, ResourceRetryReason reason)
        {
            if (mPolicy.ResourceConsumer != null)
                mPolicy.ResourceConsumer.Retry(profileId, retryStart, reason);
        }
        #endregion

        protected class PersistenceRequestHolder<KT, ET>
        {
            public PersistenceRequestHolder(Guid profileId, TransmissionPayload prq, List<TransmissionPayload> prs)
            {
                this.profileId = profileId;
                this.prq = prq;
                this.prs = prs;

                start = Environment.TickCount;

                result = null;
                rq = null;
                rs = null;
            }

            public PersistenceRepositoryHolder<KT, ET> rq;

            public PersistenceRepositoryHolder<KT, ET> rs;

            public TransmissionPayload prq;

            public List<TransmissionPayload> prs;

            public int start;

            public Guid profileId;

            public ResourceRequestResult? result;
        }

        #region PersistenceCommandRegister<KT,ET>...
        /// <summary>
        /// This method registers the specific persistence handler.
        /// </summary>
        /// <typeparam Name="KT">The key type.</typeparam>
        /// <typeparam Name="ET">The entity type.</typeparam>
        /// <param Name="actionType">The action type identifier</param>
        /// <param Name="action">The action to process the command.</param>
        protected virtual void PersistenceCommandRegister<KT, ET>(string actionType
            , Func<PersistenceRequestHolder<KT, ET>, Task> action
            , bool logEventOnSuccess = false
            , Func<PersistenceRequestHolder<KT, ET>, Task<bool>> preaction = null
            , Func<PersistenceRequestHolder<KT, ET>, Task> postaction = null
            , Func<PersistenceRequestHolder<KT, ET>, Task<bool>> timeoutcorrect = null
            , int? retryOnTimeout = null
            , string channelId = null
            , string entityType = null
            )
        {
            Func<TransmissionPayload, List<TransmissionPayload>, Task> actionPayload = async (m, l) =>
            {
                var holder = new PersistenceRequestHolder<KT,ET>(ProfileStart(m), m, l);

                try
                {
                    var rsMessage = m.Message.ToResponse();

                    rsMessage.ChannelId = m.Message.ResponseChannelId;
                    rsMessage.ChannelPriority = m.Message.ResponseChannelPriority;
                    rsMessage.MessageType = m.Message.MessageType;
                    rsMessage.ActionType = "";

                    var rsPayload = new TransmissionPayload(rsMessage);

                    bool hasTimedOut = false;

                    try
                    {
                        RepositoryHolder<KT, ET> rqTemp = m.MessageObject as RepositoryHolder<KT, ET>;

                        //Deserialize the incoming payloadRq request
                        if (rqTemp == null)
                            rqTemp = PayloadSerializer.PayloadDeserialize<RepositoryHolder<KT, ET>>(m);

                        holder.rq = new PersistenceRepositoryHolder<KT, ET>(rqTemp);

                        if (holder.rq.Timeout == null)
                            holder.rq.Timeout = TimeSpan.FromSeconds(10);

                        bool preactionFailed = false;

                        try
                        {
                            bool retryExceeded = false;

                            do
                            {
                                int attempt = Environment.TickCount;

                                //Create the payloadRs holder, and discard any previous version.
                                holder.rs = new PersistenceRepositoryHolder<KT, ET>();

                                if (preaction != null && !(await preaction(holder)))
                                {
                                    preactionFailed = true;
                                    break;
                                }

                                //Call the specific command to process the action, i.e Create, Read, Update, Delete ... etc.
                                await action(holder);

                                //Flag if the request times out at any point. 
                                //This may be required later when checking whether the action was actually successful.
                                hasTimedOut |= holder.rs.IsTimeout;

                                //OK, if this is not a time out then it is successful
                                if (!holder.rs.IsTimeout && !holder.rs.ShouldRetry)
                                    break;

                                ProfileRetry(holder.profileId, attempt, holder.rs.ShouldRetry?ResourceRetryReason.Other:ResourceRetryReason.Timeout);

                                mStatistics.RetryIncrement();

                                Logger.LogMessage(LoggingLevel.Info
                                    , string.Format("Timeout occured for {0} {1} for request:{2}", EntityType, actionType, holder.rq)
                                    , "DBTimeout");

                                holder.rq.IsRetry = true;

                                //These should not be counted against the limit.
                                if (!holder.rs.ShouldRetry)
                                    holder.rq.Retry++;

                                holder.rq.IsTimeout = false;

                                retryExceeded = m.Cancel.IsCancellationRequested || holder.rq.Retry > mPolicy.PersistenceRetryPolicy.GetMaximumRetries(m);
                            }
                            while (!retryExceeded);

                            //Signal to the underlying comms channel that the message has been processed successfully.
                            m.Signal(!retryExceeded);

                            // If we have exceeded the retry limit then Log error
                            if (retryExceeded)
                            {
                                Log(actionType
                                    , holder
                                    , LoggingLevel.Error
                                    , string.Format("Retry limit has been exceeded (cancelled ({0})) for {1} {2} for {3}", m.Cancel.IsCancellationRequested, EntityType, actionType, holder.rq)
                                    , "DBRetry");
                                holder.result = ResourceRequestResult.RetryExceeded;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogException(actionType, holder, ex);
                            m.SignalFail();
                            holder.result = ResourceRequestResult.Exception;
                        }

                        bool logEventSource = !preactionFailed && logEventOnSuccess && holder.rs.IsSuccess;

                        if (!holder.rs.IsSuccess && hasTimedOut && timeoutcorrect != null && holder.result != ResourceRequestResult.Exception)
                        {
                            if (await timeoutcorrect(holder))
                            {
                                logEventSource = true;
                                Logger.LogMessage(LoggingLevel.Info
                                    , string.Format("Recovered timeout sucessfully for {0}-{1} for request:{2} - response:{3}", EntityType, actionType, holder.rq, holder.rs)
                                    , "DBTimeout");
                            }
                            else
                            {
                                Logger.LogMessage(LoggingLevel.Error
                                    , string.Format("Not recovered timeout for {0}-{1} for request:{2} - response:{3}", EntityType, actionType, holder.rq, holder.rs)
                                    , "DBTimeout");
                            }
                        }

                        if (logEventSource && holder.rs.ShouldLogEventSource)
                            await LogEventSource(actionType, holder);

                        if (!preactionFailed && postaction != null)
                            await postaction(holder);

                        //Serialize the payloadRs
                        var reposHolder = holder.rs.ToRepositoryHolder();

                        rsPayload.MessageObject = reposHolder;
                        rsPayload.Message.Blob = PayloadSerializer.PayloadSerialize(reposHolder);

                        rsPayload.Message.Status = "200";

                        if (!holder.result.HasValue)
                            holder.result = ResourceRequestResult.Success;
                    }
                    catch (Exception ex)
                    {
                        m.SignalFail();
                        rsPayload.Message.Status = "500";
                        rsPayload.Message.StatusDescription = ex.Message;
                        Logger.LogException(
                            string.Format("Error processing message (was cancelled({0}))-{1}-{2}-{3}", m.Cancel.IsCancellationRequested, EntityType, actionType, holder.rq)
                            , ex);
                        holder.result = ResourceRequestResult.Exception;
                    }

                    // check whether we need to send a response message. If this is async and AsyncResponse is not set to true,
                    // then by default we do not send a response message to cut down on unnecessary traffic.
                    if (holder.rq == null || holder.rq.Settings == null || !holder.rq.Settings.ProcessAsync)
                        l.Add(rsPayload);
                }
                finally
                {
                    ProfileEnd(holder.profileId, holder.start, holder.result ?? ResourceRequestResult.Unknown);
                }
            };

            if (channelId == null)
                channelId = ChannelId ?? string.Empty;

            if (entityType == null)
                entityType=EntityType;

            CommandRegister(
                  channelId.ToLowerInvariant()
                , entityType.ToLowerInvariant()
                , actionType.ToLowerInvariant()
                , actionPayload);
        }
        #endregion

        #region Create
        protected virtual async Task ProcessCreate(PersistenceRequestHolder<K, E> holder)
        {
            K key = mTransform.KeyMaker(holder.rq.Entity);

            var result = await InternalCreate(holder);

            if (mCacheManager.IsActive && !mCacheManager.IsReadOnly && result.IsSuccess)
                mCacheManager.Write(mTransform, result.Entity);

            ProcessOutputEntity(key, holder.rq, holder.rs, result);
        }

        protected virtual async Task<IResponseHolder<E>> InternalCreate(PersistenceRequestHolder<K, E> holder)
        {
            return new PersistenceResponseHolder<E>() { StatusCode = 501, IsSuccess = false };
        }

        #endregion

        #region Read
        protected virtual async Task ProcessRead(PersistenceRequestHolder<K, E> holder)
        {
            IResponseHolder<E> result = null;

            if (mCacheManager.IsActive && holder.rq.Settings.UseCache)
                result = await mCacheManager.Read(mTransform, holder.rq.Key);

            if (result == null || !result.IsSuccess)
            {
                result = await InternalRead(holder.rq.Key, holder);

                if (mCacheManager.IsActive && !mCacheManager.IsReadOnly && result.IsSuccess)
                    mCacheManager.Write(mTransform, result.Entity);
            }

            ProcessOutputEntity(holder.rq.Key, holder.rq, holder.rs, result);
        }

        protected async virtual Task<IResponseHolder<E>> InternalRead(K key, PersistenceRequestHolder<K, E> holder)
        {
            return new PersistenceResponseHolder<E>() { StatusCode = 501, IsSuccess = false };
        }
        #endregion
        #region ReadByRef
        protected virtual async Task ProcessReadByRef(PersistenceRequestHolder<K, E> holder)
        {
            IResponseHolder<E> result = null;

            if (mCacheManager.IsActive && holder.rq.Settings.UseCache)
                result = await mCacheManager.Read(mTransform, holder.rq.KeyReference);

            if (result == null || !result.IsSuccess)
            {
                result = await InternalReadByRef(holder.rq.KeyReference, holder);

                if (mCacheManager.IsActive && !mCacheManager.IsReadOnly && result.IsSuccess)
                    mCacheManager.Write(mTransform, result.Entity);
            }

            ProcessOutputEntity(holder.rq.Key, holder.rq, holder.rs, result);
        }

        protected async virtual Task<IResponseHolder<E>> InternalReadByRef(Tuple<string, string> reference, PersistenceRequestHolder<K, E> holder)
        {
            return new PersistenceResponseHolder<E>() { StatusCode = 501, IsSuccess = false };
        }
        #endregion

        #region Update
        protected virtual async Task ProcessUpdate(PersistenceRequestHolder<K, E> holder)
        {
            K key = mTransform.KeyMaker(holder.rq.Entity);

            var result = await InternalUpdate(holder);

            if (mCacheManager.IsActive && result.IsSuccess)
                mCacheManager.Write(mTransform, result.Entity);

            ProcessOutputEntity(key, holder.rq, holder.rs, result);
        }

        protected virtual async Task<IResponseHolder<E>> InternalUpdate(PersistenceRequestHolder<K, E> holder)
        {
            return new PersistenceResponseHolder<E>() { StatusCode = 501, IsSuccess = false};
        }
        #endregion

        #region Delete
        protected virtual async Task ProcessDelete(PersistenceRequestHolder<K, Tuple<K, string>> holder)
        {
            var result = await InternalDelete(holder.rq.Key, holder);

            if (mCacheManager.IsActive && result.IsSuccess)
                await mCacheManager.Delete(mTransform, holder.rq.Key);

            ProcessOutputKey(holder.rq, holder.rs, result);
        }

        protected virtual async Task<IResponseHolder> InternalDelete(K key, PersistenceRequestHolder<K, Tuple<K, string>> holder)
        {
            return new PersistenceResponseHolder() { StatusCode = 501, IsSuccess = false};
        }
        #endregion
        #region DeleteByRef
        protected virtual async Task ProcessDeleteByRef(PersistenceRequestHolder<K, Tuple<K, string>> holder)
        {
            var result = await InternalDeleteByRef(holder.rq.KeyReference, holder);

            if (mCacheManager.IsActive && result.IsSuccess)
                await mCacheManager.Delete(mTransform, mTransform.KeyDeserializer(result.Id));

            ProcessOutputKey(holder.rq, holder.rs, result);
        }
        protected virtual async Task<IResponseHolder> InternalDeleteByRef(Tuple<string, string> reference, PersistenceRequestHolder<K, Tuple<K, string>> holder)
        {
            return new PersistenceResponseHolder() { StatusCode = 501, IsSuccess = false };
        }
        #endregion

        #region Version
        protected virtual async Task ProcessVersion(PersistenceRequestHolder<K, Tuple<K, string>> holder)
        {
            IResponseHolder result = null;

            if (mCacheManager.IsActive)
                result = await mCacheManager.VersionRead(mTransform, holder.rq.Key);

            if (result == null || !result.IsSuccess)
                result = await InternalVersion(holder.rq.Key, holder);

            ProcessOutputKey(holder.rq, holder.rs, result);
        }

        protected virtual async Task<IResponseHolder> InternalVersion(K key, PersistenceRequestHolder<K, Tuple<K, string>> holder)
        {
            return new PersistenceResponseHolder() { StatusCode = 501, IsSuccess = false };
        }
        #endregion
        #region VersionByRef
        protected virtual async Task ProcessVersionByRef(PersistenceRequestHolder<K, Tuple<K, string>> holder)
        {
            IResponseHolder result = null;

            if (mCacheManager.IsActive)
                result = await mCacheManager.VersionRead(mTransform, holder.rq.KeyReference);

            if (result == null || !result.IsSuccess)
                result = await InternalVersionByRef(holder.rq.KeyReference, holder);

            ProcessOutputKey(holder.rq, holder.rs, result);
        }

        protected virtual async Task<IResponseHolder> InternalVersionByRef(Tuple<string, string> reference, PersistenceRequestHolder<K, Tuple<K, string>> holder)
        {
            return new PersistenceResponseHolder() { StatusCode = 501, IsSuccess = false };
        }
        #endregion

        #region Search
        /// <summary>
        /// This is not currently used.
        /// </summary>
        /// <param name="rq"></param>
        /// <param name="rs"></param>
        /// <param name="prq"></param>
        /// <param name="prs"></param>
        /// <returns></returns>
        protected virtual async Task ProcessSearch(PersistenceRequestHolder<K, Tuple<K, string>> holder)
        {
            holder.rs.ResponseCode = 501;
            holder.rs.ResponseMessage = "Not implemented.";
        }
        #endregion

        #region ProcessOutputEntity...
        /// <summary>
        /// This method sets the entity and any associated metadata in to the response.
        /// </summary>
        /// <param name="key">The entity key.</param>
        /// <param name="rq">The original request.</param>
        /// <param name="rs">The outgoing response.</param>
        /// <param name="holderResponse">The underlying storage response.</param>
        protected virtual void ProcessOutputEntity(E entity, PersistenceRepositoryHolder<K, E> rq, PersistenceRepositoryHolder<K, E> rs)
        {
            rs.Entity = entity;
            rs.Key = mTransform.KeyMaker(rs.Entity);
            rs.Settings.VersionId = mTransform.Version?.EntityVersionAsString(rs.Entity);

            rs.KeyReference = new Tuple<string, string>(rs.Key.ToString(), rs.Settings.VersionId);
        }
   
        /// <summary>
        /// This method sets the entity and any associated metadata in to the response.
        /// </summary>
        /// <param name="key">The entity key.</param>
        /// <param name="rq">The original request.</param>
        /// <param name="rs">The outgoing response.</param>
        /// <param name="holderResponse">The underlying storage response.</param>
        protected virtual void ProcessOutputEntity(K key, PersistenceRepositoryHolder<K, E> rq, PersistenceRepositoryHolder<K, E> rs, IResponseHolder<E> holderResponse)
        {
            rs.ResponseCode = holderResponse.StatusCode;

            if (holderResponse.IsSuccess)
                ProcessOutputEntity(holderResponse.Entity, rq, rs);
            else
                ProcessOutputError(key, holderResponse, rs);
        }
        /// <summary>
        /// This method sets the entity and any associated metadata in to the response.
        /// </summary>
        /// <param name="key">The entity key.</param>
        /// <param name="rq">The original request.</param>
        /// <param name="rs">The outgoing response.</param>
        /// <param name="holderResponse">The underlying storage response.</param>
        protected virtual void ProcessOutputEntity(K key, PersistenceRepositoryHolder<K, E> rq, PersistenceRepositoryHolder<K, E> rs
            , IResponseHolder holderResponse)
        {
            rs.ResponseCode = holderResponse.StatusCode;

            if (holderResponse.IsSuccess)
                ProcessOutputEntity(mTransform.EntityDeserializer(holderResponse.Content), rq, rs);
            else
                ProcessOutputError(key, holderResponse, rs);
        }
        #endregion

        protected virtual void ProcessOutputError(K key, IResponseHolder holderResponse, PersistenceRepositoryHolder<K, E> rs)
        {
            if (holderResponse.Ex != null && !rs.IsTimeout)
                Logger.LogException(string.Format("Error in persistence {0}-{1}", typeof(E).Name, key), holderResponse.Ex);
            else
                Logger.LogMessage(
                    rs.IsTimeout ? LoggingLevel.Warning : LoggingLevel.Info,
                    string.Format("Error in persistence {0}-{1}-{2}-{3}", typeof(E).Name, rs.ResponseCode, key,
                        holderResponse.Ex != null ? holderResponse.Ex.ToString() : rs.ResponseMessage), typeof(E).Name);

            rs.IsTimeout = holderResponse.IsTimeout;
        }

        #region ProcessOutputKey...
        /// <summary>
        /// This method processes the common output method for key based operations such as delete and version.
        /// </summary>
        /// <param name="rq">The incoming request.</param>
        /// <param name="rs">The outgoing response.</param>
        /// <param name="holderResponse">The internal holder response.</param>
        protected virtual void ProcessOutputKey(PersistenceRepositoryHolder<K, Tuple<K, string>> rq, PersistenceRepositoryHolder<K, Tuple<K, string>> rs
            , IResponseHolder holderResponse)
        {
            rs.Key = rq.Key;
            rs.ResponseCode = holderResponse.StatusCode;

            if (holderResponse.IsSuccess)
            {
                rs.Settings.VersionId = holderResponse.VersionId;
                rs.Entity = new Tuple<K, string>(rs.Key, holderResponse.VersionId);
                rs.KeyReference = new Tuple<string, string>(rs.Key == null ? null : rs.Key.ToString(), holderResponse.VersionId);
            }
            else
            {
                rs.IsTimeout = holderResponse.IsTimeout;
                //rs.ResponseCode = holderResponse.IsTimeout?408:404;
            }
        }
        #endregion
    }
}