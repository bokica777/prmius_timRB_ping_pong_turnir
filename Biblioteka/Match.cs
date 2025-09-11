using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Biblioteka
{
    public class Match
    {
        public Player A { get; set; }
        public Player B { get; set; }
        public int PointsForWin { get; set; }
        public int UdpPortA { get; set; }
        public int UdpPortB { get; set; }

        public string Status { get; set; } = "Pending";
        public Player Winner { get; set; }

        public GameState State { get; set; } = new GameState();

        public Match(Player a, Player b, int pointsForWin, int udpPortA, int udpPortB)
        {
            A = a;
            B = b;
            PointsForWin = pointsForWin;
            UdpPortA = udpPortA;
            UdpPortB = udpPortB;

        }

    }
}
