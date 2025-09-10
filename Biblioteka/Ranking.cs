using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Biblioteka
{
    public class Ranking
    {
        public string Username { get; set; }
        public int Wins { get; set; }
        public int Points { get; set; }

        public Ranking(string Username)
        {
            this.Username = Username;
            Wins = 0;
            Points = 0;
        }
    }
}
