using EDevlet.Document.Common;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EDevletSample
{
    public partial class frmMain : Form
    {
        IConnection connection;
        private readonly string createDocument = "create_document_queue"; // dokuman oluştur diye haber verecek queue
        private readonly string documentCreated = "document_created_queue"; // document create edildiğinde consumer ona  haber verecek queue
        private readonly string documentCreateExchange = "document_create_exchange" ;// bu işlere bakacak exchange gerekli

        IModel _channel;
        IModel channel => _channel ?? (_channel = GetChannel()); // singleton yazdık

   
        public frmMain()
        {
            InitializeComponent();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (connection == null || !connection.IsOpen)
            {
                connection = GetConnection();

                btnCreateDocument.Enabled = true;
                // connect olduktan sonra declare işlemlerini yapıyoruz.
                // declare işlemlerimiz neler? öncelikle queue ları tanımlayacaz. sonra exchange i tanımlayacaz. sonra exchange ile queueları birbirine bağlayacaz.

                channel.ExchangeDeclare(documentCreateExchange,"direct"); // exchange de oluşturalım. tipi direct olack
                channel.QueueDeclare(createDocument, false, false, false); // createdocumenti tanımlıyorum . ilk queue

                // ilk queue yu exchange ile bind ediyoruz.
                channel.QueueBind(createDocument, documentCreateExchange,createDocument); // üçüncü create document routing key. direct seçtiğimiz için

                channel.QueueDeclare(documentCreated, false, false, false);
                channel.QueueBind(documentCreated, documentCreateExchange, documentCreated); // üçüncü documentcreated routing key

                // bind işlemlerini burada hallettik. consurmer da bir daha bununla uğraşmış olmayız.


                AddLog("Connection Is open"); // connection açıldı. bundan sonra bir model oluşturup / kuyruğa gönderiyoruz.

            }
        }

        private IModel GetChannel() // bu metot connectiondan createmodeli dönecekti
        {
            return connection.CreateModel();
        }

        private IConnection GetConnection()
        {
            var connectionFactory = new ConnectionFactory()
            {
                Uri = new Uri(txtConnection.Text)   // connection txt den alınıyor.
            };

            return connectionFactory.CreateConnection();
        }

        private void AddLog(string logStr)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => AddLog(logStr)));
                return;
            }

            logStr = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] - {logStr}";
            txtLog.AppendText($"{logStr} \n");

            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }

        private void btnCreateDocument_Click(object sender, EventArgs e)
        {
            var model = new CreateDocumentModel()
            {
                UserId =1,
                DocumentType = DocumentType.Pdf
            };

            // modelimi oluşturdum. Modelimi göndercem. Bunun için benim queue isimlerine ihtiyacım var.

            // create documentin altında writeQueue'yu çağırcaz
            WriteQueue(createDocument,model); // hangi queue ya göndercez? create dediğimiz için, create queue ya göndercez.

            frmSplash frmSplash = new frmSplash();
            frmSplash.Show();

            // bunu çıkarttıktan sonra, benim artık consumer işlemlerini toparlamam gerek. 

            var consumerEvent = new EventingBasicConsumer(channel); // böyle bir eventim var. benden channel istiyor. bu eventin recieve isminde bir metodu var. bunun ilk parametesi channel ikincisi event argüment şeklinde.
            consumerEvent.Received += (ch, ea) =>
            {
                // mesaj gelince önce modeli alalım
                var modelReceived = JsonConvert.DeserializeObject<CreateDocumentModel>(Encoding.UTF8.GetString(ea.Body.ToArray()));
                // recive data ya bu url i bastık
                AddLog($"Received Data Url: {modelReceived.Url}");

                closeSplashScreen(frmSplash);

            };

            // sonra channel a diyecez ki ben artık consume etmek isityorum. yani;

            channel.BasicConsume(documentCreated, true, consumerEvent); // o bana neyi consume edecen diyor. ben de ona document created ı consume edecem. cünkü consumer öteki taraftan yazacak bana. bana diyecek ki ben dökümanı oluşturdum. onu consume edecez diyorum. auotAck'ti true ve cunsoumer olarak da consumer eventimi parametre olarak gönderiyorum. 
     

        }

        // splash'i kapamak için;
        private void closeSplashScreen(frmSplash frmSplash)
        {
            if (frmSplash.InvokeRequired)
            {// ınvoke= metot çalışıyorsa
                frmSplash.Invoke(new Action(()=> closeSplashScreen(frmSplash))); 
                return;
            }
            frmSplash.Close();
        }

        /* Kaç tane queue kaç tane exchange'im olacak?
      * 1) dokuman oluştur diye bir queue ya kayıt yazacak. 2) consumer bunu okudum diye başka bir queue ya bu dökümanı yazacak. dolayısıyla şu an için iki tane queue ya ihtiyacımız var.
      * 
      * --- bu aradaki iletişimi sağlamak için de rabbitMq da bir tane exchange'e ihtiyacımız var. 

      */

        private void WriteQueue(string queueName, CreateDocumentModel model)
        {
            // modeli buraya yazacaz. hangi queue ya yazacağımızı biliyoruz.
            // bir tane channel nesenisine ihtiyac var. onu da yukarıda tanımladık.

            var messageArr = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(model)); // yukarıda set ettiğimiz model.

            channel.BasicPublish(documentCreateExchange,queueName,null,messageArr); // basic publish metodumuz gelsin. Burada bir exchane isityor bizden. routing key istiyor o da queue name. son olarak da body array istiyor.

            AddLog("Message published.");

        }
    }
}
