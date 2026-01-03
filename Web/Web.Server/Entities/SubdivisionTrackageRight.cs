using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Server.Entities
{
    public class SubdivisionTrackageRight
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        /// <summary>
        /// The subdivision that has trackage rights (primary/owning subdivision).
        /// </summary>
        [Required]
        public int FromSubdivisionID { get; set; }

        /// <summary>
        /// The subdivision that grants trackage rights (target subdivision on another railroad).
        /// </summary>
        [Required]
        public int ToSubdivisionID { get; set; }

        // Navigation properties
        public Subdivision? FromSubdivision { get; set; }
        public Subdivision? ToSubdivision { get; set; }
    }
}
