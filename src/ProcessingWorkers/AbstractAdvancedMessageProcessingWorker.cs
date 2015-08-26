﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vtex.RabbitMQ.Exceptions.Workflow;
using Vtex.RabbitMQ.Messaging.Interfaces;

namespace Vtex.RabbitMQ.ProcessingWorkers
{
    public abstract class AbstractAdvancedMessageProcessingWorker<T> : AbstractSimpleMessageProcessingWorker<T> where T : class
    {
        protected readonly int InvokeRetryCount;

        protected readonly int InvokeRetryWaitMilliseconds;

        protected readonly ExceptionHandlingStrategy ExceptionHandlingStrategy;

        protected AbstractAdvancedMessageProcessingWorker(IQueueConsumer consumer, 
            ExceptionHandlingStrategy exceptionHandlingStrategy = ExceptionHandlingStrategy.Requeue, 
            int invokeRetryCount = 1, int invokeRetryWaitMilliseconds = 0, bool autoStartup = true)
            : base(consumer, autoStartup)
        {
            InvokeRetryCount = invokeRetryCount;
            InvokeRetryWaitMilliseconds = invokeRetryWaitMilliseconds;
            ExceptionHandlingStrategy = exceptionHandlingStrategy;
        }

        protected AbstractAdvancedMessageProcessingWorker(IQueueClient queueClient, string queueName, 
            ExceptionHandlingStrategy exceptionHandlingStrategy = ExceptionHandlingStrategy.Requeue, 
            int invokeRetryCount = 1, int invokeRetryWaitMilliseconds = 0, ConsumerCountManager consumerCountManager = null, 
            IMessageRejectionHandler messageRejectionHandler = null, bool autoStartup = true)
            : base(queueClient, queueName, consumerCountManager, messageRejectionHandler, autoStartup)
        {
            InvokeRetryCount = invokeRetryCount;
            InvokeRetryWaitMilliseconds = invokeRetryWaitMilliseconds;
            ExceptionHandlingStrategy = exceptionHandlingStrategy;
        }

        protected abstract bool TryInvoke(T message, List<Exception> exceptions);

        public override void OnMessage(T message, IMessageFeedbackSender feedbackSender)
        {
            var invocationSuccess = false;
            var exceptions = new List<Exception>();

            var tryCount = 0;

            while (tryCount == 0 || (!invocationSuccess && ShouldRetry(tryCount, exceptions)))
            {
                if (tryCount > 0 && InvokeRetryWaitMilliseconds > 0)
                {
                    Task.Delay(InvokeRetryWaitMilliseconds);
                }

                tryCount++;

                invocationSuccess = TryInvoke(message, exceptions);
            }

            if (invocationSuccess)
            {
                feedbackSender.Ack();
            }
            else if (ShouldRequeue(exceptions))
            {
                feedbackSender.Nack(true);
            }
            else
            {
                feedbackSender.Nack(false);
            }
        }

        private static ExceptionHandlingStrategy? GetStrategyByExceptions(List<Exception> exceptions)
        {
            if (exceptions.Any())
            {
                if (exceptions.Last() is QueuingRetryException || exceptions.Last().InnerException is QueuingRetryException)
                {
                    return ExceptionHandlingStrategy.Retry;
                }
                else if (exceptions.Last() is QueuingRequeueException || exceptions.Last().InnerException is QueuingRequeueException)
                {
                    return ExceptionHandlingStrategy.Requeue;
                }
                else if (exceptions.Last() is QueuingDiscardException || exceptions.Last().InnerException is QueuingDiscardException)
                {
                    return ExceptionHandlingStrategy.Discard;
                }
            }

            return null;
        }

        private bool ShouldRetry(int tryCount, List<Exception> exceptions)
        {
            if (tryCount >= InvokeRetryCount)
            {
                return false;
            }

            var strategyByExceptions = GetStrategyByExceptions(exceptions);

            if (strategyByExceptions != null)
            {
                if (strategyByExceptions == ExceptionHandlingStrategy.Retry)
                {
                    return true;
                }
                else if (strategyByExceptions == ExceptionHandlingStrategy.Discard)
                {
                    return false;
                }
            }

            if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.Retry)
            {
                return true;
            }

            return false;
        }

        private bool ShouldRequeue(List<Exception> exceptions)
        {
            var strategyByExceptions = GetStrategyByExceptions(exceptions);

            if (strategyByExceptions != null)
            {
                if (strategyByExceptions == ExceptionHandlingStrategy.Requeue)
                {
                    return true;
                }
                else if (strategyByExceptions == ExceptionHandlingStrategy.Discard)
                {
                    return false;
                }
            }

            if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.Requeue)
            {
                return true;
            }

            return false;
        }
    }
}