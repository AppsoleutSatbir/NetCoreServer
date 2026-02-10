using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetCoreServer
{
    public class NlbTcpSession : TcpSession
    {
        protected override void TryReceive()
		{
			Logger.Debug("NlbTcpSession:: TryReceive.return");
		}
    }
}
