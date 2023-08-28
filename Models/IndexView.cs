namespace OnlineVotingSystem.Models
{

    public class IndexView
    {

        public List<VotingRound> CurrentRounds { get; set; }
        public List<VotingRound> FutureRounds { get; set; }
        public List<VotingRound> PreviousRounds { get; set; }

    }

}