using System;
using System.Threading.Tasks;

namespace TestNavicon
{
    class Program
    {
        
        static async Task Main(string[] args)
        {
            Logic logic = new Logic();
            var a =await logic.GetImages("https://ru.depositphotos.com/239952804/stock-photo-high-angle-view-young-sportsman.html", 45, 0);
        }
      
    }
}
