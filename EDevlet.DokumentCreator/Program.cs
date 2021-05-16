using EDevlet.Document.Common;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading.Tasks;

namespace EDevlet.DokumentCreator
{
    class Program
    {
        static IConnection connection;
        private static readonly string createDocument = "create_document_queue"; // dokuman oluştur diye haber verecek queue
        private static readonly string documentCreated = "document_created_queue"; // document create edildiğinde consumer ona  haber verecek queue
        private static readonly string documentCreateExchange = "document_create_exchange";// bu işlere bakacak exchange gerekli

        static IModel _channel;
        static IModel channel => _channel ?? (_channel = GetChannel()); // singleton yazdık

        static void Main(string[] args)
        {
            // bu uygulama bizim consumer'ımız olacak. hem consume edecek, sonrasında publish edecek.

            // öncelikle conneciton ihiyac var

            connection = GetConnection();
            /*
             uygulama açıldı. connection alındı. dedim ki ben bir tane queue dinlemek istiyorum. bunu dinylecem.
             */
            channel.ExchangeDeclare(documentCreateExchange, "direct"); // exchange de oluşturalım. tipi direct olack
            channel.QueueDeclare(createDocument, false, false, false); // createdocumenti tanımlıyorum . ilk queue

            // ilk queue yu exchange ile bind ediyoruz.
            channel.QueueBind(createDocument, documentCreateExchange, createDocument); // üçüncü create document routing key. direct seçtiğimiz için

            channel.QueueDeclare(documentCreated, false, false, false);
            channel.QueueBind(documentCreated, documentCreateExchange, documentCreated); // üçüncü documentcreated routing key


            var consumerEvent = new EventingBasicConsumer(channel); // böyle bir eventim var. benden channel istiyor. bu eventin recieve isminde bir metodu var. bunun ilk parametesi channel ikincisi event argüment şeklinde.
            consumerEvent.Received += (ch, ea) =>
            {
                // mesaj gelince önce modeli alalım
                var modelJson = Encoding.UTF8.GetString(ea.Body.ToArray());
                var model = JsonConvert.DeserializeObject<CreateDocumentModel>(modelJson);

                // recive data ya bu url i bastık
                Console.WriteLine($"Received Data : {modelJson}");

                // create document işlemi burada olacak. ve bunun beş saniye sürdüğünü düşünelim. simule ettik.
                Task.Delay(5000).Wait(); //data burada alındı..

                // modelin url'ini değiştirelim. mesela document goes to ftp => mesela buraya yüklenmiş dokumanın urli olsun. dokumanı ftp ye gönderdik.
                model.Url = "http://www.turkiye.gov.tr/docs/x.pdf"; // url'i setledik. sonrada bunu rabbitmq ya haber veriyoruz.

                // bu modeli bundan sonra queue ya göndercez. o de document created olacak. ben bu modeli oluşturdum diuyecez

                WriteQueue(documentCreated, model);

            };

        
            // bu sefer dinleyeceğim şey ise, ürettiğim döküman. yani created document kuyruşu 
            channel.BasicConsume(createDocument, true, consumerEvent); // document create gelirse biz bunu consume edecez.

            Console.WriteLine($"{documentCreateExchange} listening");

            Console.ReadLine();

        }


        private static IModel GetChannel() // bu metot connectiondan createmodeli dönecekti
        {
            return connection.CreateModel();
        }

        private static IConnection GetConnection()
        {
            var connectionFactory = new ConnectionFactory()
            {
                Uri = new Uri("amqps://evrdybgg:TQLBLVPPb7_OR1qBR3hajFa7ncOrB_HM@fish.rmq.cloudamqp.com/evrdybgg")   // connection txt den alınıyor.
            };

            return connectionFactory.CreateConnection();
        }

        private static void WriteQueue(string queueName, CreateDocumentModel model)
        {
            // modeli buraya yazacaz. hangi queue ya yazacağımızı biliyoruz.
            // bir tane channel nesenisine ihtiyac var. onu da yukarıda tanımladık.

            var messageArr = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(model)); // yukarıda set ettiğimiz model.

            channel.BasicPublish(documentCreateExchange, queueName, null, messageArr); // basic publish metodumuz gelsin. Burada bir exchane isityor bizden. routing key istiyor o da queue name. son olarak da body array istiyor.

            Console.WriteLine("Message published.");

        }
    }
}
