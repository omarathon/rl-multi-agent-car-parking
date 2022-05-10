using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.SideChannels;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Newtonsoft.Json;
using Unity.MLAgents;

public class TrainingSideChannel : SideChannel
{
    private EnvironmentManager environmentManager;
    
    public TrainingSideChannel(EnvironmentManager environmentManager)
    {
        ChannelId = new Guid("621f0a70-4f87-11ea-a6bf-784f4387d1f7"); // random ID we use
        this.environmentManager = environmentManager;
    }

    protected override void OnMessageReceived(IncomingMessage msg)
    {
        var receivedString = msg.ReadString();
        Debug.Log("From Python: " + receivedString);
        if (receivedString.StartsWith("config:"))
        {
            var envConfigJson = receivedString.Substring("config:".Length);
            var envConfig = JsonConvert.DeserializeObject<EnvironmentConfig>(envConfigJson);
            environmentManager.ONReceieveEnvConfig(envConfig);
            var obsActSizesMsg = new OutgoingMessage();
            var obsActSizesStr =
                $"configACK:obs[{string.Join(",", envConfig.ComputeDiscreteObsRanges())}]act[{string.Join(",", envConfig.ComputeDiscreteActionRanges())}]";
            obsActSizesMsg.WriteString(obsActSizesStr);
            QueueMessageToSend(obsActSizesMsg);
            Debug.Log($"Sent to Python: {obsActSizesStr}");
        }
    }
}
