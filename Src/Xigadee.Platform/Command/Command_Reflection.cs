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
using System.Reflection;
using System.Threading.Tasks;
#endregion
namespace Xigadee
{
    public abstract partial class CommandBase<S, P, H>
    {
        /// <summary>
        /// This method scans through the command and registers commands that are defined using the metadata tags.
        /// </summary>
        protected virtual void CommandsRegisterReflection()
        {
            foreach (var signature in this.CommandMethodAttributeSignatures(true))
            {
                var handler = new CommandHolder(CommandChannelAdjust(signature.Item1)
                    , (rq,rs) => signature.Item2.Action(rq,rs,PayloadSerializer)
                    , referenceId: signature.Item3);

                CommandRegister(handler);
            }
        }

        /// <summary>
        /// This method replaces the channel with the command default if the value specified in the attribute is null.
        /// </summary>
        /// <param name="attr">The incoming attribute whose header channel should be checked.</param>
        /// <returns>Returns a message filter wrapper for the header.</returns>
        protected MessageFilterWrapper CommandChannelAdjust(CommandContractAttribute attr)
        {
            ServiceMessageHeader header = attr.Header;

            if (header.ChannelId == null)
                header = new ServiceMessageHeader(ChannelId, header.MessageType, header.ActionType);

            return new MessageFilterWrapper(header);
        }
    }
}
