using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using OnlineVotingSystem.Models;

namespace OnlineVotingSystem.Controllers
{

    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {

        private readonly IConfiguration _configuration;
        private readonly IMongoCollection<User> userCollection;
        private readonly IMongoCollection<VotingRound> votingRoundCollection;


        public AdminController(IConfiguration configuration)
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
            var admin = userCollection.Find(filter).FirstOrDefault();

            //get the current, future, and previous voting rounds assigned to the admin
            var currentRounds = votingRoundCollection.Find(v => v.StartDate <= DateTime.Now && v.EndDate >= DateTime.Now && v.UserCanVote[admin._id]).ToList();
            var futureRounds = votingRoundCollection.Find(v => v.StartDate > DateTime.Now).ToList();
            var previousRounds = votingRoundCollection.Find(v => v.EndDate < DateTime.Now).ToList();

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
                //retrieve the admin from the claims
                var username = User.Identity.Name;

                //retrieve the admin from the database
                var filter = Builders<User>.Filter.Eq("Username", username);
                var admin = userCollection.Find(filter).FirstOrDefault();

                //check if the current password is correct
                var hashedCurrentPassword = Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(model.currentPassword)));

                if (hashedCurrentPassword == admin.Password)
                {

                    //check if the new password and confirm new password match
                    if (model.newPassword == model.confirmNewPassword)
                    {
                        
                        //update the admin's password with the new password
                        var hashedNewPassword = Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(model.newPassword)));
                        admin.Password = hashedNewPassword;

                        //update the admin's password in the database
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


        public ActionResult AddUser()
        {
            return View();
        }


        [HttpPost]
        public ActionResult AddUser(User user)
        {

            if (ModelState.IsValid)
            {
                
                User existingUser = userCollection.Find(u => u.Username == user.Username).FirstOrDefault();

                if (existingUser != null)
                {

                    ModelState.AddModelError("Username", "Username already exists. Please choose a different username.");
                    return View(user);

                }

                //generate a new _id for the user
                user._id = GenerateNewUserId();

                //hash the password with the use of SHA256
                user.Password = Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(user.Password)));

                //add the new user to the database
                userCollection.InsertOne(user);

                var filter = Builders<VotingRound>.Filter.Empty;
                var update = Builders<VotingRound>.Update.PushEach("UserVoted", new bool[] { false }).Set("UserCanVote", new List<bool>(Enumerable.Repeat(false, GetMaxUserId() + 1)));
                votingRoundCollection.UpdateMany(filter, update);

                //show success message
                ViewBag.Message = "User added successfully.";
                return RedirectToAction("Index");

            }

            return View(user);

        }


        private int GenerateNewUserId()
        {

            var users = userCollection.AsQueryable().ToList();

            //find the maximum used ID
            int maxUsedId = users.Count > 0 ? users.Max(u => u._id) : 0;

            int newId = maxUsedId + 1;

            return newId;

        }


        public ActionResult ManageUsers()
        {

            var users = userCollection.Find(u => u.Role == "User" || u.Role == "Admin").ToList();
            return View(users);

        }


        public ActionResult EditUser(int id)
        {

            var user = userCollection.Find(u => u._id == id).FirstOrDefault();
            if (user == null)
                return NotFound();

            return View(user);

        }
        

        [HttpPost]
        public ActionResult EditUser(User user)
        {

            if (ModelState.IsValid)
            {

                //update the user's properties in the database
                userCollection.ReplaceOne(u => u._id == user._id, user);
                ViewBag.Message = "User updated successfully.";
                
                return RedirectToAction("ManageUsers");

            }

            return View(user);

        }


        [HttpPost]
        public ActionResult DeleteUser(int id)
        {

            if (id == GetMaxUserId())
            {
                
                var filter = Builders<VotingRound>.Filter.Empty;
                var update = Builders<VotingRound>.Update.Pull("UserVoted", true).Pull("UserCanVote", true);
                votingRoundCollection.UpdateMany(filter, update);

            }

            var result = userCollection.DeleteOne(u => u._id == id);
            if (result.DeletedCount > 0)
                ViewBag.Message = "User deleted successfully.";

            return RedirectToAction("ManageUsers");

        }

        public ActionResult CreateVotingRound()
        {

            var model = new VotingRound();
            ViewBag.Users = userCollection.Find(_ => true).ToList();
            return View(model);

        }


        [HttpPost]
        public ActionResult CreateVotingRound(VotingRound model, List<int> UserCanVote)
        {
                        
            //generate the new _id for the voting round
            model._id = GenerateNewVotingRoundId();

            if (model.StartDate < DateTime.Now)
            {

                ModelState.AddModelError("StartDate", "The start date cannot be lower than the current date.");
                ViewBag.Users = userCollection.Find(_ => true).ToList();
                ViewBag.Message = "Voting round cannot be created successfully.";
                return View(model);

            }

            if (model.EndDate < model.StartDate)
            {

                ModelState.AddModelError("EndDate", "The end date cannot be lower than the start date.");
                ViewBag.Users = userCollection.Find(_ => true).ToList();
                ViewBag.Message = "Voting round cannot be created successfully.";
                return View(model);

            }

            //set the initial values for UserCanVote, UserVoted, and Votes
            int maxUserId = GetMaxUserId();

            var users = userCollection.Find(_ => true).ToList();

            //initialize the UserCanVote list with default values of false
            model.UserCanVote = Enumerable.Repeat(false, maxUserId + 1).ToList();

            //update the UserCanVote values based on the selected users
            foreach (var userId in UserCanVote)
            {

                var userIndex = users.FindIndex(u => u._id == userId);
                model.UserCanVote[userIndex] = true;

            }

            model.UserVoted = Enumerable.Repeat(false, maxUserId + 1).ToList();
            model.Votes = Enumerable.Repeat(0, model.PossibleAnswers.Count).ToList();


            if (model.UserVoted.Count > 0 && model.UserCanVote.Count > 0 && model.PossibleAnswers.Count > 0)
            {

                //save the new voting round to the database
                votingRoundCollection.InsertOne(model);

                //show success message
                ViewBag.Message = "Voting round created successfully.";

                return RedirectToAction("Index");

            }

            ViewBag.Users = userCollection.Find(_ => true).ToList();
            ViewBag.Message = "Voting round cannot be created successfully.";
            return View(model);

        }


        private int GenerateNewVotingRoundId()
        {

            var votingRounds = votingRoundCollection.AsQueryable().ToList();

            //find the lowest positive number that isn't already used as an _id
            int newId = 1;
            while (votingRounds.Any(v => v._id == newId))
                newId++;

            return newId;

        }


        private int GetMaxUserId()
        {

            var users = userCollection.Find(_ => true).ToList();

            //find the maximum _id value from the users collection
            if (users.Count > 0)
                return users.Max(u => u._id);
            else
                return 0;

        }


        public ActionResult ManageVotingRounds()
        {

            var votingRounds = votingRoundCollection.Find(v => v.StartDate > DateTime.Now).ToList();
            return View(votingRounds);

        }


        public ActionResult EditVotingRound(int id)
        {

            var votingRound = votingRoundCollection.Find(v => v._id == id).FirstOrDefault();
            if (votingRound == null)
                return NotFound();

            ViewBag.Users = userCollection.Find(_ => true).ToList();
            return View(votingRound);

        }

        [HttpPost]
        public ActionResult EditVotingRound(VotingRound votingRound, List<int> UserCanVote)
        {

            if (votingRound.StartDate < DateTime.Now)
            {

                ModelState.AddModelError("StartDate", "The start date cannot be lower than the current date.");
                ViewBag.Users = userCollection.Find(_ => true).ToList();
                ViewBag.Message = "Voting round cannot be updated successfully.";
                return View(votingRound);

            }

            if (votingRound.EndDate < votingRound.StartDate)
            {

                ModelState.AddModelError("EndDate", "The end date cannot be lower than the start date.");
                ViewBag.Users = userCollection.Find(_ => true).ToList();
                ViewBag.Message = "Voting round cannot be updated successfully.";
                return View(votingRound);

            }

            //set the initial values for UserCanVote, UserVoted, and Votes
            int maxUserId = GetMaxUserId();

            var users = userCollection.Find(_ => true).ToList();

            //initialize the UserCanVote list with default values of false
            votingRound.UserCanVote = Enumerable.Repeat(false, users.Count).ToList();

            //update the UserCanVote values based on the selected users
            foreach (var userId in UserCanVote)
            {

                var userIndex = users.FindIndex(u => u._id == userId);
                if (userIndex >= 0)
                    votingRound.UserCanVote[userIndex] = true;

            }

            votingRound.UserVoted = Enumerable.Repeat(false, maxUserId + 1).ToList();
            votingRound.Votes = Enumerable.Repeat(0, votingRound.PossibleAnswers.Count).ToList();


            if (votingRound.UserVoted.Count > 0 && votingRound.UserCanVote.Count > 0 && votingRound.PossibleAnswers.Count > 0)
            {

                //save the new voting round to the database
                votingRoundCollection.ReplaceOne(v => v._id == votingRound._id, votingRound);

                //show success message
                ViewBag.Message = "Voting round updated successfully.";

                return RedirectToAction("Index");

            }

            ViewBag.Users = userCollection.Find(_ => true).ToList();
            ViewBag.Message = "Voting round cannot be updated successfully.";
            return View(votingRound);

        }


        [HttpPost]
        public ActionResult DeleteVotingRound(int id)
        {

            var result = votingRoundCollection.DeleteOne(v => v._id == id);
            if (result.DeletedCount > 0)
                ViewBag.Message = "Voting Round deleted successfully.";

            return RedirectToAction("ManageVotingRounds");

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
            var admin = userCollection.Find(filter).FirstOrDefault();

            if (!votingRound.UserCanVote[admin._id])
            {

                ViewBag.Message = "You are not allowed to vote in this round.";
                return View("VotingNotAllowed");

            }

            if (votingRound.UserVoted[admin._id])
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

            //retrieve the admin from the database
            var filter = Builders<User>.Filter.Eq("Username", username);
            var admin = userCollection.Find(filter).FirstOrDefault();

            if (!votingRound.UserCanVote[admin._id] || votingRound.UserVoted[admin._id])
            {

                ViewBag.Message = "You are not allowed to vote in this round.";
                return View("VotingNotAllowed");

            }

            //update the UserVoted and Votes properties
            votingRound.UserVoted[admin._id] = true;
            votingRound.Votes[model.SelectedAnswerIndex]++;

            //update the voting round in the database
            var votingRoundFilter = Builders<VotingRound>.Filter.Eq("_id", votingRound._id);
            var update = Builders<VotingRound>.Update
                .Set("UserVoted." + admin._id, true)
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