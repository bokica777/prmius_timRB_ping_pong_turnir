using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Client
{
    public class Client
    {
        static void Main(string[] args)
        {
            Console.Write("Server IP [127.0.0.1]: ");
            var ipText = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(ipText)) ipText = "127.0.0.1";

            Console.Write("Username: ");
            var username = Console.ReadLine() ?? "player";

            var serverIp = IPAddress.Parse(ipText);

            // 1) TCP connect na server (port 50001)
            using var tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcp.Connect(new IPEndPoint(serverIp, 50001));
            Console.WriteLine("TCP connected.");

            // 2) Pošalji username (bez new line je ok; tvoj server .Trim()-uje)
            var toSend = Encoding.UTF8.GetBytes(username);
            tcp.Send(toSend);

            // 3) Čekaj poruku od servera (stiže tek kada se SVI igrači prijave i server podeli parove)
            var buf = new byte[1024];
            int n = tcp.Receive(buf); // blokira dok ne dođe poruka
            string msg = Encoding.UTF8.GetString(buf, 0, n);
            Console.WriteLine("Server: " + msg);

            // 4) (Opcionalno) izdvajanje UDP porta i slanje jednog test paketa
            var m = Regex.Match(msg, @"UDP\s*port:\s*(\d+)");
            if (m.Success)
            {
                int udpPort = int.Parse(m.Groups[1].Value);
                Console.WriteLine($"Dobijen UDP port: {udpPort}");

                // pošalji probni UDP paket (serveru na isti IP, taj port)
                using var udp = new UdpClient();
                udp.Connect(serverIp.ToString(), udpPort);
                var ping = Encoding.UTF8.GetBytes("hello");
                udp.Send(ping, ping.Length);
                Console.WriteLine("UDP test paket poslat (\"hello\").");
            }
            else
            {
                Console.WriteLine("Nije pronađen UDP port u poruci (ok ako još nema game loop-a).");
            }

            Console.WriteLine("Gotovo. Pritisni Enter za izlaz.");
            Console.ReadLine();
        }
    }
}
