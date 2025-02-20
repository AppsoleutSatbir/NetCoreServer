using AC.NetCoreServer.Logging;
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
		public TcpServer(ILogger a_logger, IPAddress address, int port) : this(a_logger, new IPEndPoint(address, port)) { }
		/// <summary>
		/// Initialize TCP server with a given IP address and port number
		/// </summary>
		/// <param name="address">IP address</param>
		/// <param name="port">Port number</param>
		public TcpServer(ILogger a_logger, string address, int port) : this(a_logger, new IPEndPoint(IPAddress.Parse(address), port)) { }
		/// <summary>
		/// Initialize TCP server with a given DNS endpoint
		/// </summary>
		/// <param name="endpoint">DNS endpoint</param>
		public TcpServer(ILogger a_logger, DnsEndPoint endpoint) : this(a_logger, endpoint as EndPoint, endpoint.Host, endpoint.Port) { }
		/// <summary>
		/// Initialize TCP server with a given IP endpoint
		/// </summary>
		/// <param name="endpoint">IP endpoint</param>
		public TcpServer(ILogger a_logger, IPEndPoint endpoint) : this(a_logger, endpoint as EndPoint, endpoint.Address.ToString(), endpoint.Port) { }
		/// <summary>
		/// Initialize TCP server with a given endpoint, address and port
		/// </summary>
		/// <param name="endpoint">Endpoint</param>
		/// <param name="address">Server address</param>
		/// <param name="port">Server port</param>
		private TcpServer(ILogger a_logger, EndPoint endpoint, string address, int port) : base(a_logger, endpoint, address, port)
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
			Logger.Debug("TCPSession::Creating new session");
			TcpSession l_session = new TcpSession();
			l_session.Initialize(this, Logger);
			return l_session;
		}

		#endregion

		#region Session management

		/// <summary>
		/// Server sessions
		/// </summary>
		protected readonly ConcurrentDictionary<Guid, TcpSession> Sessions = new ConcurrentDictionary<Guid, TcpSession>();

		/// <summary>
		/// Disconnect all connected sessions
		/// </summary>
		/// <returns>'true' if all sessions were successfully disconnected, 'false' if the server is not started</returns>
		public virtual bool DisconnectAll()
		{
			if (!IsStarted)
				return false;

			// Disconnect all sessions
			foreach (var session in Sessions.Values)
				session.Disconnect("TcpServer:DisconnectAll:");

			return true;
		}

		/// <summary>
		/// Find a session with a given Id
		/// </summary>
		/// <param name="id">Session Id</param>
		/// <returns>Session with a given Id or null if the session it not connected</returns>
		public TcpSession FindSession(Guid id)
		{
			// Try to find the required session
			return Sessions.TryGetValue(id, out TcpSession result) ? result : null;
		}

		/// <summary>
		/// Register a new session
		/// </summary>
		/// <param name="session">Session to register</param>
		internal void RegisterSession(TcpSession session)
		{
			// Register a new session
			Sessions.TryAdd(session.Id, session);
		}

		/// <summary>
		/// Unregister session by Id
		/// </summary>
		/// <param name="id">Session Id</param>
		internal void UnregisterSession(Guid id)
		{
			// Unregister session by Id
			Sessions.TryRemove(id, out TcpSession _);
		}

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
