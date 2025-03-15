//using MongoDB.Bson;
//using MongoDB.Driver;
//using Mirabel.CallCenter.Models.Twilio;
//using Mirabel.CallCenter.Common;

//namespace Mirabel.CallCenter.DAL
//{
//    public class DBContext<T> where T: BaseEntity
//    {

//        public readonly IMongoClient mongoClient;
//        public readonly string DataBaseName;

//        public DBContext()
//        {
//            var configuration = Configuration.GetConfiguration();
//            var connectionstring = configuration["connectionstring"];
//            DataBaseName = configuration["DataBaseName"];
//            mongoClient = new MongoClient(connectionstring);
//        }

//        private IMongoDatabase GetDatabase()
//        {
//          return  mongoClient.GetDatabase(DataBaseName);
//        }

//        public IMongoCollection<T> GetCollection()
//        {
//            return GetDatabase().GetCollection<T>(nameof(T));
//        }

//        public string SaveCollection(T data)
//        {
//            data._ID = Guid.NewGuid().ToString();
//            GetCollection().InsertOne(data);
//            return data._ID;
//        }

//        public T GetCollectionByID(string ID)
//        {
//            return GetCollection().Find(x => x._ID == ID).FirstOrDefault();
//        }


















//    }
//}
