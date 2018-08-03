﻿using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LunarLabs.Bots
{
    public class DiscordConnection : BotConnection
    {
        private ConcurrentQueue<BotMessage> _queue;
        private DiscordSocketClient _client;

        public DiscordConnection(string token)
        {
            _client = new DiscordSocketClient();

            //client.Log += Log;

            _client.LoginAsync(TokenType.Bot, token).Wait();
        }

        private Dictionary<ulong, ISocketMessageChannel> _channels = new Dictionary<ulong, ISocketMessageChannel>();

        public async Task Send(long target, string text)
        {
            //var channel = _channels[(ulong)target];

            var id = (ulong)target;

            var channel = _client.GetChannel(id) as ISocketMessageChannel;

            if (channel == null)
            {
                var user = _client.GetUser(id);

                if (user == null)
                {
                    return;
                }

                channel = (ISocketMessageChannel)(await user.GetOrCreateDMChannelAsync());
            }

            await channel.SendMessageAsync(text);
        }

        public void Start(ConcurrentQueue<BotMessage> queue)
        {
            this._queue = queue;
            _client.MessageReceived += MessageReceived;
            _client.StartAsync().Wait();

            Console.WriteLine($"Connected to Discord");
        }

        private async Task MessageReceived(SocketMessage src)
        {
            try
            {
                if (src.Author.Id == _client.CurrentUser.Id)
                {
                    return;
                }

                MessageKind kind;

                var msg = new BotMessage();
                msg.Visibility = (src.Channel is IPrivateChannel) ? MessageVisibility.Private : MessageVisibility.Public;


                var sourceID =  msg.Visibility == MessageVisibility.Private ? src.Author.Id : src.Channel.Id;
                var sourceHandle = msg.Visibility == MessageVisibility.Private ? src.Author.Username : src.Channel.Name;
                var sourceName = msg.Visibility == MessageVisibility.Private ? src.Author.Username : src.Channel.Name;

                msg.Kind = MessageKind.Text;
                msg.Text = src.Content;
                msg.Sender = new MessageSender() { ID = (long)sourceID, Handle = sourceHandle, Name = sourceName, Platform = BotPlatform.Discord};
                msg.File = null;

                _channels[sourceID] = src.Channel;

                _queue.Enqueue(msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public string GetCommandPrefix()
        {
            return "!";
        }
    }
}