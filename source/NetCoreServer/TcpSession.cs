using System;
using System.Net.Sockets;
using System.Threading;

namespace NetCoreServer
{
	/// <summary>
	/// TCP session is used to read and write data from the connected TCP client
	/// </summary>
	/// <remarks>Thread-safe</remarks>
	public class TcpSession : BaseSession, IDisposable
	{
		/// <summary>
		/// Initialize the session with a given server
		/// </summary>
		/// <param name="server">TCP server</param>
		public TcpSession() : base()
		{

		}

		/// <summary>
		/// Server
		/// </summary>
		public TcpServer Server { get { return (TcpServer)ServerRef; } }

		#region Connect/Disconnect session

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

			// Setup event args
			_receiveEventArg = new SocketAsyncEventArgs();
			_receiveEventArg.Completed += OnAsyncCompleted;
			_sendEventArg = new SocketAsyncEventArgs();
			_sendEventArg.Completed += OnAsyncCompleted;

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
			Logger.Debug("Client[{CLIENT_SESSION_ID}]:: Connecting.", Id);

			// Call the session connecting handler in the server
			Server.OnConnectingInternal(this);

			// Update the connected flag
			IsConnected = true;

			// Try to receive something from the client
			TryReceive();

			// Check the socket disposed state: in some rare cases it might be disconnected while receiving!
			if (IsSocketDisposed)
				return;

			// Call the session connected handler
			OnConnected();
			Logger.Debug("Client[{CLIENT_SESSION_ID}]:: Connected.", Id);

			// Call the session connected handler in the server
			Server.OnConnectedInternal(this);

			// Call the empty send buffer handler
			if (_sendBufferMain.IsEmpty)
				OnEmpty();
		}

		/// <summary>
		/// Disconnect the session
		/// </summary>
		/// <returns>'true' if the section was successfully disconnected, 'false' if the section is already disconnected</returns>
		public override bool Disconnect()
		{
			if (!IsConnected)
				return false;

			// Reset event args
			_receiveEventArg.Completed -= OnAsyncCompleted;
			_sendEventArg.Completed -= OnAsyncCompleted;

			// Call the session disconnecting handler
			OnDisconnecting();
			Logger.Debug("Client[{CLIENT_SESSION_ID}]:: Disconnecting.", Id);

			// Call the session disconnecting handler in the server
			Server.OnDisconnectingInternal(this);

			try
			{
				try
				{
					// Shutdown the socket associated with the client
					Socket.Shutdown(SocketShutdown.Both);
				}
				catch (SocketException) { }

				// Close the session socket
				Socket.Close();

				// Dispose the session socket
				Socket.Dispose();

				// Dispose event arguments
				_receiveEventArg.Dispose();
				_sendEventArg.Dispose();

				// Update the session socket disposed flag
				IsSocketDisposed = true;
			}
			catch (ObjectDisposedException) { }

			// Update the connected flag
			IsConnected = false;

			// Update sending/receiving flags
			_receiving = false;
			_sending = false;

			// Clear send/receive buffers
			ClearBuffers();

			// Call the session disconnected handler
			OnDisconnected();

			// Call the session disconnected handler in the server
			Server.OnDisconnectedInternal(this);
			Logger.Debug("Client[{CLIENT_SESSION_ID}]:: Disconnected.", Id);

			// Unregister session
			Server.UnregisterSession(Id);

			return true;
		}

		#endregion

		#region Send/Recieve data

		// Receive buffer
		private SocketAsyncEventArgs _receiveEventArg;

		/// <summary>
		/// Send data to the client (synchronous)
		/// </summary>
		/// <param name="buffer">Buffer to send as a span of bytes</param>
		/// <returns>Size of sent data</returns>
		public override long Send(ReadOnlySpan<byte> buffer)
		{
			if (!IsConnected)
				return 0;

			if (buffer.IsEmpty)
				return 0;

			// Sent data to the client
			long sent = Socket.Send(buffer, SocketFlags.None, out SocketError ec);
			if (sent > 0)
			{
				// Update statistic
				BytesSent += sent;
				Interlocked.Add(ref Server._bytesSent, sent);

				// Call the buffer sent handler
				OnSent(sent, BytesPending + BytesSending);
			}

			// Check for socket error
			if (ec != SocketError.Success)
			{
				SendError(ec);
				Disconnect();
			}

			return sent;
		}
		/// <summary>
		/// Send data to the client (asynchronous)
		/// </summary>
		/// <param name="buffer">Buffer to send as a span of bytes</param>
		/// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
		public override bool SendAsync(ReadOnlySpan<byte> buffer)
		{
			if (!IsConnected)
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
			if (!IsConnected)
				return 0;

			if (size == 0)
				return 0;

			// Receive data from the client
			long received = Socket.Receive(buffer, (int)offset, (int)size, SocketFlags.None, out SocketError ec);
			if (received > 0)
			{
				// Update statistic
				BytesReceived += received;
				Interlocked.Add(ref Server._bytesReceived, received);

				// Call the buffer received handler
				OnReceived(buffer, 0, received);
			}

			// Check for socket error
			if (ec != SocketError.Success)
			{
				SendError(ec);
				Disconnect();
			}

			return received;
		}

