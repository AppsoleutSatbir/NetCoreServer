using AC.NetCoreServer.Logging;
using System;
using System.Net.Sockets;
using System.Text;

namespace NetCoreServer
{
	public class BaseSession : IBaseSession, IDisposable
	{
		public IBaseServer ServerRef { get; private set; }

		protected ILogger Logger { get; private set; }

		/// <summary>
		/// Session Id
		/// </summary>
		public Guid Id { get; }

		/// <summary>
		/// Socket
		/// </summary>
		public Socket Socket { get; protected set; }

		/// <summary>
		/// Number of bytes pending sent by the session
		/// </summary>
		public long BytesPending { get; protected set; }
		/// <summary>
		/// Number of bytes sending by the session
		/// </summary>
		public long BytesSending { get; protected set; }
		/// <summary>
		/// Number of bytes sent by the session
		/// </summary>
		public long BytesSent { get; protected set; }
		/// <summary>
		/// Number of bytes received by the session
		/// </summary>
		public long BytesReceived { get; protected set; }

		/// <summary>
		/// Option: receive buffer limit
		/// </summary>
		public int OptionReceiveBufferLimit { get; protected set; } = 0;
		/// <summary>
		/// Option: receive buffer size
		/// </summary>
		public int OptionReceiveBufferSize { get; protected set; } = 8192;
		/// <summary>
		/// Option: send buffer limit
		/// </summary>
		public int OptionSendBufferLimit { get; protected set; } = 0;
		/// <summary>
		/// Option: send buffer size
		/// </summary>
		public int OptionSendBufferSize { get; protected set; } = 8192;

		internal BaseSession()
		{
			Id = Guid.NewGuid();
		}

		internal void Initialize(IBaseServer a_serverRef, ILogger a_logger)
		{
			ServerRef = a_serverRef;
			Logger = a_logger;
			OptionReceiveBufferSize = a_serverRef.OptionReceiveBufferSize;
			OptionSendBufferSize = a_serverRef.OptionSendBufferSize;
		}

		#region Connect/Disconnect session

		/// <summary>
		/// Is the session connected?
		/// </summary>
		public bool IsConnected { get; protected set; }

		public virtual void Connect(Socket socket)
		{

		}
		public virtual bool Disconnect()
		{
			return false;
		}
		#endregion


		#region Send/Recieve data
		// Receive buffer
		protected bool _receiving;
		protected Buffer _receiveBuffer;

		// Send buffer
		protected readonly object _sendLock = new object();
		protected bool _sending;
		protected Buffer _sendBufferMain;
		protected Buffer _sendBufferFlush;
		protected SocketAsyncEventArgs _sendEventArg;
		protected long _sendBufferFlushOffset;

		/// <summary>
		/// Send data to the client (synchronous)
		/// </summary>
		/// <param name="buffer">Buffer to send</param>
		/// <returns>Size of sent data</returns>
		public virtual long Send(byte[] buffer) => Send(buffer.AsSpan());

		public virtual bool SendAsync(ReadOnlySpan<byte> buffer)
		{
			return false;
		}

		/// <summary>
		/// Send data to the client (synchronous)
		/// </summary>
		/// <param name="buffer">Buffer to send</param>
		/// <param name="offset">Buffer offset</param>
		/// <param name="size">Buffer size</param>
		/// <returns>Size of sent data</returns>
		public virtual long Send(byte[] buffer, long offset, long size) => Send(buffer.AsSpan((int)offset, (int)size));

		public virtual long Send(ReadOnlySpan<byte> buffer)
		{
			return -1;
		}

		/// <summary>
		/// Send text to the client (synchronous)
		/// </summary>
		/// <param name="text">Text string to send</param>
		/// <returns>Size of sent data</returns>
		public virtual long Send(string text) => Send(Encoding.UTF8.GetBytes(text));

		/// <summary>
		/// Send text to the client (synchronous)
		/// </summary>
		/// <param name="text">Text to send as a span of characters</param>
		/// <returns>Size of sent data</returns>
		public virtual long Send(ReadOnlySpan<char> text) => Send(Encoding.UTF8.GetBytes(text.ToArray()));

		/// <summary>
		/// Send data to the client (asynchronous)
		/// </summary>
		/// <param name="buffer">Buffer to send</param>
		/// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
		public virtual bool SendAsync(byte[] buffer) => SendAsync(buffer.AsSpan());

		/// <summary>
		/// Send data to the client (asynchronous)
		/// </summary>
		/// <param name="buffer">Buffer to send</param>
		/// <param name="offset">Buffer offset</param>
		/// <param name="size">Buffer size</param>
		/// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
		public virtual bool SendAsync(byte[] buffer, long offset, long size) => SendAsync(buffer.AsSpan((int)offset, (int)size));


		/// <summary>
		/// Send text to the client (asynchronous)
		/// </summary>
		/// <param name="text">Text string to send</param>
		/// <returns>'true' if the text was successfully sent, 'false' if the session is not connected</returns>
		public virtual bool SendAsync(string text) => SendAsync(Encoding.UTF8.GetBytes(text));

		/// <summary>
		/// Send text to the client (asynchronous)
		/// </summary>
		/// <param name="text">Text to send as a span of characters</param>
		/// <returns>'true' if the text was successfully sent, 'false' if the session is not connected</returns>
		public virtual bool SendAsync(ReadOnlySpan<char> text) => SendAsync(Encoding.UTF8.GetBytes(text.ToArray()));

		/// <summary>
		/// Receive data from the client (synchronous)
		/// </summary>
		/// <param name="buffer">Buffer to receive</param>
		/// <returns>Size of received data</returns>
		public virtual long Receive(byte[] buffer) { return Receive(buffer, 0, buffer.Length); }


