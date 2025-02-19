using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetCoreServer
{
	/// <summary>
	/// SSL session is used to read and write data from the connected SSL client
	/// </summary>
	/// <remarks>Thread-safe</remarks>
	public class SslSession : BaseSession, IDisposable
	{
		/// <summary>
		/// Initialize the session with a given server
		/// </summary>
		/// <param name="server">SSL server</param>
		public SslSession() : base()
		{

		}

		/// <summary>
		/// Server
		/// </summary>
		public SslServer Server { get { return (SslServer)ServerRef; } }

		#region Connect/Disconnect session

		private volatile bool _disconnecting;
		private SslStream _sslStream;
		private Guid? _sslStreamId;

		/// <summary>
		/// Is the session handshaked?
		/// </summary>
		public bool IsHandshaked { get; private set; }

		/// <summary>
		/// Connect the session
		/// </summary>
		/// <param name="socket">Session socket</param>
		public override void Connect(Socket socket)
		{
			Socket = socket;

			// Update the session socket disposed flag
			IsSocketDisposed = false;

			// Setup buffers
			_receiveBuffer = new Buffer();
			_sendBufferMain = new Buffer();
			_sendBufferFlush = new Buffer();

			// Apply the option: keep alive
			if (Server.OptionKeepAlive)
				Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
			if (Server.OptionTcpKeepAliveTime >= 0)
				Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, Server.OptionTcpKeepAliveTime);
			if (Server.OptionTcpKeepAliveInterval >= 0)
				Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, Server.OptionTcpKeepAliveInterval);
			if (Server.OptionTcpKeepAliveRetryCount >= 0)
				Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, Server.OptionTcpKeepAliveRetryCount);
			// Apply the option: no delay
			if (Server.OptionNoDelay)
				Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

			// Prepare receive & send buffers
			_receiveBuffer.Reserve(OptionReceiveBufferSize);
			_sendBufferMain.Reserve(OptionSendBufferSize);
			_sendBufferFlush.Reserve(OptionSendBufferSize);

			// Reset statistic
			BytesPending = 0;
			BytesSending = 0;
			BytesSent = 0;
			BytesReceived = 0;

			// Call the session connecting handler
			OnConnecting();
			Logger.Debug("Client[{CLIENT_IP}]::[{CLIENT_SESSION_ID}]:: Connecting.", socket.RemoteEndPoint.ToString(), Id);

			// Call the session connecting handler in the server
			Server.OnConnectingInternal(this);

			// Update the connected flag
			IsConnected = true;

			// Call the session connected handler
			OnConnected();

			Logger.Debug("Client[{CLIENT_SESSION_ID}]:: Connected.", Id);

			// Call the session connected handler in the server
			Server.OnConnectedInternal(this);

			try
			{
				// Create SSL stream
				_sslStreamId = Guid.NewGuid();
				_sslStream = (Server.Context.CertificateValidationCallback != null) ? new SslStream(new NetworkStream(Socket, false), false, Server.Context.CertificateValidationCallback) : new SslStream(new NetworkStream(Socket, false), false);

				// Call the session handshaking handler
				OnHandshaking();
				Logger.Debug("Client[{CLIENT_SESSION_ID}]:: Handshaking.", Id);

				// Call the session handshaking handler in the server
				Server.OnHandshakingInternal(this);

				// Begin the SSL handshake
				_sslStream.BeginAuthenticateAsServer(Server.Context.Certificate, Server.Context.ClientCertificateRequired, Server.Context.Protocols, false, ProcessHandshake, _sslStreamId);
			}
			catch (Exception a_ex)
			{
				if (Logger == null)
				{
					FileUtilities.Write($"SSLSession::Connect:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]::Logger is null with exception {a_ex.Message}\n{a_ex}");
				}
				else
				{
					Logger.Error($"SSLSession::Connect:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
				}
				SendError(SocketError.NotConnected, a_ex);
				Disconnect("SSLSession::Connect");
			}
		}

		/// <summary>
		/// Disconnect the session
		/// </summary>
		/// <returns>'true' if the section was successfully disconnected, 'false' if the section is already disconnected</returns>
		public override bool Disconnect(string a_marker)
		{
			Logger.Information("SSlSession:Disconnect:[{SessionId}]::Marker:{Marker}", (_sslStreamId == null ? "null" : _sslStreamId.ToString()), a_marker);
			if (!IsConnected)
				return false;

			if (_disconnecting)
				return false;

			// Update the disconnecting flag
			_disconnecting = true;

			try
			{
				// Call the session disconnecting handler
				OnDisconnecting();
				Logger.Debug("Client[{CLIENT_SESSION_ID}]:: Disconnecting.", Id);

				// Call the session disconnecting handler in the server
				Server.OnDisconnectingInternal(this);

				try
				{
					// Shutdown the SSL stream
					_sslStream.ShutdownAsync().Wait();
				}
				catch (Exception a_ex)
				{
					if (Logger == null)
					{
						FileUtilities.Write($"SSLSession::Disconnect1:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]::Logger is null with exception {a_ex.Message}\n{a_ex}");
					}
					else
					{
						Logger.Error($"SSLSession::Disconnect1:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
					}
				}

				// Dispose the SSL stream & buffer
				_sslStream.Dispose();
				_sslStreamId = null;

				try
				{
					// Shutdown the socket associated with the client
					Socket.Shutdown(SocketShutdown.Both);
				}
				catch (SocketException a_ex)
				{
					if (Logger == null)
					{
						FileUtilities.Write($"SSLSession::Disconnect2:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]::Logger is null with exception {a_ex.Message}\n{a_ex}");
					}
					else
					{
						Logger.Error($"SSLSession::Disconnect2:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
					}
				}

				// Close the session socket
				Socket.Close();

				// Dispose the session socket
				Socket.Dispose();

				// Update the session socket disposed flag
				IsSocketDisposed = true;
			}
			catch (ObjectDisposedException a_ex)
			{
				if (Logger == null)
				{
					FileUtilities.Write($"SSLSession::Disconnect3:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]::Logger is null with exception {a_ex.Message}\n{a_ex}");
				}
				else
				{
					Logger.Error($"SSLSession::Disconnect3:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
				}
			}
			catch (Exception a_ex)
			{
				if (Logger == null)
				{
					FileUtilities.Write($"SSLSession::Disconnect4:[ {(_sslStreamId == null ? "null" : _sslStreamId.ToString())}::Logger is null with exception {a_ex.Message}\n{a_ex}");
				}
				else
				{
					Logger.Error($"SSLSession::Disconnect4:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
				}
			}

			// Update the handshaked flag
			IsHandshaked = false;

			// Update the connected flag
			IsConnected = false;

			// Update sending/receiving flags
			_receiving = false;
			_sending = false;

			// Clear send/receive buffers
			ClearBuffers();

			try
			{
				// Call the session disconnected handler
				OnDisconnected();
			}
			catch (Exception a_ex)
			{
				if (Logger == null)
				{
					FileUtilities.Write($"SSLSession::Disconnect5:[ {(_sslStreamId == null ? "null" : _sslStreamId.ToString())}::Logger is null with exception {a_ex.Message}\n{a_ex}");
				}
				else
				{
					Logger.Error($"SSLSession::Disconnect5:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
				}
			}

			try
			{
				Logger.Debug("Client[{CLIENT_SESSION_ID}]:: Disconnected.", Id);

				// Unregister session
				Server.UnregisterSession(Id);
			}
			catch (Exception a_ex)
			{
				if (Logger == null)
				{
					FileUtilities.Write($"SSLSession::Disconnect6:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]::Logger is null with exception {a_ex.Message}\n{a_ex}");
				}
				else
				{
					Logger.Error($"SSLSession::Disconnect6:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
				}
			}

			try
			{
				// Call the session disconnected handler in the server
				Server.OnDisconnectedInternal(this);
			}
			catch (Exception a_ex)
			{
				if (Logger == null)
				{
					FileUtilities.Write($"SSLSession::Disconnect7:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]::Logger is null with exception {a_ex.Message}\n{a_ex}");
				}
				else
				{
					Logger.Error($"SSLSession::Disconnect7:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
				}
			}

			// Reset the disconnecting flag
			_disconnecting = false;

			return true;
		}

		/// <summary>
		/// Disconnect the session
		/// </summary>
		/// <returns>'true' if the section was successfully disconnected, 'false' if the section is already disconnected</returns>
		public override async Task<bool> DisconnectAsync(string a_marker)
		{
			Logger.Information("SSlSession:DisconnectAsync:[{SessionId}]::Marker:{Marker}", (_sslStreamId == null ? "null" : _sslStreamId.ToString()), a_marker);
			if (!IsConnected)
				return false;

			if (_disconnecting)
				return false;

			// Update the disconnecting flag
			_disconnecting = true;

			// Call the session disconnecting handler
			OnDisconnecting();
			Logger.Debug("Client[{CLIENT_SESSION_ID}]::DisconnectingAsync.", Id);

			// Call the session disconnecting handler in the server
			Server.OnDisconnectingInternal(this);

			try
			{
				try
				{
					// Shutdown the SSL stream
					_sslStream.ShutdownAsync().Wait();
				}
				catch (Exception a_ex)
				{
					Logger.Error($"SSLSession::DisconnectAsync1:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
				}

				// Dispose the SSL stream & buffer
				_sslStream.Dispose();
				_sslStreamId = null;

				try
				{
					// Shutdown the socket associated with the client
					Socket.Shutdown(SocketShutdown.Both);
				}
				catch (SocketException a_ex)
				{
					Logger.Error($"SSLSession::DisconnectAsync2:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
				}

				// Close the session socket
				Socket.Close();

				// Dispose the session socket
				Socket.Dispose();

				// Update the session socket disposed flag
				IsSocketDisposed = true;
			}
			catch (ObjectDisposedException a_ex)
			{
				Logger.Error($"SSLSession::DisconnectAsync3:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
			}

			// Update the handshaked flag
			IsHandshaked = false;

			// Update the connected flag
			IsConnected = false;

			// Update sending/receiving flags
			_receiving = false;
			_sending = false;

			// Clear send/receive buffers
			ClearBuffers();

			// Call the session disconnected handler
			OnDisconnected();

			Logger.Debug("Client[{CLIENT_SESSION_ID}]:: DisconnectedAsync.", Id);

			// Unregister session
			Server.UnregisterSession(Id);

			// Call the session disconnected handler in the server
			await Server.OnDisconnectedInternalAsync(this);

			// Reset the disconnecting flag
			_disconnecting = false;

			return true;
		}

		#endregion

		#region Send/Receive data

		/// <summary>
		/// Send data to the client (synchronous)
		/// </summary>
		/// <param name="buffer">Buffer to send as a span of bytes</param>
		/// <returns>Size of sent data</returns>
		public override long Send(ReadOnlySpan<byte> buffer)
		{
			if (!IsHandshaked)
				return 0;

			if (buffer.IsEmpty)
				return 0;

			try
			{
				// Sent data to the server
				_sslStream.Write(buffer);

				long sent = buffer.Length;

				// Update statistic
				BytesSent += sent;
				Interlocked.Add(ref Server._bytesSent, sent);

				// Call the buffer sent handler
				OnSent(sent, BytesPending + BytesSending);

				return sent;
			}
			catch (Exception a_ex)
			{
				SendError(SocketError.OperationAborted, a_ex);
				//Satbir
				//Disconnect();
				return 0;
			}
		}

		/// <summary>
		/// Send data to the client (asynchronous)
		/// </summary>
		/// <param name="buffer">Buffer to send as a span of bytes</param>
		/// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
		public override bool SendAsync(ReadOnlySpan<byte> buffer)
		{
			if (!IsHandshaked)
				return false;

			if (buffer.IsEmpty)
				return true;

			lock (_sendLock)
			{
				// Check the send buffer limit
				if (((_sendBufferMain.Size + buffer.Length) > OptionSendBufferLimit) && (OptionSendBufferLimit > 0))
				{
					SendError(SocketError.NoBufferSpaceAvailable);
					return false;
				}

				// Fill the main send buffer
				_sendBufferMain.Append(buffer);

				// Update statistic
				BytesPending = _sendBufferMain.Size;

				// Avoid multiple send handlers
				if (_sending)
					return true;
				else
					_sending = true;

				// Try to send the main buffer
				TrySend();
			}

			return true;
		}

		/// <summary>
		/// Receive data from the client (synchronous)
		/// </summary>
		/// <param name="buffer">Buffer to receive</param>
		/// <param name="offset">Buffer offset</param>
		/// <param name="size">Buffer size</param>
		/// <returns>Size of received data</returns>
		public override long Receive(byte[] buffer, long offset, long size)
		{
			if (!IsHandshaked)
				return 0;

			if (size == 0)
				return 0;

			try
			{
				// Receive data from the client
				long received = _sslStream.Read(buffer, (int)offset, (int)size);
				if (received > 0)
				{
					// Update statistic
					BytesReceived += received;
					Interlocked.Add(ref Server._bytesReceived, received);

					// Call the buffer received handler
					OnReceived(buffer, 0, received);
				}

				return received;
			}
			catch (Exception a_ex)
			{
				Logger.Error($"SSLSession::Receive:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
				SendError(SocketError.OperationAborted, a_ex);
				//Satbir:
				//Disconnect();
				return 0;
			}
		}

		/// <summary>
		/// Try to receive new data
		/// </summary>
		protected override void TryReceive()
		{
			if (_receiving)
				return;

			if (!IsHandshaked)
				return;

			try
			{
				// Async receive with the receive handler
				IAsyncResult result;
				do
				{
					if (!IsHandshaked)
						return;

					_receiving = true;
					result = _sslStream.BeginRead(_receiveBuffer.Data, 0, (int)_receiveBuffer.Capacity, ProcessReceive, _sslStreamId);
				} while (result.CompletedSynchronously);
			}
			catch (ObjectDisposedException a_ex) { Logger.Error($"SSLSession::TryReceive1:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex); }
			catch (Exception a_ex) { Logger.Error($"SSLSession::TryReceive2:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex); }
		}

		/// <summary>
		/// Try to send pending data
		/// </summary>
		private void TrySend()
		{
			if (!IsHandshaked)
				return;

			bool empty = false;

			lock (_sendLock)
			{
				// Is previous socket send in progress?
				if (_sendBufferFlush.IsEmpty)
				{
					// Swap flush and main buffers
					_sendBufferFlush = Interlocked.Exchange(ref _sendBufferMain, _sendBufferFlush);
					_sendBufferFlushOffset = 0;

					// Update statistic
					BytesPending = 0;
					BytesSending += _sendBufferFlush.Size;

					// Check if the flush buffer is empty
					if (_sendBufferFlush.IsEmpty)
					{
						// Need to call empty send buffer handler
						empty = true;

						// End sending process
						_sending = false;
					}
				}
				else
					return;
			}

			// Call the empty send buffer handler
			if (empty)
			{
				OnEmpty();
				return;
			}

			try
			{
				// Async write with the write handler
				_sslStream.BeginWrite(_sendBufferFlush.Data, (int)_sendBufferFlushOffset, (int)(_sendBufferFlush.Size - _sendBufferFlushOffset), ProcessSend, _sslStreamId);
			}
			catch (ObjectDisposedException a_ex) { Logger.Error($"SSLSession::TrySend:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex); }
		}

		#endregion

		#region IO processing

		/// <summary>
		/// This method is invoked when an asynchronous handshake operation completes
		/// </summary>
		private void ProcessHandshake(IAsyncResult result)
		{
			try
			{
				if (IsHandshaked)
					return;

				// Validate SSL stream Id
				var sslStreamId = result.AsyncState as Guid?;
				if (_sslStreamId != sslStreamId)
					return;

				// End the SSL handshake
				_sslStream.EndAuthenticateAsServer(result);

				// Update the handshaked flag
				IsHandshaked = true;

				// Try to receive something from the client
				TryReceive();

				// Check the socket disposed state: in some rare cases it might be disconnected while receiving!
				if (IsSocketDisposed)
					return;

				// Call the session handshaked handler
				OnHandshaked();
				Logger.Debug("Client[{CLIENT_SESSION_ID}]:: Handshake finished.", Id);

				// Call the session handshaked handler in the server
				Server.OnHandshakedInternal(this);

				// Call the empty send buffer handler
				if (_sendBufferMain.IsEmpty)
					OnEmpty();
			}
			catch (Exception a_ex)
			{
				Logger.Error($"SSLSession::ProcessHandshake:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
				SendError(SocketError.NotConnected, a_ex);
				Disconnect("SSLSession::ProcessHandshake");
			}
		}

		/// <summary>
		/// This method is invoked when an asynchronous receive operation completes
		/// </summary>
		private void ProcessReceive(IAsyncResult result)
		{
			try
			{
				if (!IsHandshaked)
					return;

				// Validate SSL stream Id
				var sslStreamId = result.AsyncState as Guid?;
				//if(Logger.IsEnabled(AC.NetCoreServer.Logging.ELogLevel.Verbose)) Logger.Information("SSLSession::ProcessReceive:: SllStreamId: " + sslStreamId);
				if (_sslStreamId != sslStreamId)
					return;

				// End the SSL read
				long size = _sslStream.EndRead(result);
				//if (Logger.IsEnabled(AC.NetCoreServer.Logging.ELogLevel.Verbose)) Logger.Information("SSLSession::ProcessReceive:{Size}", size);

				// Received some data from the client
				if (size > 0)
				{
					// Update statistic
					BytesReceived += size;
					Interlocked.Add(ref Server._bytesReceived, size);

					try
					{
						//if (Logger.IsEnabled(AC.NetCoreServer.Logging.ELogLevel.Verbose)) Logger.Information("SSLSession:ProcessReceive:\n----------\n{String}\n----------\n{Bytes}\n--------", Encoding.UTF8.GetString(_receiveBuffer.Data), string.Join(",", _receiveBuffer.Data));
						// Call the buffer received handler
						OnReceived(_receiveBuffer.Data, 0, size);
					}
					catch (Exception a_ex)
					{
						Logger.Error($"SSLSession::ProcessReceive1:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
					}

					// If the receive buffer is full increase its size
					if (_receiveBuffer.Capacity == size)
					{
						// Check the receive buffer limit
						if (((2 * size) > OptionReceiveBufferLimit) && (OptionReceiveBufferLimit > 0))
						{
							SendError(SocketError.NoBufferSpaceAvailable);
							Disconnect("SSLSession::ProcessReceive1");
							return;
						}

						_receiveBuffer.Reserve(2 * size);
					}
				}

				_receiving = false;

				// If zero is returned from a read operation, the remote end has closed the connection
				if (size > 0)
				{
					if (!result.CompletedSynchronously)
						TryReceive();
				}
				else
					Disconnect("SSLSession::ProcessReceive2");
			}
			catch (IOException a_ex)
			{
				if (Logger != null) Logger.Error($"SSLSession::ProcessReceive2:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
				try
				{
					Disconnect("SSLSession::ProcessReceive3");
				}
				catch (Exception) { }
			}
			catch (Exception a_ex)
			{
				if (Logger != null) Logger.Error($"SSLSession::ProcessReceive3:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
				try
				{
					SendError(SocketError.OperationAborted, a_ex);
				}
				catch (Exception) { }
				//Satbir
				//Disconnect();
			}
		}

		/// <summary>
		/// This method is invoked when an asynchronous send operation completes
		/// </summary>
		private void ProcessSend(IAsyncResult result)
		{
			try
			{
				// Validate SSL stream Id
				var sslStreamId = result.AsyncState as Guid?;
				if (_sslStreamId != sslStreamId)
					return;

				if (!IsHandshaked)
					return;

				// End the SSL write
				_sslStream.EndWrite(result);

				long size = _sendBufferFlush.Size;

				// Send some data to the client
				if (size > 0)
				{
					// Update statistic
					BytesSending -= size;
					BytesSent += size;
					Interlocked.Add(ref Server._bytesSent, size);

					// Increase the flush buffer offset
					_sendBufferFlushOffset += size;

					// Successfully send the whole flush buffer
					if (_sendBufferFlushOffset == _sendBufferFlush.Size)
					{
						// Clear the flush buffer
						_sendBufferFlush.Clear();
						_sendBufferFlushOffset = 0;
					}

					// Call the buffer sent handler
					OnSent(size, BytesPending + BytesSending);
				}

				// Try to send again if the session is valid
				TrySend();
			}
			catch (Exception a_ex)
			{
				Logger.Error($"SSLSession::ProcessSend:[{(_sslStreamId == null ? "null" : _sslStreamId.ToString())}]", a_ex);
				SendError(SocketError.OperationAborted, a_ex);
				//Satbir
				//Disconnect();
			}
		}

		#endregion

        #region Session handlers

        /// <summary>
        /// Handle client connecting notification
        /// </summary>
        protected virtual void OnConnecting() {}
        /// <summary>
        /// Handle client connected notification
        /// </summary>
        protected virtual void OnConnected() {}
        /// <summary>
        /// Handle client handshaking notification
        /// </summary>
        protected virtual void OnHandshaking() {}
        /// <summary>
        /// Handle client handshaked notification
        /// </summary>
        protected virtual void OnHandshaked() {}
        /// <summary>
        /// Handle client disconnecting notification
        /// </summary>
        protected virtual void OnDisconnecting() {}
        /// <summary>
        /// Handle client disconnected notification
        /// </summary>
        protected virtual void OnDisconnected() {}

        /// <summary>
        /// Handle buffer received notification
        /// </summary>
        /// <param name="buffer">Received buffer</param>
        /// <param name="offset">Received buffer offset</param>
        /// <param name="size">Received buffer size</param>
        /// <remarks>
        /// Notification is called when another part of buffer was received from the client
        /// </remarks>
        protected virtual void OnReceived(byte[] buffer, long offset, long size) {}
        /// <summary>
        /// Handle buffer sent notification
        /// </summary>
        /// <param name="sent">Size of sent buffer</param>
        /// <param name="pending">Size of pending buffer</param>
        /// <remarks>
        /// Notification is called when another part of buffer was sent to the client.
        /// This handler could be used to send another buffer to the client for instance when the pending size is zero.
        /// </remarks>
        protected virtual void OnSent(long sent, long pending) {}

        /// <summary>
        /// Handle empty send buffer notification
        /// </summary>
        /// <remarks>
        /// Notification is called when the send buffer is empty and ready for a new data to send.
        /// This handler could be used to send another buffer to the client.
        /// </remarks>
        protected virtual void OnEmpty() {}

        /// <summary>
        /// Handle error notification
        /// </summary>
        /// <param name="error">Socket error code</param>
        protected virtual void OnError(SocketError error) {}

		#endregion

		#region Error handling

		#endregion
	}
}
