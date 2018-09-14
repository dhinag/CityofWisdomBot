using System;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Host;

public class BotMessage
{
    public string Message { get; set; }
}

public static BotMessage Run(string queueMessage, out BotMessage botMessage, TraceWriter log)
{
    botMessage = new BotMessage
    {
        Message = queueMessage
    };
    log.Info($"Message Processed: {queueMessage}");
    return botMessage;
}
