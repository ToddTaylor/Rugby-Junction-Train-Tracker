using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class PassengerMapPin : EntityBase
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        [Required]
        [StringLength(50)]
        public required string Provider { get; set; }

        [Required]
        [StringLength(100)]
        public required string RouteName { get; set; }

        [Required]
        [StringLength(10)]
        public required string TrainNum { get; set; }

        [Required]
        [StringLength(20)]
        public required string TrainId { get; set; }

        [Required]
        [StringLength(20)]
        public required string Heading { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public int Velocity { get; set; }

        public DateTime UpdatedAt { get; set; }

        public bool IsStale { get; set; }
    }
}