﻿/*
 * Copyright 2017 Fireshark Studios, LLC
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using NLog;

using Butterfly.Util;

namespace Butterfly.Channel {
    public abstract class ChannelServer : IDisposable {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

        protected readonly ConcurrentDictionary<string, IChannel> channelById = new ConcurrentDictionary<string, IChannel>();

        protected readonly List<ChannelListener> onNewChannelListeners = new List<ChannelListener>();

        protected readonly int mustReceiveHeartbeatMillis;

        public ChannelServer(int mustReceiveHeartbeatMillis = 5000) {
            this.mustReceiveHeartbeatMillis = mustReceiveHeartbeatMillis;
        }

        public IDisposable OnNewChannel(string path, Func<IChannel, IDisposable> listener) {
            return new ListItemDisposable<ChannelListener>(onNewChannelListeners, new ChannelListener(path, listener));
        }

        public IDisposable OnNewChannelAsync(string path, Func<IChannel, Task<IDisposable>> listener) {
            return new ListItemDisposable<ChannelListener>(onNewChannelListeners, new ChannelListener(path, listener));
        }

        public int ChannelCount {
            get {
                return this.channelById.Count;
            }
        }

        public IChannel GetChannel(string channelId, bool throwExceptionIfMissing = false) {
            if (!this.channelById.TryGetValue(channelId, out IChannel channel) && throwExceptionIfMissing) throw new Exception($"Invalid channel id '{channelId}'");
            return channel;
        }

        /// <summary>
        /// Queues a value to be sent to the specified channel
        /// </summary>
        public void Queue(string channelId, object value) {
            var channel = this.GetChannel(channelId);
            channel.Queue(value);
        }

        /// <summary>
        /// Implementing classes should call this when the client creates a new channel to the server
        /// </summary>
        public void AddAndStartChannel(string channelId, string path, IChannel channel) {
            var existingChannel = this.GetChannel(channelId);
            if (existingChannel!=null) {
                existingChannel.Dispose();
            }
            var initChannelListeners = this.onNewChannelListeners.Where(x => x.path == path).ToArray();
            channel.Start(initChannelListeners);
            this.channelById[channelId] = channel;
        }

        protected bool started = false;
        public void Start() {
            this.started = true;
            Task backgroundTask = Task.Run(this.CheckForDeadChannelsAsync);

            this.DoStart();
        }

        protected abstract void DoStart();

        protected async Task CheckForDeadChannelsAsync() {
            while (this.started) {
                DateTime cutoffDateTime = DateTime.Now.AddMilliseconds(-this.mustReceiveHeartbeatMillis);
                DateTime? oldestLastReceivedHearbeatReceived = null;
                foreach ((string id, IChannel channel) in this.channelById.ToArray()) {
                    logger.Debug($"CheckForDeadChannelsAsync():channel.LastHeartbeatReceived={channel.LastHeartbeatReceived},cutoffDateTime={cutoffDateTime}");
                    if (channel.LastHeartbeatReceived < cutoffDateTime) {
                        bool removed = this.channelById.TryRemove(id, out IChannel removedChannel);
                        if (!removed) throw new Exception($"Could not remove channel id {id}");
                        channel.Dispose();
                    }
                    else if (oldestLastReceivedHearbeatReceived==null || oldestLastReceivedHearbeatReceived>channel.LastHeartbeatReceived) {
                        oldestLastReceivedHearbeatReceived = channel.LastHeartbeatReceived;
                    }
                }
                int delayMillis = oldestLastReceivedHearbeatReceived == null ? this.mustReceiveHeartbeatMillis : (int)(oldestLastReceivedHearbeatReceived.Value.AddMilliseconds(this.mustReceiveHeartbeatMillis) - DateTime.Now).TotalMilliseconds;
                logger.Debug($"CheckForDeadChannelsAsync():delayMillis={delayMillis}");
                await Task.Delay(delayMillis);
            }
        }

        public void Dispose() {
            this.started = false;
        }
    }

}