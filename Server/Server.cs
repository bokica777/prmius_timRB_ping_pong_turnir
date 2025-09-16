using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using Biblioteka;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.VisualBasic;
using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;


namespace Server
{
    public class Server
    {
        private int numPlayers;
        private int pointsForWin;
        private int maxUdpPorts;

        private List<Player> players;
        private List<Match> matches;


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
            string allRegisteredMsg = "\nSvi igraci su registrovani. Cekajte na protivnike...";
            foreach (var player in players)
            {
                SendTcp(player.tcpSocket, allRegisteredMsg);
            }
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
            for (int i = 0; i < shuffledPlayers.Count; i++)
            {
                int j = random.Next(i, shuffledPlayers.Count);
                var temp = shuffledPlayers[i];
                shuffledPlayers[i] = shuffledPlayers[j];
                shuffledPlayers[j] = temp;
            }
            for (int i = 0; i < shuffledPlayers.Count; i += 2)
            {
                if (udpPortPool.Count < 2)
                {
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
                // Klijenti koriste portove 7000+ za slušanje, server koristi 6000+ za slanje
                string messageA = $"\n Protivnik: {match.B.username}, UDP port: {match.UdpPortA + 1000}";
                string messageB = $"\n Protivnik: {match.A.username}, UDP port: {match.UdpPortB + 1000}";
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
        // Metoda za pokretanje meča, kreiranje UDP soketa, primanje inputa i slanje stanja igre
        public void StartMatch(Match match)
        {
            Socket socketA = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket socketB = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Server prima poruke na portovima 6000+, klijenti šalju na te portove
            IPEndPoint endPointA = new IPEndPoint(IPAddress.Any, match.UdpPortA);
            IPEndPoint endPointB = new IPEndPoint(IPAddress.Any, match.UdpPortB);

            socketA.Bind(endPointA);
            socketB.Bind(endPointB);
            
            Console.WriteLine($"Server UDP socket A vezan na port {match.UdpPortA}");
            Console.WriteLine($"Server UDP socket B vezan na port {match.UdpPortB}");

            socketA.Blocking = false;
            socketB.Blocking = false;

            Console.WriteLine($"[MATCH]{match.A.username} vs {match.B.username}");

            EndPoint? epA = null;
            EndPoint? epB = null;
            byte[] buffer = new byte[1024];

            bool kraj = false;

            var state = match.State;

            try
            {
                while (!kraj && (state.ScoreA < match.PointsForWin) && (state.ScoreB < match.PointsForWin))
                {
                    Console.WriteLine($"Rezultat: {state.ScoreA} - {state.ScoreB} (treba {match.PointsForWin})");
                    List<Socket> checkRead = new List<Socket> { socketA, socketB };
                    List<Socket> checkError = new List<Socket> { socketA, socketB };
                    Socket.Select(checkRead, null, checkError, 1000);
                    
                    // Debug informacije
                    if (checkRead.Count == 0 && checkError.Count == 0)
                    {
                        Console.WriteLine("Nema događaja - timeout");
                    }

                    if (checkRead.Count > 0)
                    {
                        Console.WriteLine($"Desilo se {checkRead.Count} dogadjaja\n");
                        foreach (var s in checkRead)
                        {
                            EndPoint rmt = new IPEndPoint(IPAddress.Any, 0);

                            int n = s.ReceiveFrom(buffer, ref rmt);
                            string message = Encoding.UTF8.GetString(buffer, 0, n);
                            Console.WriteLine($"Primljena poruka: {message} \nSocket posiljaoca: {rmt} \nSocket primaoca: {s.LocalEndPoint}");
                            
                            if (s == socketA)
                            {
                                epA = rmt;
                                match.State.PaddleA_Y = Input(match.State.PaddleA_Y, message);
                            }
                            else if (s == socketB)
                            {
                                epB = rmt;
                                match.State.PaddleB_Y = Input(match.State.PaddleB_Y, message);
                            }
                            
                            if (message.Equals("kraj"))
                            {
                                Console.WriteLine("Igrac je poslao kraj");
                                kraj = true;
                                break;
                            }
                        }
                    }
                    if (checkError.Count > 0)
                    {
                        Console.WriteLine($"Desilo se {checkError.Count} gresaka\n");
                        foreach (var s in checkError)
                        {
                            Console.WriteLine($"Greska na socketu: {s.LocalEndPoint}");
                            Console.WriteLine("Zatvaram socket zbog greske...");
                            s.Close();
                            kraj = true;
                        }
                    }

                    // Timeout handling - ako nema događaja, nastavi sa igrom
                    if (checkRead.Count == 0 && checkError.Count == 0)
                    {
                        // Nema događaja, nastavi sa igrom
                        if (Console.KeyAvailable)
                        {
                            if (Console.ReadKey().Key == ConsoleKey.Escape)
                            {
                                Console.WriteLine("Server prekida rad...");
                                kraj = true;
                                break;
                            }
                        }
                    }

                    updatePhysics(state);

                    string payload = JsonSerializer.Serialize(state);
                    byte[] data = Encoding.UTF8.GetBytes(payload);
                    if (epA != null)
                    {
                        // Šaljemo na klijentski port (7000+)
                        IPEndPoint clientEndPointA = new IPEndPoint(IPAddress.Parse("127.0.0.1"), match.UdpPortA + 1000);
                        int bytesSentA = socketA.SendTo(data, 0, data.Length, SocketFlags.None, clientEndPointA);
                        Console.WriteLine($"Uspesno poslato {bytesSentA} bajtova ka {clientEndPointA}");
                    }
                    if (epB != null)
                    {
                        // Šaljemo na klijentski port (7000+)
                        IPEndPoint clientEndPointB = new IPEndPoint(IPAddress.Parse("127.0.0.1"), match.UdpPortB + 1000);
                        int bytesSentB = socketB.SendTo(data, 0, data.Length, SocketFlags.None, clientEndPointB);
                        Console.WriteLine($"Uspesno poslato {bytesSentB} bajtova ka {clientEndPointB}");
                    }

                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Doslo je do greske: {ex.SocketErrorCode}");
            }
            finally
            {
                socketB.Close();
                socketA.Close();
                udpPortPool.Enqueue(match.UdpPortA);
                udpPortPool.Enqueue(match.UdpPortB);

                string winner = state.ScoreA >= match.PointsForWin ? match.A.username :
                                state.ScoreB >= match.PointsForWin ? match.B.username : "n/a";
                Console.WriteLine($"[MATCH] Kraj — pobednik: {winner}");
            }
        }


        private const int STEP = 1;
        private int Input(int currentY, string msg)
        {
            msg = (msg ?? "").Trim().ToUpperInvariant();

            int dy = msg == "UP" ? -STEP :
                     msg == "DOWN" ? +STEP : 0;

            int lo = 0, hi = GameState.Height - GameState.PaddleH;
            int newY = currentY + dy;
            return Math.Clamp(newY, lo, hi);
        }
        //Metoda za odredjivanje kretanja loptice, sudara sa reketima i osvajanja poena
        private void updatePhysics(GameState s)
        {
            s.BallX += s.BallVX;
            s.BallY += s.BallVY;

            //Situacija kada lopta ima sudar sa reketima
            if (s.BallX == 1 && Overlaps(s.BallY, s.PaddleA_Y))
            {
                s.BallVX = +Math.Abs(s.BallVX);
                s.BallVY += Spin(s.BallY, s.PaddleA_Y);
                s.BallVY = Math.Clamp(s.BallVY, -1, +1);
            }
            if (s.BallX == GameState.Width - 2 && Overlaps(s.BallY, s.PaddleB_Y))
            {
                s.BallVX = -Math.Abs(s.BallVX);
                s.BallVY += Spin(s.BallY, s.PaddleB_Y);
                s.BallVY = Math.Clamp(s.BallVY, -1, +1);
            }
            //Sitacija kada loptica izadje van igraceg polja
            if (s.BallY < 0 || s.BallY >= GameState.Height)
            {
                if (s.BallVX > 0)
                {
                    s.ScoreB++;
                    ResetAfterScore(s, false);
                }
                else
                {
                    s.ScoreA++;
                    ResetAfterScore(s, true);
                }
                return;
            }
            //Sitacija kada loptica prodje pored reketa
            if (s.BallX < 0)
            {
                s.ScoreB++;
                ResetAfterScore(s, false);
            }
            else if (s.BallX >= GameState.Width)
            {
                s.ScoreA++;
                ResetAfterScore(s, true);
            }
        }
        //Pomocne metode za odredjivanje sudara loptice i reketa i efekta spina
        private static bool Overlaps(int ballY, int paddleY)
        {
            return ballY >= paddleY && ballY < paddleY + GameState.PaddleH;
        }
        private static int Spin(int BallY, int paddleY)
        {
            int center = paddleY + GameState.PaddleH / 2;
            return Math.Sign(BallY - center);
        }
        private static void ResetAfterScore(GameState s, bool serve)
        {
            s.BallX = GameState.Width / 2;
            s.BallY = GameState.Height / 2;

            s.BallVX = serve ? +1 : -1;
            s.BallVY = 0;

            s.PaddleA_Y = GameState.Height / 2 - GameState.PaddleH / 2;
            s.PaddleB_Y = GameState.Height / 2 - GameState.PaddleH / 2;
        }
        //Metoda za zapis rezultata
        private void LogResult(Match match)
        {
            var s = match.State;
            match.A.Points += s.ScoreA;
            match.B.Points += s.ScoreB;

            if (s.ScoreA > s.ScoreB)
                match.A.Wins++;
            else if (s.ScoreB > s.ScoreA)
                match.B.Wins++;
        }
        private string BuildRankingTable()
        {
            var ordered = players.OrderByDescending(p => p.Wins).ThenByDescending(p => p.Points).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("\nRang lista:");
            sb.AppendLine("Pozicija | Igrac | Pobede | Poeni");
            for (int i = 0; i < ordered.Count; i++)
            {
                var p = ordered[i];
                sb.AppendLine($"{i + 1,8} | {p.username,-20} | {p.Wins,6} | {p.Points,5}");
            }
            return sb.ToString();
        }
        private void PrintRankingTable()
        {
            string table = BuildRankingTable();
            Console.WriteLine(table);
            foreach (var p in players)
            {
                SendTcp(p.tcpSocket, table);
            }
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

            // Pokretanje svih mečeva paralelno
            var matchTasks = new List<Task>();
            foreach (var match in matches)
            {
                matchTasks.Add(Task.Run(() => {
                    server.StartMatch(match);
                    server.LogResult(match);
                }));
            }
            
            // Čekanje da se svi mečevi završe
            Task.WaitAll(matchTasks.ToArray());
            server.PrintRankingTable();
            
            Console.WriteLine("Server zavrsava sa radom");
            Console.WriteLine("Pritisnite bilo koji taster za izlaz...");
            Console.ReadKey();
        }

    }
}
