namespace OnlineVotingSystem.Models
{

    public class ResultView
    {
        public int VotingRoundId { get; set; }
        public string Question { get; set; }
        public List<string> PossibleAnswers { get; set; }
        public List<int> Votes { get; set; }
    }

}