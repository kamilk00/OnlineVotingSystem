using System.ComponentModel.DataAnnotations;

namespace OnlineVotingSystem.Models
{

    public class ChangePasswordView
    {

        [Required]
        public string currentPassword { get; set; }

        [Required]
        public string newPassword { get; set; }

        [Required]
        [Compare("newPassword")]
        public string confirmNewPassword { get; set; }

    }

}