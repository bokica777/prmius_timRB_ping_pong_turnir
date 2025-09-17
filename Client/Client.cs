using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using Biblioteka;

namespace Client
{
    public class Client
    {
        private string username;
        private Socket tcpSocket;
        private Socket udpSocket;
        private IPEndPoint serverEndPoint;
        private int udpPort;
        private bool gameRunning = false;
        private GameState currentGameState;

        private readonly object _stateLock = new object();
        private volatile string _lastServerMessage = "";
        private int _uiTop = 0; 

        public Client(string username)
        {
            this.username = username;
            this.currentGameState = new GameState();
        }

        // TCP pomagaci
        private static string Procitaj(Socket s)
        {
            byte[] buf = new byte[1024];
            int n = s.Receive(buf);
            if (n <= 0) return "";
            return Encoding.UTF8.GetString(buf, 0, n).Trim();
        }
        private static void Posalji(Socket s, string msg)
        {
            byte[] data = Encoding.UTF8.GetBytes(msg);
            s.Send(data);
        }

        public bool ConnectToServer(string serverIP = "127.0.0.1", int serverPort = 50001)
        {
            try
            {
                tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
                tcpSocket.Connect(serverEndPoint);

                Posalji(tcpSocket, username);
                tcpSocket.Blocking = false;

                Console.WriteLine($"Povezan na server {serverIP}:{serverPort}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška pri povezivanju na server: {ex.Message}");
                return false;
            }
        }

