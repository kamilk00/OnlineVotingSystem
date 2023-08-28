using System.ComponentModel.DataAnnotations;

namespace OnlineVotingSystem.Models
{

    public class VoteView
    {

        public int VotingRoundId { get; set; }
        public List<string> PossibleAnswers { get; set; }

        [Required(ErrorMessage = "Please select an answer.")]
        public int SelectedAnswerIndex { get; set; }

    }

}