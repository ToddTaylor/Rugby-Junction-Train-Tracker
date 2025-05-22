namespace Web.Server.Entities
{
    public class EntityBase
    {
        /// <summary>
        /// The date and time when the entity was created.
        /// </summary>
        /// <remarks>
        /// The 'required' attribute was omitted because DateTime is already not nullable and
        /// this property is automatically being set to the current UTC time.
        /// </remarks>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// The date and time when the entity was last updated.
        /// </summary>
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }
}