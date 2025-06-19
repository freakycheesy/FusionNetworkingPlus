// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.P2P;
using Riptide;
using Riptide.Transports;
using System;
using System.Net;
using System.Net.Sockets;

namespace Riptide.Transports.Eos {
    /// <summary>The kind of socket to create.</summary>
    public enum SocketMode {
        /// <summary>Dual-mode. Works with both IPv4 and IPv6.</summary>
        Both,
        /// <summary>IPv4 only mode.</summary>
        IPv4Only,
        /// <summary>IPv6 only mode.</summary>
        IPv6Only
    }

    /// <summary>Provides base send &#38; receive functionality for <see cref="EosServer"/> and <see cref="EosClient"/>.</summary>
    public abstract class EosPeer {
        /// <inheritdoc cref="IPeer.Disconnected"/>
        public event EventHandler<DisconnectedEventArgs> Disconnected;

        /// <summary>The default size used for the socket's send and receive buffers.</summary>
        protected const int DefaultSocketBufferSize = 1024 * 1024; // 1MB
        /// <summary>The minimum size that may be used for the socket's send and receive buffers.</summary>
        private const int MinSocketBufferSize = 256 * 1024; // 256KB
        /// <summary>How long to wait for a packet, in microseconds.</summary>
        private const int ReceivePollingTime = 500000; // 0.5 seconds

        /// <summary>Whether to create an IPv4 only, IPv6 only, or dual-mode socket.</summary>
        /// <summary>The size to use for the socket's send and receive buffers.</summary>
        private readonly int socketBufferSize;
        /// <summary>The array that incoming data is received into.</summary>
        private readonly byte[] receivedData;
        /// <summary>The socket to use for sending and receiving.</summary>
        /// <summary>Whether or not the transport is running.</summary>
        private bool isRunning;
        /// <summary>A reusable endpoint.</summary>

        /// <summary>Initializes the transport.</summary>
        /// <param name="mode">Whether to create an IPv4 only, IPv6 only, or dual-mode socket.</param>
        /// <param name="socketBufferSize">How big the socket's send and receive buffers should be.</param>
        protected EosPeer(string v, int socketBufferSize) {
            if (socketBufferSize < MinSocketBufferSize)
                throw new ArgumentOutOfRangeException(nameof(socketBufferSize), $"The minimum socket buffer size is {MinSocketBufferSize}!");

            this.socketBufferSize = socketBufferSize;
            receivedData = new byte[Message.MaxSize];
        }

        protected struct PacketKey {
            public ProductUserId productUserId;
            public byte channel;
        }
        /// <inheritdoc cref="IPeer.Poll"/>
        public void Poll() {
            for (int chNum = 0; chNum < channels.Length; chNum++) {
                Receive(out _, out _, out _, (byte)chNum);
            }
        }
        public static string currentListenAddress;
        /// <summary>Opens the socket and starts the transport.</summary>
        /// <param name="listenAddress">The IP address to bind the socket to, if any.</param>
        /// <param name="port">The port to bind the socket to.</param>
        protected void OpenSocket(string listenAddress = null, ushort port = 0) {
            if (isRunning)
                CloseSocket();
            CreateLobbyOptions lobby = new();
            lobby.LobbyId = listenAddress;
            lobby.DisableHostMigration = true;
            currentListenAddress = listenAddress;
            EOSSDK.PlatformInterface.GetLobbyInterface().CreateLobby(ref lobby, null, null);

            isRunning = true;
        }

        /// <summary>Closes the socket and stops the transport.</summary>
        protected void CloseSocket() {
            if (!isRunning)
                return;

            isRunning = false;

            DestroyLobbyOptions lobby = new();
            lobby.LocalUserId = EOSSDK.LocalUserProductId;
            lobby.LobbyId = currentListenAddress;
            EOSSDK.PlatformInterface.GetLobbyInterface().DestroyLobby(ref lobby, null, null);
        }

        /// <summary>Polls the socket and checks if any data was received.</summary>
        private bool Receive(out ProductUserId clientProductUserId, out SocketId socketId, out ArraySegment<byte> receiveBuffer, byte channel) {
            var receivePacketOptions = new ReceivePacketOptions() {
                LocalUserId = EOSSDK.LocalUserProductId,
                MaxDataSizeBytes = P2PInterface.MaxPacketSize,
                RequestedChannel = channel
            };

            Helper.Get(System.IntPtr.Zero, out clientProductUserId);
            var outSocketIdInternal = Helper.GetDefault<SocketIdInternal>();
            Helper.Get(ref outSocketIdInternal, out socketId);

            /* var getNextReceivedPacketSizeOptions = new GetNextReceivedPacketSizeOptions() {
		        LocalUserId = EOSSDKComponent.LocalUserProductId,
		        RequestedChannel = channel
	        };
            Result getPacketSizeResult = p2pInterface
                .GetNextReceivedPacketSize(ref getNextReceivedPacketSizeOptions, out var packetSize);
            
            if (getPacketSizeResult != Result.Success || packetSize == 0)
            {
                receiveBuffer = null;
                clientProductUserId = null;
                socketId = default;
                return false;
            } */
            uint bytesWritten = 0;
            ArraySegment<byte> outData = new();
            Result result = EOSSDK.PlatformInterface.GetP2PInterface().ReceivePacket(
                ref receivePacketOptions,
                ref clientProductUserId,
                ref socketId,
                out channel,
                outData,
                out bytesWritten);

            receiveBuffer = outData[..(int)bytesWritten];

            if (result == Result.Success && receiveBuffer.Count > 0) {
                return true;
            }

            receiveBuffer = null;
            clientProductUserId = null;
            return false;
        }

        public static PacketReliability[] channels = { PacketReliability.ReliableOrdered, PacketReliability.UnreliableUnordered };

        /// <summary>Sends data to a given endpoint.</summary>
        /// <param name="dataBuffer">The array containing the data.</param>
        /// <param name="numBytes">The number of bytes in the array which should be sent.</param>
        /// <param name="toEndPoint">The endpoint to send the data to.</param>
        internal void Send(ProductUserId host, SocketId socketId, byte[] msgBuffer, byte channel) {
            var options = new SendPacketOptions() {
                AllowDelayedDelivery = true,
                Channel = channel,
                Data = msgBuffer,
                LocalUserId = EOSSDK.LocalUserProductId,
                Reliability = channels[channel],
                RemoteUserId = host,
                SocketId = socketId
            };

            Result result = EOSSDK.PlatformInterface.GetP2PInterface().SendPacket(ref options);

            if (result != Result.Success) {
                Debug.LogError("Send failed " + result);
            }
        }

        /// <summary>Handles received data.</summary>
        /// <param name="dataBuffer">A byte array containing the received data.</param>
        /// <param name="amount">The number of bytes in <paramref name="dataBuffer"/> used by the received data.</param>
        /// <param name="fromEndPoint">The endpoint from which the data was received.</param>
        protected abstract void OnDataReceived(byte[] dataBuffer, int amount, IPEndPoint fromEndPoint);

        /// <summary>Invokes the <see cref="Disconnected"/> event.</summary>
        /// <param name="connection">The closed connection.</param>
        /// <param name="reason">The reason for the disconnection.</param>
        protected virtual void OnDisconnected(Connection connection, DisconnectReason reason) {
            Disconnected?.Invoke(this, new DisconnectedEventArgs(connection, reason));
        }
    }
}
