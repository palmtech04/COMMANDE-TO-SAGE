using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Objets100cLib;
using OrderNameSpace;
using ProductNameSpace;
using System.Diagnostics;
using System.Net.Http;
using System.Timers;
class Program
{
    // Declaration of global objects
    private static readonly HttpClient httpClient = new HttpClient(); // Use global HttpClient
    private static BSCPTAApplication100c bCpta = new();
    private static BSCIALApplication100c bCial = new();
    private static string reference;
    private static IBODocumentVente3 DocEntete;
 



     static async Task Main(string[] args)
    {
        Console.Clear();
        try
        {
            httpClient. BaseAddress = new Uri("https://localhost:85/");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            // Build configuration from the JSON file
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "D:\\Developpement\\ApplicationCaisse\\COMMANDETOSAGE\\Test Sage - app\\settings.json"), optional: true, reloadOnChange: true)
                .Build();

            int nborders = 0;
            string logFilePath = "logfile.txt";

            // Retrieve values from configuration
            string bCptaSetting = configuration["bCpta"];
            string bCialSetting = configuration["bCial"];
            string Usernamesetting = configuration["username"];
            string passwordsetting = configuration["password"];
            Console.WriteLine($"bCptaSetting: {bCptaSetting}");
            Console.WriteLine($"bCialSetting: {bCialSetting}");
            Console.WriteLine($"Usernamesetting: {Usernamesetting}");
            Console.WriteLine($"passwordsetting: {passwordsetting}");

            string cmds = configuration["commandes"];
            string cmdDetails = configuration["commandesDetails"];

