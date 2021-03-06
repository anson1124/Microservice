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
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
#endregion
namespace Xigadee
{
    /// <summary>
    /// This class holds the Handler and the action and its associated statistics.
    /// </summary>
    public class CommandHandler<S>: StatisticsBase<S>, ICommandHandler where S: CommandHandlerStatistics, new()
    {
        #region Declarations
        /// <summary>
        /// This is the tick count of the last time the command was accessed.
        /// </summary>
        public int? mLastAccessed = null;
        #endregion        
        #region Constructor
        /// <summary>
        /// This is the default constructor.
        /// </summary>
        public CommandHandler()
        {
        }
        #endregion

        #region Initialise(MessageFilterWrapper key, Func<TransmissionPayload, List<TransmissionPayload>, Task> action)
        /// <summary>
        /// This method initialises the holder.
        /// </summary>
        /// <param name="parent">The name of the parent container.</param>
        /// <param name="key"></param>
        /// <param name="action"></param>
        public virtual void Initialise(CommandHolder holder)
        {
            Key = holder.Message;
            Action = holder.Command;
        }
        #endregion

        #region Key
        /// <summary>
        /// This is the key for the specific handler.
        /// </summary>
        public MessageFilterWrapper Key { get; protected set; }
        #endregion
        #region Action
        /// <summary>
        /// This is the action called when an incoming message comes in.
        /// </summary>
        public Func<TransmissionPayload, List<TransmissionPayload>, Task> Action { get; protected set; }
        #endregion

        /// <summary>
        /// This is the HandlerStatistics
        /// </summary>
        public ICommandHandlerStatistics HandlerStatistics
        {
            get
            {
                return Statistics;
            }
        }
        /// <summary>
        /// This method recalculates the statistics for the command handler.
        /// </summary>
        /// <param name="stats">The stats to recalculate.</param>
        protected override void StatisticsRecalculate(S stats)
        {
            stats.Name = Key.Header.ToKey();
            stats.LastAccessed = mLastAccessed.HasValue ? ConversionHelper.DeltaAsFriendlyTime(mLastAccessed.Value, Environment.TickCount):"Not accessed";
        }

        #region Execute(TransmissionPayload rq, List<TransmissionPayload> rs)
        /// <summary>
        /// This method executes the message handler and logs the time statistics.
        /// </summary>
        /// <param name="rq">The incoming requests.</param>
        /// <param name="rs">The outgoing responses.</param>
        public async virtual Task Execute(TransmissionPayload rq, List<TransmissionPayload> rs)
        {
            int timerStart = StatisticsInternal.ActiveIncrement();
            mLastAccessed = Environment.TickCount;

            try
            {
                await Action(rq, rs);
            }
            catch (Exception ex)
            {
                StatisticsInternal.ErrorIncrement();
                StatisticsInternal.Ex = ex;
                throw;
            }
            finally
            {
                StatisticsInternal.ActiveDecrement(timerStart);
            }
        } 
        #endregion
    }
}
