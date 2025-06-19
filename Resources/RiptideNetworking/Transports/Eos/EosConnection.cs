// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Epic.OnlineServices.Lobby;
using Riptide;
using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Net;

namespace Riptide.Transports.Eos {
    /// <summary>Represents a connection to a <see cref="EosServer"/> or <see cref="EosClient"/>.</summary>
    public class EosConnection : Connection, IEquatable<EosConnection> {
        /// <summary>The endpoint representing the other end of the connection.</summary>
        public readonly IPEndPoint RemoteEndPoint;

        /// <summary>The local peer this connection is associated with.</summary>
        private readonly EosPeer peer;

        /// <summary>Initializes the connection.</summary>
        /// <param name="remoteEndPoint">The endpoint representing the other end of the connection.</param>
        /// <param name="peer">The local peer this connection is associated with.</param>
        internal EosConnection(IPEndPoint remoteEndPoint, EosPeer peer) {
            RemoteEndPoint = remoteEndPoint;
            this.peer = peer;
        }

        /// <inheritdoc/>
        protected internal override void Send(byte[] dataBuffer, int amount) {
            CopyLobbyDetailsHandleOptions options = new();
            options.LocalUserId = EOSSDK.LocalUserProductId;
            options.LobbyId = EosPeer.currentListenAddress;
            // Does not know how to send to all clients...
            //peer.Send(, dataBuffer, amount, RemoteEndPoint);
        }

        /// <inheritdoc/>
        public override string ToString() => RemoteEndPoint.ToStringBasedOnIPFormat();

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as EosConnection);
        /// <inheritdoc/>
        public bool Equals(EosConnection other) {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return RemoteEndPoint.Equals(other.RemoteEndPoint);
        }

        /// <inheritdoc/>
        public override int GetHashCode() {
            return -288961498 + EqualityComparer<IPEndPoint>.Default.GetHashCode(RemoteEndPoint);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static bool operator ==(EosConnection left, EosConnection right)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (left is null) {
                if (right is null)
                    return true;

                return false; // Only the left side is null
            }

            // Equals handles case of null on right side
            return left.Equals(right);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static bool operator !=(EosConnection left, EosConnection right) => !(left == right);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
