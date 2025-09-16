using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
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

        public Client(string username)
        {
            this.username = username;
            this.currentGameState = new GameState();
        }

        // Metoda za povezivanje na server preko TCP protokola
        public bool ConnectToServer(string serverIP = "127.0.0.1", int serverPort = 50001)
        {
            try
            {
                tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
                tcpSocket.Connect(serverEndPoint);

                // Šaljemo korisničko ime serveru
                byte[] usernameData = Encoding.UTF8.GetBytes(username);
                tcpSocket.Send(usernameData);

                Console.WriteLine($"Povezan na server {serverIP}:{serverPort}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška pri povezivanju na server: {ex.Message}");
                return false;
            }
        }

        // Metoda za slušanje TCP poruka od servera (obaveštenja, rang lista)
        public void ListenForTcpMessages()
        {
            try
            {
                while (true)
                {
                    byte[] buffer = new byte[1024];
                    int receivedBytes = tcpSocket.Receive(buffer);
                    string message = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
                    
                    Console.WriteLine($"\n[SERVER]: {message}");
                    
                    // Ako poruka sadrži UDP port, inicijalizujemo UDP konekciju
                    if (message.Contains("UDP port:"))
                    {
                        ExtractUdpPort(message);
                        StartUdpConnection();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u TCP komunikaciji: {ex.Message}");
            }
        }

        // Metoda za izdvajanje UDP porta iz server poruke
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

        // Metoda za inicijalizaciju UDP konekcije
        private void StartUdpConnection()
        {
            try
            {
                udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, udpPort);
                udpSocket.Bind(localEndPoint);
                udpSocket.Blocking = false;

                Console.WriteLine($"UDP socket vezan na port {udpPort}");
                gameRunning = true;

                // Pokretanje thread-a za slušanje UDP poruka
                Thread udpListenerThread = new Thread(ListenForUdpMessages);
                udpListenerThread.IsBackground = true;
                udpListenerThread.Start();

                // Pokretanje thread-a za upravljanje inputom
                Thread inputThread = new Thread(HandleUserInput);
                inputThread.IsBackground = true;
                inputThread.Start();

                // Pokretanje thread-a za prikaz igre
                Thread displayThread = new Thread(DisplayGame);
                displayThread.IsBackground = true;
                displayThread.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška pri inicijalizaciji UDP konekcije: {ex.Message}");
            }
        }

        // Metoda za slušanje UDP poruka od servera (stanje igre)
        private void ListenForUdpMessages()
        {
            try
            {
                while (gameRunning)
                {
                    if (udpSocket.Available > 0)
                    {
                        byte[] buffer = new byte[1024];
                        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                        int receivedBytes = udpSocket.ReceiveFrom(buffer, ref remoteEndPoint);
                        
                        string jsonData = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
                        currentGameState = JsonSerializer.Deserialize<GameState>(jsonData);
                    }
                    Thread.Sleep(16); // ~60 FPS
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u UDP komunikaciji: {ex.Message}");
            }
        }

        // Metoda za upravljanje korisničkim inputom (strelicama)
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
                            case ConsoleKey.UpArrow:
                                command = "UP";
                                break;
                            case ConsoleKey.DownArrow:
                                command = "DOWN";
                                break;
                            case ConsoleKey.Escape:
                                command = "kraj";
                                gameRunning = false;
                                break;
                        }

                        if (!string.IsNullOrEmpty(command))
                        {
                            SendUdpCommand(command);
                        }
                    }
                    Thread.Sleep(50);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u upravljanju inputom: {ex.Message}");
            }
        }

        // Metoda za slanje UDP komande serveru
        private void SendUdpCommand(string command)
        {
            try
            {
                if (udpSocket != null && serverEndPoint != null)
                {
                    byte[] data = Encoding.UTF8.GetBytes(command);
                    udpSocket.SendTo(data, serverEndPoint);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška pri slanju UDP komande: {ex.Message}");
            }
        }

        // Metoda za prikaz igre na terminalu
        private void DisplayGame()
        {
            try
            {
                while (gameRunning)
                {
                    Console.Clear();
                    Console.WriteLine($"=== PING PONG - {username} ===");
                    Console.WriteLine($"Rezultat: {currentGameState.ScoreA} - {currentGameState.ScoreB}");
                    Console.WriteLine("Kontrole: ↑ ↓ (strelicama), ESC (izlaz)");
                    Console.WriteLine();

                    // Crtanje terena
                    DrawGameField();
                    
                    Thread.Sleep(100); // 10 FPS za prikaz
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u prikazu igre: {ex.Message}");
            }
        }

        // Metoda za crtanje igračkog polja
        private void DrawGameField()
        {
            char[,] field = new char[GameState.Height, GameState.Width];
            
            // Inicijalizacija praznog polja
            for (int y = 0; y < GameState.Height; y++)
            {
                for (int x = 0; x < GameState.Width; x++)
                {
                    field[y, x] = ' ';
                }
            }

            // Crtanje loptice
            if (currentGameState.BallX >= 0 && currentGameState.BallX < GameState.Width &&
                currentGameState.BallY >= 0 && currentGameState.BallY < GameState.Height)
            {
                field[currentGameState.BallY, currentGameState.BallX] = 'O';
            }

            // Crtanje reketa A (levi)
            for (int i = 0; i < GameState.PaddleH; i++)
            {
                int y = currentGameState.PaddleA_Y + i;
                if (y >= 0 && y < GameState.Height)
                {
                    field[y, 0] = '|';
                    field[y, 1] = '|';
                }
            }

            // Crtanje reketa B (desni)
            for (int i = 0; i < GameState.PaddleH; i++)
            {
                int y = currentGameState.PaddleB_Y + i;
                if (y >= 0 && y < GameState.Height)
                {
                    field[y, GameState.Width - 2] = '|';
                    field[y, GameState.Width - 1] = '|';
                }
            }

            // Ispis polja
            for (int y = 0; y < GameState.Height; y++)
            {
                for (int x = 0; x < GameState.Width; x++)
                {
                    Console.Write(field[y, x]);
                }
                Console.WriteLine();
            }
        }

        // Metoda za zatvaranje konekcija
        public void Disconnect()
        {
            gameRunning = false;
            
            try
            {
                if (udpSocket != null)
                {
                    udpSocket.Close();
                }
                if (tcpSocket != null)
                {
                    tcpSocket.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška pri zatvaranju konekcija: {ex.Message}");
            }
        }

        // Glavna metoda za pokretanje klijenta
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

            if (client.ConnectToServer())
            {
                try
                {
                    client.ListenForTcpMessages();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška u radu klijenta: {ex.Message}");
                }
                finally
                {
                    client.Disconnect();
                }
            }

            Console.WriteLine("Pritisnite bilo koji taster za izlaz...");
            Console.ReadKey();
        }
    }
}