		/// <summary>
		/// Try to receive new data
		/// </summary>
		protected override void TryReceive()
		{
			if (_receiving)
				return;

			if (!IsConnected)
				return;

			bool process = true;

			while (process)
			{
				process = false;

				try
				{
					// Async receive with the receive handler
					_receiving = true;
					_receiveEventArg.SetBuffer(_receiveBuffer.Data, 0, (int)_receiveBuffer.Capacity);
					if (!Socket.ReceiveAsync(_receiveEventArg))
						process = ProcessReceive(_receiveEventArg);
				}
				catch (ObjectDisposedException) { }
			}
		}

		/// <summary>
		/// Try to send pending data
		/// </summary>
		private void TrySend()
		{
			if (!IsConnected)
				return;

			bool empty = false;
			bool process = true;

			while (process)
			{
				process = false;

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
					_sendEventArg.SetBuffer(_sendBufferFlush.Data, (int)_sendBufferFlushOffset, (int)(_sendBufferFlush.Size - _sendBufferFlushOffset));
					if (!Socket.SendAsync(_sendEventArg))
						process = ProcessSend(_sendEventArg);
				}
				catch (ObjectDisposedException) { }
			}
		}

		#endregion

		#region IO processing

		/// <summary>
		/// This method is called whenever a receive or send operation is completed on a socket
		/// </summary>
		private void OnAsyncCompleted(object sender, SocketAsyncEventArgs e)
		{
			if (IsSocketDisposed)
				return;

			// Determine which type of operation just completed and call the associated handler
			switch (e.LastOperation)
			{
				case SocketAsyncOperation.Receive:
					if (ProcessReceive(e))
						TryReceive();
					break;
				case SocketAsyncOperation.Send:
					if (ProcessSend(e))
						TrySend();
					break;
				default:
					throw new ArgumentException("The last operation completed on the socket was not a receive or send");
			}

		}

		/// <summary>
		/// This method is invoked when an asynchronous receive operation completes
		/// </summary>
		private bool ProcessReceive(SocketAsyncEventArgs e)
		{
			if (!IsConnected)
				return false;

			long size = e.BytesTransferred;

			// Received some data from the client
			if (size > 0)
			{
				// Update statistic
				BytesReceived += size;
				Interlocked.Add(ref Server._bytesReceived, size);

				// Call the buffer received handler
				OnReceived(_receiveBuffer.Data, 0, size);

				// If the receive buffer is full increase its size
				if (_receiveBuffer.Capacity == size)
				{
					// Check the receive buffer limit
					if (((2 * size) > OptionReceiveBufferLimit) && (OptionReceiveBufferLimit > 0))
					{
						SendError(SocketError.NoBufferSpaceAvailable);
						Disconnect();
						return false;
					}

					_receiveBuffer.Reserve(2 * size);
				}
			}

			_receiving = false;

			// Try to receive again if the session is valid
			if (e.SocketError == SocketError.Success)
			{
				// If zero is returned from a read operation, the remote end has closed the connection
				if (size > 0)
					return true;
				else
					Disconnect();
			}
			else
			{
				SendError(e.SocketError);
				Disconnect();
			}

			return false;
		}

		/// <summary>
		/// This method is invoked when an asynchronous send operation completes
		/// </summary>
		private bool ProcessSend(SocketAsyncEventArgs e)
		{
			if (!IsConnected)
				return false;

			long size = e.BytesTransferred;

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
			if (e.SocketError == SocketError.Success)
				return true;
			else
			{
				SendError(e.SocketError);
				Disconnect();
				return false;
			}
		}

		#endregion

		#region Session handlers

		#endregion

		#region Error handling


		#endregion
	}
}
