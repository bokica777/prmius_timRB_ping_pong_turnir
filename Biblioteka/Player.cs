using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Biblioteka
{
    public class Player
    {
        public string username { get; set; }

        public Socket tcpSocket { get; set; }

        public Player(string username, Socket tcpSocket)
        {
            this.username = username;
            this.tcpSocket = tcpSocket;
        }
        public void showPlayer()
        {
            Console.WriteLine(" Igrac: " + username);
        }
    }
}