            try
            {

                // Send a GET request to the specified URL
                HttpResponseMessage response = await httpClient.GetAsync("api/commande/today");
                Console.WriteLine("cmds response :" + response.StatusCode);
                Console.WriteLine($"Command URL: {cmds}");

                if (response.IsSuccessStatusCode)
                {
                    // Read and parse the JSON content
                    string jsonContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(jsonContent);

                    // Deserialize the JSON into a list of Order objects
                    List<Order> orders = JsonConvert.DeserializeObject<List<Order>>(jsonContent);

                    stopwatch.Stop();
                    Console.WriteLine($"Temps ecoule Avant ouvrir base sage: {stopwatch.Elapsed}");
                    Console.WriteLine("Essayer d'ouvrir la base de données, veuillez patienter...");
                    stopwatch.Start();

                    // Open the  database connection
                    if (OpenBase(ref bCpta, ref bCial, @bCptaSetting, @bCialSetting, @Usernamesetting, @passwordsetting))
                    {
                        stopwatch.Stop();
                        Console.WriteLine($"Temps ecoule pour ouvrir base sage: {stopwatch.Elapsed}");
                        Console.WriteLine($"Nombre de commandes est: {orders.Count}");

                        foreach (var order in orders)
                        {
                            if (order.etat == 1 && order.isgenerated == "false")
                            {
                                // Create the header for the order in Sage
                                DocEntete = (IBODocumentVente3)bCial.FactoryDocumentVente.CreateType(DocumentType.DocumentTypeVenteFacture);
                                DocEntete.DO_Date = order.dateCreation;
                                DocEntete.SetDefaultClient(bCpta.FactoryClient.ReadNumero(order.clientId.Trim()));
                                DocEntete.DO_Ref = "Commande N " + order.idCommande;
                                DocEntete.DO_Statut = DocumentStatutType.DocumentStatutTypeAPrepare;
                                DocEntete.Write();
                                Console.WriteLine("Commande " + order.idCommande + " entete crée sur sage");

                                // Fetch order details
                                HttpResponseMessage responseCmdDetails = await httpClient.GetAsync("api/CommandeDetails/" + order.idCommande);

                                if (responseCmdDetails.IsSuccessStatusCode)
                                {
                                    string jsonContentCmdDetails = await responseCmdDetails.Content.ReadAsStringAsync();
                                    Console.WriteLine($"{jsonContentCmdDetails}");
                                    List<Product> cmdDetailsList = JsonConvert.DeserializeObject<List<Product>>(jsonContentCmdDetails);

                                    foreach (var product in cmdDetailsList)
                                    {
                                        Console.WriteLine($"Product ID: {product.idArticle}, Quantity: {product.quantite}");
                                        IBODocumentVenteLigne3 DocLigneCompose = (IBODocumentVenteLigne3)DocEntete.FactoryDocumentLigne.Create();
                                        DocLigneCompose.SetDefaultArticle(bCial.FactoryArticle.ReadReference(product.idArticle.Trim()), (double)product.quantite);
                                        DocLigneCompose.Write();
                                     }
                                }
                                HttpResponseMessage responseCmdReglments = await httpClient.GetAsync("api/Reglements/commandereglements/" + order.idCommande);
                                if (responseCmdReglments.IsSuccessStatusCode)
                                {
                                    string jsonContentCmdReglements = await responseCmdReglments.Content.ReadAsStringAsync();
                                    Console.WriteLine($"{jsonContentCmdReglements}");
                                    List<Reglement> cmdRegList = JsonConvert.DeserializeObject<List<Reglement>>(jsonContentCmdReglements);
                                    /*
                                    foreach (var reglement in cmdRegList)
                                    {
                                        
                                        IBPReglement3 reg = (IBPReglement3)bCpta.FactoryReglement.Create();
                                        reg.R_Code = "01";
                                        reg.R_Intitule = "cheque";
                                        reg.R_Type = ReglementType.ReglementTypeCheque;
                                        reg.JournalClient = bCpta.FactoryJournal.ReadNumero("BEU");
                                        reg.Write();
                                        IBODocumentReglement iReglt = (IBODocumentReglement)bCial.FactoryDocumentReglement.Create();
                                        iReglt.RG_Date = DateTime.Now;
                                        iReglt.Reglement = reg;
                                        iReglt.TiersPayeur = DocEntete.Client;
                                        iReglt.RG_Reference = "Réglement";
                                        iReglt.RG_Libelle = "Réglement";
                                        iReglt.RG_Montant = DocEntete.DO_NetAPayer - DocEntete.DO_MontantRegle;
                                        iReglt.Journal = bCpta.FactoryJournal.ReadNumero("BEU");
                                        iReglt.Write();
                                    }
                                    */
                                    IBODocumentReglement iReglt = (IBODocumentReglement)bCial.FactoryDocumentReglement.Create();
                                    iReglt.TiersPayeur = DocEntete.Client;
                                    iReglt.RG_Date = DateTime.Now;
                                    iReglt.RG_Reference = "Référence";
                                    iReglt.RG_Libelle = "Libellé";
                                    iReglt.RG_Montant = DocEntete.DO_NetAPayer - DocEntete.DO_MontantRegle;
                                    iReglt.Journal = bCial.CptaApplication.FactoryJournal.ReadNumero("BEU");
                                    iReglt.CompteG = DocEntete.Client.CompteGPrinc;
                                    iReglt.Write();
                                }

                                // Write log
                                WriteToLog(logFilePath, DateTime.Now.ToString());
                                WriteToLog(logFilePath, order.idCommande + " sur sage " + DocEntete.DO_Piece);
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Request error: {ex.Message}");
                Console.WriteLine($"Requested URI: {cmds}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
 
    }
 
    static void WriteToLog(string fileName, string message)
    {
        // Check if fileName is empty or null
        if (string.IsNullOrEmpty(fileName))
        {
            Console.WriteLine("Invalid log file name.");
            return;
        }

        // Get the current timestamp
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        try
        {
            // Combine the current directory with the fileName to get the full path
            string filePath = Path.Combine(Environment.CurrentDirectory, fileName);

            // Open the log file in append mode or create if it doesn't exist
            using (StreamWriter sw = new StreamWriter(filePath, true))
            {
                // Write the timestamp and message to the log file
                sw.WriteLine($"{timestamp} - {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to log file: {ex}");
        }
    }



    public static bool OpenBase(ref BSCPTAApplication100c BaseCpta, ref BSCIALApplication100c BaseCial, string sMae, string sGcm, string sUid, string sPwd)
    {
        try
        {
            BaseCpta.Name = sMae;
            BaseCpta.Loggable.UserName = sUid;
            BaseCpta.Loggable.UserPwd = sPwd;

            BaseCial.CptaApplication = BaseCpta;
            BaseCial.Name = sGcm;
            BaseCial.Loggable.UserName = sUid;
            BaseCial.Loggable.UserPwd = sPwd;
            BaseCial.Open();
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public static bool CloseBase(ref BSCIALApplication100c bCial)
    {
        try
        {
            if (bCial.IsOpen)
                bCial.Close();
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public class ResponseWrapper
    {
        [JsonProperty("order")]
        public List<Order> Record { get; set; }

        // You can add other metadata properties here if needed
    }


    public class ResponseWrapperCmdDetails
    {
        [JsonProperty("product")]
        public List<Product> Record { get; set; }

        // You can add other metadata properties here if needed
    }
}




