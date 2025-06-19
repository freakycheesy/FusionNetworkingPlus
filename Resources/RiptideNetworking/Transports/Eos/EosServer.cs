// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using FNPlus.Network;
using Riptide;
using Riptide.Transports;
using System;
using System.Collections.Generic;
using System.Net;

namespace Riptide.Transports.Eos {
    /// <summary>A server which can accept connections from <see cref="EosClient"/>s.</summary>
    public class EosServer : EosPeer, IServer {
        /// <inheritdoc/>
        public event EventHandler<ConnectedEventArgs> Connected;
        /// <inheritdoc/>
        public event EventHandler<DataReceivedEventArgs> DataReceived;

        /// <inheritdoc/>
        public ushort Port {
            get; private set;
        }

        /// <summary>The currently open connections, accessible by their endpoints.</summary>
        private Dictionary<IPEndPoint, Connection> connections;
        /// <summary>The IP address to bind the socket to, if any.</summary>
        public readonly string listenAddress;

        /// <inheritdoc/>
        /// <summary>Initializes the transport, binding the socket to a specific IP address.</summary>
        /// <param name="listenAddress">The IP address to bind the socket to.</param>
        /// <param name="socketBufferSize">How big the socket's send and receive buffers should be.</param>
        public EosServer(string listenAddress, int socketBufferSize = DefaultSocketBufferSize) : base(UnityEngine.Random.Range(11111111, 88888888).ToString(), socketBufferSize) {
            this.listenAddress = listenAddress;
        }

        /// <inheritdoc/>
        public void Start(ushort port) {
            Port = port;
            connections = new Dictionary<IPEndPoint, Connection>();

            OpenSocket(UnityEngine.Random.Range(11111111, 99999999).ToString(), port);
        }

        /// <summary>Decides what to do with a connection attempt.</summary>
        /// <param name="fromEndPoint">The endpoint the connection attempt is coming from.</param>
        /// <returns>Whether or not the connection attempt was from a new connection.</returns>
        private bool HandleConnectionAttempt(IPEndPoint fromEndPoint) {
            if (connections.ContainsKey(fromEndPoint))
                return false;

            EosConnection connection = new EosConnection(fromEndPoint, this);
            connections.Add(fromEndPoint, connection);
            OnConnected(connection);
            return true;
        }

        /// <inheritdoc/>
        public void Close(ProductUserId connection) {
            var options = new CloseConnectionOptions();
            options.RemoteUserId = connection;
            options.LocalUserId = EOSSDK.LocalUserProductId;
            if (RiptideThreader.IsServerRunning) EOSSDK.PlatformInterface.GetP2PInterface().CloseConnection(ref options);
        }

        /// <inheritdoc/>
        public void Shutdown() {
            CloseSocket();
            connections.Clear();
        }

        /// <summary>Invokes the <see cref="Connected"/> event.</summary>
        /// <param name="connection">The successfully established connection.</param>
        protected virtual void OnConnected(Connection connection) {
            Connected?.Invoke(this, new ConnectedEventArgs(connection));
        }

        /// <inheritdoc/>
        protected override void OnDataReceived(byte[] dataBuffer, int amount, IPEndPoint fromEndPoint) {
            if ((MessageHeader)(dataBuffer[0] & Message.HeaderBitmask) == MessageHeader.Connect && !HandleConnectionAttempt(fromEndPoint))
                return;

            if (connections.TryGetValue(fromEndPoint, out Connection connection) && !connection.IsNotConnected)
                DataReceived?.Invoke(this, new DataReceivedEventArgs(dataBuffer, amount, connection));
        }

        public void Close(Connection connection) {
            throw new NotImplementedException();
        }
    }
}
