namespace ConsoleApp.Deserializers
{
    public interface IDeserializer<T>
    {
        static abstract T Deserialize(string data);
    }
}