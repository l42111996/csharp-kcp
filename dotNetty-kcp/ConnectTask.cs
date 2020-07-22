using System;
using dotNetty_kcp.thread;

namespace dotNetty_kcp
{
    public  class ConnectTask:ITask
    {
        private readonly Ukcp _ukcp;

        private readonly KcpListener _listener;

        public ConnectTask(Ukcp ukcp, KcpListener listener)
        {
            _ukcp = ukcp;
            _listener = listener;
        }


        public override void execute()
        {
            try
            {
                _listener.onConnected(_ukcp);
            }
            catch (Exception e)
            {
                _listener.handleException(e,_ukcp);
            }
        }
    }
}