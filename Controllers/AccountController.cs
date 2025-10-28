using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using PersonalSchedulingAssistant.Data;
using PersonalSchedulingAssistant.Models;
using PersonalSchedulingAssistant.Models.ViewModels;

namespace PersonalSchedulingAssistant.Controllers
{
    public class AccountController : Controller
    {

        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AccountController> _logger;
        private readonly IEmailSender emailSender;

        public AccountController(UserManager<User> userManager,
        SignInManager<User> signInManager, RoleManager<IdentityRole> roleManager, ILogger<AccountController> logger, IEmailSender _emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _logger = logger;
            emailSender = _emailSender;
        }

        [AllowAnonymous]
        public IActionResult Login(string returnUrl = "/")
        {
            return View(new LoginViewModel
            {
                ReturnUrl = returnUrl
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel loginModel)
        {
            if (ModelState.IsValid)
            {
                User user =
                await _userManager.FindByEmailAsync(loginModel.Email);
                if (user != null)
                {
                    if (!user.EmailConfirmed)
                    {
                        return RedirectToAction("EmailNotConfirmed", new { email = user.Email });
                    }

                    //Check if user is approved
                    if (!user.Approved)
                    {
                        ModelState.AddModelError("", "Your account has not been approved by an administrator yet.");
                        return View(loginModel);
                    }

                    var result = await _signInManager.PasswordSignInAsync(user,
                        loginModel.Password, isPersistent: loginModel.RememberMe, false);
                    if (result.Succeeded)
                    {
                        var roles = await _userManager.GetRolesAsync(user);

                        // Redirect based on role
                        if (roles.Contains("Admin"))
                            return RedirectToAction("Index", "Admin");
                        else if (roles.Contains("Lecturer"))
                            return RedirectToAction("Index", "Lecturer");
                        else if (roles.Contains("Demmie"))
                            return RedirectToAction("Index", "Demmie");
                        else
                            return RedirectToAction("Index", "Home");
                    }
                }
            }
            ModelState.AddModelError("", "Invalid username or password");
            return View(loginModel);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterViewModel registerModel)
        {
            if (!ModelState.IsValid)
                return View(registerModel);

            var role = registerModel.Role;

            // Ensure the selected role exists
            if (await _roleManager.FindByNameAsync(role) == null)
                await _roleManager.CreateAsync(new IdentityRole(role));

            // Create the Identity user
            var user = new User
            {
                UserName = registerModel.Email,
                Email = registerModel.Email,
                FirstName = registerModel.FirstName,
                SecondName = registerModel.LastName,
                Title = registerModel.Title,
                Department = registerModel.Department,
                Approved = false
            };

            var result = await _userManager.CreateAsync(user, registerModel.Password);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Unable to register new user");
                return View(registerModel);
            }

            // Assign role
            await _userManager.AddToRoleAsync(user, role);

            // Add to corresponding domain entity
            using (var scope = HttpContext.RequestServices.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                switch (role)
                {
                    case "Lecturer":
                        db.Lecturers.Add(new Lecturer
                        {
                            UserId = user.Id,
                            FirstName = user.FirstName,
                            LastName = user.SecondName
                        });
                        break;

                    case "Demmie":
                        db.Demmies.Add(new Demmie
                        {
                            UserId = user.Id,
                            FirstName = user.FirstName,
                            LastName = user.SecondName,
                            IsAssigned = false,
                            WeeklyHourLimit = 10,
                            AssignedDate = DateTime.Now
                        });
                        break;
                }

                await db.SaveChangesAsync();
            }

            // Send email confirmation
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Action(
                "ConfirmEmail",
                "Account",
                new { userId = user.Id, token },
                Request.Scheme);

            await emailSender.SendEmailAsync(
                user.Email,
                "Confirm your email",
                @$"
                <h3>Welcome to the Personal Scheduling Assistant</h3>
                <p>To complete your registration, please confirm your email address by clicking below:</p>
                <a href='{confirmationLink}' 
                   style='background-color:#007bff;color:white;
                   padding:10px 15px;text-decoration:none;
                   border-radius:5px;'>Confirm Email</a>
                <p>If the button doesn't work, copy this link: <a href='{confirmationLink}'>{confirmationLink}</a></p>"
            );

            TempData["Message"] = "A confirmation email has been sent to your inbox.";
            return RedirectToAction("EmailNotConfirmed", new { email = user.Email });
        }


        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null)
            {
                return RedirectToAction("Index", "Home");
            }
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{userId}'.");
            }
            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                return View("ConfirmEmail");
            }
            else
            {
                return NotFound($"Error confirming email for user with ID '{userId}':");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
        [AllowAnonymous]
        public IActionResult EmailNotConfirmed(string email)
        {
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResendConfirmationEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login");

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || user.EmailConfirmed)
                return RedirectToAction("Login");

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Action("ConfirmEmail", "Account",
                new { userId = user.Id, token = token }, Request.Scheme);

            await emailSender.SendEmailAsync(
                user.Email,
                "Confirm your email",
                @$"
            <h3>Email Confirmation</h3>
            <p>Please confirm your account by clicking below:</p>
            <a href='{confirmationLink}'
               style='background-color:#007bff;color:white;
                      padding:10px 15px;text-decoration:none;
                      border-radius:5px;'>
                Confirm Email
            </a>
            <p>If the button doesn’t work, copy and paste this link:</p>
            <p><a href='{confirmationLink}'>{confirmationLink}</a></p>
        ");

            TempData["Message"] = "A new confirmation email has been sent.";
            return RedirectToAction("EmailNotConfirmed", new { email });
        }

        // Forgot Password
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                TempData["Info"] = "If an account with that email exists, a reset link has been sent.";
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = Url.Action("ResetPassword", "Account",
                new { token, email = user.Email }, Request.Scheme);

            string emailBody = $@"
        <h3>Password Reset Request</h3>
        <p>Hi {user.FirstName},</p>
        <p>You requested to reset your password. Click the button below to proceed:</p>
        <a href='{resetLink}' 
           style='background-color:#28a745;color:white;
                  padding:10px 15px;text-decoration:none;
                  border-radius:5px;'>Reset Password</a>
        <p>If this wasn't you, you can safely ignore this email.</p>
        <p> If the button doesn’t work, copy and paste this link:</p>
        <p><a href = '{resetLink}' >{resetLink}</a></p>";

            await emailSender.SendEmailAsync(user.Email, "Reset Your Password", emailBody);

            TempData["Success"] = "Password reset email has been sent.";
            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        // Reset Password
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string token, string email)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
                return RedirectToAction("Index", "Home");

            return View(new ResetPasswordViewModel { Token = token, Email = email });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return RedirectToAction(nameof(ResetPasswordConfirmation));

            var result = await _userManager.ResetPasswordAsync(user, model.Token!, model.Password);
            if (result.Succeeded)
            {
                TempData["Success"] = "Password has been reset successfully.";
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

    }
}
