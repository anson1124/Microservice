﻿#region using
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#endregion
namespace Xigadee
{
    public partial class CommunicationContainer
    {
        //Senders
        #region --> SenderAdd(ISender sender)
        /// <summary>
        /// This method adds a registered sender to the communication collection.
        /// </summary>
        /// <param name="sender">The sender to add.</param>
        public void SenderAdd(ISender sender)
        {
            mSenders.Add(sender);
            mMessageSenderMap.Clear();
        }
        #endregion

        #region SendersStart()
        /// <summary>
        /// This method starts the registered senders in the container.
        /// </summary>
        public void SendersStart()
        {
            try
            {
                mSenders.ForEach(l => ServiceStart(l));
            }
            catch (Exception ex)
            {
                Logger.LogException("Communication/SendersStart", ex);
                throw;
            }
        }
        #endregion
        #region SendersStop()
        /// <summary>
        /// This method stops the senders in the container.
        /// </summary>
        public void SendersStop()
        {
            mSenders.ForEach(l => ServiceStop(l));
        }
        #endregion

        #region Send(TransmissionPayload requestPayload)
        /// <summary>
        /// This method transmits the messages to the relevant senders.
        /// </summary>
        /// <param name="payload">The payload messages to externalOnly</param>
        public virtual async Task<bool> Send(TransmissionPayload payload)
        {
            try
            {
                //No, we want to send the message externally.
                List<ISender> messageSenders = null;
                //Get the supported message handler
                if (payload.Message.ChannelId != null && !mMessageSenderMap.TryGetValue(payload.Message.ChannelId, out messageSenders))
                    messageSenders = MessageSenderResolve(payload);

                //If there are no supported senders for the particular channelId then throw an exception
                if (messageSenders == null || messageSenders.Count == 0)
                {
                    Logger.LogMessage(LoggingLevel.Info, string.Format("Unable to resolve sender for message {0}", payload != null ? payload.Message : null), "Communication");
                    return false;
                }

                //Set the outgoing originator if not set.
                if (string.IsNullOrEmpty(payload.Message.OriginatorServiceId))
                    payload.Message.OriginatorServiceId = OriginatorId;

                //Send the message to the supported senders.
                await Task.WhenAll(messageSenders.Select(s => s.ProcessMessage(payload)));
            }
            catch (Exception ex)
            {
                Logger?.LogException(string.Format("Unable to send message {0}", payload != null ? payload.Message : null), ex);
                return false;
            }

            return true;
        }
        #endregion
        #region MessageSenderResolve(TransmissionPayload payload)
        /// <summary>
        /// This message resolves the specific handler that can process the incoming message.
        /// </summary>
        /// <param name="payload">The incoming message payload.</param>
        /// <returns>Returns the supported handlers or null.</returns>
        protected virtual List<ISender> MessageSenderResolve(TransmissionPayload payload)
        {
            var message = payload.Message;

            string channelId = message.ChannelId;
            List<ISender> newMap = mSenders.Where(h => h.SupportsChannel(channelId)).ToList();

            //Make sure that the handler is queueAdded as a null value to stop further resolution attemps
            mMessageSenderMap.AddOrUpdate(channelId, newMap, (k, u) => newMap.Count == 0 ? null : newMap);

            return newMap;
        }
        #endregion

    }
}