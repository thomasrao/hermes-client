namespace TwitchChatTTS.Hermes
{
    public interface ICustomDataManager {
        void Add(string key, object value, string type);
        void Change(string key, object value);
        void Delete(string key);
        object? Get(string key);
    }

    public class CustomDataManager : ICustomDataManager
    {
        private IDictionary<string, DataInfo> _data;

        public CustomDataManager() {
            _data = new Dictionary<string, DataInfo>();
        }


        public void Add(string key, object value, string type)
        {
            throw new NotImplementedException();
        }

        public void Change(string key, object value)
        {
            throw new NotImplementedException();
        }

        public void Delete(string key)
        {
            throw new NotImplementedException();
        }

        public object? Get(string key)
        {
            throw new NotImplementedException();
        }
    }

// type: text (string), whole number (int), number (double), boolean, formula (string, data type of number)
    public struct DataInfo {
        public string Id { get; set; }
        public string Type { get; set; }
        public object Value { get; set; }
    }
}