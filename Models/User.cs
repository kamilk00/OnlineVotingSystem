using System.ComponentModel.DataAnnotations;

namespace OnlineVotingSystem.Models
{
    public class User
    {

        [Required]
        public int _id { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        public string Role { get; set; }

    }

}