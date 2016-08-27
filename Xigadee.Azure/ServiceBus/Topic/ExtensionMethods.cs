﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xigadee
{
    public static class AzureServiceBusTopicExtensionMethods
    {
        public static ChannelPipelineIncoming AddAzureSBTopicListener(this ChannelPipelineIncoming cpipe
            , string connectionName
            , IEnumerable<ListenerPartitionConfig> priorityPartitions
            , string serviceBusConnection = null
            , string subscriptionId = null
            , bool isDeadLetterListener = false
            , bool deleteOnStop = true
            , bool listenOnOriginatorId = false
            , string mappingChannelId = null
            , TimeSpan? deleteOnIdleTime = null
            , IEnumerable<ResourceProfile> resourceProfiles = null
            , IBoundaryLogger boundaryLogger = null
            , Action<AzureSBTopicListener> onCreate = null)
        {
            var component = cpipe.Pipeline.AddListener((c) => new AzureSBTopicListener(
                  cpipe.Channel.Id
                , serviceBusConnection ?? c.ServiceBusConnection()
                , connectionName
                , priorityPartitions
                , subscriptionId
                , isDeadLetterListener
                , deleteOnStop
                , listenOnOriginatorId
                , mappingChannelId
                , deleteOnIdleTime
                , resourceProfiles
                , boundaryLogger ?? cpipe.Channel.BoundaryLogger));
            
            onCreate?.Invoke(component);

            return cpipe;
        }

        public static ChannelPipelineOutgoing AddAzureSBTopicSender(this ChannelPipelineOutgoing cpipe
            , string connectionName
            , IEnumerable<SenderPartitionConfig> priorityPartitions
            , string serviceBusConnection = null
            , IBoundaryLogger boundaryLogger = null
            , Action<AzureSBTopicSender> onCreate = null
            )
        {
            var component = cpipe.Pipeline.AddSender((c) => new AzureSBTopicSender(
                  cpipe.Channel.Id
                , serviceBusConnection ?? c.ServiceBusConnection()
                , connectionName
                , priorityPartitions
                , boundaryLogger ?? cpipe.Channel.BoundaryLogger));

            onCreate?.Invoke(component);

            return cpipe;
        }

    }
}