namespace Web.Server.Models
{
    public class MessageEnvelope<T>
    {
        public T Data { get; set; }
        public List<string> Errors { get; set; }

        public MessageEnvelope(T data, List<string> errors)
        {
            Data = data;
            Errors = errors;
        }
    }
}
