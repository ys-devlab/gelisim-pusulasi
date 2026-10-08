using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsApp1
{
    public class EvaluatorInfo
    {
        public bool IsBlueCollar { get; set; } // Kişi Mavi Yaka mı?

        // Beyaz Yaka İçin
        public int ManagerCount { get; set; }
        public int PeerCount { get; set; }
        public int SubordinateCount { get; set; }
        public int OtherCount { get; set; }

        // Mavi Yaka İçin Ekstra
        public int SecondManagerCount { get; set; } // 2. Yönetici Sayısı
    }
}
