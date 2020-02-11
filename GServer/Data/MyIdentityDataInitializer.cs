using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace GServer.Data
{
    //class for creating default uses & roles in database at program start
    //not yet implemented
    
    public static class MyIdentityDataInitializer
    {
        public static void SeedData(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            SeedRoles(roleManager);
            SeedUsers(userManager);
        }

        public static void SeedUsers(UserManager<ApplicationUser> userManager)
        {
            string defaultName = "Graham";
            string defaultPW = "123456";

            if ((userManager.FindByNameAsync(defaultName).Result == null))
            {
                ApplicationUser user = new ApplicationUser();
                user.UserName = defaultName;

                IdentityResult result = userManager.CreateAsync(user, defaultPW).Result;

                if (result.Succeeded)
                {
                    userManager.AddToRoleAsync(user, RoleTypes.Admin).Wait();
                }
            }
        }

        public static void SeedRoles(RoleManager<IdentityRole> roleManager)
        {
            List<string> roleNames = new List<string> { 
                RoleTypes.Admin       
            }; //create a list with each of the default names
            
            //iterate through all and add them
            foreach(string roleName in roleNames)
            {
                if (!roleManager.RoleExistsAsync(roleName).Result)
                {
                    IdentityResult roleResult = roleManager.CreateAsync(new IdentityRole(roleName)).Result;
                }
            }          
        }
    }
}
