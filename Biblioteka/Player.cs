using System.Runtime.CompilerServices;

namespace Biblioteka
{
    public class Player
    {
        private string username { get; set; }

        public int udpPort { get; set; }

        public Player(string username, int udpPort)
        {
            this.username = username;
            this.udpPort = udpPort;
        }
        public void showPlayer()
        {
            Console.WriteLine("Player: " + username);
        }
    }
}
