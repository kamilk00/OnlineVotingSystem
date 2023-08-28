using System.ComponentModel.DataAnnotations;

namespace OnlineVotingSystem.Models
{

    public class VotingRound
    {

        [Required]
        public int _id { get; set; }
        [Required]
        public string Question { get; set; }
        [Required]
        public DateTime StartDate { get; set; }
        [Required]
        public DateTime EndDate { get; set; }
        [Required]
        public List<string> PossibleAnswers { get; set; }
        [Required]
        public List<bool> UserCanVote { get; set; }
        public List<bool> UserVoted { get; set; }
        public List<int> Votes { get; set; }

        public VotingRound()
        {

            _id = new int();
            Question = string.Empty;
            StartDate = DateTime.Now;
            EndDate = DateTime.Now;
            PossibleAnswers = new List<string>();
            UserCanVote = new List<bool>();
            UserVoted = new List<bool>();
            Votes = new List<int>();

        }

    }

}