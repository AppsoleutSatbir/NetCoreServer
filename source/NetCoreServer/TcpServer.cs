using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NetCoreServer
{
	/// <summary>
	/// TCP server is used to connect, disconnect and manage TCP sessions
	/// </summary>
	/// <remarks>Thread-safe</remarks>
	public class TcpServer : BaseServer<TcpSession>, IDisposable
	{
		/// <summary>
		/// Initialize TCP server with a given IP address and port number
		/// </summary>
		/// <param name="address">IP address</param>
		/// <param name="port">Port number</param>
		public TcpServer(IPAddress address, int port) : this(new IPEndPoint(address, port)) { }
		/// <summary>
		/// Initialize TCP server with a given IP address and port number
		/// </summary>
		/// <param name="address">IP address</param>
		/// <param name="port">Port number</param>
		public TcpServer(string address, int port) : this(new IPEndPoint(IPAddress.Parse(address), port)) { }
		/// <summary>
		/// Initialize TCP server with a given DNS endpoint
		/// </summary>
		/// <param name="endpoint">DNS endpoint</param>
		public TcpServer(DnsEndPoint endpoint) : this(endpoint as EndPoint, endpoint.Host, endpoint.Port) { }
		/// <summary>
		/// Initialize TCP server with a given IP endpoint
		/// </summary>
		/// <param name="endpoint">IP endpoint</param>
		public TcpServer(IPEndPoint endpoint) : this(endpoint as EndPoint, endpoint.Address.ToString(), endpoint.Port) { }
		/// <summary>
		/// Initialize TCP server with a given endpoint, address and port
		/// </summary>
		/// <param name="endpoint">Endpoint</param>
		/// <param name="address">Server address</param>
		/// <param name="port">Server port</param>
		private TcpServer(EndPoint endpoint, string address, int port) : base(endpoint, address, port)
		{
		}


		#region Start/Stop server

		/// <summary>
		/// Start the server
		/// </summary>
		/// <returns>'true' if the server was successfully started, 'false' if the server failed to start</returns>
		public override bool Start()
		{
			Debug.Assert(!IsStarted, "TCP server is already started!");
			return base.Start();
		}

		/// <summary>
		/// Stop the server
		/// </summary>
		/// <returns>'true' if the server was successfully stopped, 'false' if the server is already stopped</returns>
		public override bool Stop()
		{
			Debug.Assert(IsStarted, "TCP server is not started!");
			return base.Stop();
		}

		/// <summary>
		/// Restart the server
		/// </summary>
		/// <returns>'true' if the server was successfully restarted, 'false' if the server failed to restart</returns>
		public override bool Restart()
		{
			return base.Restart();
		}

		#endregion

		#region Accepting clients
		#endregion

		#region Session factory

		/// <summary>
		/// Create TCP session factory method
		/// </summary>
		/// <returns>TCP session</returns>
		protected override TcpSession CreateSession()
		{
			TcpSession l_session = new TcpSession();
			l_session.Initialize(this);
			return l_session;
		}

		#endregion

		#region Session management		

		#endregion

		#region Multicasting
		#endregion

		#region Server handlers
		#endregion

		#region Error handling

		#endregion

		#region IDisposable implementation

		#endregion
	}
}
