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
            //serverSocket.Blocking = false;
            serverSocket.Listen(numPlayers + 2);

            Console.WriteLine($"Server je pokrenut...");
            Console.WriteLine($"Server spreman za {numPlayers} igraca, do {pointsForWin} poena, UDP portova: {maxUdpPorts}");
        }
        // Metoda za registraciju igrača, prihvatanje soketa i primanje korisničkog imena
        public void RegisterPlayers()
        {
            Console.Write($"Cekam {numPlayers} igraca...\n");
            while (players.Count < numPlayers)
            {
                Socket clientSocket = serverSocket.Accept();
                string username = ReceiveUsername(clientSocket);
                players.Add(new Player(username, clientSocket));
                Console.WriteLine($"\n Igrac {username} ({players.Count}/{numPlayers})");
            }
            /*string allRegisteredMsg = "\nSvi igraci su registrovani. Cekajte na protivnike...";
            foreach (var player in players)
            {
                SendTcp(player.tcpSocket, allRegisteredMsg);
            }*/
        }
        // Metoda za primanje korisničkog imena od klijenta preko TCP soketa
        private string ReceiveUsername(Socket clientSocket)
        {
            byte[] buffer = new byte[1024];
            int receivedBytes = clientSocket.Receive(buffer);
            return Encoding.UTF8.GetString(buffer, 0, receivedBytes).Trim();
        }
        // Metoda za kreiranje mečeva, nasumično sparivanje igrača i dodjeljivanje UDP portova
        public List<Match> CreateMatch()
        {
            var matches = new List<Match>();
            var random = new Random();

            var shuffledPlayers = new List<Player>(players);
            for(int i = 0; i < shuffledPlayers.Count; i++)
            {
                int j = random.Next(i, shuffledPlayers.Count);
                var temp = shuffledPlayers[i];
                shuffledPlayers[i] = shuffledPlayers[j];
                shuffledPlayers[j] = temp;
            }
            for (int i = 0; i < shuffledPlayers.Count; i += 2)
            {
                if(udpPortPool.Count < 2) { 
                    Console.WriteLine("Nema dovoljno UDP portova za kreiranje meca.");
                    break;
                }
                Player A = shuffledPlayers[i];
                Player B = shuffledPlayers[i + 1];

                int udpPortA = udpPortPool.Dequeue();
                int udpPortB = udpPortPool.Dequeue();

                matches.Add(new Match(A, B, pointsForWin, udpPortA, udpPortB));
            }
            this.matches = matches;
            return matches;
        }
        // Metoda za obavještavanje igrača o njihovim protivnicima i dodijeljenim UDP portovima
        public void NotifyPlayers(List<Match> matches)
        {
            foreach (var match in matches)
            {
                string messageA = $"\n Protivnik: {match.B.username}, UDP port: {match.UdpPortA}";
                string messageB = $"\n Protivnik: {match.A.username}, UDP port: {match.UdpPortB}";
                SendTcp(match.A.tcpSocket, messageA);
                SendTcp(match.B.tcpSocket, messageB);
            }
        }
        //POmocna metoda za slanje poruke preko TCP soketa
        private void SendTcp(Socket socket, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            socket.Send(data);
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=====Ping Pong Turnir=======");
            Console.WriteLine("-----Server---------");
            Console.WriteLine("Unesite broj igraca: ");
            int num_of_players = Int32.Parse(Console.ReadLine());
            Console.WriteLine("Unesite potreban broj poena za pobedu: ");
            int points_for_win = Int32.Parse(Console.ReadLine());
            Console.WriteLine("Unesite maksimalan broj UDP port-ova: ");
            int max_udp_ports = Int32.Parse(Console.ReadLine());

            var server = new Server(num_of_players, points_for_win, max_udp_ports);
            server.Initialize();
            server.RegisterPlayers();
            var matches = server.CreateMatch();
            server.NotifyPlayers(matches);

        }
       
    }
}
