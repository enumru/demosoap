using System.ComponentModel.DataAnnotations;

namespace Enum2.DemoSoap.Models
{
    public class Login2Form
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Challenge { get; set; }

        [Required]
        public string Response { get; set; }

        public string QrUrl { get; set; } 
    }
}