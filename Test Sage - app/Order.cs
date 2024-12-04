 
using ProductNameSpace;

namespace OrderNameSpace
{

    public class Order
    {
        public int idCommande { get; set; }
        public DateTime dateCreation { get; set; }
        public DateTime Date { get; set; }
        public string clientId {  get; set; }
        public string isgenerated { get; set; }
        public int etat { get; set; }
    }

}