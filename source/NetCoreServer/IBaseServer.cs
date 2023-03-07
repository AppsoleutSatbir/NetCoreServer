using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetCoreServer
{
	public interface IBaseServer : IDisposable
	{
		/// <summary>
		/// Server Id
		/// </summary>
		public Guid Id { get; }

		/// <summary>
		/// SSL server address
		/// </summary>
		public string Address { get; }
		/// <summary>
		/// SSL server port
		/// </summary>
		public int Port { get; }
		/// <summary>
		/// Endpoint
		/// </summary>
		public EndPoint Endpoint { get; }
		/// <summary>
		/// Number of sessions connected to the server
		/// </summary>
		public long ConnectedSessions { get; }
		/// <summary>
		/// Number of bytes pending sent by the server
		/// </summary>
		public long BytesPending { get; }
		/// <summary>
		/// Number of bytes sent by the server
		/// </summary>
		public long BytesSent { get; }
		/// <summary>
		/// Number of bytes received by the server
		/// </summary>
		public long BytesReceived { get; }

		/// <summary>
		/// Option: acceptor backlog size
		/// </summary>
		/// <remarks>
		/// This option will set the listening socket's backlog size
		/// </remarks>
		public int OptionAcceptorBacklog { get; set; }
		/// <summary>
		/// Option: dual mode socket
		/// </summary>
		/// <remarks>
		/// Specifies whether the Socket is a dual-mode socket used for both IPv4 and IPv6.
		/// Will work only if socket is bound on IPv6 address.
		/// </remarks>
		public bool OptionDualMode { get; set; }
		/// <summary>
		/// Option: keep alive
		/// </summary>
		/// <remarks>
		/// This option will setup SO_KEEPALIVE if the OS support this feature
		/// </remarks>
		public bool OptionKeepAlive { get; set; }
		/// <summary>
		/// Option: TCP keep alive time
		/// </summary>
		/// <remarks>
		/// The number of seconds a TCP connection will remain alive/idle before keepalive probes are sent to the remote
		/// </remarks>
		public int OptionTcpKeepAliveTime { get; set; }
		/// <summary>
		/// Option: TCP keep alive interval
		/// </summary>
		/// <remarks>
		/// The number of seconds a TCP connection will wait for a keepalive response before sending another keepalive probe
		/// </remarks>
		public int OptionTcpKeepAliveInterval { get; set; }
		/// <summary>
		/// Option: TCP keep alive retry count
		/// </summary>
		/// <remarks>
		/// The number of TCP keep alive probes that will be sent before the connection is terminated
		/// </remarks>
		public int OptionTcpKeepAliveRetryCount { get; set; }
		/// <summary>
		/// Option: no delay
		/// </summary>
		/// <remarks>
		/// This option will enable/disable Nagle's algorithm for TCP protocol
		/// </remarks>
		public bool OptionNoDelay { get; set; }
		/// <summary>
		/// Option: reuse address
		/// </summary>
		/// <remarks>
		/// This option will enable/disable SO_REUSEADDR if the OS support this feature
		/// </remarks>
		public bool OptionReuseAddress { get; set; }
		/// <summary>
		/// Option: enables a socket to be bound for exclusive access
		/// </summary>
		/// <remarks>
		/// This option will enable/disable SO_EXCLUSIVEADDRUSE if the OS support this feature
		/// </remarks>
		public bool OptionExclusiveAddressUse { get; set; }
		/// <summary>
		/// Option: receive buffer size
		/// </summary>
		public int OptionReceiveBufferSize { get; set; }
		/// <summary>
		/// Option: send buffer size
		/// </summary>
		public int OptionSendBufferSize { get; set; }

		bool Start();
		bool Stop();
		bool Restart();
	}
}
