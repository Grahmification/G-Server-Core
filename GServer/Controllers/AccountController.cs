using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using GServer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using GServer.Data;
using System.Collections.Generic;

namespace GServer.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly ILogger<AccountController> _logger;
        protected ApplicationDbContext _Context; // The scoped Application context
        protected UserManager<ApplicationUser> _UserManager; // The manager for handling user creation, deletion, searching, roles etc...
        protected SignInManager<ApplicationUser> _SignInManager; // The manager for handling signing in and out for our users

        //injected objects to controller
        public AccountController(ILogger<AccountController> logger, ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _logger = logger;
            _Context = context;
            _UserManager = userManager;
            _SignInManager = signInManager;

            //SetupDefaultUser(); //this probably isn't the best place to call this. Ideally call it once at app startup
        }

        //----------------------------------- Logging in/out -----------------------------------

        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {

                var user = await _UserManager.FindByNameAsync(model.UserName);
                if (user != null)
                {
                    var result = await SignInAsync(user, model.Password, model.RememberMe);
                    
                    if (result.Succeeded)
                        return RedirectToLocal(returnUrl); //success
                    else if(result.IsLockedOut)
                        ModelState.AddModelError("", "Your account is temporarily locked. Please try again later.");
                    else
                        ModelState.AddModelError("", "Invalid login attempt.");
                }
                else
                {
                    ModelState.AddModelError("", "Invalid username or password.");
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogOff()
        {
            await _SignInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        //----------------------------------- Registration -----------------------------------

        // GET: /Account/Register
        [Authorize(Roles = RoleTypes.Admin)] //only allow admin to create accounts
        public ActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [Authorize(Roles = RoleTypes.Admin)] //only allow admin to create accounts
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser() { UserName = model.UserName };
                var result = await _UserManager.CreateAsync(new ApplicationUser
                {
                    UserName = model.UserName
                }, model.Password);

                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    AddErrors(result);
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //----------------------------------- User Self Management -----------------------------------

        // POST: /Account/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete()
        {
            await _SignInManager.SignOutAsync();
            await _UserManager.DeleteAsync(_UserManager.GetUserAsync(User).Result);
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Manage
        public ActionResult Manage(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                message == ManageMessageId.ChangePasswordSuccess ? "Your password has been changed."
                : message == ManageMessageId.SetPasswordSuccess ? "Your password has been set."
                : message == ManageMessageId.RemoveLoginSuccess ? "The external login was removed."
                : message == ManageMessageId.Error ? "An error has occurred."
                : "";
            ViewBag.ReturnUrl = Url.Action("Manage");
            ViewBag.HasLocalPassword = HasPassword().Result;
            return View();
        }

        // POST: /Account/Manage
        [HttpPost]
        [ValidateAntiForgeryToken] //needed anywhere when a form past login has a submit button
        public async Task<ActionResult> Manage(ManageUserViewModel model)
        {
            bool hasPassword = HasPassword().Result;
            ViewBag.HasLocalPassword = hasPassword;
            ViewBag.ReturnUrl = Url.Action("Manage");

            var user = _UserManager.GetUserAsync(User).Result;

            if (hasPassword)
            {
                if (ModelState.IsValid)
                {
                    IdentityResult result = await _UserManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);

                    if (result.Succeeded)
                    {
                        return RedirectToAction("Manage", new { Message = ManageMessageId.ChangePasswordSuccess });
                    }
                    else
                    {
                        AddErrors(result);
                    }
                }
            }
            else
            {

                // User does not have a password so remove any validation errors caused by a missing OldPassword field               
                var state = ModelState["OldPassword"];
                if (state != null)
                {
                    state.Errors.Clear();
                }

                if (ModelState.IsValid)
                {
                    IdentityResult result = await _UserManager.AddPasswordAsync(user, model.NewPassword);


                    if (result.Succeeded)
                    {
                        return RedirectToAction("Manage", new { Message = ManageMessageId.SetPasswordSuccess });
                    }
                    else
                    {
                        AddErrors(result);
                    }
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //-----------------------------------Admin Multi User Management -----------------------------------

        // GET: /Account/ManageAccounts
        [Authorize(Roles = RoleTypes.Admin)] //only allow admin to view all accounts
        public ActionResult ManageAccounts()
        {
            var VModel = new ManageAccountsViewModel(); //viewmodel to be used in form
            VModel.Users = _Context.Users.ToList(); //get list of users to fill model
           
            return View(VModel); 
        }

        public async Task<ActionResult> ManageUserAdminOLD(string userName)
        {
            var user = await _UserManager.FindByNameAsync(userName);
            return View(user);
        }

        // GET: /Account/ManageUserAdmin
        [Authorize(Roles = RoleTypes.Admin)]
        public ActionResult ManageUserAdmin(string userName)
        {
            var model = new ManageUserAdminViewModel(); //viewmodel to be used in form
            model.UserName = userName;
            ViewBag.UserName = userName;

            var usr = _UserManager.FindByNameAsync(userName).Result;

            var roles = _Context.Roles.ToList();
            
            for(int i = 0; i < roles.Count; i++)
            {
                string roleName = roles[i].Name;
                model.UserRoles.Add(roleName, _UserManager.IsInRoleAsync(usr, roleName).Result);          
            }

            return View(model); //return view, with data stored in viewResult.ViewData.Model
        }

        // POST: /Account/ManageUserAdmin
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleTypes.Admin)]
        public async Task<ActionResult> ManageUserAdmin(ManageUserAdminViewModel model)
        {
            List<string> AddRoles = new List<string>();
            List<string> DeleteRoles = new List<string>();

            var usr = _UserManager.FindByNameAsync(model.UserName).Result;

            foreach (string roleName in model.UserRoles.Keys)
            {
                if (model.UserRoles[roleName]) 
                { AddRoles.Add(roleName); } //if true we want the user to have this role
                else { DeleteRoles.Add(roleName); }
            }

            await _UserManager.AddToRolesAsync(usr, AddRoles); //assign role
            await _UserManager.RemoveFromRolesAsync(usr, DeleteRoles); //remove other roles

            return RedirectToAction("ManageAccounts", "Account");
        }



        // POST: /Account/DeleteUserAdmin
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleTypes.Admin)]
        public async Task<IActionResult> DeleteUserAdmin(string userName)
        {
            var usr = _UserManager.FindByNameAsync(userName).Result;          
            await _UserManager.DeleteAsync(usr);
            return RedirectToAction("ManageAccounts", "Account");
        }


  

        //----------------------------------- Helpers -----------------------------------
        
            //need to turn lockout on here
        private async Task<Microsoft.AspNetCore.Identity.SignInResult> SignInAsync(ApplicationUser user, string passWord, bool isPersistent)
        {
            // Sign out any previous sessions
            await _SignInManager.SignOutAsync();
            // Sign user in with the valid credentials
            var result = await _SignInManager.PasswordSignInAsync(user.UserName, passWord, isPersistent, true);
            return result;
        }
        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }
        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.ToString());
            }
        }
        private async Task<bool> HasPassword()
        {
            var user = await _UserManager.FindByNameAsync(User.Identity.Name);

            if (user != null)
            {
                return user.PasswordHash != null;
            }
            return false;
        } //checks if current user has a locally stored password
        public enum ManageMessageId
        {
            ChangePasswordSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            Error
        }

        private async Task SetupDefaultUser()
        {
            string defaultName = "Graham";
            string defaultPW = "123456";

            if (_UserManager.Users.Count() == 0) //no users exist
            {
                //------------------------- Register Default User --------------------------------

                var user = new ApplicationUser() { UserName = defaultName };
                var result = await _UserManager.CreateAsync(new ApplicationUser
                {
                    UserName = defaultName
                }, defaultPW);

                //------------------------- Give Default User Admin Role --------------------------------

                if (result.Succeeded)
                {
                    var usr = await _UserManager.FindByNameAsync(defaultName);
                    await _UserManager.AddToRoleAsync(usr, RoleTypes.Admin);
                }
            }
        }

    }
}