using LMS.Business.Security;
using LMS.Business.Services.Implementations;
using LMS.Business.Services.Interfaces;
using LMS.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LMS.Business;

public static class BusinessServiceExtensions
{
    public static IServiceCollection AddBusinessLogicLayer(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IBookService, BookService>();
        services.AddScoped<IBorrowingService, BorrowingService>();
        services.AddScoped<ISystemSettingService, SystemSettingService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
