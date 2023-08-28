using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using OnlineVotingSystem.Models;
using System.Security.Claims;

namespace OnlineVotingSystem.Controllers
{

    public class AccountController : Controller
    {

        private readonly IConfiguration _configuration;

        public AccountController(IConfiguration configuration) 
        {
            _configuration = configuration;           
        }

        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> Login(Login login)
        {

            if (ModelState.IsValid) // check if the user input is valid
            {

                //connect to database
                string connectionString = _configuration.GetConnectionString("MongoDB");

                var settings = MongoClientSettings.FromConnectionString(connectionString);
                settings.ServerApi = new ServerApi(ServerApiVersion.V1);
                var db = new MongoClient(settings);
                var database = db.GetDatabase("OVS");
                var collection = database.GetCollection<User>("users");
                var filter = Builders<User>.Filter.Eq("Username", login.Username);
                var result = collection.Find(filter).FirstOrDefault();

                if (result != null) // if the user exists in the database
                {

                    //compare the hashed passwords using SHA256
                    var hashedPassword = Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(login.Password)));
                    
                    if (hashedPassword == result.Password) //if the password is correct
                    {
                        
                        //create a list of claims with the user name and role
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, result.Username),
                            new Claim(ClaimTypes.Role, result.Role)
                        };

                        //create a claims identity with the cookie authentication scheme
                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                        //create an authentication properties object with the expiration time of 20 minutes
                        var authProperties = new AuthenticationProperties
                        {
                            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(20)
                        };

                        //sign in the user with the claims identity and the authentication properties
                        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

                        //redirect the user to the main page according to their role
                        if (result.Role == "Admin")
                            return RedirectToAction("Index", "Admin");

                        else if (result.Role == "User")
                            return RedirectToAction("Index", "User");

                    }

                    else //if the password is incorrect
                        ModelState.AddModelError("", "Invalid password");
                
                }

                else //if the user doesn't exist in the database - login is incorrect
                    ModelState.AddModelError("", "Invalid username");

            }

            return View(login); //return the same view with error messages

        }


        public async Task<ActionResult> Logout()
        {

            // sign out the user and clear the cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account"); // redirect to the login page

        }


        public IActionResult NotFound(int statusCode)
        {

            Response.StatusCode = statusCode;
            return View("NotFound");

        }


        public IActionResult AccessDenied(int statusCode)
        {

            Response.StatusCode = statusCode;
            return View("AccessDenied");

        }

    }

}