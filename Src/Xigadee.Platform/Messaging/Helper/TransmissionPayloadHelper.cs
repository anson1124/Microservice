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
using System.Linq;
using System.Threading;

namespace Xigadee
{
    /// <summary>
    /// This class provides static extension methods for the transmission payload.
    /// </summary>
    public static class TransmissionPayloadHelper
    {
        /// <summary>
        /// This helper method turns round an incoming payload request in to its corresponding response payload.
        /// </summary>
        /// <param name="incoming">The incoming payload.</param>
        /// <returns></returns>
        public static TransmissionPayload ToResponse(this TransmissionPayload incoming)
        {
            var m = incoming.Message;
            var rsMessage = m.ToResponse();

            rsMessage.ChannelId = m.ResponseChannelId;
            rsMessage.ChannelPriority = m.ResponseChannelPriority;
            rsMessage.MessageType = m.ResponseMessageType;
            rsMessage.ActionType = m.ResponseActionType; 
            
            return new TransmissionPayload(rsMessage);
        }

        public static void PayloadPack<O>(this TransmissionPayload incoming, IPayloadSerializationContainer c, O data)
        {
            incoming.Message.Blob = c.PayloadSerialize(data);
        }

        public static void PayloadPack(this TransmissionPayload incoming, IPayloadSerializationContainer c)
        {
            incoming.Message.Blob = c.PayloadSerialize(incoming.MessageObject);
        }

        public static S PayloadUnpack<S>(this TransmissionPayload incoming, IPayloadSerializationContainer c)
        {
            return c.PayloadDeserialize<S>(incoming.Message.Blob);
        }

    }
}
