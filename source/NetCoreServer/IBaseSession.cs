using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetCoreServer
{
	/// <summary>
	/// Defines an interface for entities that receive packets, providing access to unique identification and various data
	/// buffers.
	/// </summary>
	public interface IPacketReceiver
	{
		/// <summary>
		/// Gets the unique identifier for the entity.
		/// </summary>
		Guid Id { get; }

		/// <summary>
		/// Gets the buffer containing received data.
		/// </summary>
		Buffer ReceivedBuffer { get; }

		/// <summary>
		/// Gets the intermediate buffer used for temporary data storage.
		/// </summary>
		Buffer IntermediateBuffer { get; }

		/// <summary>
		/// Gets the buffer used for processing operations.
		/// </summary>
		Buffer ProcessBuffer { get; }

		/// <summary>
		/// Gets a reference to an integer indicating whether the parser is active.
		/// </summary>
		ref int ParserActive { get; }
	}

	public interface IBaseSession : IPacketReceiver, IDisposable
	{
		IBaseServer ServerRef { get; }

		/// <summary>
		/// Session Id
		/// </summary>
		Guid Id { get; }

		/// <summary>
		/// Socket
		/// </summary>
		Socket Socket { get; }

		/// <summary>
		/// Number of bytes pending sent by the session
		/// </summary>
		long BytesPending { get; }
		/// <summary>
		/// Number of bytes sending by the session
		/// </summary>
		long BytesSending { get; }
		/// <summary>
		/// Number of bytes sent by the session
		/// </summary>
		long BytesSent { get; }
		/// <summary>
		/// Number of bytes received by the session
		/// </summary>
		long BytesReceived { get; }

		/// <summary>
		/// Option: receive buffer limit
		/// </summary>
		int OptionReceiveBufferLimit { get; }
		/// <summary>
		/// Option: receive buffer size
		/// </summary>
		int OptionReceiveBufferSize { get; }
		/// <summary>
		/// Option: send buffer limit
		/// </summary>
		int OptionSendBufferLimit { get; }
		/// <summary>
		/// Option: send buffer size
		/// </summary>
		int OptionSendBufferSize { get; }

		bool IsConnected { get; }

		#region Connect/Disconnect session
		void Connect(Socket socket);
		bool Disconnect(string a_marker);
		Task<bool> DisconnectAsync(string a_marker);
		#endregion

		#region Send/Recieve data
		long Send(byte[] buffer);
		bool SendAsync(ReadOnlySpan<byte> buffer);
		long Send(byte[] buffer, long offset, long size);
		long Send(ReadOnlySpan<byte> buffer);
		long Send(string text);
		long Send(ReadOnlySpan<char> text);
		bool SendAsync(byte[] buffer);
		bool SendAsync(byte[] buffer, long offset, long size);
		bool SendAsync(string text);
		bool SendAsync(ReadOnlySpan<char> text);
		long Receive(byte[] buffer);
		long Receive(byte[] buffer, long offset, long size);
		string Receive(long size);
		#endregion

		#region Session handlers
		public event Action<IBaseSession, byte[], long, long> Event_OnReceived;
		public event Action<IBaseSession, long, long> Event_OnSent;
		#endregion
	}
}