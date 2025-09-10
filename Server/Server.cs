using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using Biblioteka;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.CompilerServices;
using System.Text;


namespace Server
{
    public class Server
    {
        private int numPlayers;
        private int pointsForWin;
        private int maxUdpPorts;

        private List<Player> players;
        private List<Match> matches;
        private Dictionary<Player, int> ranking;

        private Queue<int> udpPortPool;

        private Socket serverSocket;
        public Server(int numPlayers, int pointsForWin, int maxUdpPorts)
        {
            this.numPlayers = numPlayers;
            this.pointsForWin = pointsForWin;
            this.maxUdpPorts = maxUdpPorts;

            this.players = new List<Player>();
            this.udpPortPool = new Queue<int>();
        }
        // Metoda za inicijalizaciju servera, kreiranje TCP soketa i popunjavanje pool-a UDP portova
        public void Initialize()
        {

            for (int port = 6000; port < 6000 + maxUdpPorts; port++)
            {
                udpPortPool.Enqueue(port);
            }

            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 50001);
            serverSocket.Bind(endPoint);
            serverSocket.Blocking = false;
            serverSocket.Listen(numPlayers + 2);

            Console.WriteLine($"Server spreman za {numPlayers} igrača, do {pointsForWin} poena, UDP portova: {maxUdpPorts}");
        }
        // Metoda za registraciju igrača, prihvatanje soketa i primanje korisničkog imena kao i dodeljivanje udp porta
        public void RegisterPlayers()
        {
            Console.Write($"Cekam {numPlayers} igraca");
            while (players.Count < numPlayers)
            {
                Socket clientSocket = null;
                try
                {
                    clientSocket = serverSocket.Accept();
                }
                catch (SocketException ex)
                {
                    Console.WriteLine("Socket exception: " + ex.Message);
                }
                if (clientSocket != null)
                {
                    string username = ReceiveUsername(clientSocket);
                    var player = new Player(username, udpPortPool.Dequeue());
                    players.Add(player);
                    Console.WriteLine($"Igrac {username} je registrovan sa UDP portom {player.udpPort}. Trenutno registrovano: {players.Count}/{numPlayers}");
                }
            }
        }
        // Metoda za primanje korisničkog imena od klijenta preko TCP soketa
        private string ReceiveUsername(Socket clientSocket)
        {
            byte[] buffer = new byte[1024];
            int receivedBytes = clientSocket.Receive(buffer);
            return Encoding.UTF8.GetString(buffer, 0, receivedBytes).Trim();
        }








        static void Main(string[] args)
        {
            Console.WriteLine("Server started...");
            Console.WriteLine("Enter number of players: ");
            int num_of_players = Int32.Parse(Console.ReadLine());
            Console.WriteLine("Enter number of points: ");
            int points_for_win = Int32.Parse(Console.ReadLine());
            Console.WriteLine("Enter number of udp ports: ");
            int max_udp_ports = Int32.Parse(Console.ReadLine());

            var server = new Server(num_of_players, points_for_win, max_udp_ports);
            server.Initialize();
            server.RegisterPlayers();



        }
       
    }
}