		/// <summary>
		/// Receive data from the client (synchronous)
		/// </summary>
		/// <param name="buffer">Buffer to receive</param>
		/// <param name="offset">Buffer offset</param>
		/// <param name="size">Buffer size</param>
		/// <returns>Size of received data</returns>
		public virtual long Receive(byte[] buffer, long offset, long size)
		{
			return -1;
		}

		/// <summary>
		/// Receive text from the client (synchronous)
		/// </summary>
		/// <param name="size">Text size to receive</param>
		/// <returns>Received text</returns>
		public virtual string Receive(long size)
		{
			var buffer = new byte[size];
			var length = Receive(buffer);
			return Encoding.UTF8.GetString(buffer, 0, (int)length);
		}

		/// <summary>
		/// Receive data from the client (asynchronous)
		/// </summary>
		public virtual void ReceiveAsync()
		{
			// Try to receive data from the client
			TryReceive();
		}
		protected virtual void TryReceive()
		{
		}

		/// <summary>
		/// Clear send/receive buffers
		/// </summary>
		protected void ClearBuffers()
		{
			lock (_sendLock)
			{
				// Clear send buffers
				_sendBufferMain.Clear();
				_sendBufferFlush.Clear();
				_sendBufferFlushOffset = 0;

				// Update statistic
				BytesPending = 0;
				BytesSending = 0;
			}
		}
		#endregion

		#region Session handlers
		public event Action<IBaseSession, byte[], long, long> Event_OnReceived;
		public event Action<IBaseSession, long, long> Event_OnSent;

		/// <summary>
		/// Handle client connecting notification
		/// </summary>
		protected virtual void OnConnecting() { }
		/// <summary>
		/// Handle client connected notification
		/// </summary>
		protected virtual void OnConnected() { }
		/// <summary>
		/// Handle client disconnecting notification
		/// </summary>
		protected virtual void OnDisconnecting() { }
		/// <summary>
		/// Handle client disconnected notification
		/// </summary>
		protected virtual void OnDisconnected() { }

		/// <summary>
		/// Handle buffer received notification
		/// </summary>
		/// <param name="buffer">Received buffer</param>
		/// <param name="offset">Received buffer offset</param>
		/// <param name="size">Received buffer size</param>
		/// <remarks>
		/// Notification is called when another chunk of buffer was received from the client
		/// </remarks>
		protected virtual void OnReceived(byte[] buffer, long offset, long size)
		{
			if (ServerRef.IsLogging) Logger.Verbose("Client[{CLIENT_SESSION_ID}]::OnReceived:: Offset: {OFFSET}, Size: {SIZE}", Id, offset, size);
			Event_OnReceived?.Invoke(this, buffer, offset, size);
		}
		/// <summary>
		/// Handle buffer sent notification
		/// </summary>
		/// <param name="sent">Size of sent buffer</param>
		/// <param name="pending">Size of pending buffer</param>
		/// <remarks>
		/// Notification is called when another chunk of buffer was sent to the client.
		/// This handler could be used to send another buffer to the client for instance when the pending size is zero.
		/// </remarks>
		protected virtual void OnSent(long sent, long pending)
		{
			if (ServerRef.IsLogging) Logger.Verbose("Client[{CLIENT_SESSION_ID}]::OnSent:: Sent: {sent}, Pending: {pending}", Id, sent, pending);
			Event_OnSent?.Invoke(this, sent, pending);
		}

		/// <summary>
		/// Handle empty send buffer notification
		/// </summary>
		/// <remarks>
		/// Notification is called when the send buffer is empty and ready for a new data to send.
		/// This handler could be used to send another buffer to the client.
		/// </remarks>
		protected virtual void OnEmpty() { }

		/// <summary>
		/// Handle error notification
		/// </summary>
		/// <param name="error">Socket error code</param>
		protected virtual void OnError(SocketError error, Exception a_ex) { }
		#endregion

		#region Error handling

		/// <summary>
		/// Send error notification
		/// </summary>
		/// <param name="error">Socket error code</param>
		protected void SendError(SocketError error, Exception a_ex = null)
		{
			// Skip disconnect errors
			if ((error == SocketError.ConnectionAborted) ||
				(error == SocketError.ConnectionRefused) ||
				(error == SocketError.ConnectionReset) ||
				(error == SocketError.OperationAborted) ||
				(error == SocketError.Shutdown))
				return;

			if (a_ex != null)
				Logger.Error(a_ex);

			OnError(error, a_ex);
		}

		#endregion

		#region IDisposable implementation

		/// <summary>
		/// Disposed flag
		/// </summary>
		public bool IsDisposed { get; protected set; }

		/// <summary>
		/// Session socket disposed flag
		/// </summary>
		public bool IsSocketDisposed { get; protected set; } = true;

		// Implement IDisposable.
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposingManagedResources)
		{
			// The idea here is that Dispose(Boolean) knows whether it is
			// being called to do explicit cleanup (the Boolean is true)
			// versus being called due to a garbage collection (the Boolean
			// is false). This distinction is useful because, when being
			// disposed explicitly, the Dispose(Boolean) method can safely
			// execute code using reference type fields that refer to other
			// objects knowing for sure that these other objects have not been
			// finalized or disposed of yet. When the Boolean is false,
			// the Dispose(Boolean) method should not execute code that
			// refer to reference type fields because those objects may
			// have already been finalized."

			if (!IsDisposed)
			{
				if (disposingManagedResources)
				{
					// Dispose managed resources here...
					Disconnect();
					Event_OnSent = null;
					Event_OnReceived = null;
					ServerRef = null;
					Logger = null;
				}

				// Dispose unmanaged resources here...

				// Set large fields to null here...

				// Mark as disposed.
				IsDisposed = true;
			}
		}

		#endregion
	}
}
