using System.Security.Cryptography.X509Certificates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bilbioteka
{
    [Serializable]
    public class Student
    {
        public string ImePrezime { get; set; }
        public int Poeni { get; set; }
    }
}
