using AC.NetCoreServer.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetCoreServer
{
	public class NlbServer : TcpServer
    {
		/// <summary>
		/// Initialize TCP server with a given IP address and port number
		/// </summary>
		/// <param name="address">IP address</param>
		/// <param name="port">Port number</param>
		public NlbServer(ILogger a_logger, IPAddress address, int port) : base(a_logger, new IPEndPoint(address, port)) { }
		/// <summary>
		/// Initialize TCP server with a given IP address and port number
		/// </summary>
		/// <param name="address">IP address</param>
		/// <param name="port">Port number</param>
		public NlbServer(ILogger a_logger, string address, int port) : base(a_logger, new IPEndPoint(IPAddress.Parse(address), port)) { }

		protected override TcpSession CreateSession()
		{
			Logger.Debug("NlbServer::Creating new session");
			TcpSession l_session = new NlbTcpSession();
			l_session.Initialize(this, Logger);
			return l_session;
		}
    }
}
