using Microsoft.AspNetCore.Identity;

namespace GServer.Data
{
    /// <summary>
    /// The user data and profile for our application
    /// </summary>
    /// You can add profile data for the user by adding more properties to your ApplicationUser class, please visit http://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {

    }

    //safely store role strings instead of re-typing multiple times
    public static class RoleTypes
    {
        public const string Admin = "Admin";
    }

}
