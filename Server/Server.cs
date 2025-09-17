using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Biblioteka;

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
        private static readonly object _logLock = new object();

        private const int TICK_MS = 90;  
        private const int BALL_SPEED_X = 2;    
        private const double VY_DAMP = 0.5;  
        private const int STEP = 2;    

        public Server(int numPlayers, int pointsForWin, int maxUdpPorts)
        {
            this.numPlayers = numPlayers;
            this.pointsForWin = pointsForWin;
            this.maxUdpPorts = maxUdpPorts;

            this.players = new List<Player>();
            this.udpPortPool = new Queue<int>();
        }

        // TCP pomagaci
        private static Socket NapraviTcpListener(int port, int backlog)
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s.Bind(new IPEndPoint(IPAddress.Any, port));
            s.Listen(backlog);
            return s;
        }
        private static void Posalji(Socket s, string msg)
        {
            byte[] data = Encoding.UTF8.GetBytes(msg);
            s.Send(data);
        }

        private void SendTcp(Socket socket, string message) => Posalji(socket, message);

        public void Initialize()
        {
            for (int port = 6000; port < 6000 + maxUdpPorts; port++)
                udpPortPool.Enqueue(port);

            serverSocket = NapraviTcpListener(50001, numPlayers + 2);
            serverSocket.Blocking = false; 

            Console.WriteLine("Server je pokrenut...");
            Console.WriteLine($"Server spreman za {numPlayers} igraca, do {pointsForWin} poena, UDP portova: {maxUdpPorts}");
        }

        public void RegisterPlayers()
        {
            Console.Write($"Cekam {numPlayers} igraca...\n");

            var pending = new List<Socket>();

            while (players.Count < numPlayers)
            {
                var readList = new List<Socket>(pending.Count + 1) { serverSocket };
                readList.AddRange(pending);
                var errList = new List<Socket>(readList);

                Socket.Select(readList, null, errList, 200_000);

                if (readList.Contains(serverSocket))
                {
                    while (true)
                    {
                        try
                        {
                            var cl = serverSocket.Accept();
                            cl.Blocking = false;
                            pending.Add(cl);
                        }
                        catch (SocketException se) when (se.SocketErrorCode == SocketError.WouldBlock)
                        {
                            break;
                        }
                    }
                    readList.Remove(serverSocket);
                }

                foreach (var cl in readList.ToArray())
                {
                    if (!pending.Contains(cl)) continue;
                    try
                    {
                        byte[] buf = new byte[1024];
                        int n = cl.Receive(buf);
                        if (n > 0)
                        {
                            string username = Encoding.UTF8.GetString(buf, 0, n).Trim();
                            players.Add(new Player(username, cl));
                            pending.Remove(cl);
                            Console.WriteLine($"\n Igrac {username} ({players.Count}/{numPlayers})");
                        }
                        else
                        {
                            pending.Remove(cl);
                            cl.Close();
                        }
                    }
                    catch (SocketException se) when (se.SocketErrorCode == SocketError.WouldBlock)
                    {
                        
                    }
                }

                if (errList.Count > 0)
                {
                    foreach (var s in errList)
                    {
                        if (s == serverSocket) continue;
                        if (pending.Remove(s))
                        {
                            try { s.Close(); } catch { }
                        }
                    }
                }
            }

            string allRegisteredMsg = "\nSvi igraci su registrovani. Cekajte na protivnike...";
            foreach (var player in players)
                SendTcp(player.tcpSocket, allRegisteredMsg);
        }

        public List<Match> CreateMatch()
        {
            var matches = new List<Match>();
            var random = new Random();

            var shuffledPlayers = new List<Player>(players);
            for (int i = 0; i < shuffledPlayers.Count; i++)
            {
                int j = random.Next(i, shuffledPlayers.Count);
                (shuffledPlayers[i], shuffledPlayers[j]) = (shuffledPlayers[j], shuffledPlayers[i]);
            }

            for (int i = 0; i + 1 < shuffledPlayers.Count; i += 2)
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

        public void NotifyPlayers(List<Match> matches)
        {
            foreach (var match in matches)
            {
                string messageA = $"\n Protivnik: {match.B.username}, UDP port: {match.UdpPortA + 1000}";
                string messageB = $"\n Protivnik: {match.A.username}, UDP port: {match.UdpPortB + 1000}";
                SendTcp(match.A.tcpSocket, messageA);
                SendTcp(match.B.tcpSocket, messageB);
            }
        }



        //Binarni prijem GameState-a
        private static byte[] SerializeGameState(GameState s)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(s.BallX);
            bw.Write(s.BallY);
            bw.Write(s.BallVX);
            bw.Write(s.BallVY);
            bw.Write(s.PaddleA_Y);
            bw.Write(s.PaddleB_Y);
            bw.Write(s.ScoreA);
            bw.Write(s.ScoreB);
            bw.Flush();
            return ms.ToArray();
        }
        private static void TrySendState(Socket s, EndPoint ep, GameState gs)
        {
            try
            {
                var data = SerializeGameState(gs);
                s.SendTo(data, 0, data.Length, SocketFlags.None, ep);
            }
            catch { }
        }

        // UDP match loop
        public void StartMatch(Match match)
        {
            const int SELECT_TIMEOUT_US = 50_000;

            Socket socketA = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket socketB = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            IPEndPoint endPointA = new IPEndPoint(IPAddress.Any, match.UdpPortA);
            IPEndPoint endPointB = new IPEndPoint(IPAddress.Any, match.UdpPortB);

            socketA.Bind(endPointA);
            socketB.Bind(endPointB);

            Console.WriteLine($"Server UDP socket A vezan na port {match.UdpPortA}");
            Console.WriteLine($"Server UDP socket B vezan na port {match.UdpPortB}");

            socketA.Blocking = false;
            socketB.Blocking = false;

            Console.WriteLine($"[MATCH]{match.A.username} vs {match.B.username}");

            var clientEndPointA = new IPEndPoint(IPAddress.Loopback, match.UdpPortA + 1000);
            var clientEndPointB = new IPEndPoint(IPAddress.Loopback, match.UdpPortB + 1000);

            byte[] buffer = new byte[1024];
            bool kraj = false;
            var state = match.State;

            if (state.BallVX == 0 && state.BallVY == 0) state.BallVX = BALL_SPEED_X;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            long last = 0;

            TrySendState(socketA, clientEndPointA, state);
            TrySendState(socketB, clientEndPointB, state);

            try
            {
                while (!kraj && (state.ScoreA < match.PointsForWin) && (state.ScoreB < match.PointsForWin))
                {
                    var checkRead = new List<Socket> { socketA, socketB };
                    var checkError = new List<Socket> { socketA, socketB };
                    Socket.Select(checkRead, null, checkError, SELECT_TIMEOUT_US);

                    foreach (var s in checkRead)
                    {
                        try
                        {
                            EndPoint rmt = new IPEndPoint(IPAddress.Any, 0);
                            int n = s.ReceiveFrom(buffer, ref rmt);
                            string message = Encoding.UTF8.GetString(buffer, 0, n).Trim();

                            if (s == socketA) state.PaddleA_Y = Input(state.PaddleA_Y, message);
                            else state.PaddleB_Y = Input(state.PaddleB_Y, message);

                            if (message.Equals("KRAJ", StringComparison.OrdinalIgnoreCase))
                                kraj = true;
                        }
                        catch (SocketException) { }
                    }

                    if (checkError.Count > 0)
                    {
                        foreach (var s in checkError)
                        {
                            Console.WriteLine($"Greska na socketu: {s.LocalEndPoint} — zatvaram mec");
                            s.Close();
                        }
                        kraj = true;
                    }

                    long now = sw.ElapsedMilliseconds;
                    if (now - last >= TICK_MS)
                    {
                        updatePhysics(state);
                        last = now;

                        TrySendState(socketA, clientEndPointA, state);
                        TrySendState(socketB, clientEndPointB, state);
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

                LogResult(match);

                string winner = ComputeWinnerName(match);
                var s = match.State;
                lock (_logLock)
                {
                    Console.WriteLine($"[MATCH] Kraj — {match.A.username} vs {match.B.username} → pobednik: {winner} (rezultat: {s.ScoreA}-{s.ScoreB})");
                }
            }
        }

        private static string ComputeWinnerName(Match match)
        {
            var s = match.State;
            if (s.ScoreA == s.ScoreB) return "nereseno";
            return (s.ScoreA > s.ScoreB) ? match.A.username : match.B.username;
        }

        private int Input(int currentY, string msg)
        {
            msg = (msg ?? "").Trim().ToUpperInvariant();
            int dy = msg == "UP" ? -STEP :
                     msg == "DOWN" ? +STEP : 0;
            int lo = 0, hi = GameState.Height - GameState.PaddleH;
            int newY = currentY + dy;
            return Math.Clamp(newY, lo, hi);
        }

        private void updatePhysics(GameState s)
        {
            s.BallX += s.BallVX;
            s.BallY += s.BallVY;

            // zidovi
            if (s.BallY < 0) { s.BallY = 0; s.BallVY = -s.BallVY; }
            else if (s.BallY >= GameState.Height) { s.BallY = GameState.Height - 1; s.BallVY = -s.BallVY; }

            // sudar sa reketima
            if (s.BallX == 1 && Overlaps(s.BallY, s.PaddleA_Y))
            {
                int spin = Spin(s.BallY, s.PaddleA_Y);
                s.BallVX = +BALL_SPEED_X;
                s.BallVY = (int)Math.Clamp(Math.Round(s.BallVY * VY_DAMP) + spin, -1, +1);
            }
            if (s.BallX == GameState.Width - 2 && Overlaps(s.BallY, s.PaddleB_Y))
            {
                int spin = Spin(s.BallY, s.PaddleB_Y);
                s.BallVX = -BALL_SPEED_X;
                s.BallVY = (int)Math.Clamp(Math.Round(s.BallVY * VY_DAMP) + spin, -1, +1);
            }

            // poeni
            if (s.BallX < 0) { s.ScoreB++; ResetAfterScore(s, false); return; }
            if (s.BallX >= GameState.Width) { s.ScoreA++; ResetAfterScore(s, true); return; }
        }

        private static bool Overlaps(int ballY, int paddleY)
            => ballY >= paddleY && ballY < paddleY + GameState.PaddleH;

        private static int Spin(int ballY, int paddleY)
        {
            int center = paddleY + GameState.PaddleH / 2;
            int delta = ballY - center;
            if (Math.Abs(delta) >= 2) return Math.Sign(delta);
            return 0;
        }

        private static void ResetAfterScore(GameState s, bool serveA)
        {
            s.BallX = GameState.Width / 2;
            s.BallY = GameState.Height / 2;

            s.BallVX = serveA ? +BALL_SPEED_X : -BALL_SPEED_X;
            s.BallVY = 0;

            s.PaddleA_Y = GameState.Height / 2 - GameState.PaddleH / 2;
            s.PaddleB_Y = GameState.Height / 2 - GameState.PaddleH / 2;
        }

        private void LogResult(Match match)
        {
            var s = match.State;
            match.A.Points += s.ScoreA;
            match.B.Points += s.ScoreB;

            if (s.ScoreA > s.ScoreB) match.A.Wins++;
            else if (s.ScoreB > s.ScoreA) match.B.Wins++;
        }

        private string BuildRankingTable()
        {
            var ordered = players.OrderByDescending(p => p.Wins).ThenByDescending(p => p.Points).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("\nRang lista:");
            sb.AppendLine("Pozicija | Igrac                | Pobede | Poeni");
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
                SendTcp(p.tcpSocket, table);
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=====Ping Pong Turnir=======");
            Console.WriteLine("-----Server---------");
            Console.Write("Unesite broj igraca: ");
            int num_of_players = int.Parse(Console.ReadLine() ?? "2");
            Console.Write("Unesite potreban broj poena za pobedu: ");
            int points_for_win = int.Parse(Console.ReadLine() ?? "5");
            Console.Write("Unesite maksimalan broj UDP port-ova: ");
            int max_udp_ports = int.Parse(Console.ReadLine() ?? "8");

            var server = new Server(num_of_players, points_for_win, max_udp_ports);
            server.Initialize();
            server.RegisterPlayers();
            var matches = server.CreateMatch();
            server.NotifyPlayers(matches);

            var matchTasks = new List<Task>();
            foreach (var match in matches)
                matchTasks.Add(Task.Run(() => server.StartMatch(match)));

            Task.WaitAll(matchTasks.ToArray());
            server.PrintRankingTable();

            Console.WriteLine("Server zavrsava sa radom");
            Console.WriteLine("Pritisnite bilo koji taster za izlaz...");
            Console.ReadKey();
        }
    }
}



