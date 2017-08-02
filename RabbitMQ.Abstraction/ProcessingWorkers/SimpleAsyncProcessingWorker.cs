﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Abstraction.Interfaces;
using RabbitMQ.Abstraction.Messaging.Interfaces;

namespace RabbitMQ.Abstraction.ProcessingWorkers
{
    public class SimpleAsyncProcessingWorker<T> : AbstractSimpleProcessingWorker<T> where T : class
    {
        private readonly Func<T, CancellationToken, Task> _callbackFunc;

        private readonly TimeSpan _processingTimeout;

        private readonly Func<IEnumerable<T>, CancellationToken, Task> _batchCallbackFunc;

        private readonly ushort _batchSize;

        public SimpleAsyncProcessingWorker(IQueueConsumer consumer, Func<T, CancellationToken, Task> callbackFunc, 
            TimeSpan processingTimeout, Func<IEnumerable<T>, CancellationToken, Task> batchCallbackFunc = null, ushort batchSize = 1)
            : base(consumer)
        {
            _callbackFunc = callbackFunc;
            _batchCallbackFunc = batchCallbackFunc;
            _processingTimeout = processingTimeout;
            _batchSize = batchSize;
        }

        public SimpleAsyncProcessingWorker(IQueueClient queueClient, string queueName, 
            Func<T, CancellationToken, Task> callbackFunc, TimeSpan processingTimeout, 
            Func<IEnumerable<T>, CancellationToken, Task> batchCallbackFunc = null, ushort batchSize = 1,
            IConsumerCountManager consumerCountManager = null, IMessageRejectionHandler messageRejectionHandler = null)
            : base(queueClient, queueName, consumerCountManager, messageRejectionHandler)
        {
            _callbackFunc = callbackFunc;
            _batchCallbackFunc = batchCallbackFunc;
            _processingTimeout = processingTimeout;
            _batchSize = batchSize;
        }

        public static async Task<SimpleAsyncProcessingWorker<T>> CreateAndStartAsync(IQueueConsumer consumer, 
            Func<T, CancellationToken, Task> callbackFunc, TimeSpan processingTimeout, CancellationToken cancellationToken, 
            Func<IEnumerable<T>, CancellationToken, Task> batchCallbackFunc = null, ushort batchSize = 1)
        {
            var instance = new SimpleAsyncProcessingWorker<T>(consumer, callbackFunc, processingTimeout, batchCallbackFunc, batchSize);

            await instance.StartAsync(cancellationToken).ConfigureAwait(false);

            return instance;
        }

        public static async Task<SimpleAsyncProcessingWorker<T>> CreateAndStartAsync(IQueueClient queueClient, 
            string queueName, Func<T, CancellationToken, Task> callbackFunc, TimeSpan processingTimeout, 
            CancellationToken cancellationToken, Func<IEnumerable<T>, CancellationToken, Task> batchCallbackFunc = null, 
            ushort batchSize = 1, IConsumerCountManager consumerCountManager = null, IMessageRejectionHandler messageRejectionHandler = null)
        {
            var instance = new SimpleAsyncProcessingWorker<T>(queueClient, queueName, callbackFunc,
                processingTimeout, batchCallbackFunc, batchSize, consumerCountManager, messageRejectionHandler);

            await instance.StartAsync(cancellationToken).ConfigureAwait(false);

            return instance;
        }

        public override async Task OnMessageAsync(T message, IFeedbackSender feedbackSender, CancellationToken cancellationToken)
        {
            try
            {
                var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(_processingTimeout);

                await _callbackFunc(message, tokenSource.Token).ConfigureAwait(false);
                feedbackSender.Ack();
            }
            catch (Exception)
            {
                feedbackSender.Nack(true);
            }
        }

        public override async Task OnBatchAsync(IEnumerable<T> batch, IFeedbackSender feedbackSender, CancellationToken cancellationToken)
        {
            if (_batchCallbackFunc == null)
            {
                throw new Exception("Undefined batch callback function");
            }

            try
            {
                var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(_processingTimeout);

                await _batchCallbackFunc(batch, tokenSource.Token).ConfigureAwait(false);
                feedbackSender.Ack();
            }
            catch (Exception)
            {
                feedbackSender.Nack(true);
            }
        }

        public override ushort GetBatchSize()
        {
            return _batchSize;
        }
    }
}