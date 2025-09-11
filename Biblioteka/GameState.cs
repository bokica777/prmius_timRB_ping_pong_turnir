using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Biblioteka
{
    public class GameState
    {
        public const int Height = 20, Width = 40, PaddleH = 4;

        public int BallX { get; set; } = Width / 2;
        public int BallY { get; set; } = Height / 2;
        public int BallVX { get; set; } = 1;
        public int BallVY { get; set; } = 1;

        public int PaddleA_Y { get; set; } = Height / 2 - PaddleH / 2;
        public int PaddleB_Y { get; set; } = Height / 2 - PaddleH / 2;

        public int ScoreA { get; set; } = 0;
        public int ScoreB { get; set; } = 0;
    }
}
