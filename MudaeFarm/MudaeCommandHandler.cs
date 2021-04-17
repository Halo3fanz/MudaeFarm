using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Events;
using Microsoft.Extensions.Logging;

namespace MudaeFarm
{
    public interface IMudaeCommandHandler
    {
        /// <summary>
        /// Sends the given command and returns a task that resolves to the first message received from Mudae.
        /// </summary>
        Task<IUserMessage> SendAsync(IMessageChannel channel, string command, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reacts to the given message with an emoji and returns a task that resolves to the first message received from Mudae.
        /// </summary>
        Task<IUserMessage> ReactAsync(IUserMessage message, IEmoji emoji, CancellationToken cancellationToken = default);
    }

    public static class MudaeSemaphore
    {
        public static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
    }

    public class MudaeCommandHandler : IMudaeCommandHandler
    {
        readonly IDiscordClientService _discord;
        readonly IMudaeUserFilter _userFilter;
        readonly ILogger<MudaeCommandHandler> _logger;

        public MudaeCommandHandler(IDiscordClientService discord, IMudaeUserFilter userFilter, ILogger<MudaeCommandHandler> logger)
        {
            _discord    = discord;
            _userFilter = userFilter;
            _logger     = logger;
        }

        public async Task<IUserMessage> SendAsync(IMessageChannel channel, string command, CancellationToken cancellationToken = default)
        {
            var client = await _discord.GetClientAsync();

            var watch = Stopwatch.StartNew();

            await MudaeSemaphore.semaphoreSlim.WaitAsync(cancellationToken);
            try
            {
                await client.SendMessageAsync(channel.Id, command);
                
                try
                {
                    var response = await ReceiveAsync(client, channel, cancellationToken);

                    _logger.LogDebug($"Sent command '{command}' in channel '{channel.Name}' ({channel.Id}) and received Mudae response '{response.Content}' ({response.Embeds.Count} embeds) in {watch.Elapsed.TotalMilliseconds}ms.");

                    return response;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning($"Sent command '{command}' in channel '{channel.Name}' ({channel.Id}) but did not receive expected Mudae response.");
                    throw;
                }
            } 
            finally
            {
                MudaeSemaphore.semaphoreSlim.Release();
            }

        }

        public async Task<IUserMessage> ReactAsync(IUserMessage message, IEmoji emoji, CancellationToken cancellationToken = default)
        {
            var client  = await _discord.GetClientAsync();
            var channel = client.GetChannel(message.ChannelId);

            var watch = Stopwatch.StartNew();

            await MudaeSemaphore.semaphoreSlim.WaitAsync(cancellationToken);

            try
            {
                await message.AddReactionAsync(emoji);

                try
                {
                    var response = await ReceiveAsync(client, channel, cancellationToken);

                    _logger.LogDebug($"Attached reaction '{emoji}' to message {message.Id} in channel '{channel.Name}' ({channel.Id}) and received Mudae response '{response.Content}' ({response.Embeds.Count} embeds) in {watch.Elapsed.TotalMilliseconds}ms.");

                    return response;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning($"Attached reaction '{emoji}' to message {message.Id} in channel '{channel.Name}' ({channel.Id}) but did not receive expected Mudae response.");
                    throw;
                }
            }
            finally
            {
                MudaeSemaphore.semaphoreSlim.Release();
            }

        }

        async Task<IUserMessage> ReceiveAsync(DiscordClient client, IChannel channel, CancellationToken cancellationToken = default)
        {
            var response = new TaskCompletionSource<IUserMessage>();

            Task handleMessage(MessageReceivedEventArgs e)
            {
                bool isUserMessage = e.Message is IUserMessage;
                bool channelid = e.Message is IUserMessage message1 && message1.ChannelId == channel.Id;
                bool isMudae = e.Message is IUserMessage message2 && message2.ChannelId == channel.Id && _userFilter.IsMudae(message2.Author);
                
                if (e.Message is IUserMessage message && message.ChannelId == channel.Id && _userFilter.IsMudae(message.Author))
                    response.TrySetResult(message);

                return Task.CompletedTask;

            }

            client.MessageReceived += handleMessage;

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Cancellation token already cancelled!");
                    cancellationToken = default;
                }

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

                cancellationToken = linkedCts.Token;

                if (response.Task.IsCanceled)
                {
                    _logger.LogWarning("Task already cancelled!");
                }

                await using (cancellationToken.Register(() => response.TrySetCanceled(cancellationToken)))
                    return await response.Task;
            }
            finally
            {
                client.MessageReceived -= handleMessage;
            }
        }
    }
}