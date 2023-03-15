using AC.SocketServerCore.Logging;
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
	/// SSL server is used to connect, disconnect and manage SSL sessions
	/// </summary>
	/// <remarks>Thread-safe</remarks>
	public class SslServer : BaseServer<SslSession>, IDisposable
	{
		/// <summary>
		/// Initialize SSL server with a given IP address and port number
		/// </summary>
		/// <param name="context">SSL context</param>
		/// <param name="address">IP address</param>
		/// <param name="port">Port number</param>
		public SslServer(ILogger a_logger, SslContext context, IPAddress address, int port) : this(a_logger, context, new IPEndPoint(address, port)) { }
		/// <summary>
		/// Initialize SSL server with a given IP address and port number
		/// </summary>
		/// <param name="context">SSL context</param>
		/// <param name="address">IP address</param>
		/// <param name="port">Port number</param>
		public SslServer(ILogger a_logger, SslContext context, string address, int port) : this(a_logger, context, new IPEndPoint(IPAddress.Parse(address), port)) { }
		/// <summary>
		/// Initialize SSL server with a given DNS endpoint
		/// </summary>
		/// <param name="context">SSL context</param>
		/// <param name="endpoint">DNS endpoint</param>
		public SslServer(ILogger a_logger, SslContext context, DnsEndPoint endpoint) : this(a_logger, context, endpoint as EndPoint, endpoint.Host, endpoint.Port) { }
		/// <summary>
		/// Initialize SSL server with a given IP endpoint
		/// </summary>
		/// <param name="context">SSL context</param>
		/// <param name="endpoint">IP endpoint</param>
		public SslServer(ILogger a_logger, SslContext context, IPEndPoint endpoint) : this(a_logger, context, endpoint as EndPoint, endpoint.Address.ToString(), endpoint.Port) { }
		/// <summary>
		/// Initialize SSL server with a given SSL context, endpoint, address and port
		/// </summary>
		/// <param name="context">SSL context</param>
		/// <param name="endpoint">Endpoint</param>
		/// <param name="address">Server address</param>
		/// <param name="port">Server port</param>
		private SslServer(ILogger a_logger, SslContext context, EndPoint endpoint, string address, int port) : base(a_logger, endpoint, address, port)
		{
			Context = context;
		}

		/// <summary>
		/// SSL context
		/// </summary>
		public SslContext Context { get; }

		#region Start/Stop server
		/// <summary>
		/// Start the server
		/// </summary>
		/// <returns>'true' if the server was successfully started, 'false' if the server failed to start</returns>
		public override bool Start()
		{
			Debug.Assert(!IsStarted, "SSL server is already started!");
			return base.Start();
		}

		/// <summary>
		/// Stop the server
		/// </summary>
		/// <returns>'true' if the server was successfully stopped, 'false' if the server is already stopped</returns>
		public override bool Stop()
		{
			Debug.Assert(IsStarted, "SSL server is not started!");
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
		/// Create SSL session factory method
		/// </summary>
		/// <returns>SSL session</returns>
		protected override SslSession CreateSession()
		{
			Logger.Debug("Creating new session");
			SslSession l_session = new SslSession();
			l_session.Initialize(this, Logger);
			return l_session;
		}

		#endregion

		#region Session management

		#endregion

		#region Multicasting
		#endregion

		#region Server handlers
		internal void OnHandshakingInternal(SslSession session) { OnHandshaking(session); }
		internal void OnHandshakedInternal(SslSession session) { OnHandshaked(session); }

		/// <summary>
		/// Handle session handshaking notification
		/// </summary>
		/// <param name="session">Handshaking session</param>
		protected virtual void OnHandshaking(SslSession session) { Logger.Verbose("SslServer::Handshaking"); }
		/// <summary>
		/// Handle session handshaked notification
		/// </summary>
		/// <param name="session">Handshaked session</param>
		protected virtual void OnHandshaked(SslSession session) { Logger.Verbose("SslServer::OnHandshaked"); }

		#endregion

		#region Error handling
		#endregion

		#region IDisposable implementation

		#endregion
	}
}
