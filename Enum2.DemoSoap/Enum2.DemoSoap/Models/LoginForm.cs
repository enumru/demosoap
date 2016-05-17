using System.ComponentModel.DataAnnotations;

namespace Enum2.DemoSoap.Models
{
    public class LoginForm
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }  
    }
}