        public void ListenForTcpMessages()
        {
            try
            {
                while (true)
                {
                    var rd = new List<Socket> { tcpSocket };
                    Socket.Select(rd, null, null, 500_000);

                    if (rd.Count > 0)
                    {
                        try
                        {
                            string message = Procitaj(tcpSocket);
                            if (string.IsNullOrEmpty(message))
                                break;

                            if (!gameRunning)
                                Console.WriteLine($"\n[SERVER]: {message}");
                            else
                                _lastServerMessage = message;

                            if (message.Contains("UDP port:"))
                            {
                                ExtractUdpPort(message);
                                StartUdpConnection();
                            }
                        }
                        catch (SocketException se) when (se.SocketErrorCode == SocketError.WouldBlock)
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u TCP komunikaciji: {ex.Message}");
            }
        }
        // UDP pomagaci
        private void ExtractUdpPort(string message)
        {
            try
            {
                string[] parts = message.Split(new string[] { "UDP port: " }, StringSplitOptions.None);
                if (parts.Length > 1)
                {
                    string portString = parts[1].Trim();
                    udpPort = int.Parse(portString);
                    Console.WriteLine($"Dodeljen UDP port: {udpPort}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška pri parsiranju UDP porta: {ex.Message}");
            }
        }

        private void StartUdpConnection()
        {
            try
            {
                udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, udpPort);
                udpSocket.Bind(localEndPoint);
                udpSocket.Blocking = false;

                Console.WriteLine($"UDP socket vezan na port {udpPort}");
                Console.WriteLine($"Server port (za slanje): {udpPort - 1000}");
                gameRunning = true;

                SendUdpCommand("READY");

                Thread udpListenerThread = new Thread(ListenForUdpMessages) { IsBackground = true };
                udpListenerThread.Start();

                Thread inputThread = new Thread(HandleUserInput) { IsBackground = true };
                inputThread.Start();

                Thread displayThread = new Thread(DisplayGame) { IsBackground = true };
                displayThread.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška pri inicijalizaciji UDP konekcije: {ex.Message}");
            }
        }

        private void ListenForUdpMessages()
        {
            const int SELECT_TIMEOUT_US = 50_000;

            try
            {
                while (gameRunning)
                {
                    var checkRead = new List<Socket> { udpSocket };
                    var checkError = new List<Socket> { udpSocket };

                    Socket.Select(checkRead, null, checkError, SELECT_TIMEOUT_US);

                    if (checkRead.Count > 0)
                    {
                        try
                        {
                            byte[] buffer = new byte[4096];
                            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                            int receivedBytes = udpSocket.ReceiveFrom(buffer, ref remoteEndPoint);

                            var newState = DeserializeGameState(buffer, receivedBytes);
                            if (newState != null)
                            {
                                lock (_stateLock)
                                    currentGameState = newState;
                            }
                        }
                        catch (SocketException) { }
                        catch { }
                    }

                    if (checkError.Count > 0)
                    {
                        udpSocket.Close();
                        gameRunning = false;
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Greška u UDP komunikaciji: {ex.SocketErrorCode}");
            }
        }

        private void SendUdpCommand(string command)
        {
            try
            {
                if (udpSocket != null)
                {
                    int serverPort = udpPort - 1000;
                    IPEndPoint serverUdpEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), serverPort);
                    byte[] data = Encoding.UTF8.GetBytes(command);
                    int bytesSent = udpSocket.SendTo(data, 0, data.Length, SocketFlags.None, serverUdpEndPoint);

                    _lastServerMessage = $"TX {bytesSent}B: \"{command}\" → {serverUdpEndPoint}";
                }
            }
            catch (SocketException ex)
            {
                _lastServerMessage = $"TX error: {ex.SocketErrorCode}";
            }
        }

        //Binarni prijem GameState-a
        private static GameState DeserializeGameState(byte[] buf, int len)
        {
            using var ms = new MemoryStream(buf, 0, len);
            using var br = new BinaryReader(ms);
            var s = new GameState();
            s.BallX = br.ReadInt32();
            s.BallY = br.ReadInt32();
            s.BallVX = br.ReadInt32();
            s.BallVY = br.ReadInt32();
            s.PaddleA_Y = br.ReadInt32();
            s.PaddleB_Y = br.ReadInt32();
            s.ScoreA = br.ReadInt32();
            s.ScoreB = br.ReadInt32();
            return s;
        }

        private void HandleUserInput()
        {
            try
            {
                while (gameRunning)
                {
                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                        string command = "";
                        switch (keyInfo.Key)
                        {
                            case ConsoleKey.UpArrow: command = "UP"; break;
                            case ConsoleKey.DownArrow: command = "DOWN"; break;
                            case ConsoleKey.Escape:
                                command = "KRAJ";
                                gameRunning = false;
                                break;
                        }

                        if (!string.IsNullOrEmpty(command))
                            SendUdpCommand(command);
                    }
                    Thread.Sleep(50);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u upravljanju inputom: {ex.Message}");
            }
        }

        private void DisplayGame()
        {
            try
            {
                const int DISPLAY_MS = 90;
                Console.CursorVisible = false;

                _uiTop = Console.CursorTop;

                while (gameRunning)
                {
                    int ballX, ballY, pAy, pBy, sA, sB;
                    lock (_stateLock)
                    {
                        ballX = currentGameState.BallX;
                        ballY = currentGameState.BallY;
                        pAy = currentGameState.PaddleA_Y;
                        pBy = currentGameState.PaddleB_Y;
                        sA = currentGameState.ScoreA;
                        sB = currentGameState.ScoreB;
                    }

                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine(PadToFieldWidth($"=== PING PONG - {username} ==="));
                    sb.AppendLine(PadToFieldWidth($"Rezultat: {sA} - {sB}"));
                    sb.AppendLine(PadToFieldWidth("Kontrole: \u2191 \u2193 (strelice), ESC (izlaz)"));
                    sb.AppendLine(PadToFieldWidth(string.IsNullOrWhiteSpace(_lastServerMessage) ? "" : $"[SERVER MSG]: {_lastServerMessage.Trim()}"));
                    sb.Append(RenderField(ballX, ballY, pAy, pBy));

                    Console.SetCursorPosition(0, _uiTop);
                    Console.Write(sb.ToString());

                    Thread.Sleep(DISPLAY_MS);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u prikazu igre: {ex.Message}");
            }
            finally
            {
                Console.CursorVisible = true;
            }
        }
        private string PadToFieldWidth(string s)
        {
            int w = GameState.Width;
            if (s.Length >= w) return s.Substring(0, w);
            return s.PadRight(w, ' ');
        }

        private string RenderField(int ballX, int ballY, int paddleA_Y, int paddleB_Y)
        {
            char[,] field = new char[GameState.Height, GameState.Width];

            for (int y = 0; y < GameState.Height; y++)
                for (int x = 0; x < GameState.Width; x++)
                    field[y, x] = ' ';

            if (ballX >= 0 && ballX < GameState.Width && ballY >= 0 && ballY < GameState.Height)
                field[ballY, ballX] = 'O';

            for (int i = 0; i < GameState.PaddleH; i++)
            {
                int y = paddleA_Y + i;
                if (y >= 0 && y < GameState.Height) { field[y, 0] = '|'; field[y, 1] = '|'; }
            }

            for (int i = 0; i < GameState.PaddleH; i++)
            {
                int y = paddleB_Y + i;
                if (y >= 0 && y < GameState.Height) { field[y, GameState.Width - 2] = '|'; field[y, GameState.Width - 1] = '|'; }
            }

            var sb = new System.Text.StringBuilder();
            for (int y = 0; y < GameState.Height; y++)
            {
                for (int x = 0; x < GameState.Width; x++) sb.Append(field[y, x]);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public void Disconnect()
        {
            gameRunning = false;
            try
            {
                udpSocket?.Close();
                tcpSocket?.Close();
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Greška pri zatvaranju konekcija: {ex.SocketErrorCode}");
            }
        }

        public static void Main(string[] args)
        {
            Console.WriteLine("=== PING PONG TURNIR - KLIJENT ===");
            Console.Write("Unesite vaše ime: ");
            string username = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(username))
            {
                Console.WriteLine("Ime ne može biti prazno!");
                return;
            }

            Client client = new Client(username);

            try
            {
                if (client.ConnectToServer())
                    client.ListenForTcpMessages();
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Greška u radu klijenta: {ex.SocketErrorCode}");
            }
            finally
            {
                client.Disconnect();
            }

            Console.WriteLine("Klijent završava sa radom");
            Console.WriteLine("Pritisnite bilo koji taster za izlaz...");
            Console.ReadKey();
        }
    }
}



