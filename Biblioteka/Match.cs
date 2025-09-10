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
        public int ScoreA { get; set; }
        public int ScoreB { get; set; }
        public int UdpPortA { get; set; }
        public int UdpPortB { get; set; }

        public string Status { get; set; } = "Pending";
        public Player Winner { get; set; }

        public Match(Player a, Player b, int pointsForWin, int UdpPortA, int UdpPortB)
        {
            A = a;
            B = b;
            PointsForWin = pointsForWin;
            this.UdpPortA = UdpPortA;
            this.UdpPortB = UdpPortB;
            ScoreA = 0;
            ScoreB = 0;
        }

    }
}
