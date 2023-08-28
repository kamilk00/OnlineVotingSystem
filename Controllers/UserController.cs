using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using OnlineVotingSystem.Models;

namespace OnlineVotingSystem.Controllers
{

    [Authorize(Roles = "User")]
    public class UserController : Controller
    {

        private readonly IConfiguration _configuration;
        private readonly IMongoCollection<User> userCollection;
        private readonly IMongoCollection<VotingRound> votingRoundCollection;


        public UserController(IConfiguration configuration)
        {

            _configuration = configuration;
            var connectionString = _configuration.GetConnectionString("MongoDB");
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerApi = new ServerApi(ServerApiVersion.V1);
            var db = new MongoClient(settings);
            var database = db.GetDatabase("OVS");
            userCollection = database.GetCollection<User>("users");
            votingRoundCollection = database.GetCollection<VotingRound>("votingRounds");

        }


        public IActionResult Index()
        {

            var username = User.Identity.Name;

            //retrieve the admin from the database
            var filter = Builders<User>.Filter.Eq("Username", username);
            var user = userCollection.Find(filter).FirstOrDefault();

            //get the current, future, and previous voting rounds assigned to the admin
            var currentRounds = votingRoundCollection.Find(v => v.StartDate <= DateTime.Now && v.EndDate >= DateTime.Now && v.UserCanVote[user._id]).ToList();
            var futureRounds = votingRoundCollection.Find(v => v.StartDate > DateTime.Now && v.UserCanVote[user._id]).ToList();
            var previousRounds = votingRoundCollection.Find(v => v.EndDate < DateTime.Now && v.UserCanVote[user._id]).ToList();

            //create the model for the index view
            var model = new IndexView
            {
                CurrentRounds = currentRounds,
                FutureRounds = futureRounds,
                PreviousRounds = previousRounds
            };

            return View(model);

        }


        public ActionResult ChangePassword()
        {
            return View();
        }


        [HttpPost]
        public ActionResult ChangePassword(ChangePasswordView model)
        {

            if (ModelState.IsValid)
            {

                //retrieve the user from the claims
                var username = User.Identity.Name;

                var filter = Builders<User>.Filter.Eq("Username", username);
                var admin = userCollection.Find(filter).FirstOrDefault();

                //check if the current password is correct
                var hashedCurrentPassword = Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(model.currentPassword)));

                if (hashedCurrentPassword == admin.Password)
                {

                    //check if the new password and confirm new password match
                    if (model.newPassword == model.confirmNewPassword)
                    {

                        //update the user's password with the new password
                        var hashedNewPassword = Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(model.newPassword)));
                        admin.Password = hashedNewPassword;

                        //update the user's password in the database
                        var updateFilter = Builders<User>.Filter.Eq("_id", admin._id);
                        var update = Builders<User>.Update.Set("Password", admin.Password);
                        userCollection.UpdateOne(updateFilter, update);

                        //show success message
                        ViewBag.Message = "Password changed successfully.";

                    }

                    else
                        ModelState.AddModelError("ConfirmNewPassword", "The new password and confirm password do not match.");

                }

                else
                    ModelState.AddModelError("CurrentPassword", "Incorrect current password.");

            }

            return View(model);

        }

        [HttpGet]
        public IActionResult Vote(int id)
        {

            var votingRound = votingRoundCollection.Find(v => v._id == id).FirstOrDefault();

            if (votingRound == null)
                return NotFound();

            var username = User.Identity.Name;

            //retrieve the admin from the database
            var filter = Builders<User>.Filter.Eq("Username", username);
            var user = userCollection.Find(filter).FirstOrDefault();

            if (!votingRound.UserCanVote[user._id])
            {

                ViewBag.Message = "You are not allowed to vote in this round.";
                return View("VotingNotAllowed");

            }

            if (votingRound.UserVoted[user._id])
            {

                ViewBag.Message = "You have already voted in this round.";
                return View("VotingNotAllowed");

            }

            var model = new VoteView
            {
                VotingRoundId = id,
                PossibleAnswers = votingRound.PossibleAnswers
            };

            return View(model);

        }


        [HttpPost]
        public IActionResult Vote(VoteView model)
        {

            var votingRound = votingRoundCollection.Find(v => v._id == model.VotingRoundId).FirstOrDefault();

            if (votingRound == null)
                return NotFound();

            var username = User.Identity.Name;

            //retrieve the user from the database
            var filter = Builders<User>.Filter.Eq("Username", username);
            var user = userCollection.Find(filter).FirstOrDefault();

            if (!votingRound.UserCanVote[user._id] || votingRound.UserVoted[user._id])
            {

                ViewBag.Message = "You are not allowed to vote in this round.";
                return View("VotingNotAllowed");

            }

            //update the UserVoted and Votes properties
            votingRound.UserVoted[user._id] = true;
            votingRound.Votes[model.SelectedAnswerIndex]++;

            //update the voting round in the database
            var votingRoundFilter = Builders<VotingRound>.Filter.Eq("_id", votingRound._id);
            var update = Builders<VotingRound>.Update
                .Set("UserVoted." + user._id, true)
                .Set("Votes." + model.SelectedAnswerIndex, votingRound.Votes[model.SelectedAnswerIndex]);
            votingRoundCollection.UpdateOne(votingRoundFilter, update);

            ViewBag.Message = "Vote submitted successfully.";
            return View("VotingSuccessfull");

        }


        [HttpGet]
        public IActionResult Results(int id)
        {

            var votingRound = votingRoundCollection.Find(v => v._id == id).FirstOrDefault();

            if (votingRound == null)
                return NotFound();

            var model = new ResultView
            {
                VotingRoundId = id,
                Question = votingRound.Question,
                PossibleAnswers = votingRound.PossibleAnswers,
                Votes = votingRound.Votes
            };

            return View(model);

        }

    }

}