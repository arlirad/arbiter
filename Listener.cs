using System.Net; 
using System.Net.Sockets; 
using System.Threading.Tasks; 

namespace Arbiter {
    public class Listener {
        public delegate void OnConnectionHandler(object sender, Socket socket);
        public event OnConnectionHandler OnConnection;

        private List<IPAddress> _addresses = new List<IPAddress>();
        private List<int> _ports = new List<int>();
        private List<Socket> _sockets = new List<Socket>();

        public void Start() {
            foreach (ushort port in _ports) {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                foreach (var address in _addresses)
                    socket.Bind(new IPEndPoint(address, port));

                var acceptEventArgs = new SocketAsyncEventArgs();
                acceptEventArgs.Completed += AcceptEventArgs_Completed;

                socket.Listen();

                if (!socket.AcceptAsync(acceptEventArgs))
                    AcceptEventArgs_Completed(socket, acceptEventArgs);

                _sockets.Add(socket);
            }
        }

        public void Bind(IPAddress addr) {
            _addresses.Add(addr);
        }

        public void Bind(int port) {
            if (!_ports.Contains(port))
                _ports.Add(port);
        }

        void AcceptEventArgs_Completed(object sender, SocketAsyncEventArgs e) {
            var socket = e.AcceptSocket;
            
            OnConnection?.Invoke(this, socket);

            e.AcceptSocket = null; // unix hhh
            if (!((Socket) sender).AcceptAsync(e))
                AcceptEventArgs_Completed(sender, e);
        }
    }
}