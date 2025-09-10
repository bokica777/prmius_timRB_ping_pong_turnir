using System.Net.Sockets;
using System.Net;
using System.Text;

namespace Server
{
    public class Server
    {
        static void Main(string[] args)
        {
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Any, 50001); // Serverov IPEndPoint, IP i port na kom ce server soket primati poruke
            serverSocket.Bind(serverEP); // Povezujemo serverov soket sa njegovim EP
            Console.WriteLine($"Server je pokrenut i ceka poruku na: {serverEP}");

            EndPoint posiljaocEP = new IPEndPoint(IPAddress.Any, 0); // Serverov IPEndPoint, IP i port na kom ce server soket primati poruke

            byte[] prijemniBafer = new byte[1024]; // Inicijalizujemo bafer za prijem podataka sa pretpostavkom da poruka nece biti duza od 1024 bajta
            try
            {
                int brBajta = serverSocket.ReceiveFrom(prijemniBafer, ref posiljaocEP); // Primamo poruku i podatke o posiljaocu
                string poruka = Encoding.UTF8.GetString(prijemniBafer, 0, brBajta);
                Console.WriteLine($"Stiglo je {brBajta} od {posiljaocEP}, poruka:\n{poruka}");

            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Doslo je do greske tokom prijema poruke: \n{ex}");
            }

            Console.WriteLine("Server zavrsava sa radom");
            serverSocket.Close(); // Zatvaramo soket na kraju rada
            Console.ReadKey();
        }
    }
}
