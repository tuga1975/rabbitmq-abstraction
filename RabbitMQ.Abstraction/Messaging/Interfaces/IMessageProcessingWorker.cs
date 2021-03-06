﻿using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Abstraction.Messaging.Interfaces
{
    public interface IMessageProcessingWorker<in T> where T : class
    {
        Task OnMessageAsync(T message, RabbitMQConsumerContext consumerContext, IFeedbackSender feedbackSender, CancellationToken cancellationToken);
    }
}
