using GServer.Data;
using Microsoft.Extensions.DependencyInjection;

namespace GServer.IoC
{
    /// <summary>
    /// A shorthand access class to get DI services with nice clean short code
    /// </summary>
    public static class IoC
    {
        public static ApplicationDbContext ApplicationDbContext => IocContainer.Provider.GetService<ApplicationDbContext>();
    }

    public static class IocContainer
    {
        public static ServiceProvider Provider { get; set; }
    }
}
